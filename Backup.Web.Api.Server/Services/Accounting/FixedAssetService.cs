using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>Fiches immobilisations, plan d'amortissement et constatation OD mensuelle.</summary>
    public static class FixedAssetService
    {
        public sealed class ScheduleLineDto
        {
            public int Id { get; set; }
            public int Year { get; set; }
            public int Month { get; set; }
            public decimal Charge { get; set; }
            public decimal Accumulated { get; set; }
            public decimal NetBookValue { get; set; }
            public bool IsPosted { get; set; }
            public int? AccountingEntryId { get; set; }
        }

        public sealed class AssetDto
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Designation { get; set; } = string.Empty;
            public string AssetAccountCode { get; set; } = string.Empty;
            public string DepreciationAccountCode { get; set; } = string.Empty;
            public string ExpenseAccountCode { get; set; } = string.Empty;
            public DateTime AcquisitionDate { get; set; }
            public DateTime ServiceDate { get; set; }
            public decimal OriginValue { get; set; }
            public decimal ResidualValue { get; set; }
            public int DurationMonths { get; set; }
            public string Mode { get; set; } = DepreciationCalculator.ModeLinear;
            public decimal? DecliningRate { get; set; }
            public decimal AccumulatedDepreciation { get; set; }
            public decimal NetBookValue { get; set; }
            public bool IsActive { get; set; }
            public DateTime? DisposalDate { get; set; }
            public List<ScheduleLineDto> Schedule { get; set; } = new();
        }

        public sealed class AssetForm
        {
            public string? Code { get; set; }
            public string Designation { get; set; } = string.Empty;
            public string? AssetAccountCode { get; set; }
            public string? DepreciationAccountCode { get; set; }
            public string? ExpenseAccountCode { get; set; }
            public DateTime AcquisitionDate { get; set; }
            public DateTime ServiceDate { get; set; }
            public decimal OriginValue { get; set; }
            public decimal ResidualValue { get; set; }
            public int DurationMonths { get; set; } = 36;
            public string Mode { get; set; } = DepreciationCalculator.ModeLinear;
        }

        public sealed class PostResultDto
        {
            public int PostedLines { get; set; }
            public int? AccountingEntryId { get; set; }
            public string? EntryNumber { get; set; }
        }

        public static List<AssetDto> List(IStorageBroker storage, string? companyId) =>
            storage.SelectAllFixedAssets()
                .ForCompany(companyId)
                .AsEnumerable()
                .OrderBy(a => a.Code)
                .Select(a => ToDto(a, includeSchedule: false))
                .ToList();

        public static AssetDto? Get(IStorageBroker storage, string? companyId, int id)
        {
            var asset = Find(storage, companyId, id);
            return asset == null ? null : ToDto(asset, includeSchedule: true);
        }

        public static async Task<(AssetDto? Dto, string? Error)> CreateAsync(
            IStorageBroker storage, string? companyId, AssetForm form, string? actor)
        {
            var error = Validate(form);
            if (error != null) return (null, error);

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, companyId);
            var defaults = DefaultAccounts(settings.PlanType);
            var code = string.IsNullOrWhiteSpace(form.Code) ? NextCode(storage, companyId) : form.Code.Trim();
            if (storage.SelectAllFixedAssets().ForCompany(companyId).Any(a => a.Code == code))
                return (null, "Ce code d'immobilisation existe déjà.");

            var asset = new FixedAsset
            {
                Code = code,
                Designation = form.Designation.Trim(),
                AssetAccountCode = Coalesce(form.AssetAccountCode, defaults.Asset),
                DepreciationAccountCode = Coalesce(form.DepreciationAccountCode, defaults.Depreciation),
                ExpenseAccountCode = Coalesce(form.ExpenseAccountCode, defaults.Expense),
                AcquisitionDate = form.AcquisitionDate.Date,
                ServiceDate = form.ServiceDate.Date,
                OriginValue = DepreciationCalculator.Round(form.OriginValue),
                ResidualValue = DepreciationCalculator.Round(form.ResidualValue),
                DurationMonths = form.DurationMonths,
                Mode = NormalizeMode(form.Mode),
                IsActive = true,
                CompanyId = companyId,
                CreatedBy = actor,
                UpdatedBy = actor
            };
            RebuildSchedule(asset);
            var saved = await storage.InsertFixedAssetAsync(asset);
            return (ToDto(saved, includeSchedule: true), null);
        }

        public static async Task<(AssetDto? Dto, string? Error)> UpdateAsync(
            IStorageBroker storage, string? companyId, int id, AssetForm form, string? actor)
        {
            var asset = Find(storage, companyId, id);
            if (asset == null) return (null, "Immobilisation introuvable.");
            var error = Validate(form);
            if (error != null) return (null, error);

            var posted = asset.Schedule.Any(s => s.IsPosted);
            asset.Designation = form.Designation.Trim();
            asset.UpdatedBy = actor;
            asset.UpdatedAt = DateTime.UtcNow;
            if (!posted)
            {
                asset.AssetAccountCode = form.AssetAccountCode?.Trim() ?? asset.AssetAccountCode;
                asset.DepreciationAccountCode = form.DepreciationAccountCode?.Trim() ?? asset.DepreciationAccountCode;
                asset.ExpenseAccountCode = form.ExpenseAccountCode?.Trim() ?? asset.ExpenseAccountCode;
                asset.AcquisitionDate = form.AcquisitionDate.Date;
                asset.ServiceDate = form.ServiceDate.Date;
                asset.OriginValue = DepreciationCalculator.Round(form.OriginValue);
                asset.ResidualValue = DepreciationCalculator.Round(form.ResidualValue);
                asset.DurationMonths = form.DurationMonths;
                asset.Mode = NormalizeMode(form.Mode);
                RebuildSchedule(asset);
            }
            await storage.UpdateFixedAssetAsync(asset);
            return (ToDto(asset, includeSchedule: true), null);
        }

        public static async Task<(AssetDto? Dto, string? Error)> RecalculateAsync(
            IStorageBroker storage, string? companyId, int id, string? actor)
        {
            var asset = Find(storage, companyId, id);
            if (asset == null) return (null, "Immobilisation introuvable.");
            RebuildSchedule(asset);
            asset.UpdatedBy = actor;
            asset.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateFixedAssetAsync(asset);
            return (ToDto(asset, includeSchedule: true), null);
        }

        public static async Task<(AssetDto? Dto, string? Error)> DeactivateAsync(
            IStorageBroker storage, string? companyId, int id, DateTime? disposalDate, string? actor)
        {
            var asset = Find(storage, companyId, id);
            if (asset == null) return (null, "Immobilisation introuvable.");
            asset.IsActive = false;
            asset.DisposalDate = (disposalDate ?? DateTime.UtcNow).Date;
            asset.UpdatedBy = actor;
            asset.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateFixedAssetAsync(asset);
            return (ToDto(asset, includeSchedule: true), null);
        }

        public static async Task<(PostResultDto? Result, string? Error)> PostMonthAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            string? companyId,
            int year,
            int month,
            string? actor)
        {
            if (month is < 1 or > 12) return (null, "Mois invalide.");
            var period = storage.SelectAllFiscalPeriods()
                .ForCompany(companyId)
                .FirstOrDefault(p => p.Year == year && p.Month == month);
            if (period is { IsLocked: true })
                return (null, $"La période {month:00}/{year} est verrouillée.");

            var assets = storage.SelectAllFixedAssets()
                .ForCompany(companyId)
                .AsEnumerable()
                .Where(a => a.IsActive)
                .ToList();
            var pending = assets
                .SelectMany(a => a.Schedule
                    .Where(s => !s.IsPosted && s.Year == year && s.Month == month && s.Charge > 0)
                    .Select(s => (Asset: a, Line: s)))
                .ToList();
            if (pending.Count == 0) return (new PostResultDto { PostedLines = 0 }, null);

            var entryDate = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            var lines = new List<AccountingEntryLine>();
            var n = 1;
            foreach (var (asset, line) in pending)
            {
                lines.Add(new AccountingEntryLine
                {
                    AccountCode = asset.ExpenseAccountCode,
                    AccountLabel = $"Dotation {asset.Code}",
                    Debit = line.Charge,
                    Credit = 0,
                    LineNumber = n++
                });
                lines.Add(new AccountingEntryLine
                {
                    AccountCode = asset.DepreciationAccountCode,
                    AccountLabel = $"Amortissement {asset.Code}",
                    Debit = 0,
                    Credit = line.Charge,
                    LineNumber = n++
                });
            }

            var journal = await AccountingEntryResolver.ResolveJournalAsync(storage, companyId, "OD");
            var entry = new AccountingEntry
            {
                EntryNumber = await numbering.GetNextNumberAsync("AccountingEntry", companyId),
                EntryDate = entryDate,
                JournalType = "OD",
                JournalId = journal?.Id,
                FiscalPeriodId = period?.Id,
                ReferenceType = "Depreciation",
                ReferenceId = year * 100 + month,
                Description = $"Dotations aux amortissements — {month:00}/{year}",
                Status = "Posted",
                CompanyId = companyId,
                CreatedBy = SalesDocumentAudit.IsReadableActor(actor) ? actor!.Trim() : null,
                Lines = lines
            };
            var saved = await storage.InsertAccountingEntryAsync(entry);

            foreach (var (asset, line) in pending)
            {
                line.IsPosted = true;
                line.AccountingEntryId = saved.Id;
                line.PostedAt = DateTime.UtcNow;
                asset.AccumulatedDepreciation = asset.Schedule.Where(s => s.IsPosted).Sum(s => s.Charge);
                asset.UpdatedAt = DateTime.UtcNow;
                await storage.UpdateFixedAssetAsync(asset);
            }

            return (new PostResultDto
            {
                PostedLines = pending.Count,
                AccountingEntryId = saved.Id,
                EntryNumber = saved.EntryNumber
            }, null);
        }

        private static void RebuildSchedule(FixedAsset asset)
        {
            var kept = asset.Schedule.Where(s => s.IsPosted).ToList();
            var plan = DepreciationCalculator.Build(
                asset.ServiceDate, asset.OriginValue, asset.ResidualValue, asset.DurationMonths, asset.Mode);
            if (string.Equals(asset.Mode, DepreciationCalculator.ModeDeclining, StringComparison.OrdinalIgnoreCase))
                asset.DecliningRate = DepreciationCalculator.DecliningCoefficient(asset.DurationMonths) / asset.DurationMonths;
            else
                asset.DecliningRate = null;

            var postedKeys = kept.Select(s => (s.Year, s.Month)).ToHashSet();
            asset.Schedule = kept;
            foreach (var item in plan)
            {
                if (postedKeys.Contains((item.Year, item.Month))) continue;
                asset.Schedule.Add(new DepreciationScheduleLine
                {
                    Year = item.Year,
                    Month = item.Month,
                    Charge = item.Charge,
                    Accumulated = item.Accumulated,
                    NetBookValue = item.NetBookValue
                });
            }
            asset.Schedule = asset.Schedule
                .OrderBy(s => s.Year).ThenBy(s => s.Month)
                .ToList();
            asset.AccumulatedDepreciation = asset.Schedule.Where(s => s.IsPosted).Sum(s => s.Charge);
        }

        private static string? Validate(AssetForm form)
        {
            if (string.IsNullOrWhiteSpace(form.Designation)) return "La désignation est obligatoire.";
            if (form.OriginValue <= 0) return "La valeur d'origine doit être positive.";
            if (form.ResidualValue < 0) return "La valeur résiduelle ne peut pas être négative.";
            if (form.ResidualValue >= form.OriginValue) return "La valeur résiduelle doit être inférieure à la valeur d'origine.";
            if (form.DurationMonths < 1) return "La durée doit être d'au moins 1 mois.";
            if (form.ServiceDate.Date < form.AcquisitionDate.Date)
                return "La date de mise en service ne peut pas précéder l'acquisition.";
            return null;
        }

        private static string NormalizeMode(string? mode) =>
            string.Equals(mode, DepreciationCalculator.ModeDeclining, StringComparison.OrdinalIgnoreCase)
                ? DepreciationCalculator.ModeDeclining
                : DepreciationCalculator.ModeLinear;

        private static string NextCode(IStorageBroker storage, string? companyId)
        {
            var max = storage.SelectAllFixedAssets().ForCompany(companyId)
                .AsEnumerable()
                .Select(a => a.Code)
                .Select(c => int.TryParse(c.Replace("IM-", "", StringComparison.OrdinalIgnoreCase), out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();
            return $"IM-{max + 1:D4}";
        }

        private static (string Asset, string Depreciation, string Expense) DefaultAccounts(string? planType) =>
            string.Equals(planType, "PcmMaroc", StringComparison.OrdinalIgnoreCase)
                ? ("235200", "283500", "618100")
                : ("218300", "281500", "681000");

        private static string Coalesce(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static FixedAsset? Find(IStorageBroker storage, string? companyId, int id) =>
            storage.SelectAllFixedAssets().ForCompany(companyId).FirstOrDefault(a => a.Id == id);

        private static AssetDto ToDto(FixedAsset asset, bool includeSchedule) => new()
        {
            Id = asset.Id,
            Code = asset.Code,
            Designation = asset.Designation,
            AssetAccountCode = asset.AssetAccountCode,
            DepreciationAccountCode = asset.DepreciationAccountCode,
            ExpenseAccountCode = asset.ExpenseAccountCode,
            AcquisitionDate = asset.AcquisitionDate,
            ServiceDate = asset.ServiceDate,
            OriginValue = asset.OriginValue,
            ResidualValue = asset.ResidualValue,
            DurationMonths = asset.DurationMonths,
            Mode = asset.Mode,
            DecliningRate = asset.DecliningRate,
            AccumulatedDepreciation = asset.AccumulatedDepreciation,
            NetBookValue = DepreciationCalculator.Round(asset.OriginValue - asset.AccumulatedDepreciation),
            IsActive = asset.IsActive,
            DisposalDate = asset.DisposalDate,
            Schedule = includeSchedule
                ? asset.Schedule.OrderBy(s => s.Year).ThenBy(s => s.Month).Select(s => new ScheduleLineDto
                {
                    Id = s.Id,
                    Year = s.Year,
                    Month = s.Month,
                    Charge = s.Charge,
                    Accumulated = s.Accumulated,
                    NetBookValue = s.NetBookValue,
                    IsPosted = s.IsPosted,
                    AccountingEntryId = s.AccountingEntryId
                }).ToList()
                : new List<ScheduleLineDto>()
        };
    }
}
