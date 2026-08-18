using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>Portail cabinet : dossiers multi-sociétés, annotations, validation de clôture mensuelle.</summary>
    public static class CabinetService
    {
        public sealed class DossierDto
        {
            public string CompanyId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string MissionLevel { get; set; } = "Revue";
            public bool IsActive { get; set; }
            public string? CurrentPeriod { get; set; }
            public string ClosingStatus { get; set; } = "Ouverte";
            public int UnresolvedAnnotations { get; set; }
        }

        public sealed class AnnotationDto
        {
            public int Id { get; set; }
            public int? AccountingEntryId { get; set; }
            public string Type { get; set; } = "Question";
            public string Message { get; set; } = string.Empty;
            public string? Author { get; set; }
            public bool IsResolved { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public sealed class EntryDto
        {
            public int Id { get; set; }
            public DateTime EntryDate { get; set; }
            public string EntryNumber { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
            public int AnnotationCount { get; set; }
        }

        public sealed class CompanyOptionDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        public static async Task<List<DossierDto>> ListDossiersAsync(IStorageBroker storage, string? firmCompanyId)
        {
            if (string.IsNullOrWhiteSpace(firmCompanyId)) return new List<DossierDto>();
            var firm = await EnsureFirmAsync(storage, firmCompanyId);
            var companies = storage.SelectAllCompanies().AsEnumerable().ToDictionary(c => c.Id, c => c.Name);
            var annotations = storage.SelectAllAccountingAnnotations().AsEnumerable()
                .Where(a => !a.IsResolved)
                .GroupBy(a => a.CompanyId)
                .ToDictionary(g => g.Key ?? "", g => g.Count());
            var periods = storage.SelectAllFiscalPeriods().AsEnumerable()
                .GroupBy(p => p.CompanyId)
                .ToDictionary(g => g.Key ?? "", g => g.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month).ToList());

            return firm.Clients.Where(c => c.IsActive).Select(c =>
            {
                var list = periods.GetValueOrDefault(c.ClientCompanyId) ?? new List<FiscalPeriod>();
                var current = list.FirstOrDefault(p => !p.IsLocked) ?? list.FirstOrDefault();
                return new DossierDto
                {
                    CompanyId = c.ClientCompanyId,
                    Name = companies.GetValueOrDefault(c.ClientCompanyId) ?? c.ClientCompanyId,
                    MissionLevel = c.MissionLevel,
                    IsActive = c.IsActive,
                    CurrentPeriod = current == null ? null : $"{current.Month:00}/{current.Year}",
                    ClosingStatus = current == null ? "—" : (current.IsLocked ? "Clôturée" : "Ouverte"),
                    UnresolvedAnnotations = annotations.GetValueOrDefault(c.ClientCompanyId)
                };
            }).OrderBy(d => d.Name).ToList();
        }

        public static List<CompanyOptionDto> ListLinkableCompanies(IStorageBroker storage, string? firmCompanyId) =>
            storage.SelectAllCompanies().AsEnumerable()
                .Where(c => c.IsActive && c.Id != firmCompanyId)
                .OrderBy(c => c.Name)
                .Select(c => new CompanyOptionDto { Id = c.Id, Name = c.Name })
                .ToList();

        public static async Task<(DossierDto? Dto, string? Error)> LinkClientAsync(
            IStorageBroker storage, string? firmCompanyId, string clientCompanyId, string? level, string? actor)
        {
            if (string.IsNullOrWhiteSpace(clientCompanyId)) return (null, "Société cliente requise.");
            if (clientCompanyId == firmCompanyId) return (null, "Impossible de lier le cabinet à lui-même.");
            var company = storage.SelectAllCompanies().FirstOrDefault(c => c.Id == clientCompanyId);
            if (company == null) return (null, "Société introuvable.");

            var firm = await EnsureFirmAsync(storage, firmCompanyId, actor);
            var existing = firm.Clients.FirstOrDefault(c => c.ClientCompanyId == clientCompanyId);
            if (existing == null)
            {
                firm.Clients.Add(new AccountingFirmClient
                {
                    ClientCompanyId = clientCompanyId,
                    MissionLevel = NormalizeLevel(level),
                    IsActive = true,
                    StartDate = DateTime.UtcNow.Date
                });
            }
            else
            {
                existing.IsActive = true;
                existing.MissionLevel = NormalizeLevel(level ?? existing.MissionLevel);
            }
            firm.UpdatedBy = actor;
            firm.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateAccountingFirmAsync(firm);
            var dossiers = await ListDossiersAsync(storage, firmCompanyId);
            return (dossiers.FirstOrDefault(d => d.CompanyId == clientCompanyId), null);
        }

        public static List<EntryDto> ListEntries(
            IStorageBroker storage, string? firmCompanyId, string clientCompanyId, DateTime? from, DateTime? to)
        {
            if (!HasAccess(storage, firmCompanyId, clientCompanyId)) return new List<EntryDto>();
            var notes = storage.SelectAllAccountingAnnotations().AsEnumerable()
                .Where(a => a.CompanyId == clientCompanyId)
                .GroupBy(a => a.AccountingEntryId)
                .ToDictionary(g => g.Key ?? 0, g => g.Count());
            return storage.SelectAllAccountingEntries()
                .Where(e => e.CompanyId == clientCompanyId)
                .AsEnumerable()
                .Where(e => from == null || e.EntryDate.Date >= from.Value.Date)
                .Where(e => to == null || e.EntryDate.Date <= to.Value.Date)
                .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.Id)
                .Take(200)
                .Select(e => new EntryDto
                {
                    Id = e.Id,
                    EntryDate = e.EntryDate,
                    EntryNumber = e.EntryNumber,
                    Description = e.Description,
                    Status = e.Status,
                    Debit = e.Lines.Sum(l => l.Debit),
                    Credit = e.Lines.Sum(l => l.Credit),
                    AnnotationCount = notes.GetValueOrDefault(e.Id)
                })
                .ToList();
        }

        public static List<AnnotationDto> ListAnnotations(
            IStorageBroker storage, string? firmCompanyId, string clientCompanyId, int? entryId)
        {
            if (!HasAccess(storage, firmCompanyId, clientCompanyId)) return new List<AnnotationDto>();
            return storage.SelectAllAccountingAnnotations()
                .ForCompany(clientCompanyId)
                .AsEnumerable()
                .Where(a => entryId == null || a.AccountingEntryId == entryId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(ToAnnotationDto)
                .ToList();
        }

        public static async Task<(AnnotationDto? Dto, string? Error)> AddAnnotationAsync(
            IStorageBroker storage, string? firmCompanyId, string clientCompanyId,
            string? type, string message, int? entryId, string? actor)
        {
            if (!HasAccess(storage, firmCompanyId, clientCompanyId))
                return (null, "Dossier non rattaché à ce cabinet.");
            if (string.IsNullOrWhiteSpace(message)) return (null, "Le message est obligatoire.");
            var saved = await storage.InsertAccountingAnnotationAsync(new AccountingAnnotation
            {
                CompanyId = clientCompanyId,
                AccountingEntryId = entryId,
                Type = NormalizeType(type),
                Message = message.Trim(),
                Author = actor,
                CreatedBy = actor
            });
            return (ToAnnotationDto(saved), null);
        }

        public static async Task<(AnnotationDto? Dto, string? Error)> ResolveAnnotationAsync(
            IStorageBroker storage, string? firmCompanyId, int annotationId, string? actor)
        {
            var note = storage.SelectAllAccountingAnnotations().FirstOrDefault(a => a.Id == annotationId);
            if (note == null) return (null, "Annotation introuvable.");
            if (!HasAccess(storage, firmCompanyId, note.CompanyId))
                return (null, "Dossier non rattaché à ce cabinet.");
            note.IsResolved = true;
            note.ResolvedAt = DateTime.UtcNow;
            note.UpdatedBy = actor;
            note.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateAccountingAnnotationAsync(note);
            return (ToAnnotationDto(note), null);
        }

        public static async Task<(string? Message, string? Error)> ValidateCloseAsync(
            IStorageBroker storage, string? firmCompanyId, string clientCompanyId, int year, int month, bool force, string? actor)
        {
            if (!HasAccess(storage, firmCompanyId, clientCompanyId))
                return (null, "Dossier non rattaché à ce cabinet.");
            var period = storage.SelectAllFiscalPeriods()
                .FirstOrDefault(p => p.CompanyId == clientCompanyId && p.Year == year && p.Month == month);
            if (period == null) return (null, "Période introuvable.");
            if (period.IsLocked) return (null, "Période déjà clôturée.");

            if (!force)
            {
                var monthStart = new DateTime(year, month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                var unlettered = storage.SelectAllAccountingEntries()
                    .Where(e => e.CompanyId == clientCompanyId && e.Status != "Draft" && e.Status != "Reversed")
                    .AsEnumerable()
                    .Where(e => e.EntryDate.Date >= monthStart && e.EntryDate.Date <= monthEnd)
                    .SelectMany(e => e.Lines.Select(l => (e, l)))
                    .Any(x => IsCustomerAccount(x.l.AccountCode) && string.IsNullOrEmpty(x.l.LettrageCode));
                if (unlettered)
                    return (null, "Des écritures clients ne sont pas lettrées. Relancez avec force=true pour outrepasser.");

                var openRec = storage.SelectAllBankReconciliations()
                    .Where(r => r.CompanyId == clientCompanyId && r.Status != "Balanced")
                    .AsEnumerable()
                    .Any(r => r.FromDate <= monthEnd && r.ToDate >= monthStart);
                if (openRec)
                    return (null, "Le rapprochement bancaire n'est pas équilibré. Relancez avec force=true pour outrepasser.");
            }

            period.IsLocked = true;
            period.UpdatedBy = actor;
            period.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateFiscalPeriodAsync(period);
            await storage.InsertAccountingAnnotationAsync(new AccountingAnnotation
            {
                CompanyId = clientCompanyId,
                Type = "Information",
                Message = $"Clôture {month:00}/{year} validée par le cabinet",
                Author = actor,
                IsResolved = true,
                ResolvedAt = DateTime.UtcNow,
                CreatedBy = actor
            });
            return ($"Période {month:00}/{year} clôturée.", null);
        }

        private static async Task<AccountingFirm> EnsureFirmAsync(IStorageBroker storage, string? firmCompanyId, string? actor = null)
        {
            var existing = storage.SelectAllAccountingFirms()
                .FirstOrDefault(f => f.FirmCompanyId == firmCompanyId);
            if (existing != null) return existing;
            var company = storage.SelectAllCompanies().FirstOrDefault(c => c.Id == firmCompanyId);
            return await storage.InsertAccountingFirmAsync(new AccountingFirm
            {
                Name = company?.Name ?? "Cabinet",
                FirmCompanyId = firmCompanyId ?? string.Empty,
                CreatedBy = actor
            });
        }

        private static bool HasAccess(IStorageBroker storage, string? firmCompanyId, string? clientCompanyId)
        {
            if (string.IsNullOrWhiteSpace(firmCompanyId) || string.IsNullOrWhiteSpace(clientCompanyId)) return false;
            var firm = storage.SelectAllAccountingFirms().FirstOrDefault(f => f.FirmCompanyId == firmCompanyId);
            return firm?.Clients.Any(c => c.IsActive && c.ClientCompanyId == clientCompanyId) == true;
        }

        private static bool IsCustomerAccount(string code) =>
            (code ?? "").StartsWith("411") || (code ?? "").StartsWith("342");

        private static string NormalizeLevel(string? level) =>
            level is "Saisie" or "Audit" or "Revue" ? level : "Revue";

        private static string NormalizeType(string? type) =>
            type is "Correction" or "Information" or "Avertissement" or "Question" ? type : "Question";

        private static AnnotationDto ToAnnotationDto(AccountingAnnotation a) => new()
        {
            Id = a.Id,
            AccountingEntryId = a.AccountingEntryId,
            Type = a.Type,
            Message = a.Message,
            Author = a.Author,
            IsResolved = a.IsResolved,
            CreatedAt = a.CreatedAt
        };
    }
}
