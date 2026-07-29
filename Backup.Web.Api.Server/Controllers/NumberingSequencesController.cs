using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NumberingSequencesController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public NumberingSequencesController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.NumberingManage)]
        public IActionResult GetAll()
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var sequences = this.storage.SelectAllNumberSequences()
                .ForCompany(companyId)
                .OrderBy(s => s.DocumentType)
                .ThenBy(s => s.Prefix)
                .ToList();
            return Ok(sequences);
        }

        [HttpPost("ensure-defaults")]
        [RequirePermission(Permissions.NumberingManage)]
        public async Task<IActionResult> EnsureDefaults()
        {
            var sequences = await this.numberingService.EnsureDefaultSequencesAsync(this.companyContext.GetCurrentCompanyId());
            return Ok(sequences);
        }

        [HttpGet("preview")]
        [RequirePermission(Permissions.NumberingManage)]
        public async Task<IActionResult> Preview([FromQuery] string documentType)
        {
            if (string.IsNullOrWhiteSpace(documentType)) return BadRequest("documentType required");
            var number = await this.numberingService.PreviewNextNumberAsync(documentType, this.companyContext.GetCurrentCompanyId());
            return Ok(new { number });
        }

        [HttpPost("next-number")]
        [RequirePermission(Permissions.NumberingManage)]
        public async Task<IActionResult> GetNextNumber([FromQuery] string documentType)
        {
            if (string.IsNullOrWhiteSpace(documentType)) return BadRequest("documentType required");
            var number = await this.numberingService.GetNextNumberAsync(documentType, this.companyContext.GetCurrentCompanyId());
            return Ok(new { number });
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.NumberingManage)]
        public async Task<IActionResult> Put(int id, [FromBody] DocumentNumberSequence sequence)
        {
            var existing = this.storage.SelectAllNumberSequences()
                .ForCompany(this.companyContext.GetCurrentCompanyId())
                .FirstOrDefault(s => s.Id == id);
            if (existing == null) return NotFound();

            if (string.IsNullOrWhiteSpace(sequence.Prefix))
            {
                return BadRequest("Prefix is required");
            }

            if (sequence.NextNumber < 1)
            {
                return BadRequest("NextNumber must be >= 1");
            }

            if (string.IsNullOrWhiteSpace(sequence.FormatPattern))
            {
                return BadRequest("FormatPattern is required");
            }

            existing.Prefix = sequence.Prefix.Trim();
            existing.NextNumber = sequence.NextNumber;
            existing.FormatPattern = sequence.FormatPattern.Trim();
            if (sequence.Year > 2000)
            {
                existing.Year = sequence.Year;
            }

            var updated = await this.storage.UpdateNumberSequenceAsync(existing);
            return Ok(updated);
        }
    }
}
