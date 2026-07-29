using System.Collections.Generic;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities;

namespace Backup.Web.Api.Server.Services.Numbering
{
    public interface INumberingSequenceService
    {
        Task<string> GetNextNumberAsync(string documentType, string? companyId = null);
        Task<string> PreviewNextNumberAsync(string documentType, string? companyId = null);
        Task<IReadOnlyList<DocumentNumberSequence>> EnsureDefaultSequencesAsync(string? companyId = null);
    }
}
