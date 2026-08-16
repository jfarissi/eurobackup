using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    public interface IAutoPartsSymptomService
    {
        Task<AutoPartsSymptomResult> DiagnoseAsync(string? query, CancellationToken ct = default);
    }

    public sealed class AutoPartsSymptomResult
    {
        public string Query { get; set; } = string.Empty;
        public string Source { get; set; } = "demo";
        public List<AutoPartsSymptomHitDto> Matches { get; set; } = new();
    }

    public sealed class AutoPartsSymptomHitDto
    {
        public string Code { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public List<AutoPartsSymptomProductDto> Products { get; set; } = new();
    }

    public sealed class AutoPartsSymptomProductDto
    {
        public int Id { get; set; }
        public string ErpProductId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Reference { get; set; }
        public decimal? UnitPrice { get; set; }
    }

    public sealed class AutoPartsSymptomService : IAutoPartsSymptomService
    {
        private readonly IStorageBroker storage;

        public AutoPartsSymptomService(IStorageBroker storage) => this.storage = storage;

        public async Task<AutoPartsSymptomResult> DiagnoseAsync(string? query, CancellationToken ct = default)
        {
            var q = (query ?? string.Empty).Trim();
            var result = new AutoPartsSymptomResult { Query = q, Source = "demo" };
            var matches = AutoPartsSymptomMatcher.Match(q);
            if (matches.Count == 0 && AutoPartsSymptomMatcher.LooksLike(q))
            {
                matches = new[]
                {
                    new AutoPartsSymptomMatcher.SymptomMatch(
                        "kit",
                        "Diagnostic Demo — kit frein avant",
                        new[] { "DIAG-KIT", "DIAG-PAD", "DIAG-DISC", "DIAG-CAL" })
                };
            }

            var refs = matches.SelectMany(m => m.ProductRefs).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var products = refs.Count == 0
                ? new List<AutoPartsSymptomProductDto>()
                : await this.storage.SelectAllErpProducts()
                    .AsNoTracking()
                    .Where(p => refs.Contains(p.ErpProductId))
                    .Select(p => new AutoPartsSymptomProductDto
                    {
                        Id = p.Id,
                        ErpProductId = p.ErpProductId,
                        Name = p.Name,
                        Reference = p.Reference,
                        UnitPrice = p.UnitPrice ?? p.RPrice
                    })
                    .ToListAsync(ct);

            foreach (var match in matches)
            {
                result.Matches.Add(new AutoPartsSymptomHitDto
                {
                    Code = match.Code,
                    Reason = match.Reason,
                    Products = products
                        .Where(p => match.ProductRefs.Any(r =>
                            string.Equals(r, p.ErpProductId, StringComparison.OrdinalIgnoreCase)))
                        .ToList()
                });
            }

            return result;
        }
    }
}
