using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities.Email;
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
    [Route("api/email-settings")]
    public class EmailSettingsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;
        private readonly IEmailDispatchService dispatch;

        public EmailSettingsController(IStorageBroker storage, ICompanyContextService companyContext, IEmailDispatchService dispatch)
        {
            this.storage = storage;
            this.companyContext = companyContext;
            this.dispatch = dispatch;
        }

        [HttpGet]
        [RequirePermission(Permissions.EmailSettingsManage)]
        public async Task<IActionResult> Get()
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId)) return BadRequest("Société requise.");

            var settings = await this.storage.SelectCompanyEmailSettingsByCompanyIdAsync(companyId);
            if (settings == null)
            {
                var company = this.storage.SelectAllCompanies().FirstOrDefault(c => c.Id == companyId);
                return Ok(new CompanyEmailSettings
                {
                    CompanyId = companyId,
                    FromDisplayName = company?.Name ?? "ERP",
                    MaxEmailsPerHour = 500,
                    MaxAttachmentBytes = 10 * 1024 * 1024
                });
            }

            settings.Password = null;
            return Ok(settings);
        }

        [HttpPut]
        [RequirePermission(Permissions.EmailSettingsManage)]
        public async Task<IActionResult> Put([FromBody] CompanyEmailSettings dto)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId)) return BadRequest("Société requise.");

            dto.CompanyId = companyId;
            dto.UpdatedAt = DateTime.UtcNow;
            dto.UpdatedBy = User.Identity?.Name ?? "System";

            if (string.IsNullOrWhiteSpace(dto.Password))
            {
                var existing = await this.storage.SelectCompanyEmailSettingsByCompanyIdAsync(companyId);
                if (existing != null) dto.Password = existing.Password;
            }

            var saved = await this.storage.UpsertCompanyEmailSettingsAsync(dto);
            saved.Password = null;
            return Ok(saved);
        }

        [HttpPost("test")]
        [RequirePermission(Permissions.EmailSettingsManage)]
        public async Task<IActionResult> Test([FromBody] CompanyEmailSettings? draft = null)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId)) return BadRequest(new { error = "Société requise." });

            try
            {
                CompanyEmailSettings? settings = null;
                if (draft != null && !string.IsNullOrWhiteSpace(draft.SmtpHost))
                {
                    settings = draft;
                    settings.CompanyId = companyId;
                    // Mot de passe vide dans le formulaire → reprendre celui en base
                    if (string.IsNullOrWhiteSpace(settings.Password))
                    {
                        var existing = await this.storage.SelectCompanyEmailSettingsByCompanyIdAsync(companyId);
                        if (existing != null) settings.Password = existing.Password;
                    }
                }
                else
                {
                    settings = await this.storage.SelectCompanyEmailSettingsByCompanyIdAsync(companyId);
                }

                if (settings == null || string.IsNullOrWhiteSpace(settings.SmtpHost))
                    return BadRequest(new { error = "Serveur SMTP manquant. Renseignez l'hôte puis Enregistrer, ou testez avec le formulaire." });

                await this.dispatch.TestConnectionWithSettingsAsync(settings);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
