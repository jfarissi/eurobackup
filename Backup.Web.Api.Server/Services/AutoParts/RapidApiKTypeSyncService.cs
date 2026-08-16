using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    public class RapidApiKTypeSyncService : IRapidApiKTypeSyncService
    {
        private static readonly JsonSerializerOptions CategoryJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private static readonly ConcurrentDictionary<string, (DateTime At, RapidApiCategoryListDto List)> CategoryCache =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly IOptions<RapidApiOptions> options;
        private readonly IKTypeSyncProgressStore progressStore;
        private readonly IStorageBroker storage;
        private readonly IWebHostEnvironment env;
        private readonly ILogger<RapidApiKTypeSyncService> logger;

        public RapidApiKTypeSyncService(
            IOptions<RapidApiOptions> options,
            IKTypeSyncProgressStore progressStore,
            IStorageBroker storage,
            IWebHostEnvironment env,
            ILogger<RapidApiKTypeSyncService> logger)
        {
            this.options = options;
            this.progressStore = progressStore;
            this.storage = storage;
            this.env = env;
            this.logger = logger;
        }

        public async Task<RapidApiCategoryListDto> ListCategoriesAsync(string kType, CancellationToken ct = default)
        {
            var key = (kType ?? string.Empty).Trim();
            if (!int.TryParse(key, out var vehicleId) || vehicleId <= 0)
                return new RapidApiCategoryListDto(key, new List<RapidApiCategoryDto>());

            if (CategoryCache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.At < TimeSpan.FromMinutes(15))
                return cached.List;

            try
            {
                var fromDb = await storage.SelectRapidApiKTypeCategoryCacheAsync(key);
                if (fromDb != null && !string.IsNullOrWhiteSpace(fromDb.CategoriesJson))
                {
                    var dbCats = ParseCategoryArrayJson(fromDb.CategoriesJson);
                    if (dbCats.Count > 0)
                    {
                        var dbList = new RapidApiCategoryListDto(key, dbCats);
                        CategoryCache[key] = (DateTime.UtcNow, dbList);
                        logger.LogDebug(
                            "Catégories RapidAPI K-Type {KType} depuis le cache DB ({Count})",
                            key, dbCats.Count);
                        return dbList;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Lecture cache DB catégories ignorée pour K-Type {KType}", key);
            }

            var cfg = options.Value;
            var script = ResolveScriptPath(cfg.KTypeSyncScriptPath);
            if (!File.Exists(script))
            {
                logger.LogWarning("Script sync K-Type introuvable pour liste catégories: {Script}", script);
                return new RapidApiCategoryListDto(key, new List<RapidApiCategoryDto>());
            }

            var python = string.IsNullOrWhiteSpace(cfg.PythonExecutable) ? "python" : cfg.PythonExecutable.Trim();
            var args = $"\"{script}\" --ktype {vehicleId} --list-categories";
            try
            {
                var (exitCode, stdout, stderr) = await RunPythonAsync(python, args, script, key, applyProgress: false, ct);
                if (!string.IsNullOrWhiteSpace(stderr))
                    logger.LogDebug("list categories stderr: {Stderr}", stderr.Trim());

                var cats = ParseCategories(stdout);
                var list = new RapidApiCategoryListDto(key, cats);
                if (exitCode == 0 || cats.Count > 0)
                {
                    CategoryCache[key] = (DateTime.UtcNow, list);
                    if (cats.Count > 0)
                    {
                        try
                        {
                            var json = JsonSerializer.Serialize(cats, CategoryJsonOptions);
                            await storage.UpsertRapidApiKTypeCategoryCacheAsync(key, json, cats.Count);
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(ex, "Enregistrement cache DB catégories ignoré pour K-Type {KType}", key);
                        }
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Liste catégories RapidAPI échouée pour K-Type {KType}", key);
                return new RapidApiCategoryListDto(key, new List<RapidApiCategoryDto>());
            }
        }

        public async Task<int> SyncKTypeAsync(
            string kType,
            string make,
            string model,
            int? year = null,
            IReadOnlyList<int>? categoryIds = null,
            string? fuelType = null,
            CancellationToken ct = default)
        {
            var cfg = options.Value;
            if (!cfg.Enabled || !cfg.EnableOnDemandKTypeSync)
                return 0;

            if (!int.TryParse((kType ?? string.Empty).Trim(), out var vehicleId) || vehicleId <= 0)
            {
                logger.LogWarning("K-Type invalide pour sync RapidAPI: {KType}", kType);
                return 0;
            }

            var script = ResolveScriptPath(cfg.KTypeSyncScriptPath);
            if (!File.Exists(script))
            {
                logger.LogWarning("Script sync K-Type introuvable: {Script}", script);
                progressStore.Fail(kType, "Script d'import catalogue introuvable sur le serveur.");
                return 0;
            }

            var python = string.IsNullOrWhiteSpace(cfg.PythonExecutable) ? "python" : cfg.PythonExecutable.Trim();
            var selected = (categoryIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().Take(20).ToList();
            var maxProducts = Math.Clamp(cfg.OnDemandMaxProducts, 1, 200);
            if (selected.Count > 0)
                maxProducts = Math.Clamp(cfg.OnDemandMaxProducts * selected.Count, 1, 200);
            var maxCats = selected.Count > 0
                ? selected.Count
                : Math.Clamp(cfg.OnDemandMaxCategories, 1, 20);
            var fastFlag = cfg.OnDemandFastMode ? " --fast" : string.Empty;
            var refreshFlag = cfg.OnDemandFastMode ? string.Empty : " --refresh";
            var yearFlag = year is > 1900 and < 2100 ? $" --year {year.Value}" : string.Empty;
            var fuelFlag = string.IsNullOrWhiteSpace(fuelType) ? string.Empty : $" --fuel \"{EscapeArg(fuelType.Trim())}\"";
            var catsFlag = selected.Count > 0 ? $" --category-ids {string.Join(",", selected)}" : string.Empty;

            var args =
                $"\"{script}\" --ktype {vehicleId} " +
                $"--make \"{EscapeArg(make)}\" --model \"{EscapeArg(model)}\" " +
                $"--max-products {maxProducts} --max-categories {maxCats}{yearFlag}{fuelFlag}{fastFlag}{refreshFlag}{catsFlag}";

            logger.LogInformation(
                "Sync K-Type {KType} ({Make} {Model}) via Python — max {MaxProducts} produits, cats={Cats}",
                vehicleId,
                make,
                model,
                maxProducts,
                selected.Count > 0 ? string.Join(",", selected) : "auto");

            try
            {
                var (exitCode, stdout, stderr) = await RunPythonAsync(python, args, script, kType, applyProgress: true, ct);
                if (!string.IsNullOrWhiteSpace(stderr))
                    logger.LogDebug("sync_ktype_vehicle stderr: {Stderr}", stderr.Trim());

                var imported = ParseProductsImported(stdout);
                if (exitCode != 0 && imported == 0)
                {
                    logger.LogWarning(
                        "Sync K-Type {KType} exit {Code}. stdout: {Stdout}",
                        vehicleId,
                        exitCode,
                        Truncate(stdout));
                    progressStore.Fail(kType, "Import catalogue interrompu.");
                    return 0;
                }

                logger.LogInformation(
                    "Sync K-Type {KType} terminé — {Count} produit(s) importé(s)",
                    vehicleId,
                    imported);
                return imported;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Échec sync Python K-Type {KType}", vehicleId);
                progressStore.Fail(kType, "Échec import catalogue.");
                return 0;
            }
        }

        private async Task<(int ExitCode, string Stdout, string Stderr)> RunPythonAsync(
            string python,
            string args,
            string script,
            string kType,
            bool applyProgress,
            CancellationToken ct)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = args,
                    WorkingDirectory = Path.GetDirectoryName(script) ?? env.ContentRootPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            var stdout = new StringBuilder();
            try
            {
                process.Start();
            }
            catch (Exception startEx)
            {
                logger.LogWarning(startEx, "Impossible de démarrer Python pour K-Type {KType}", kType);
                if (applyProgress)
                    progressStore.Fail(kType, "Python indisponible sur le serveur API (import à la demande impossible).");
                throw;
            }

            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(ct)) != null)
            {
                stdout.AppendLine(line);
                if (applyProgress && line.StartsWith("PROGRESS_JSON=", StringComparison.Ordinal))
                    progressStore.ApplyProgressJson(kType, line["PROGRESS_JSON=".Length..]);
            }

            var stderr = await stderrTask;
            await process.WaitForExitAsync(ct);
            return (process.ExitCode, stdout.ToString(), stderr);
        }

        private string ResolveScriptPath(string? configured)
        {
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var p = configured.Trim();
                return Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(env.ContentRootPath, p));
            }

            var candidate = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "docs", "sync_ktype_vehicle.py"));
            if (File.Exists(candidate)) return candidate;

            return Path.GetFullPath(Path.Combine(env.ContentRootPath, "docs", "sync_ktype_vehicle.py"));
        }

        private static List<RapidApiCategoryDto> ParseCategories(string stdout)
        {
            var json = ExtractPrefixedJson(stdout, "CATEGORIES_JSON=");
            if (string.IsNullOrWhiteSpace(json)) return new List<RapidApiCategoryDto>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("categories", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    return ParseCategoryElements(arr);
            }
            catch
            {
                // ignore
            }
            return new List<RapidApiCategoryDto>();
        }

        private static List<RapidApiCategoryDto> ParseCategoryArrayJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<RapidApiCategoryDto>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                    return ParseCategoryElements(root);
                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("categories", out var arr) &&
                    arr.ValueKind == JsonValueKind.Array)
                    return ParseCategoryElements(arr);
            }
            catch
            {
                // ignore
            }
            return new List<RapidApiCategoryDto>();
        }

        private static List<RapidApiCategoryDto> ParseCategoryElements(JsonElement arr)
        {
            var list = new List<RapidApiCategoryDto>();
            foreach (var el in arr.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var n) ? n : 0;
                if (id <= 0) continue;
                var parent = el.TryGetProperty("parent", out var parEl) ? parEl.GetString()
                    : el.TryGetProperty("parentName", out var pnEl) ? pnEl.GetString() : null;
                list.Add(new RapidApiCategoryDto(
                    id,
                    el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? $"#{id}" : $"#{id}",
                    el.TryGetProperty("family", out var famEl) ? famEl.GetString() ?? "other" : "other",
                    el.TryGetProperty("familyLabel", out var labEl) ? labEl.GetString() ?? "Autres" : "Autres",
                    parent));
            }
            return list;
        }

        private static int ParseProductsImported(string stdout)
        {
            var json = ExtractPrefixedJson(stdout, "RESULT_JSON=");
            if (string.IsNullOrWhiteSpace(json)) return 0;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("products", out var p) && p.TryGetInt32(out var n))
                    return n;
            }
            catch
            {
                // ignore
            }

            return 0;
        }

        private static string? ExtractPrefixedJson(string stdout, string prefix)
        {
            if (string.IsNullOrWhiteSpace(stdout)) return null;
            var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                if (lines[i].StartsWith(prefix, StringComparison.Ordinal))
                    return lines[i][prefix.Length..];
            }

            return null;
        }

        private static string EscapeArg(string? value) =>
            (value ?? string.Empty).Replace("\"", "\\\"", StringComparison.Ordinal);

        private static string Truncate(string? s) =>
            string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= 400 ? s : s[..400]);
    }
}
