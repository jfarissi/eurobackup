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

            // Nom lisible (prénom+nom / username / email) — jamais l'id GUID.
            foreach (var candidate in new[]
            {
                user.FindFirst("display_name")?.Value,
                user.Identity?.Name,
                user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value,
                user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value,
                user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value,
                user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            })
            {
                if (IsReadableActor(candidate))
                    return candidate!.Trim();
            }

            return "System";
        }

        /// <summary>True si la valeur est un libellé affichable (pas un GUID / id technique).</summary>
        public static bool IsReadableActor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var trimmed = value.Trim();
            if (Guid.TryParse(trimmed, out _)) return false;
            return true;
        }

        public static string FormatUserDisplayName(
            string? firstName,
            string? lastName,
            string? userName = null,
            string? email = null,
            string? fallbackId = null)
        {
            var full = $"{firstName} {lastName}".Trim();
            if (!string.IsNullOrWhiteSpace(full)) return full;
            if (!string.IsNullOrWhiteSpace(userName)) return userName!.Trim();
            if (!string.IsNullOrWhiteSpace(email)) return email!.Trim();
            if (!string.IsNullOrWhiteSpace(fallbackId)) return fallbackId!.Trim();
            return "System";
        }
    }
}
