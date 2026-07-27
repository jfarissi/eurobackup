using System;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/store-chat")]
    public class StoreChatController : ControllerBase
    {
        private readonly ISalesAssistantFacade _assistant;
        private readonly IStoreChatTurnLogService _turnLog;

        public StoreChatController(ISalesAssistantFacade assistant, IStoreChatTurnLogService turnLog)
        {
            _assistant = assistant;
            _turnLog = turnLog;
        }

        [HttpPost("message")]
        [RequestTimeout(300_000)]
        public async Task<IActionResult> PostMessage(
            [FromBody] StoreChatMessageRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
                return BadRequest(new { message = "Message requis" });

            var hasIntent = !string.IsNullOrWhiteSpace(request.ClientIntent);
            var hasImage = !string.IsNullOrWhiteSpace(request.ImageBase64)
                           || !string.IsNullOrWhiteSpace(request.ImageCaption);
            if (string.IsNullOrWhiteSpace(request.Text) && !hasIntent && !hasImage)
                return BadRequest(new { message = "Message, intent ou photo requis" });

            if (string.IsNullOrWhiteSpace(request.SessionId)
                && Request.Headers.TryGetValue("X-Store-Chat-Session", out var headerSession))
            {
                request.SessionId = headerSession.ToString();
            }

            var response = await _assistant.ProcessMessageAsync(request, ct);
            Response.Headers["X-Store-Chat-Session"] = response.SessionId;
            return Ok(response);
        }

        [HttpGet("payment-result/{orderId:guid}")]
        public async Task<IActionResult> GetPaymentResult(Guid orderId, CancellationToken ct = default)
        {
            if (orderId == Guid.Empty)
                return BadRequest(new { message = "Commande invalide" });

            var result = await _assistant.GetPaymentResultAsync(orderId, ct);
            if (result == null)
                return NotFound();
            return Ok(result);
        }

        [HttpPost("confirm-payment")]
        public async Task<IActionResult> ConfirmPayment(
            [FromBody] StoreChatConfirmPaymentDto request,
            CancellationToken ct = default)
        {
            if (request == null || request.OrderId == Guid.Empty)
                return BadRequest(new { message = "Commande invalide" });

            var result = await _assistant.ConfirmPaymentAsync(request.OrderId, request.SessionId, ct);
            if (result == null)
                return NotFound();
            return Ok(result);
        }

        [HttpPost("turns/{turnId:guid}/review")]
        public async Task<IActionResult> ReviewTurn(
            Guid turnId,
            [FromBody] StoreChatTurnReviewRequest request,
            CancellationToken ct = default)
        {
            if (turnId == Guid.Empty)
                return BadRequest(new { message = "Tour invalide" });

            var status = request?.Status ?? "bad";
            var ok = await _turnLog.SetReviewAsync(turnId, status, request?.Note, ct, request?.Source ?? "manual");
            if (!ok)
                return NotFound(new { message = "Tour introuvable ou statut invalide" });
            return Ok(new
            {
                id = turnId,
                reviewStatus = status.Trim().ToLowerInvariant(),
                isCorrected = string.Equals(status, "fixed", StringComparison.OrdinalIgnoreCase)
            });
        }

        [HttpGet("turns")]
        public async Task<IActionResult> ListTurns(
            [FromQuery] int take = 50,
            [FromQuery] string? reviewStatus = null,
            [FromQuery] bool? isCorrected = null,
            CancellationToken ct = default)
        {
            var items = await _turnLog.ListRecentAsync(take, reviewStatus, isCorrected, ct);
            return Ok(items);
        }
    }
}
