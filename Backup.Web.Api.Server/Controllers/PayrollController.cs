using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/payroll")]
    public class PayrollController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numbering;
        private readonly ICompanyContextService companyContext;

        public PayrollController(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numbering = numbering;
            this.companyContext = companyContext;
        }

        [HttpGet("employees")]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult ListEmployees() =>
            Ok(PayrollService.ListEmployees(this.storage, this.companyContext.GetCurrentCompanyId()));

        [HttpPost("employees")]
        [RequirePermission(Permissions.AccountingCreate)]
        public async Task<IActionResult> CreateEmployee([FromBody] PayrollService.EmployeeForm form)
        {
            var (dto, error) = await PayrollService.UpsertEmployeeAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), null, form, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        [HttpPut("employees/{id:int}")]
        [RequirePermission(Permissions.AccountingCreate)]
        public async Task<IActionResult> UpdateEmployee(int id, [FromBody] PayrollService.EmployeeForm form)
        {
            var (dto, error) = await PayrollService.UpsertEmployeeAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), id, form, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        [HttpGet("payslips")]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult ListPayslips([FromQuery] int year, [FromQuery] int month) =>
            Ok(PayrollService.ListPayslips(this.storage, this.companyContext.GetCurrentCompanyId(), year, month));

        public class CalculateRequest
        {
            public int? EmployeeId { get; set; }
            public int Year { get; set; }
            public int Month { get; set; }
        }

        [HttpPost("payslips/calculate")]
        [RequirePermission(Permissions.AccountingCreate)]
        public async Task<IActionResult> Calculate([FromBody] CalculateRequest request)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var actor = SalesDocumentAudit.ActorFrom(User);
            if (request.EmployeeId is int employeeId)
            {
                var (dto, error) = await PayrollService.CalculateAsync(
                    this.storage, companyId, employeeId, request.Year, request.Month, actor);
                if (error != null) return BadRequest(error);
                return Ok(dto);
            }

            var (count, allError) = await PayrollService.CalculateAllAsync(
                this.storage, companyId, request.Year, request.Month, actor);
            if (allError != null) return BadRequest(allError);
            return Ok(new { count });
        }

        [HttpPost("payslips/post")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> Post([FromQuery] int year, [FromQuery] int month)
        {
            var (result, error) = await PayrollService.PostMonthAsync(
                this.storage,
                this.numbering,
                this.companyContext.GetCurrentCompanyId(),
                year,
                month,
                SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(result);
        }

        [HttpGet("cnss")]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> ExportCnss(
            [FromQuery] int year, [FromQuery] int month, [FromQuery] string? format)
        {
            var (file, error) = await PayrollService.ExportCnssAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), year, month, format);
            if (error != null) return BadRequest(error);
            var mime = (file!.FileName.EndsWith(".xml", System.StringComparison.OrdinalIgnoreCase))
                ? "application/xml; charset=utf-8"
                : "text/plain; charset=utf-8";
            return File(file.Content, mime, file.FileName);
        }
    }
}
