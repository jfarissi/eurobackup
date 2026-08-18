using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AccountingEntriesController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;
        private readonly INumberingSequenceService numberingService;
        private readonly UserManager<User> userManager;

        public AccountingEntriesController(
            IStorageBroker storage,
            ICompanyContextService companyContext,
            INumberingSequenceService numberingService,
            UserManager<User> userManager)
        {
            this.storage = storage;
            this.companyContext = companyContext;
            this.numberingService = numberingService;
            this.userManager = userManager;
        }

        public class ManualEntryLineRequest
        {
            public string AccountCode { get; set; } = string.Empty;
            public string AccountLabel { get; set; } = string.Empty;
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
        }

        public class ManualEntryRequest
        {
            public DateTime? EntryDate { get; set; }
            public string JournalType { get; set; } = "Manual";
            public string? Description { get; set; }
            public string? ReferenceType { get; set; }
            public int ReferenceId { get; set; }
            /// <summary>Phase 3 : true = enregistre au brouillon (Draft), sans validation de période bloquante.</summary>
            public bool SaveAsDraft { get; set; }
            public List<ManualEntryLineRequest> Lines { get; set; } = new();
        }

        [HttpGet]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? referenceType = null,
            [FromQuery] int? referenceId = null,
            [FromQuery] string? journalType = null,
            [FromQuery] string? search = null)
        {
            var query = this.storage.SelectAllAccountingEntries().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (!string.IsNullOrWhiteSpace(referenceType))
                query = query.Where(e => e.ReferenceType == referenceType);
            if (referenceId.HasValue)
                query = query.Where(e => e.ReferenceId == referenceId.Value);
            if (!string.IsNullOrWhiteSpace(journalType))
                query = query.Where(e => e.JournalType == journalType);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(e =>
                    e.EntryNumber.ToLower().Contains(s) ||
                    e.Description.ToLower().Contains(s) ||
                    e.ReferenceType.ToLower().Contains(s));
            }

            var entries = query.OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.Id).Take(200).ToList();
            await ResolveCreatedByDisplayNamesAsync(entries);
            return Ok(entries);
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var entry = await this.storage.SelectAccountingEntryByIdAsync(id);
            if (entry == null || !entry.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            await ResolveCreatedByDisplayNamesAsync(new List<AccountingEntry> { entry });
            return Ok(entry);
        }

        /// <summary>Saisie manuelle d'une écriture équilibrée (débit = crédit).</summary>
        [HttpPost]
        [RequirePermission(Permissions.AccountingCreate)]
        public async Task<IActionResult> Post([FromBody] ManualEntryRequest request)
        {
            if (request.Lines == null || request.Lines.Count < 2)
                return BadRequest("Au moins deux lignes comptables sont requises.");

            var lines = request.Lines
                .Where(l => !string.IsNullOrWhiteSpace(l.AccountCode) && (l.Debit > 0 || l.Credit > 0))
                .ToList();
            if (lines.Count < 2)
                return BadRequest("Au moins deux lignes avec montant sont requises.");

            var totalDebit = lines.Sum(l => l.Debit);
            var totalCredit = lines.Sum(l => l.Credit);
            if (Math.Abs(totalDebit - totalCredit) > 0.01m)
                return BadRequest($"Écriture non équilibrée : débit {totalDebit:0.##} ≠ crédit {totalCredit:0.##}.");

            var companyId = this.companyContext.GetCurrentCompanyId();
            var actor = SalesDocumentAudit.ActorFrom(User);
            var entryDate = request.EntryDate ?? DateTime.UtcNow;

            int? fiscalPeriodId = null;
            if (!request.SaveAsDraft)
            {
                // Phase 2 : même validation de période que le générateur (verrouillée / hors exercice → 400).
                // Un brouillon n'est pas bloqué à la création : la période sera validée à la comptabilisation.
                var period = await AccountingEntryResolver.ResolvePeriodAsync(this.storage, companyId, entryDate);
                if (period.Error != null) return BadRequest(period.Error);
                fiscalPeriodId = period.Period?.Id;
            }

            // Journal des opérations diverses (null si absent : ne bloque jamais la saisie).
            var journal = await AccountingEntryResolver.ResolveJournalAsync(this.storage, companyId, "OD");

            var entry = new AccountingEntry
            {
                // Index unique (EntryNumber, CompanyId) : numéro temporaire pour le brouillon,
                // le numéro EC- définitif est attribué à la comptabilisation (POST {id}/post).
                EntryNumber = request.SaveAsDraft
                    ? $"DRAFT-{Guid.NewGuid().ToString("N")[..8]}"
                    : await this.numberingService.GetNextNumberAsync("AccountingEntry", companyId),
                EntryDate = entryDate,
                JournalType = string.IsNullOrWhiteSpace(request.JournalType) ? "Manual" : request.JournalType.Trim(),
                JournalId = journal?.Id,
                FiscalPeriodId = fiscalPeriodId,
                ReferenceType = string.IsNullOrWhiteSpace(request.ReferenceType) ? "Manual" : request.ReferenceType.Trim(),
                ReferenceId = request.ReferenceId,
                Description = string.IsNullOrWhiteSpace(request.Description) ? "Écriture manuelle" : request.Description.Trim(),
                Status = request.SaveAsDraft ? "Draft" : "Posted",
                CompanyId = companyId,
                CreatedBy = actor,
                CreatedAt = DateTime.UtcNow,
                Lines = lines.Select((l, i) => new AccountingEntryLine
                {
                    AccountCode = l.AccountCode.Trim(),
                    AccountLabel = string.IsNullOrWhiteSpace(l.AccountLabel) ? l.AccountCode.Trim() : l.AccountLabel.Trim(),
                    Debit = Math.Round(l.Debit, 4),
                    Credit = Math.Round(l.Credit, 4),
                    LineNumber = i + 1
                }).ToList()
            };

            var created = await this.storage.InsertAccountingEntryAsync(entry);
            await ResolveCreatedByDisplayNamesAsync(new List<AccountingEntry> { created });
            return Created(created);
        }

        /// <summary>Phase 3 : remplace libellé/date/lignes d'un brouillon (Draft uniquement).</summary>
        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.AccountingCreate)]
        public async Task<IActionResult> Put(int id, [FromBody] ManualEntryRequest request)
        {
            var entry = await this.storage.SelectAccountingEntryByIdAsync(id);
            if (entry == null || !entry.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var notDraft = AccountingEntryLifecycleService.RejectIfNotDraft(entry.Status);
            if (notDraft != null) return BadRequest(notDraft);

            if (request.Lines == null || request.Lines.Count < 2)
                return BadRequest("Au moins deux lignes comptables sont requises.");

            var lines = request.Lines
                .Where(l => !string.IsNullOrWhiteSpace(l.AccountCode) && (l.Debit > 0 || l.Credit > 0))
                .ToList();
            if (lines.Count < 2)
                return BadRequest("Au moins deux lignes avec montant sont requises.");

            var totalDebit = lines.Sum(l => l.Debit);
            var totalCredit = lines.Sum(l => l.Credit);
            if (Math.Abs(totalDebit - totalCredit) > 0.01m)
                return BadRequest($"Écriture non équilibrée : débit {totalDebit:0.##} ≠ crédit {totalCredit:0.##}.");

            entry.EntryDate = request.EntryDate ?? entry.EntryDate;
            entry.Description = string.IsNullOrWhiteSpace(request.Description) ? entry.Description : request.Description.Trim();
            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedBy = SalesDocumentAudit.ActorFrom(User);

            // Remplace les lignes (les anciennes, trackées, sont supprimées en cascade).
            entry.Lines.Clear();
            foreach (var (line, index) in lines.Select((l, i) => (l, i)))
            {
                entry.Lines.Add(new AccountingEntryLine
                {
                    AccountCode = line.AccountCode.Trim(),
                    AccountLabel = string.IsNullOrWhiteSpace(line.AccountLabel) ? line.AccountCode.Trim() : line.AccountLabel.Trim(),
                    Debit = Math.Round(line.Debit, 4),
                    Credit = Math.Round(line.Credit, 4),
                    LineNumber = index + 1
                });
            }

            var updated = await this.storage.UpdateAccountingEntryAsync(entry);
            return Ok(updated);
        }

        /// <summary>Phase 3 : suppression réservée aux brouillons (Draft uniquement).</summary>
        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.AccountingCreate)]
        public async Task<IActionResult> Delete(int id)
        {
            var entry = await this.storage.SelectAccountingEntryByIdAsync(id);
            if (entry == null || !entry.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var notDraft = AccountingEntryLifecycleService.RejectIfNotDraft(entry.Status);
            if (notDraft != null) return BadRequest(notDraft);

            await this.storage.DeleteAccountingEntryAsync(entry);
            return NoContent();
        }

        /// <summary>Phase 3 : comptabilise un brouillon (Draft → Posted, attribution du numéro EC-).</summary>
        [HttpPost("{id:int}/post")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> PostDraft(int id)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var entry = await this.storage.SelectAccountingEntryByIdAsync(id);
            if (entry == null || !entry.BelongsToCompany(companyId)) return NotFound();

            var (posted, error) = await AccountingEntryLifecycleService.PostDraftAsync(
                this.storage, this.numberingService, id, companyId, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(posted);
        }

        /// <summary>Phase 3 : valide une écriture comptabilisée (Posted → Validated, immuable).</summary>
        [HttpPost("{id:int}/validate")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> Validate(int id)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var entry = await this.storage.SelectAccountingEntryByIdAsync(id);
            if (entry == null || !entry.BelongsToCompany(companyId)) return NotFound();

            var (validated, error) = await AccountingEntryLifecycleService.ValidateAsync(
                this.storage, id, companyId, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(validated);
        }

        /// <summary>Phase 3 : extourne une écriture (crée l'écriture inverse, originale → Reversed). Retourne l'extourne.</summary>
        [HttpPost("{id:int}/reverse")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> Reverse(int id)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var entry = await this.storage.SelectAccountingEntryByIdAsync(id);
            if (entry == null || !entry.BelongsToCompany(companyId)) return NotFound();

            var (reversal, error) = await AccountingEntryLifecycleService.ReverseAsync(
                this.storage, this.numberingService, id, companyId, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(reversal);
        }

        /// <summary>Remplace CreatedBy stocké en GUID par Prénom Nom (ou username / email).</summary>
        private async Task ResolveCreatedByDisplayNamesAsync(List<AccountingEntry> entries)
        {
            var guidActors = entries
                .Select(e => e.CreatedBy)
                .Where(a => !string.IsNullOrWhiteSpace(a) && Guid.TryParse(a, out _))
                .Select(a => Guid.Parse(a!))
                .Distinct()
                .ToList();

            if (guidActors.Count == 0) return;

            var users = await this.userManager.Users
                .AsNoTracking()
                .Where(u => guidActors.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName, u.Email, u.Name, u.FamilyName })
                .ToListAsync();

            var map = users.ToDictionary(
                u => u.Id.ToString(),
                u => SalesDocumentAudit.FormatUserDisplayName(u.Name, u.FamilyName, u.UserName, u.Email, u.Id.ToString()),
                StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                if (entry.CreatedBy != null && map.TryGetValue(entry.CreatedBy, out var display))
                    entry.CreatedBy = display;
            }
        }
    }
}
