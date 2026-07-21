using System;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Web.Api.Server.Controllers
{
    [ApiController]
    [Route("api/store-chat")]
    public class StoreChatController : ControllerBase
    {
        private readonly IStoreChatService _chat;

        public StoreChatController(IStoreChatService chat)
        {
            _chat = chat;
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
            if (string.IsNullOrWhiteSpace(request.Text) && !hasIntent)
                return BadRequest(new { message = "Message ou intent requis" });

            if (string.IsNullOrWhiteSpace(request.SessionId)
                && Request.Headers.TryGetValue("X-Store-Chat-Session", out var headerSession))
            {
                request.SessionId = headerSession.ToString();
            }

            var response = await _chat.ProcessMessageAsync(request, ct);
            Response.Headers["X-Store-Chat-Session"] = response.SessionId;
            return Ok(response);
        }

        [HttpGet("payment-result/{orderId:guid}")]
        public async Task<IActionResult> GetPaymentResult(Guid orderId, CancellationToken ct = default)
        {
            if (orderId == Guid.Empty)
                return BadRequest(new { message = "Commande invalide" });

            var result = await _chat.GetPaymentResultAsync(orderId, ct);
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

            var result = await _chat.ConfirmPaymentAsync(request.OrderId, request.SessionId, ct);
            if (result == null)
                return NotFound();
            return Ok(result);
        }
    }
}
