using System.Net.Http.Headers;
using Backup.Web.Api.Server.Services.Documents.Python;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Controllers
{
    /// <summary>
    /// Reverse proxy vers le service Python FastAPI (dev / tests Angular).
    /// </summary>
    [ApiController]
    [Route("api/python")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 100_000_000)]
    public class PythonProxyController : ControllerBase
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly PythonExtractorOptions options;
        private readonly ILogger<PythonProxyController> logger;

        public PythonProxyController(
            IHttpClientFactory httpClientFactory,
            IOptions<PythonExtractorOptions> options,
            ILogger<PythonProxyController> logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.options = options.Value ?? new PythonExtractorOptions();
            this.logger = logger;
        }

        [HttpGet("{**path}")]
        [HttpPost("{**path}")]
        [HttpPut("{**path}")]
        [HttpDelete("{**path}")]
        public async Task<IActionResult> Proxy(string? path, CancellationToken ct)
        {
            var pythonBase = (options.Url ?? "http://localhost:8000").TrimEnd('/');
            var relativePath = string.IsNullOrWhiteSpace(path) ? string.Empty : path.TrimStart('/');
            var targetUrl = string.IsNullOrEmpty(relativePath)
                ? pythonBase
                : $"{pythonBase}/{relativePath}";
            targetUrl += Request.QueryString.Value ?? string.Empty;

            try
            {
                using var forward = new HttpRequestMessage(new HttpMethod(Request.Method), targetUrl);
                forward.Content = await BuildForwardContentAsync(ct);

                var client = httpClientFactory.CreateClient(nameof(PythonProxyController));
                using var response = await client.SendAsync(
                    forward,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct);

                var responseContentType = response.Content.Headers.ContentType?.MediaType
                    ?? response.Content.Headers.ContentType?.ToString()
                    ?? "application/json";
                var body = await response.Content.ReadAsByteArrayAsync(ct);

                Response.StatusCode = (int)response.StatusCode;
                Response.ContentType = responseContentType;
                await Response.Body.WriteAsync(body, ct);
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Python proxy failed for {TargetUrl}", targetUrl);
                return StatusCode(502, new { error = "Python service unreachable", detail = ex.Message, target = targetUrl });
            }
        }

        private async Task<HttpContent?> BuildForwardContentAsync(CancellationToken ct)
        {
            if (HttpMethods.IsGet(Request.Method) || HttpMethods.IsHead(Request.Method) || HttpMethods.IsDelete(Request.Method))
            {
                return null;
            }

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(ct);
                var multipart = new MultipartFormDataContent();
                foreach (var field in form)
                {
                    foreach (var value in field.Value)
                    {
                        multipart.Add(new StringContent(value ?? string.Empty), field.Key);
                    }
                }

                foreach (var file in form.Files)
                {
                    await using var stream = file.OpenReadStream();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms, ct);
                    var fileContent = new ByteArrayContent(ms.ToArray());
                    if (!string.IsNullOrWhiteSpace(file.ContentType))
                    {
                        try
                        {
                            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
                        }
                        catch (FormatException ex)
                        {
                            logger.LogWarning(ex, "Invalid uploaded file content-type: {ContentType}", file.ContentType);
                        }
                    }
                    multipart.Add(fileContent, file.Name, file.FileName);
                }

                return multipart;
            }

            Request.EnableBuffering();
            using var raw = new MemoryStream();
            await Request.Body.CopyToAsync(raw, ct);
            if (raw.Length == 0)
            {
                return null;
            }

            var content = new ByteArrayContent(raw.ToArray());
            if (!string.IsNullOrWhiteSpace(Request.ContentType))
            {
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(Request.ContentType);
            }
            return content;
        }
    }
}
