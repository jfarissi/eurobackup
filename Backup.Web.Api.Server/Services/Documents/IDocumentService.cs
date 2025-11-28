using Backup.Web.Api.Server.Models;
using Microsoft.AspNetCore.Http;

namespace Backup.Web.Api.Server.Services.Documents
{
    public interface IDocumentService
    {
        Task<Document> UploadAsync(IFormFile file, string typeDocument, string? numero, string? client, DateTime? dateDocument, CancellationToken ct);
        Task<Document> UploadAsync(IFormFile file, string typeDocument, string? numero, string? client, DateTime? dateDocument, CancellationToken ct, string? supplier);
        IQueryable<Document> GetAll();
        IQueryable<Document> Search(string query);
        Task<(byte[] bytes, string contentType, string downloadName)> DownloadAsync(int id);
		Task<bool> ReparseDocumentLinesAsync(int documentId, bool useAiFallback, CancellationToken ct);
        Task<DocumentMetadata> InspectAsync(IFormFile file, CancellationToken ct);
    }
}


