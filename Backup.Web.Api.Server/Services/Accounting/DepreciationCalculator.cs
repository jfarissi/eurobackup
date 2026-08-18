using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>
    /// Plan d'amortissement mensuel : linéaire (RG-AM3 prorata 1er mois) et dégressif (RG-AM5, bascule linéaire).
    /// </summary>
    public static class DepreciationCalculator
    {
        public const string ModeLinear = "Lineaire";
        public const string ModeDeclining = "Degressif";

        public sealed class ScheduleItem
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public decimal Charge { get; set; }
            public decimal Accumulated { get; set; }
            public decimal NetBookValue { get; set; }
        }

        public static decimal Round(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);

        public static decimal DecliningCoefficient(int durationMonths)
        {
            if (durationMonths <= 48) return 1.75m;
            if (durationMonths <= 72) return 2.25m;
            return 2.75m;
        }

        public static List<ScheduleItem> Build(
            DateTime serviceDate,
            decimal originValue,
            decimal residualValue,
            int durationMonths,
            string mode)
        {
            if (durationMonths < 1) throw new InvalidOperationException("La durée d'amortissement doit être d'au moins 1 mois.");
            var baseAmount = Round(originValue - residualValue);
            if (baseAmount <= 0) throw new InvalidOperationException("La base amortissable doit être positive.");

            var declining = string.Equals(mode, ModeDeclining, StringComparison.OrdinalIgnoreCase)
                && durationMonths >= 36;
            return declining
                ? BuildDeclining(serviceDate, originValue, baseAmount, durationMonths)
                : BuildLinear(serviceDate, originValue, baseAmount, durationMonths);
        }

        private static List<ScheduleItem> BuildLinear(
            DateTime serviceDate, decimal origin, decimal baseAmount, int durationMonths)
        {
            var monthly = Round(baseAmount / durationMonths);
            var daysInMonth = DateTime.DaysInMonth(serviceDate.Year, serviceDate.Month);
            var daysLeft = daysInMonth - serviceDate.Day + 1;
            var first = Round(monthly * daysLeft / daysInMonth);

            var plan = new List<ScheduleItem>(durationMonths);
            var cumul = 0m;
            var cursor = new DateTime(serviceDate.Year, serviceDate.Month, 1);
            for (var i = 0; i < durationMonths; i++)
            {
                var remaining = Round(baseAmount - cumul);
                decimal charge;
                if (i == 0) charge = first;
                else if (i == durationMonths - 1) charge = remaining;
                else charge = monthly;
                charge = Math.Min(Round(charge), remaining);
                if (charge < 0) charge = 0;
                cumul = Round(cumul + charge);
                plan.Add(Item(cursor, charge, cumul, origin));
                cursor = cursor.AddMonths(1);
            }
            return plan;
        }

        private static List<ScheduleItem> BuildDeclining(
            DateTime serviceDate, decimal origin, decimal baseAmount, int durationMonths)
        {
            var coefficient = DecliningCoefficient(durationMonths);
            var rate = coefficient / durationMonths;
            var plan = new List<ScheduleItem>();
            var vncBase = baseAmount;
            var cumul = 0m;
            var cursor = new DateTime(serviceDate.Year, serviceDate.Month, 1);
            var remaining = durationMonths;
            var first = true;

            while (vncBase > 0.01m && remaining > 0)
            {
                var declining = vncBase * rate;
                var linearReliquat = vncBase / remaining;
                var charge = declining < linearReliquat ? linearReliquat : declining;
                if (first)
                {
                    var daysInMonth = DateTime.DaysInMonth(serviceDate.Year, serviceDate.Month);
                    var daysLeft = daysInMonth - serviceDate.Day + 1;
                    charge = charge * daysLeft / daysInMonth;
                    first = false;
                }
                if (remaining == 1) charge = vncBase;
                charge = Math.Min(Round(charge), Round(vncBase));
                if (charge < 0) charge = 0;
                cumul = Round(cumul + charge);
                vncBase = Round(baseAmount - cumul);
                plan.Add(Item(cursor, charge, cumul, origin));
                cursor = cursor.AddMonths(1);
                remaining--;
            }
            return plan;
        }

        private static ScheduleItem Item(DateTime cursor, decimal charge, decimal cumul, decimal origin) => new()
        {
            Year = cursor.Year,
            Month = cursor.Month,
            Charge = charge,
            Accumulated = cumul,
            NetBookValue = Round(origin - cumul)
        };
    }
}
