using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using MsAuthorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.AutoParts;
using Backup.Web.Api.Server.Services.Modules;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [MsAuthorize]
    [ApiController]
    [Route("api/auto-parts/symptoms")]
    [RequireModule(ModuleCodes.AutoParts)]
    public class AutoPartsSymptomsController : RESTFulController
    {
        private readonly IAutoPartsSymptomService symptoms;

        public AutoPartsSymptomsController(IAutoPartsSymptomService symptoms)
        {
            this.symptoms = symptoms;
        }

        [HttpGet]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> Get([FromQuery] string? q, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { error = "Query q required (ex. bruit de frein)." });

            var result = await this.symptoms.DiagnoseAsync(q, ct);
            return Ok(result);
        }
    }
}
