using System;

namespace Backup.Web.Api.Server.Services.Stock
{
    /// <summary>Coût Moyen Unitaire Pondéré (CMUP / CMP) — recalcul à chaque entrée valorisée.</summary>
    public static class CmupCalculator
    {
        /// <summary>
        /// Nouveau CMUP après une entrée : (stock × CMUP + qté × coût) / (stock + qté).
        /// Si stock avant ≤ 0, le CMUP devient le coût d'entrée.
        /// </summary>
        public static decimal AfterInbound(decimal qtyBefore, decimal averageBefore, decimal qtyIn, decimal unitCost)
        {
            if (qtyIn <= 0.0000001m) return Math.Max(0m, averageBefore);
            var before = qtyBefore;
            if (before <= 0.0001m)
                return Math.Max(0m, unitCost);

            var totalQty = before + qtyIn;
            if (totalQty <= 0.0001m)
                return Math.Max(0m, unitCost);

            return Round((before * Math.Max(0m, averageBefore) + qtyIn * unitCost) / totalQty);
        }

        public static decimal Round(decimal value) =>
            Math.Round(value, 4, MidpointRounding.AwayFromZero);
    }
}
