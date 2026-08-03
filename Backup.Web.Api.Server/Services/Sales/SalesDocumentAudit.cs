using System;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;

namespace Backup.Web.Api.Server.Services.Sales
{
    public static class SalesDocumentAudit
    {
        public static async Task LogAsync(
            IStorageBroker storage,
            string? companyId,
            string documentType,
            int documentId,
            string action,
            string? actor,
            string? summary = null,
            string? details = null)
        {
            if (documentId <= 0 || string.IsNullOrWhiteSpace(documentType) || string.IsNullOrWhiteSpace(action))
                return;

            await storage.InsertDocumentAuditLogAsync(new DocumentAuditLog
            {
                DocumentType = documentType.Trim(),
                DocumentId = documentId,
                Action = action.Trim(),
                Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(),
                Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
                Actor = string.IsNullOrWhiteSpace(actor) ? "System" : actor.Trim(),
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow
            });
        }

        public static string ActorFrom(System.Security.Claims.ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true) return "System";
            return user.FindFirst("id")?.Value
                ?? user.Identity?.Name
                ?? user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? "System";
        }
    }
}
