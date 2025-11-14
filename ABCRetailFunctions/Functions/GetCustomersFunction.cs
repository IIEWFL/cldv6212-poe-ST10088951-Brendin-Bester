using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using ABCRetailFunctions.Models;

namespace ABCRetailFunctions.Functions;

public class GetCustomersFunction
{
    private readonly TableStorageService _tableStorageService;
    private readonly ILogger<GetCustomersFunction> _logger;

    public GetCustomersFunction(TableStorageService tableStorageService, ILogger<GetCustomersFunction> logger)
    {
        _tableStorageService = tableStorageService;
        _logger = logger;
    }

    [Function("GetCustomers")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers")] HttpRequest req,
        FunctionContext context)
    {
        _logger.LogInformation("Processing request to retrieve customers...");

        var customers = await _tableStorageService.GetCustomersAsync();

        //covert Customer to a customer data transfer object

        var customerDtos = customers.Select(c => new CustomerDto
        {
          PartitionKey = c.PartitionKey,
          RowKey = c.RowKey,
          Timestamp = c.Timestamp,
          ETag = c.ETag.ToString(),
          CustomerId = c.CustomerId,
          Customerphoto = c.Customerphoto,
          FirstName = c.FirstName,
          LastName = c.LastName,
          Email = c.Email,
          CreatedAt = c.CreatedAt

        }).ToList();

        return new OkObjectResult(customerDtos);
    }
}