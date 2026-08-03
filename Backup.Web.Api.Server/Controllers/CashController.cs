using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CashController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public CashController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        [HttpGet("active-session")]
        [RequirePermission(Permissions.CashRead)]
        public async Task<IActionResult> GetActiveSession()
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var active = await this.storage.SelectActiveCashSessionAsync(companyId);
            // 200 + null (pas 404) : absence de session = état normal, évite le bruit console côté SPA
            if (active == null) return new JsonResult(null);
            return Ok(active);
        }

        [HttpGet("sessions")]
        [RequirePermission(Permissions.CashRead)]
        public async Task<IActionResult> GetSessions([FromQuery] int take = 50)
        {
            take = Math.Clamp(take, 1, 200);
            var query = this.storage.SelectAllCashSessions()
                .ForCompany(this.companyContext.GetCurrentCompanyId());

            var sessions = await query
                .OrderByDescending(s => s.OpenedAt)
                .Take(take)
                .ToListAsync();

            return Ok(sessions);
        }

        [HttpGet("sessions/{id:int}")]
        [RequirePermission(Permissions.CashRead)]
        public async Task<IActionResult> GetSessionById(int id)
        {
            var session = await this.storage.SelectCashSessionByIdAsync(id);
            if (session == null || !session.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Session de caisse non trouvée");
            return Ok(session);
        }

        public class OpenSessionRequest
        {
            public decimal OpeningBalance { get; set; }
        }

        [HttpPost("open-session")]
        [RequirePermission(Permissions.CashManage)]
        public async Task<IActionResult> OpenSession([FromBody] OpenSessionRequest request)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var existing = await this.storage.SelectActiveCashSessionAsync(companyId);
            if (existing != null) return BadRequest("Une session de caisse est déjà ouverte");

            var session = new CashSession
            {
                SessionNumber = "CS-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"),
                OpenedAt = DateTime.UtcNow,
                OpeningBalance = request.OpeningBalance,
                Status = "Open",
                OpenedBy = User.Identity?.Name ?? "System",
                CompanyId = companyId
            };

            var created = await this.storage.InsertCashSessionAsync(session);
            return Created(created);
        }

        public class CloseSessionRequest
        {
            public decimal ClosingBalance { get; set; }
        }

        [HttpPost("close-session/{id:int}")]
        [RequirePermission(Permissions.CashManage)]
        public async Task<IActionResult> CloseSession(int id, [FromBody] CloseSessionRequest request)
        {
            var session = await this.storage.SelectCashSessionByIdAsync(id);
            if (session == null || !session.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Session de caisse non trouvée");
            if (session.Status == "Closed") return BadRequest("Session déjà fermée");

            decimal totalOps = session.Operations.Sum(o => o.OperationType == "Deposit" || o.OperationType == "SalePayment" ? o.Amount : -o.Amount);
            session.ExpectedClosingBalance = session.OpeningBalance + totalOps;
            session.ClosingBalance = request.ClosingBalance;
            session.ClosedAt = DateTime.UtcNow;
            session.ClosedBy = User.Identity?.Name ?? "System";
            session.Status = "Closed";

            var updated = await this.storage.UpdateCashSessionAsync(session);
            return Ok(updated);
        }

        public class OperationRequest
        {
            public int CashSessionId { get; set; }
            public string OperationType { get; set; } = "Deposit";
            public decimal Amount { get; set; }
            public string? Description { get; set; }
            public string? ReferenceDocument { get; set; }
        }

        [HttpPost("operation")]
        [RequirePermission(Permissions.CashManage)]
        public async Task<IActionResult> PostOperation([FromBody] OperationRequest request)
        {
            var session = await this.storage.SelectCashSessionByIdAsync(request.CashSessionId);
            if (session == null || !session.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return BadRequest("Session de caisse invalide ou fermée");
            if (session.Status == "Closed") return BadRequest("Session de caisse invalide ou fermée");

            var op = new CashOperation
            {
                CashSessionId = request.CashSessionId,
                OperationType = request.OperationType,
                Amount = request.Amount,
                Description = request.Description,
                ReferenceDocument = request.ReferenceDocument,
                CreatedBy = User.Identity?.Name ?? "System",
                CreatedAt = DateTime.UtcNow
            };

            var created = await this.storage.InsertCashOperationAsync(op);
            return Created(created);
        }
    }
}
