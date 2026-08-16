using System;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    public class KTypeSyncProgressStore : IKTypeSyncProgressStore
    {
        private readonly ConcurrentDictionary<string, KTypeSyncProgressDto> entries =
            new(StringComparer.OrdinalIgnoreCase);

        public void Start(string kType, int total, string? make = null, string? model = null)
        {
            var key = Normalize(kType);
            var label = string.IsNullOrWhiteSpace(make)
                ? "Import catalogue RapidAPI…"
                : $"Import {make} {model}".Trim();
            entries[key] = new KTypeSyncProgressDto(
                key,
                KTypeSyncStatus.Running,
                "start",
                0,
                Math.Max(total, 1),
                0,
                label,
                null,
                DateTime.UtcNow);
        }

        public void Update(string kType, string? phase, int current, int total, string? message = null)
        {
            var key = Normalize(kType);
            var safeTotal = Math.Max(total, 1);
            var percent = Math.Clamp((int)Math.Round(100.0 * current / safeTotal), 0, 100);
            entries.AddOrUpdate(
                key,
                _ => new KTypeSyncProgressDto(
                    key, KTypeSyncStatus.Running, phase, current, safeTotal, percent, message, null, DateTime.UtcNow),
                (_, existing) => existing with
                {
                    Status = KTypeSyncStatus.Running,
                    Phase = phase ?? existing.Phase,
                    Current = current,
                    Total = safeTotal,
                    Percent = percent,
                    Message = message ?? existing.Message,
                    UpdatedAt = DateTime.UtcNow
                });
        }

        public void ApplyProgressJson(string kType, string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var phase = root.TryGetProperty("phase", out var p) ? p.GetString() : null;
                var current = root.TryGetProperty("current", out var c) && c.TryGetInt32(out var cv) ? cv : 0;
                var total = root.TryGetProperty("total", out var t) && t.TryGetInt32(out var tv) ? tv : 1;
                var message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
                var percent = root.TryGetProperty("percent", out var pct) && pct.TryGetInt32(out var pv)
                    ? Math.Clamp(pv, 0, 100)
                    : Math.Clamp((int)Math.Round(100.0 * current / Math.Max(total, 1)), 0, 100);

                var key = Normalize(kType);
                entries.AddOrUpdate(
                    key,
                    _ => new KTypeSyncProgressDto(
                        key, KTypeSyncStatus.Running, phase, current, Math.Max(total, 1), percent, message, null, DateTime.UtcNow),
                    (_, existing) => existing with
                    {
                        Status = KTypeSyncStatus.Running,
                        Phase = phase ?? existing.Phase,
                        Current = current,
                        Total = Math.Max(total, 1),
                        Percent = percent,
                        Message = message ?? existing.Message,
                        UpdatedAt = DateTime.UtcNow
                    });
            }
            catch
            {
                // ignore malformed progress payloads
            }
        }

        public void Complete(string kType, int productsImported)
        {
            var key = Normalize(kType);
            entries.AddOrUpdate(
                key,
                _ => new KTypeSyncProgressDto(
                    key, KTypeSyncStatus.Done, "done", productsImported, productsImported, 100,
                    $"{productsImported} pièce(s) importée(s).", productsImported, DateTime.UtcNow),
                (_, existing) => existing with
                {
                    Status = KTypeSyncStatus.Done,
                    Phase = "done",
                    Current = productsImported,
                    Total = Math.Max(existing.Total, productsImported),
                    Percent = 100,
                    Message = $"{productsImported} pièce(s) importée(s).",
                    ProductsImported = productsImported,
                    UpdatedAt = DateTime.UtcNow
                });
        }

        public void Fail(string kType, string? message)
        {
            var key = Normalize(kType);
            entries.AddOrUpdate(
                key,
                _ => new KTypeSyncProgressDto(
                    key, KTypeSyncStatus.Failed, "error", 0, 1, 0, message, null, DateTime.UtcNow),
                (_, existing) => existing with
                {
                    Status = KTypeSyncStatus.Failed,
                    Phase = "error",
                    Message = message ?? existing.Message,
                    UpdatedAt = DateTime.UtcNow
                });
        }

        public KTypeSyncProgressDto? Get(string kType)
        {
            entries.TryGetValue(Normalize(kType), out var value);
            return value;
        }

        public bool IsRunning(string kType) =>
            Get(kType)?.Status == KTypeSyncStatus.Running;

        private static string Normalize(string kType) =>
            (kType ?? string.Empty).Trim();
    }
}
