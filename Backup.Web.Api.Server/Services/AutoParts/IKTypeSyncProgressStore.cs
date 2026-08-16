using System;
using System.Text.Json.Serialization;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    /// <summary>Statuts exposés à l'UI — sérialisés en string ("Running"), pas en int.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum KTypeSyncStatus
    {
        Idle,
        Running,
        Done,
        Failed
    }

    public record KTypeSyncProgressDto(
        string KType,
        KTypeSyncStatus Status,
        string? Phase,
        int Current,
        int Total,
        int Percent,
        string? Message,
        int? ProductsImported,
        DateTime UpdatedAt);

    public interface IKTypeSyncProgressStore
    {
        void Start(string kType, int total, string? make = null, string? model = null);
        void Update(string kType, string? phase, int current, int total, string? message = null);
        void ApplyProgressJson(string kType, string json);
        void Complete(string kType, int productsImported);
        void Fail(string kType, string? message);
        KTypeSyncProgressDto? Get(string kType);
        bool IsRunning(string kType);
    }
}
