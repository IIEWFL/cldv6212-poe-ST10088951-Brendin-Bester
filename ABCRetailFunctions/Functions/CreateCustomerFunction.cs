#nullable enable
using ABCRetailFunctions.Models;
using ABCRetailFunctions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ABCRetailFunctions.Functions;

public class CreateCustomerFunction
{
    private readonly BlobStorageService _blobService;
    private readonly TableStorageService _tableService;
    private readonly ILogger<CreateCustomerFunction> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public CreateCustomerFunction(
        BlobStorageService blobService,
        TableStorageService tableService,
        ILogger<CreateCustomerFunction> logger)
    {
        _blobService = blobService;
        _tableService = tableService;
        _logger = logger;
    }

    [Function("CreateCustomer")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "customers")] HttpRequest req,
        FunctionContext context)
    {
        _logger.LogInformation("CreateCustomer invoked at {Now}", DateTimeOffset.UtcNow);

        try
        {
            Customer? customer = null;

            // If content is multipart/form-data, prefer form parsing
            if (req.HasFormContentType)
            {
                var form = await req.ReadFormAsync();

                // Look for a form field named "customer" that contains JSON
                var customerJson = form["customer"].Count > 0 ? form["customer"].ToString() : null;
                if (!string.IsNullOrWhiteSpace(customerJson))
                {
                    try
                    {
                        customer = JsonSerializer.Deserialize<Customer>(customerJson, _jsonOptions);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize 'customer' JSON from form. JSON: {Json}", customerJson);
                        return new BadRequestObjectResult("Invalid 'customer' JSON in form field.");
                    }
                }

                // If a file is provided, upload it and ensure customer exists
                if (form.Files.Count > 0)
                {
                    var file = form.Files[0];
                    var blobName = $"customers/{Guid.NewGuid():N}-{Path.GetFileName(file.FileName)}";
                    using var stream = file.OpenReadStream();
                    var blobUri = await _blobService.UploadPhotoAsync(blobName, stream);
                    if (customer == null) customer = new Customer();
                    customer.Customerphoto = blobUri;
                }
            }
            else
            {
                // Expect application/json raw body
                using var reader = new StreamReader(req.Body);
                var body = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        customer = JsonSerializer.Deserialize<Customer>(body, _jsonOptions);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize JSON body. Body: {Body}", body);
                        return new BadRequestObjectResult("Invalid JSON body.");
                    }
                }
            }

            if (customer == null)
            {
                return new BadRequestObjectResult("Customer payload is required either as JSON body or as a 'customer' form field.");
            }

            // Ensure required identifiers / timestamps
            if (string.IsNullOrWhiteSpace(customer.CustomerId))
            {
                customer.CustomerId = Guid.NewGuid().ToString("N");
            }

            customer.CreatedAt ??= DateTime.UtcNow;

            // Save using your existing TableStorageService.AddCustomerAsync (which sets PartitionKey and RowKey)
            try
            {
                await _tableService.AddCustomerAsync(customer);
            }
            catch (Azure.RequestFailedException rfEx) when (rfEx.Status == 409)
            {
                _logger.LogWarning(rfEx, "Customer add conflict for CustomerId {CustomerId}", customer.CustomerId);
                return new ConflictObjectResult(new { error = "A customer with this CustomerId already exists." });
            }

            _logger.LogInformation("Created customer {CustomerId}", customer.CustomerId);
            return new OkObjectResult(customer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer.");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }
}