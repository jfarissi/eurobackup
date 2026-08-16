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

            // Phase 2 : même validation de période que le générateur (verrouillée / hors exercice → 400).
            var period = await AccountingEntryResolver.ResolvePeriodAsync(this.storage, companyId, entryDate);
            if (period.Error != null) return BadRequest(period.Error);

            // Journal des opérations diverses (null si absent : ne bloque jamais la saisie).
            var journal = await AccountingEntryResolver.ResolveJournalAsync(this.storage, companyId, "OD");

            var entry = new AccountingEntry
            {
                EntryNumber = await this.numberingService.GetNextNumberAsync("AccountingEntry", companyId),
                EntryDate = entryDate,
                JournalType = string.IsNullOrWhiteSpace(request.JournalType) ? "Manual" : request.JournalType.Trim(),
                JournalId = journal?.Id,
                FiscalPeriodId = period.Period?.Id,
                ReferenceType = string.IsNullOrWhiteSpace(request.ReferenceType) ? "Manual" : request.ReferenceType.Trim(),
                ReferenceId = request.ReferenceId,
                Description = string.IsNullOrWhiteSpace(request.Description) ? "Écriture manuelle" : request.Description.Trim(),
                Status = "Posted",
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
