using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/fiscal-years")]
    public class FiscalYearsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public FiscalYearsController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        public class OpenFiscalYearRequest
        {
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string? Name { get; set; }
        }

        [HttpGet]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult GetAll()
        {
            var years = this.storage.SelectAllFiscalYears()
                .ForCompany(this.companyContext.GetCurrentCompanyId())
                .OrderByDescending(f => f.StartDate)
                .ToList();
            foreach (var year in years)
                year.Periods = year.Periods.OrderBy(p => p.Year).ThenBy(p => p.Month).ToList();
            return Ok(years);
        }

        /// <summary>Ouvre un exercice : crée l'exercice + ses périodes mensuelles. Refuse le chevauchement avec un exercice ouvert.</summary>
        [HttpPost("open")]
        [RequirePermission(Permissions.AccountingManageFiscalYears)]
        public async Task<IActionResult> Open([FromBody] OpenFiscalYearRequest request)
        {
            var start = request.StartDate.Date;
            var end = request.EndDate.Date;
            if (start == default || end == default) return BadRequest("Dates de début et de fin requises.");
            if (end < start) return BadRequest("La date de fin doit être postérieure à la date de début.");

            var companyId = this.companyContext.GetCurrentCompanyId();
            var overlapsOpen = this.storage.SelectAllFiscalYears()
                .ForCompany(companyId)
                .Any(f => f.Status == "Open" && f.StartDate <= end && f.EndDate >= start);
            if (overlapsOpen) return BadRequest("Cette période chevauche un exercice déjà ouvert.");

            var fiscalYear = new FiscalYear
            {
                Name = string.IsNullOrWhiteSpace(request.Name)
                    ? FiscalYearCalendar.BuildYearName(start, end)
                    : request.Name.Trim(),
                StartDate = start,
                EndDate = end,
                Status = "Open",
                CompanyId = companyId,
                Periods = FiscalYearCalendar.BuildMonthlyPeriods(start, end, companyId)
            };

            var created = await this.storage.InsertFiscalYearAsync(fiscalYear);
            created.Periods = created.Periods.OrderBy(p => p.Year).ThenBy(p => p.Month).ToList();
            return Created(created);
        }

        /// <summary>Verrouille une période (plus aucune écriture ne devra y être postée).</summary>
        [HttpPost("periods/{id:int}/lock")]
        [RequirePermission(Permissions.AccountingManageFiscalYears)]
        public async Task<IActionResult> LockPeriod(int id) => await this.SetPeriodLockedAsync(id, true);

        /// <summary>Déverrouille une période.</summary>
        [HttpPost("periods/{id:int}/unlock")]
        [RequirePermission(Permissions.AccountingManageFiscalYears)]
        public async Task<IActionResult> UnlockPeriod(int id) => await this.SetPeriodLockedAsync(id, false);

        private async Task<IActionResult> SetPeriodLockedAsync(int id, bool locked)
        {
            var period = await this.storage.SelectFiscalPeriodByIdAsync(id);
            if (period == null || !period.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            period.IsLocked = locked;
            period.UpdatedAt = DateTime.UtcNow;
            var updated = await this.storage.UpdateFiscalPeriodAsync(period);
            return Ok(updated);
        }
    }
}
