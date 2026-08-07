using System;

namespace Backup.Web.Api.Server.Services.Audit
{
    /// <summary>
    /// Traçabilité création / modification (qui / quand).
    /// </summary>
    public interface IHasAuditTrail
    {
        DateTime CreatedAt { get; set; }
        DateTime UpdatedAt { get; set; }
        string? CreatedBy { get; set; }
        string? UpdatedBy { get; set; }
    }

    public static class AuditTrail
    {
        public const string SystemActor = "System";

        public static void StampCreate(IHasAuditTrail entity, string? actor, DateTime? utcNow = null)
        {
            var now = utcNow ?? DateTime.UtcNow;
            var who = NormalizeActor(actor);
            if (entity.CreatedAt == default)
                entity.CreatedAt = now;
            entity.UpdatedAt = now;
            if (string.IsNullOrWhiteSpace(entity.CreatedBy))
                entity.CreatedBy = who;
            entity.UpdatedBy = who;
        }

        public static void StampUpdate(IHasAuditTrail entity, string? actor, DateTime? utcNow = null)
        {
            entity.UpdatedAt = utcNow ?? DateTime.UtcNow;
            entity.UpdatedBy = NormalizeActor(actor);
        }

        public static string NormalizeActor(string? actor) =>
            string.IsNullOrWhiteSpace(actor) ? SystemActor : actor.Trim();
    }
}
