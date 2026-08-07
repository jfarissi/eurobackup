using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Email;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/emails")]
    public class EmailsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;
        private readonly IEmailDispatchService dispatch;
        private readonly IEmailAutomationService automation;

        public EmailsController(
            IStorageBroker storage,
            ICompanyContextService companyContext,
            IEmailDispatchService dispatch,
            IEmailAutomationService automation)
        {
            this.storage = storage;
            this.companyContext = companyContext;
            this.dispatch = dispatch;
            this.automation = automation;
        }

        [HttpGet]
        [RequirePermission(Permissions.EmailRead)]
        public IActionResult GetHistory([FromQuery] string? documentType = null, [FromQuery] int? documentId = null, [FromQuery] int limit = 50)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var query = this.storage.SelectAllEmailMessages().ForCompany(companyId);
            if (!string.IsNullOrWhiteSpace(documentType))
                query = query.Where(m => m.DocumentType == documentType);
            if (documentId.HasValue)
                query = query.Where(m => m.DocumentId == documentId);

            var items = query
                .OrderByDescending(m => m.CreatedAt)
                .Take(Math.Clamp(limit, 1, 200))
                .Select(m => new
                {
                    m.Id,
                    m.TrackingId,
                    m.TemplateCode,
                    m.DocumentType,
                    m.DocumentId,
                    m.DocumentNumber,
                    m.ToEmail,
                    m.CcEmails,
                    m.Subject,
                    m.Status,
                    m.ScheduledAt,
                    m.SentAt,
                    m.LastError,
                    m.CreatedBy,
                    m.CreatedAt,
                    HasAttachment = m.AttachmentBytes != null && m.AttachmentBytes.Length > 0
                })
                .ToList();

            return Ok(items);
        }

        [HttpGet("preview")]
        [RequirePermission(Permissions.EmailSend)]
        public async Task<IActionResult> Preview([FromQuery] string documentType, [FromQuery] int documentId, [FromQuery] string? templateCode = null)
        {
            if (string.IsNullOrWhiteSpace(documentType) || documentId <= 0)
                return BadRequest("documentType et documentId requis.");

            var preview = await this.dispatch.PreviewAsync(this.companyContext.GetCurrentCompanyId(), documentType, documentId, templateCode);
            if (preview == null) return NotFound("Document introuvable.");
            return Ok(preview);
        }

        [HttpPost("send")]
        [RequirePermission(Permissions.EmailSend)]
        public async Task<IActionResult> Send([FromBody] SendEmailRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentType) || request.DocumentId <= 0)
                return BadRequest("documentType et documentId requis.");

            try
            {
                var message = await this.dispatch.QueueAsync(
                    this.companyContext.GetCurrentCompanyId(),
                    request,
                    User.Identity?.Name ?? "System");

                return Ok(new
                {
                    message.Id,
                    message.TrackingId,
                    message.Status,
                    message.ToEmail,
                    message.Subject,
                    message.SentAt,
                    message.LastError
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("reminders/run")]
        [RequirePermission(Permissions.EmailSend)]
        public async Task<IActionResult> RunPaymentReminders()
        {
            var result = await this.automation.RunPaymentRemindersAsync(
                this.companyContext.GetCurrentCompanyId(),
                User.Identity?.Name ?? "System",
                manual: true);
            return Ok(result);
        }

        [HttpPost("stock-alerts/run")]
        [RequirePermission(Permissions.EmailSend)]
        public async Task<IActionResult> RunStockAlerts()
        {
            var result = await this.automation.RunStockAlertsAsync(
                this.companyContext.GetCurrentCompanyId(),
                User.Identity?.Name ?? "System");
            return Ok(result);
        }
    }
}
