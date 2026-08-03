using System;

namespace Backup.Web.Api.Server.Services.Sales
{
    public interface IHasArchive
    {
        bool IsArchived { get; set; }
        DateTime? ArchivedAt { get; set; }
        string? ArchivedBy { get; set; }
    }
}
