using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class ErpExcelImportService : IErpExcelImportService
    {
        private static readonly Dictionary<string, string[]> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["erp_id"] = new[] { "id" },
            ["denomination"] = new[] { "dénomination", "denomination" },
            ["denomination2"] = new[] { "dénomination 2", "denomination 2" },
            ["barcode"] = new[] { "code barre:", "code barre", "barcode", "ean" },
            ["supplier_ref"] = new[] { "ref. fournisseur:", "ref fournisseur", "ref. fournisseur" },
            ["ref"] = new[] { "ref.", "ref", "reference" },
            ["cost_price"] = new[] { "prix achat" },
            // Excel "prix vente" = TTC (équivalent ERP UnitPrice), pas le PriceHT (vente HT).
            ["selling_price"] = new[] { "prix vente" },
            ["brand"] = new[] { "marque" },
            ["comment"] = new[] { "commentaire:", "commentaire" }
        };

        private readonly IStorageBroker _storage;
        private readonly ErpSyncOptions _options;
        private readonly ILogger<ErpExcelImportService> _logger;

        public ErpExcelImportService(
            IStorageBroker storage,
            IOptions<ErpSyncOptions> options,
            ILogger<ErpExcelImportService> logger)
        {
            _storage = storage;
            _options = options.Value ?? new ErpSyncOptions();
            _logger = logger;
        }

        private const int SaveBatchSize = 100;

        public async Task<ExcelImportResult> ImportFromDirectoryAsync(string? directoryPath = null, CancellationToken ct = default)
        {
            var result = new ExcelImportResult();
            var path = string.IsNullOrWhiteSpace(directoryPath) ? _options.ExcelProductPath : directoryPath;

            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                result.Errors.Add($"Dossier Excel introuvable: {path}");
                return result;
            }

            var files = Directory.GetFiles(path, "*.xlsx")
                .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.Ordinal))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.FilesScanned = files.Count;
            _logger.LogInformation("Excel import: {Count} fichiers dans {Path}", files.Count, path);

            var index = new ProductIndex(await _storage.SelectAllErpProducts().ToListAsync(ct));
            var pendingChanges = 0;

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    pendingChanges += ImportFileAsync(file, result, index, ct);
                    if (pendingChanges >= SaveBatchSize)
                    {
                        await index.FlushAsync(_storage, ct);
                        pendingChanges = 0;
                    }
                }
                catch (Exception ex)
                {
                    var inner = GetInnermostMessage(ex);
                    result.Errors.Add($"{Path.GetFileName(file)}: {inner}");
                    _logger.LogWarning(ex, "Excel import failed for {File}", file);
                }
            }

            if (pendingChanges > 0)
                await index.FlushAsync(_storage, ct);

            _logger.LogInformation(
                "Excel import terminé: created={Created} updated={Updated} skipped={Skipped} errors={Errors}",
                result.Created, result.Updated, result.Skipped, result.Errors.Count);

            return result;
        }

        private int ImportFileAsync(
            string filePath,
            ExcelImportResult result,
            ProductIndex index,
            CancellationToken ct)
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName.Equals("marques.xlsx", StringComparison.OrdinalIgnoreCase))
            {
                result.Skipped++;
                return 0;
            }

            var changes = 0;
            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheets.First();
            var used = sheet.RangeUsed();
            if (used == null)
                return 0;

            var firstRow = used.FirstRow().RowNumber();
            var lastRow = used.LastRow().RowNumber();
            if (lastRow < firstRow + 1)
                return 0;

            Dictionary<string, int>? colMap = null;
            var headerRow = -1;
            for (var r = firstRow; r <= Math.Min(firstRow + 29, lastRow); r++)
            {
                colMap = TryMapHeader(sheet.Row(r));
                if (colMap != null && colMap.ContainsKey("denomination"))
                {
                    headerRow = r;
                    break;
                }
            }

            if (colMap == null || headerRow < 0)
            {
                result.Errors.Add($"{fileName}: colonne Dénomination introuvable");
                return 0;
            }

            var brandFallback = Path.GetFileNameWithoutExtension(fileName).Trim();

            for (var r = headerRow + 1; r <= lastRow; r++)
            {
                ct.ThrowIfCancellationRequested();
                var row = sheet.Row(r);

                try
                {
                    var denomination = GetCell(row, colMap, "denomination");
                    if (string.IsNullOrWhiteSpace(denomination))
                    {
                        result.Skipped++;
                        continue;
                    }

                    var sku = GetCell(row, colMap, "ref");
                    if (string.IsNullOrWhiteSpace(sku))
                        sku = GetCell(row, colMap, "supplier_ref");
                    if (string.IsNullOrWhiteSpace(sku))
                    {
                        result.Skipped++;
                        continue;
                    }

                    var barcode = NullIfEmpty(GetCell(row, colMap, "barcode"));
                    var brand = NullIfEmpty(GetCell(row, colMap, "brand")) ?? brandFallback;
                    var comment = NullIfEmpty(GetCell(row, colMap, "comment"));
                    var name2 = NullIfEmpty(GetCell(row, colMap, "denomination2"));
                    var erpIdRaw = NullIfEmpty(GetCell(row, colMap, "erp_id"));
                    var cost = ParseDecimal(GetCell(row, colMap, "cost_price"));
                    var sell = ParseDecimal(GetCell(row, colMap, "selling_price"));

                    barcode = NormalizeBarcode(barcode);

                    result.RowsRead++;
                    if (UpsertExcelRow(
                        fileName,
                        Truncate(denomination.Trim(), 512)!,
                        Truncate(sku.Trim(), 128)!,
                        Truncate(barcode, 64),
                        Truncate(brand, 256),
                        Truncate(comment, 2048),
                        Truncate(name2, 512),
                        Truncate(erpIdRaw, 64),
                        cost,
                        sell,
                        result,
                        index))
                    {
                        changes++;
                    }
                }
                catch (Exception ex)
                {
                    result.Skipped++;
                    var msg = $"{fileName} ligne {r}: {GetInnermostMessage(ex)}";
                    if (result.Errors.Count < 50)
                        result.Errors.Add(msg);
                    _logger.LogDebug(ex, "Excel row import failed {File} row {Row}", fileName, r);
                }
            }

            return changes;
        }

        private static bool UpsertExcelRow(
            string sourceFile,
            string name,
            string reference,
            string? ean,
            string? brand,
            string? comment,
            string? name2,
            string? excelErpId,
            decimal? costPrice,
            decimal? sellingPrice,
            ExcelImportResult result,
            ProductIndex index)
        {
            var existing = index.Find(excelErpId, ean, reference, name, sellingPrice);

            if (existing == null)
            {
                var provisionalId = ResolveProductId(excelErpId, ean, reference, index);
                var created = new ErpProduct
                {
                    ErpProductId = provisionalId,
                    Name = name,
                    Name2 = name2,
                    Reference = reference,
                    Ean = ean,
                    Brand = brand,
                    Comment = comment,
                    // Excel: prix achat → CPrice ; prix vente TTC → UnitPrice/RPrice.
                    // PriceHT (vente HT) vient uniquement de l'ERP.
                    CPrice = costPrice,
                    UnitPrice = sellingPrice,
                    RPrice = sellingPrice,
                    DataSource = "Excel",
                    SourceFile = Truncate(sourceFile, 512),
                    FromExcel = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                index.TrackNew(created);
                result.Created++;
                return true;
            }

            existing.Name = name;
            if (!string.IsNullOrWhiteSpace(name2))
                existing.Name2 = name2;
            existing.Reference = reference;

            // Avec ID ERP Excel, la ligne est la source de vérité (EAN vide = pack sans code-barres).
            // Sinon on ne remplit l'EAN que s'il est fourni (ne pas écraser à vide par erreur).
            var hasAuthoritativeErpId = !string.IsNullOrWhiteSpace(excelErpId)
                && !excelErpId.StartsWith("XLS-", StringComparison.OrdinalIgnoreCase);
            if (hasAuthoritativeErpId)
            {
                if (!string.Equals(existing.Ean, ean, StringComparison.OrdinalIgnoreCase))
                {
                    index.RemoveFromLookup(existing);
                    existing.Ean = ean;
                    index.AddToLookup(existing);
                }
            }
            else if (!string.IsNullOrWhiteSpace(ean))
            {
                existing.Ean = ean;
            }

            // Pack (verpakking…) sans EAN Excel : ne jamais conserver un EAN d'unité collé par erreur.
            if (string.IsNullOrWhiteSpace(ean) && IsPackProduct(name, name2) && !string.IsNullOrWhiteSpace(existing.Ean))
            {
                index.RemoveFromLookup(existing);
                existing.Ean = null;
                index.AddToLookup(existing);
            }

            if (!string.IsNullOrWhiteSpace(brand))
                existing.Brand = brand;
            if (!string.IsNullOrWhiteSpace(comment))
                existing.Comment = comment;
            if (costPrice.HasValue)
                existing.CPrice = costPrice;
            if (sellingPrice.HasValue)
            {
                existing.UnitPrice = sellingPrice;
                existing.RPrice = sellingPrice;
            }

            // PriceHT = vente HT ERP uniquement.
            // Si l'ancien import avait mis le prix d'achat dans PriceHT, on le purge.
            if (existing.PriceHT.HasValue
                && ((costPrice.HasValue && existing.PriceHT == costPrice)
                    || (existing.CPrice.HasValue && existing.PriceHT == existing.CPrice)))
            {
                existing.PriceHT = null;
            }
            existing.SourceFile = Truncate(sourceFile, 512);
            existing.FromExcel = true;
            existing.DataSource = string.IsNullOrWhiteSpace(existing.DataSource) || existing.DataSource == "Excel"
                ? "Excel"
                : "Merged";

            if (!string.IsNullOrWhiteSpace(excelErpId)
                && !excelErpId.StartsWith("XLS-", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(existing.ErpProductId, excelErpId, StringComparison.OrdinalIgnoreCase)
                && !index.IsErpIdTaken(excelErpId, existing.Id))
            {
                index.RemoveFromLookup(existing);
                existing.ErpProductId = excelErpId!;
                index.AddToLookup(existing);
            }

            existing.UpdatedAt = DateTime.UtcNow;
            index.MarkUpdated(existing);
            result.Updated++;
            return true;
        }

        private sealed class ProductIndex
        {
            private readonly Dictionary<string, ErpProduct> _byErpId = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, ErpProduct> _byEan = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, List<ErpProduct>> _byReference = new(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<ErpProduct> _newProducts = new();
            private readonly HashSet<ErpProduct> _updatedProducts = new();

            public ProductIndex(IEnumerable<ErpProduct> products)
            {
                foreach (var product in products)
                    AddToLookup(product);
            }

            public bool HasErpId(string erpId) => _byErpId.ContainsKey(erpId);

            public void TrackNew(ErpProduct product)
            {
                AddToLookup(product);
                _newProducts.Add(product);
            }

            public void AddToLookup(ErpProduct product)
            {
                if (!string.IsNullOrWhiteSpace(product.ErpProductId))
                    _byErpId[product.ErpProductId] = product;
                if (!string.IsNullOrWhiteSpace(product.Ean))
                    _byEan[product.Ean] = product;
                if (!string.IsNullOrWhiteSpace(product.Reference))
                {
                    if (!_byReference.TryGetValue(product.Reference, out var list))
                    {
                        list = new List<ErpProduct>();
                        _byReference[product.Reference] = list;
                    }

                    if (!list.Contains(product))
                        list.Add(product);
                }
            }

            public void RemoveFromLookup(ErpProduct product)
            {
                if (!string.IsNullOrWhiteSpace(product.ErpProductId))
                    _byErpId.Remove(product.ErpProductId);
                if (!string.IsNullOrWhiteSpace(product.Ean))
                    _byEan.Remove(product.Ean);
                if (!string.IsNullOrWhiteSpace(product.Reference)
                    && _byReference.TryGetValue(product.Reference, out var list))
                {
                    list.Remove(product);
                    if (list.Count == 0)
                        _byReference.Remove(product.Reference);
                }
            }

            public ErpProduct? Find(
                string? excelErpId,
                string? ean,
                string reference,
                string? nameHint,
                decimal? sellingPriceHint)
            {
                // 1) ID ERP Excel (prioritaire) — ne PAS retomber sur la référence :
                //    une même ref peut désigner l'unité ET le pack (ex. verpakking 50).
                if (!string.IsNullOrWhiteSpace(excelErpId)
                    && !excelErpId.StartsWith("XLS-", StringComparison.OrdinalIgnoreCase))
                {
                    if (_byErpId.TryGetValue(excelErpId, out var byErpId))
                        return byErpId;

                    if (!string.IsNullOrWhiteSpace(ean) && _byEan.TryGetValue(ean, out var byEanWithId))
                        return byEanWithId;

                    return null;
                }

                if (!string.IsNullOrWhiteSpace(ean) && _byEan.TryGetValue(ean, out var byEan))
                    return byEan;

                if (!_byReference.TryGetValue(reference, out var byRef) || byRef.Count == 0)
                    return null;

                if (byRef.Count == 1)
                    return byRef[0];

                return PickBestSameReferenceProduct(byRef, ean, nameHint, sellingPriceHint);
            }

            public bool IsErpIdTaken(string erpId, int localId) =>
                _byErpId.TryGetValue(erpId, out var existing) && existing.Id != localId;

            public void MarkUpdated(ErpProduct product)
            {
                if (!_newProducts.Contains(product))
                    _updatedProducts.Add(product);
            }

            public async Task FlushAsync(IStorageBroker storage, CancellationToken ct)
            {
                if (_newProducts.Count == 0 && _updatedProducts.Count == 0)
                    return;

                foreach (var product in _newProducts)
                    await storage.StageInsertErpProductAsync(product);

                await storage.FlushChangesAsync(ct);
                _newProducts.Clear();
                _updatedProducts.Clear();
            }
        }

        /// <summary>
        /// Départage unité vs pack quand plusieurs fiches partagent la même référence.
        /// </summary>
        private static ErpProduct? PickBestSameReferenceProduct(
            IReadOnlyList<ErpProduct> candidates,
            string? ean,
            string? nameHint,
            decimal? sellingPriceHint)
        {
            if (candidates.Count == 0)
                return null;
            if (candidates.Count == 1)
                return candidates[0];

            IEnumerable<ErpProduct> pool = candidates;
            if (!string.IsNullOrWhiteSpace(ean))
            {
                var byEan = candidates
                    .Where(p => string.Equals(p.Ean, ean, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (byEan.Count == 1)
                    return byEan[0];
                if (byEan.Count > 1)
                    pool = byEan;
            }

            var hintPack = IsPackProduct(nameHint, null);
            var packMatches = pool
                .Where(p => IsPackProduct(p.Name, p.Name2) == hintPack)
                .ToList();
            if (packMatches.Count == 1)
                return packMatches[0];
            if (packMatches.Count > 1)
                pool = packMatches;

            if (sellingPriceHint.HasValue)
            {
                var nearest = pool
                    .Where(p => p.UnitPrice.HasValue)
                    .OrderBy(p => Math.Abs(p.UnitPrice!.Value - sellingPriceHint.Value))
                    .FirstOrDefault();
                if (nearest != null
                    && Math.Abs(nearest.UnitPrice!.Value - sellingPriceHint.Value) <= Math.Max(0.05m, sellingPriceHint.Value * 0.15m))
                    return nearest;
            }

            // Ambigu : mieux créer une nouvelle fiche que fusionner unité + pack.
            return null;
        }

        private static bool IsPackProduct(string? name, string? name2)
        {
            var text = $"{name} {name2}";
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return Regex.IsMatch(
                text,
                @"verpakking|\bpack\b|\bbo[iî]te\b|\bcartouche\b|\b\d+\s*st\.?\b|\bx\s*\d+\b|\b\d+\s*pi[eè]ces?\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string ResolveProductId(string? excelErpId, string? ean, string reference, ProductIndex index)
        {
            var id = !string.IsNullOrWhiteSpace(excelErpId)
                ? Truncate(excelErpId, 64)!
                : BuildProvisionalId(ean, reference);

            if (index.HasErpId(id))
                id = Truncate($"XLS-{Guid.NewGuid():N}", 64)!;

            return id;
        }

        private static string BuildProvisionalId(string? ean, string reference)
        {
            var key = !string.IsNullOrWhiteSpace(ean) ? ean! : reference;
            key = Regex.Replace(key.Trim(), @"[^\w\-]+", "-");
            key = Regex.Replace(key, @"-+", "-").Trim('-');
            if (string.IsNullOrWhiteSpace(key))
                key = Guid.NewGuid().ToString("N");
            if (key.Length > 60)
                key = key[..60];
            return $"XLS-{key}";
        }

        private static Dictionary<string, int>? TryMapHeader(IXLRow row)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in row.CellsUsed())
            {
                var header = NormalizeHeader(cell.GetFormattedString());
                if (string.IsNullOrWhiteSpace(header))
                    continue;

                foreach (var (canonical, aliases) in HeaderAliases)
                {
                    if (map.ContainsKey(canonical))
                        continue;

                    // Match exact, ou header qui commence par l'alias (ex: "code barre:")
                    if (aliases.Any(a =>
                            string.Equals(a, header, StringComparison.OrdinalIgnoreCase)
                            || header.Equals(a.TrimEnd(':'), StringComparison.OrdinalIgnoreCase)))
                    {
                        // Éviter que "ref." match "ref. fournisseur"
                        if (canonical == "ref" && header.Contains("fournisseur", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (canonical == "denomination" && header.Contains('2'))
                            continue;

                        map[canonical] = cell.Address.ColumnNumber;
                    }
                }
            }

            return map.Count > 0 ? map : null;
        }

        private static string GetCell(IXLRow row, Dictionary<string, int> colMap, string key)
        {
            if (!colMap.TryGetValue(key, out var col))
                return string.Empty;

            var cell = row.Cell(col);
            if (cell.IsEmpty())
                return string.Empty;

            // Préférer la valeur formatée texte pour EAN/REF (évite 9.0E+12)
            if (cell.DataType == XLDataType.Number)
            {
                var formatted = cell.GetFormattedString()?.Trim();
                if (!string.IsNullOrWhiteSpace(formatted) && !formatted.Contains('E', StringComparison.OrdinalIgnoreCase))
                    return formatted;

                return cell.GetDouble().ToString("0.################", CultureInfo.InvariantCulture);
            }

            return cell.GetString()?.Trim() ?? string.Empty;
        }

        private static string? NormalizeBarcode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            value = value.Trim();
            // "9002741216147.0" → "9002741216147"
            if (value.EndsWith(".0", StringComparison.Ordinal) && value.All(c => char.IsDigit(c) || c == '.'))
                value = value[..^2];
            return value;
        }

        private static string NormalizeHeader(string? value)
        {
            var s = (value ?? string.Empty).Trim().ToLowerInvariant();
            s = s.Normalize(NormalizationForm.FormC);
            return Regex.Replace(s, @"\s+", " ");
        }

        private static string? NullIfEmpty(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? Truncate(string? value, int max)
        {
            if (value == null)
                return null;
            value = value.Trim();
            return value.Length <= max ? value : value[..max];
        }

        private static decimal? ParseDecimal(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            value = value.Replace(" ", "").Replace(",", ".");
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        private static string GetInnermostMessage(Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;
            return ex.Message;
        }
    }
}
