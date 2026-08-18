using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>
    /// Déclaration TVA mensuelle : collectée / déductible par taux (mapping CompanyVatRateAccount),
    /// crédit du mois précédent, TVA nette. Écritures Posted / Validated uniquement.
    /// </summary>
    public static class VatDeclarationService
    {
        public const string StatusDraft = "Draft";
        public const string StatusDeclared = "Declared";

        public sealed class VatRateRowDto
        {
            public decimal Rate { get; set; }
            public decimal CollectedBase { get; set; }
            public decimal CollectedVat { get; set; }
            public decimal DeductibleBase { get; set; }
            public decimal DeductibleVat { get; set; }
        }

        public sealed class VatDeclarationDto
        {
            public int? Id { get; set; }
            public int Year { get; set; }
            public int Month { get; set; }
            public DateTime From { get; set; }
            public DateTime To { get; set; }
            public string Status { get; set; } = StatusDraft;
            public int? FiscalPeriodId { get; set; }
            public bool PeriodVatDeclared { get; set; }
            public List<VatRateRowDto> Rates { get; set; } = new();
            public decimal TotalCollected { get; set; }
            public decimal TotalDeductible { get; set; }
            public decimal PreviousCredit { get; set; }
            public decimal NetToPay { get; set; }
            public DateTime? DeclaredAt { get; set; }
            public string? DeclaredBy { get; set; }
            public List<string> Alerts { get; set; } = new();
        }

        public sealed class ExportFile
        {
            public byte[] Content { get; set; } = Array.Empty<byte>();
            public string FileName { get; set; } = string.Empty;
        }

        public static string? ValidatePeriod(int year, int month)
        {
            if (year < 2000 || year > 2100) return "L'année est invalide.";
            if (month < 1 || month > 12) return "Le mois doit être compris entre 1 et 12.";
            return null;
        }

        /// <summary>
        /// Calcule la déclaration. Si une déclaration figée existe, retourne le snapshot ;
        /// sinon recalcule à partir des écritures de la période.
        /// </summary>
        public static async Task<VatDeclarationDto> GetAsync(
            IStorageBroker storage,
            string? companyId,
            int year,
            int month)
        {
            var existing = FindDeclared(storage, companyId, year, month);
            if (existing != null) return ToDto(existing, FindPeriod(storage, companyId, year, month));

            var live = await CalculateLiveAsync(storage, companyId, year, month);
            return live;
        }

        /// <summary>Fige le calcul, marque la période IsVatDeclared. Refuse si déjà déclarée.</summary>
        public static async Task<(VatDeclarationDto? Dto, string? Error)> DeclareAsync(
            IStorageBroker storage,
            string? companyId,
            int year,
            int month,
            string? actor)
        {
            if (FindDeclared(storage, companyId, year, month) != null)
                return (null, "Cette période est déjà déclarée.");

            var live = await CalculateLiveAsync(storage, companyId, year, month);
            var now = DateTime.UtcNow;
            var entity = new VatDeclaration
            {
                Year = year,
                Month = month,
                FiscalPeriodId = live.FiscalPeriodId,
                Status = StatusDeclared,
                TotalCollected = live.TotalCollected,
                TotalDeductible = live.TotalDeductible,
                PreviousCredit = live.PreviousCredit,
                NetToPay = live.NetToPay,
                DeclaredAt = now,
                DeclaredBy = actor,
                CompanyId = companyId,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actor,
                UpdatedBy = actor,
                Lines = live.Rates.Select(r => new VatDeclarationLine
                {
                    Rate = r.Rate,
                    CollectedBase = r.CollectedBase,
                    CollectedVat = r.CollectedVat,
                    DeductibleBase = r.DeductibleBase,
                    DeductibleVat = r.DeductibleVat
                }).ToList()
            };

            await storage.InsertVatDeclarationAsync(entity);

            var period = FindPeriod(storage, companyId, year, month);
            if (period != null)
            {
                period.IsVatDeclared = true;
                period.UpdatedAt = now;
                period.UpdatedBy = actor;
                await storage.UpdateFiscalPeriodAsync(period);
            }

            return (ToDto(entity, period), null);
        }

        /// <summary>Annule la déclaration et remet IsVatDeclared à false (période non verrouillée).</summary>
        public static async Task<(bool Ok, string? Error)> UndeclareAsync(
            IStorageBroker storage,
            string? companyId,
            int year,
            int month,
            string? actor)
        {
            var existing = FindDeclared(storage, companyId, year, month);
            if (existing == null) return (false, "Aucune déclaration à annuler pour cette période.");

            var period = FindPeriod(storage, companyId, year, month);
            if (period is { IsLocked: true })
                return (false, "La période est verrouillée : déverrouillez-la avant d'annuler la déclaration TVA.");

            await storage.DeleteVatDeclarationAsync(existing);

            if (period != null)
            {
                period.IsVatDeclared = false;
                period.UpdatedAt = DateTime.UtcNow;
                period.UpdatedBy = actor;
                await storage.UpdateFiscalPeriodAsync(period);
            }

            return (true, null);
        }

        /// <summary>Fichier XML DGI (simpl-TVA) à partir du calcul live ou du snapshot déclaré.</summary>
        public static async Task<(ExportFile? File, string? Error)> ExportEdiAsync(
            IStorageBroker storage,
            string? companyId,
            int year,
            int month)
        {
            var periodError = ValidatePeriod(year, month);
            if (periodError != null) return (null, periodError);

            var dto = await GetAsync(storage, companyId, year, month);
            var companyName = storage.SelectAllCompanies().AsEnumerable()
                .FirstOrDefault(c => c.Id == companyId)?.Name;
            var xml = BuildDgiXml(dto, companyId, companyName);
            return (new ExportFile
            {
                Content = Encoding.UTF8.GetBytes(xml),
                FileName = $"TVA_{month:00}_{year}.xml"
            }, null);
        }

        public static string BuildDgiXml(VatDeclarationDto dto, string? companyId, string? companyName)
        {
            string Amt(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);
            VatRateRowDto Rate(decimal rate) =>
                dto.Rates.FirstOrDefault(r => r.Rate == rate) ?? new VatRateRowDto { Rate = rate };

            var r20 = Rate(20m);
            var r14 = Rate(14m);
            var r10 = Rate(10m);
            var r7 = Rate(7m);
            var extra = dto.Rates.Where(r => r.Rate is not (20m or 14m or 10m or 7m)).ToList();

            var collected = new XElement("TVA_Collectee",
                RateNode("Taux20", r20),
                RateNode("Taux14", r14),
                RateNode("Taux10", r10),
                RateNode("Taux7", r7),
                extra.Select(r => RateNode($"Taux{r.Rate.ToString(CultureInfo.InvariantCulture).Replace('.', '_')}", r)),
                new XElement("Total", Amt(dto.TotalCollected)));

            var xml = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement("DeclarationTVA",
                    new XAttribute("Version", "1.0"),
                    new XElement("Identifiant",
                        new XElement("SocieteId", companyId ?? ""),
                        new XElement("Nom", companyName ?? "")),
                    new XElement("Periode",
                        new XElement("Mois", dto.Month),
                        new XElement("Annee", dto.Year)),
                    collected,
                    new XElement("TVA_Deductible",
                        new XElement("Biens", Amt(dto.TotalDeductible)),
                        new XElement("Services", Amt(0m)),
                        new XElement("Total", Amt(dto.TotalDeductible))),
                    new XElement("Credit_Precedent", Amt(dto.PreviousCredit)),
                    new XElement("TVA_Nette", Amt(dto.NetToPay)),
                    new XElement("Statut", dto.Status),
                    new XElement("DateGeneration", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))));

            return xml.Declaration + Environment.NewLine + xml.ToString();

            XElement RateNode(string name, VatRateRowDto row) =>
                new XElement(name,
                    new XElement("Base", Amt(row.CollectedBase)),
                    new XElement("Montant", Amt(row.CollectedVat)),
                    new XElement("BaseDeductible", Amt(row.DeductibleBase)),
                    new XElement("MontantDeductible", Amt(row.DeductibleVat)));
        }

        internal static async Task<VatDeclarationDto> CalculateLiveAsync(
            IStorageBroker storage,
            string? companyId,
            int year,
            int month)
        {
            var from = new DateTime(year, month, 1);
            var to = from.AddMonths(1).AddDays(-1);
            var period = FindPeriod(storage, companyId, year, month);
            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, companyId);
            var (collectedByAccount, deductibleByAccount) = BuildAccountMaps(storage, companyId, settings);

            var collectedByRate = new Dictionary<decimal, decimal>();
            var deductibleByRate = new Dictionary<decimal, decimal>();

            foreach (var (entry, line) in LoadBookedLines(storage, companyId, from, to))
            {
                if (string.IsNullOrWhiteSpace(line.AccountCode)) continue;
                var code = line.AccountCode.Trim();
                var netCredit = line.Credit - line.Debit;
                var netDebit = line.Debit - line.Credit;

                if (collectedByAccount.TryGetValue(code, out var collectedRate) && netCredit != 0)
                    AddRate(collectedByRate, collectedRate, netCredit);
                if (deductibleByAccount.TryGetValue(code, out var deductibleRate) && netDebit != 0)
                    AddRate(deductibleByRate, deductibleRate, netDebit);
            }

            var rates = collectedByRate.Keys
                .Union(deductibleByRate.Keys)
                .OrderBy(r => r == 0m ? decimal.MaxValue : r)
                .Select(rate =>
                {
                    collectedByRate.TryGetValue(rate, out var collectedVat);
                    deductibleByRate.TryGetValue(rate, out var deductibleVat);
                    return new VatRateRowDto
                    {
                        Rate = rate,
                        CollectedVat = Round(collectedVat),
                        CollectedBase = InferBase(collectedVat, rate),
                        DeductibleVat = Round(deductibleVat),
                        DeductibleBase = InferBase(deductibleVat, rate)
                    };
                })
                .Where(r => r.CollectedVat != 0 || r.DeductibleVat != 0)
                .ToList();

            var totalCollected = Round(rates.Sum(r => r.CollectedVat));
            var totalDeductible = Round(rates.Sum(r => r.DeductibleVat));
            var previousCredit = PreviousCredit(storage, companyId, year, month);
            var net = Round(totalCollected - totalDeductible - previousCredit);

            return new VatDeclarationDto
            {
                Year = year,
                Month = month,
                From = from,
                To = to,
                Status = StatusDraft,
                FiscalPeriodId = period?.Id,
                PeriodVatDeclared = period?.IsVatDeclared == true,
                Rates = rates,
                TotalCollected = totalCollected,
                TotalDeductible = totalDeductible,
                PreviousCredit = previousCredit,
                NetToPay = net,
                Alerts = BuildAlerts(rates, totalCollected, totalDeductible, net)
            };
        }

        private static void AddRate(Dictionary<decimal, decimal> map, decimal? rate, decimal amount)
        {
            var key = rate ?? 0m;
            map[key] = map.GetValueOrDefault(key) + amount;
        }

        private static decimal InferBase(decimal vat, decimal rate)
        {
            if (rate <= 0 || vat == 0) return 0m;
            return Round(vat / (rate / 100m));
        }

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private static decimal PreviousCredit(IStorageBroker storage, string? companyId, int year, int month)
        {
            var prevMonth = month == 1 ? 12 : month - 1;
            var prevYear = month == 1 ? year - 1 : year;
            var previous = FindDeclared(storage, companyId, prevYear, prevMonth);
            if (previous == null || previous.NetToPay >= 0) return 0m;
            return Round(-previous.NetToPay);
        }

        private static List<string> BuildAlerts(
            List<VatRateRowDto> rates,
            decimal totalCollected,
            decimal totalDeductible,
            decimal net)
        {
            var alerts = new List<string>();
            if (net < 0 && Math.Abs(net) > 30000m) alerts.Add("largeCredit");
            if (totalCollected == 0 && totalDeductible > 10000m) alerts.Add("deductibleWithoutCollected");
            foreach (var row in rates)
            {
                if (row.Rate <= 0 || row.CollectedBase <= 0) continue;
                var expected = row.CollectedBase * (row.Rate / 100m);
                if (Math.Abs(expected - row.CollectedVat) > 0.05m) alerts.Add("rateMismatch");
            }
            return alerts.Distinct().ToList();
        }

        private static (Dictionary<string, decimal?> Collected, Dictionary<string, decimal?> Deductible) BuildAccountMaps(
            IStorageBroker storage,
            string? companyId,
            CompanyAccountingSettings settings)
        {
            var collected = new Dictionary<string, decimal?>(StringComparer.Ordinal);
            var deductible = new Dictionary<string, decimal?>(StringComparer.Ordinal);
            var mappings = storage.SelectAllCompanyVatRateAccounts()
                .ForCompany(companyId)
                .AsEnumerable()
                .ToList();

            foreach (var mapping in mappings)
            {
                Register(collected, mapping.CollectedAccountCode, mapping.Rate);
                Register(deductible, mapping.DeductibleAccountCode, mapping.Rate);
            }

            RegisterIfMissing(collected, settings.VatCollectedAccountCode);
            RegisterIfMissing(deductible, settings.VatDeductibleAccountCode);
            return (collected, deductible);
        }

        private static void Register(Dictionary<string, decimal?> map, string? account, decimal rate)
        {
            if (string.IsNullOrWhiteSpace(account)) return;
            var code = account.Trim();
            if (map.TryGetValue(code, out var existing) && existing != null && existing != rate)
                map[code] = null;
            else
                map[code] = rate;
        }

        private static void RegisterIfMissing(Dictionary<string, decimal?> map, string? account)
        {
            if (string.IsNullOrWhiteSpace(account)) return;
            var code = account.Trim();
            if (!map.ContainsKey(code)) map[code] = null;
        }

        private static IEnumerable<(AccountingEntry Entry, AccountingEntryLine Line)> LoadBookedLines(
            IStorageBroker storage,
            string? companyId,
            DateTime from,
            DateTime to)
        {
            return storage.SelectAllAccountingEntries()
                .ForCompany(companyId)
                .Where(e => e.Status == "Posted" || e.Status == "Validated")
                .AsEnumerable()
                .Where(e => e.EntryDate.Date >= from && e.EntryDate.Date <= to)
                .SelectMany(e => (e.Lines ?? new List<AccountingEntryLine>())
                    .Select(l => (Entry: e, Line: l)));
        }

        private static VatDeclaration? FindDeclared(IStorageBroker storage, string? companyId, int year, int month) =>
            storage.SelectAllVatDeclarations()
                .ForCompany(companyId)
                .AsEnumerable()
                .FirstOrDefault(d => d.Year == year && d.Month == month && d.Status == StatusDeclared);

        private static FiscalPeriod? FindPeriod(IStorageBroker storage, string? companyId, int year, int month) =>
            storage.SelectAllFiscalPeriods()
                .ForCompany(companyId)
                .FirstOrDefault(p => p.Year == year && p.Month == month);

        private static VatDeclarationDto ToDto(VatDeclaration entity, FiscalPeriod? period)
        {
            var from = new DateTime(entity.Year, entity.Month, 1);
            var rates = (entity.Lines ?? new List<VatDeclarationLine>())
                .OrderBy(l => l.Rate == 0m ? decimal.MaxValue : l.Rate)
                .Select(l => new VatRateRowDto
                {
                    Rate = l.Rate,
                    CollectedBase = l.CollectedBase,
                    CollectedVat = l.CollectedVat,
                    DeductibleBase = l.DeductibleBase,
                    DeductibleVat = l.DeductibleVat
                })
                .ToList();

            return new VatDeclarationDto
            {
                Id = entity.Id,
                Year = entity.Year,
                Month = entity.Month,
                From = from,
                To = from.AddMonths(1).AddDays(-1),
                Status = entity.Status,
                FiscalPeriodId = entity.FiscalPeriodId ?? period?.Id,
                PeriodVatDeclared = period?.IsVatDeclared == true,
                Rates = rates,
                TotalCollected = entity.TotalCollected,
                TotalDeductible = entity.TotalDeductible,
                PreviousCredit = entity.PreviousCredit,
                NetToPay = entity.NetToPay,
                DeclaredAt = entity.DeclaredAt,
                DeclaredBy = entity.DeclaredBy,
                Alerts = BuildAlerts(rates, entity.TotalCollected, entity.TotalDeductible, entity.NetToPay)
            };
        }
    }
}
