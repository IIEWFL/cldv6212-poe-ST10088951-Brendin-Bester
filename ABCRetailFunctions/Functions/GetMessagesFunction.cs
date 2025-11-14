using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ABCRetailFunctions.Functions;

public class GetMessagesFunction
{
    private readonly QueueStorageService _queueService;
    private readonly ILogger<GetMessagesFunction> _logger;

    public GetMessagesFunction(QueueStorageService queueService, ILogger<GetMessagesFunction> logger)
    {
        _queueService = queueService;
        _logger = logger;
    }

    [Function("GetMessages")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "messages")] HttpRequest req,
        FunctionContext context)
    {
        try
        {
            _logger.LogInformation("GetMessages invoked at {Now}", DateTimeOffset.UtcNow);

            var messages = await _queueService.GetMessagesAsync();

            if (messages == null || messages.Count == 0)
            {
                _logger.LogInformation("No messages returned from queue.");
                return new OkObjectResult(Array.Empty<object>());
            }

            _logger.LogInformation("Returned {Count} messages from queue.", messages.Count);
            return new OkObjectResult(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while retrieving messages from queue.");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }
}