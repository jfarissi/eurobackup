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

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await ImportFileAsync(file, result, ct);
                }
                catch (Exception ex)
                {
                    var inner = GetInnermostMessage(ex);
                    result.Errors.Add($"{Path.GetFileName(file)}: {inner}");
                    _logger.LogWarning(ex, "Excel import failed for {File}", file);
                }
            }

            return result;
        }

        private async Task ImportFileAsync(string filePath, ExcelImportResult result, CancellationToken ct)
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName.Equals("marques.xlsx", StringComparison.OrdinalIgnoreCase))
            {
                result.Skipped++;
                return;
            }

            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheets.First();
            var used = sheet.RangeUsed();
            if (used == null)
                return;

            var firstRow = used.FirstRow().RowNumber();
            var lastRow = used.LastRow().RowNumber();
            if (lastRow < firstRow + 1)
                return;

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
                return;
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

                    // Code-barres Excel parfois lu comme nombre scientifique / double
                    barcode = NormalizeBarcode(barcode);

                    result.RowsRead++;
                    await UpsertExcelRowAsync(
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
                        ct);
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
        }

        private async Task UpsertExcelRowAsync(
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
            CancellationToken ct)
        {
            ErpProduct? existing = null;

            if (!string.IsNullOrWhiteSpace(excelErpId)
                && !excelErpId.StartsWith("XLS-", StringComparison.OrdinalIgnoreCase))
            {
                existing = await _storage.SelectAllErpProducts()
                    .FirstOrDefaultAsync(p => p.ErpProductId == excelErpId, ct);
            }

            if (existing == null && !string.IsNullOrWhiteSpace(ean))
            {
                existing = await _storage.SelectAllErpProducts()
                    .FirstOrDefaultAsync(p => p.Ean == ean, ct);
            }

            if (existing == null)
            {
                existing = await _storage.SelectAllErpProducts()
                    .FirstOrDefaultAsync(p => p.Reference == reference, ct);
            }

            if (existing == null)
            {
                var provisionalId = ResolveProductId(excelErpId, ean, reference);
                var collision = await _storage.SelectAllErpProducts()
                    .AnyAsync(p => p.ErpProductId == provisionalId, ct);
                if (collision)
                    provisionalId = Truncate($"XLS-{Guid.NewGuid():N}", 64)!;

                var created = new ErpProduct
                {
                    ErpProductId = provisionalId,
                    Name = name,
                    Name2 = name2,
                    Reference = reference,
                    Ean = ean,
                    Brand = brand,
                    Comment = comment,
                    PriceHT = costPrice,
                    CPrice = costPrice,
                    UnitPrice = sellingPrice,
                    RPrice = sellingPrice,
                    DataSource = "Excel",
                    SourceFile = Truncate(sourceFile, 512),
                    FromExcel = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _storage.InsertErpProductAsync(created);
                result.Created++;
                return;
            }

            existing.Name = name;
            if (!string.IsNullOrWhiteSpace(name2))
                existing.Name2 = name2;
            existing.Reference = reference;
            if (!string.IsNullOrWhiteSpace(ean))
                existing.Ean = ean;
            if (!string.IsNullOrWhiteSpace(brand))
                existing.Brand = brand;
            if (!string.IsNullOrWhiteSpace(comment))
                existing.Comment = comment;
            existing.PriceHT = costPrice;
            existing.CPrice = costPrice;
            existing.UnitPrice = sellingPrice;
            existing.RPrice = sellingPrice;
            existing.SourceFile = Truncate(sourceFile, 512);
            existing.FromExcel = true;
            existing.DataSource = string.IsNullOrWhiteSpace(existing.DataSource) || existing.DataSource == "Excel"
                ? "Excel"
                : "Merged";

            // Remplacer l'ID provisoire XLS-* par l'ID ERP du fichier si dispo
            if (!string.IsNullOrWhiteSpace(excelErpId)
                && existing.ErpProductId.StartsWith("XLS-", StringComparison.OrdinalIgnoreCase))
            {
                var taken = await _storage.SelectAllErpProducts()
                    .AnyAsync(p => p.ErpProductId == excelErpId && p.Id != existing.Id, ct);
                if (!taken)
                    existing.ErpProductId = excelErpId!;
            }

            existing.UpdatedAt = DateTime.UtcNow;
            await _storage.UpdateErpProductAsync(existing);
            result.Updated++;
        }

        private static string ResolveProductId(string? excelErpId, string? ean, string reference)
        {
            if (!string.IsNullOrWhiteSpace(excelErpId))
                return Truncate(excelErpId, 64)!;

            return BuildProvisionalId(ean, reference);
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
