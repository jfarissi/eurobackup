using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    public interface IVehicleKTypeResolver
    {
        /// <summary>
        /// Déduit le K-Type TecDoc le plus fréquent pour Make/Model/(année)/(moteur)
        /// à partir de ErpProductVehicles (catalogue sync).
        /// </summary>
        Task<string?> ResolveAsync(
            string? make,
            string? model,
            int? year = null,
            string? engineCode = null,
            CancellationToken ct = default);
    }

    public class VehicleKTypeResolver : IVehicleKTypeResolver
    {
        private readonly IStorageBroker storage;

        public VehicleKTypeResolver(IStorageBroker storage)
        {
            this.storage = storage;
        }

        public async Task<string?> ResolveAsync(
            string? make,
            string? model,
            int? year = null,
            string? engineCode = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(make) || string.IsNullOrWhiteSpace(model))
                return null;

            var makeAliases = VehicleMakeAliases.Expand(make);
            var modelLower = model.Trim().ToLowerInvariant();
            var engine = engineCode?.Trim();

            var query = storage.SelectAllErpProductVehicles().AsNoTracking()
                .Where(v =>
                    v.KType != null && v.KType != ""
                    && makeAliases.Contains(v.Make.ToLower())
                    && (v.Model.ToLower() == modelLower || v.Model.ToLower().StartsWith(modelLower)));

            if (year.HasValue)
            {
                var y = year.Value;
                query = query.Where(v =>
                    (!v.YearFrom.HasValue || v.YearFrom <= y)
                    && (!v.YearTo.HasValue || v.YearTo >= y));
            }

            if (!string.IsNullOrWhiteSpace(engine))
            {
                var engineLower = engine.ToLowerInvariant();
                var withEngine = query.Where(v =>
                    v.EngineCode != null && v.EngineCode.ToLower() == engineLower);
                var enginePick = await PickTopKTypeAsync(withEngine, ct);
                if (!string.IsNullOrWhiteSpace(enginePick))
                    return enginePick;
            }

            return await PickTopKTypeAsync(query, ct);
        }

        private static async Task<string?> PickTopKTypeAsync(
            IQueryable<Models.Catalog.ErpProductVehicle> query,
            CancellationToken ct)
        {
            var top = await query
                .GroupBy(v => v.KType!)
                .Select(g => new { KType = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.KType)
                .FirstOrDefaultAsync(ct);

            return string.IsNullOrWhiteSpace(top?.KType) ? null : top.KType.Trim();
        }
    }
}
