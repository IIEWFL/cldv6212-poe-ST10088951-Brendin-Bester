#nullable enable
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace ABCRetailFunctions.Functions;

public class UploadLogFileFunction
{
    private readonly FileShareStorageService _fileShareService;
    private readonly ILogger<UploadLogFileFunction> _logger;

    public UploadLogFileFunction(FileShareStorageService fileShareService, ILogger<UploadLogFileFunction> logger)
    {
        _fileShareService = fileShareService;
        _logger = logger;
    }

    [Function("UploadLogFile")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "logs/upload")] HttpRequest req,
        FunctionContext context)
    {
        _logger.LogInformation("UploadLogFile invoked at {Now}", DateTimeOffset.UtcNow);

        try
        {
            // 1) Raw binary body with X-Filename header (works from Postman)
            if (req.Headers.TryGetValue("X-Filename", out var headerValues) && headerValues.Count > 0)
            {
                var headerName = headerValues.FirstOrDefault() ?? string.Empty;
                var fileName = string.IsNullOrWhiteSpace(headerName) ? $"log-{Guid.NewGuid():N}.log" : Path.GetFileName(headerName);

                var bodyStream = req.Body;
                if (bodyStream == null || (bodyStream.CanSeek && bodyStream.Length == 0))
                {
                    _logger.LogWarning("Request body is empty for raw upload.");
                    return new BadRequestObjectResult("Request body is empty; provide raw binary body and X-Filename header.");
                }

                if (bodyStream.CanSeek) bodyStream.Position = 0;
                await _fileShareService.UploadFileAsync(fileName, bodyStream);

                _logger.LogInformation("Uploaded log file {FileName} via raw body.", fileName);
                return new OkObjectResult(new { FileName = fileName });
            }

            // 2) multipart/form-data (requires builder.ConfigureFunctionsWebApplication() in Program.cs)
            if (req.HasFormContentType)
            {
                var form = await req.ReadFormAsync();
                if (form.Files.Count == 0)
                {
                    _logger.LogWarning("Multipart form submitted but no file found.");
                    return new BadRequestObjectResult("Multipart form submitted but no file provided.");
                }

                var file = form.Files.First();
                var fileName = string.IsNullOrWhiteSpace(file.FileName) ? $"log-{Guid.NewGuid():N}.log" : Path.GetFileName(file.FileName);

                using var stream = file.OpenReadStream();
                await _fileShareService.UploadFileAsync(fileName, stream);

                _logger.LogInformation("Uploaded log file {FileName} via multipart form.", fileName);
                return new OkObjectResult(new { FileName = fileName });
            }

            // 3) Unsupported content
            _logger.LogWarning("UploadLogFile request missing X-Filename header and not multipart/form-data.");
            return new BadRequestObjectResult("Provide either a raw binary body with X-Filename header or a multipart/form-data file upload.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload log file.");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = (int)HttpStatusCode.InternalServerError };
        }
    }
}