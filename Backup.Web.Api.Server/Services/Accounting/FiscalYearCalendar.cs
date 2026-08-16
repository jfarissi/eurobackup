using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Models.Entities.Accounting;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>Génère les périodes mensuelles (mois calendaires) d'un exercice comptable.</summary>
    public static class FiscalYearCalendar
    {
        /// <summary>Une période par mois calendaire couvert par [startDate, endDate] (12 pour une année pleine).</summary>
        public static List<FiscalPeriod> BuildMonthlyPeriods(DateTime startDate, DateTime endDate, string? companyId)
        {
            var periods = new List<FiscalPeriod>();
            var cursor = new DateTime(startDate.Year, startDate.Month, 1);
            var last = new DateTime(endDate.Year, endDate.Month, 1);
            while (cursor <= last)
            {
                periods.Add(new FiscalPeriod
                {
                    Year = cursor.Year,
                    Month = cursor.Month,
                    IsLocked = false,
                    IsVatDeclared = false,
                    IsBankReconciled = false,
                    CompanyId = companyId
                });
                cursor = cursor.AddMonths(1);
            }
            return periods;
        }

        /// <summary>Nom d'exercice : "Exercice 2026" ou "Exercice 2026-2027" si à cheval.</summary>
        public static string BuildYearName(DateTime startDate, DateTime endDate) =>
            startDate.Year == endDate.Year
                ? $"Exercice {startDate.Year}"
                : $"Exercice {startDate.Year}-{endDate.Year}";
    }
}
