using System;

namespace Backup.Web.Api.Server.Services.Sales
{
    public interface IHasSoftDelete
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
        string? DeletedBy { get; set; }
    }
}
