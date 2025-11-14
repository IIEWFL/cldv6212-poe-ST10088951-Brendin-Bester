using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ABC_Retail_WebApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ABC_Retail_WebApp.Services
{
    public class FunctionsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FunctionsService> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        private readonly string? _functionKey; // optional function key appended as query ?code=...

        public FunctionsService(HttpClient httpClient, ILogger<FunctionsService> logger, IConfiguration config)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _functionKey = config["AzureFunctions:FunctionKey"]; // optional, set in App Settings if needed

            // Optional: a sane default timeout for long uploads
            _httpClient.Timeout = TimeSpan.FromSeconds(100);
        }

        private string AppendFunctionKeyIfNeeded(string relativeUrl)
        {
            if (string.IsNullOrWhiteSpace(_functionKey)) return relativeUrl;
            // preserve existing query string if present
            return relativeUrl.Contains("?")
                ? $"{relativeUrl}&code={Uri.EscapeDataString(_functionKey)}"
                : $"{relativeUrl}?code={Uri.EscapeDataString(_functionKey)}";
        }

        private async Task<T?> ReadFromResponseAsync<T>(HttpResponseMessage resp)
        {
            var respBody = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(respBody)) return default;

            try
            {
                return JsonSerializer.Deserialize<T>(respBody, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize response to {Type}. Body: {Body}", typeof(T).Name, respBody);
                return default;
            }
        }

        public async Task<Customer?> CreateCustomerAsync(Customer customer, IFormFile? photo)
        {
            try
            {
                if (photo == null)
                {
                    var json = JsonSerializer.Serialize(customer, _jsonOptions);
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var url = AppendFunctionKeyIfNeeded("customers");
                    using var resp = await _httpClient.PostAsync(url, content);

                    if (!resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync();
                        _logger.LogWarning("CreateCustomerAsync failed: {Status} {Reason}. Body: {Body}", resp.StatusCode, resp.ReasonPhrase, body);
                        return null;
                    }

                    return await ReadFromResponseAsync<Customer>(resp);
                }

                using var multipart = new MultipartFormDataContent();

                // customer JSON part (as plain string content)
                var customerJson = JsonSerializer.Serialize(customer, _jsonOptions);
                var customerPart = new StringContent(customerJson, Encoding.UTF8, "application/json");
                multipart.Add(customerPart, "customer");

                // file part - ensure stream is disposed after sending
                await using var stream = photo.OpenReadStream();
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(photo.ContentType ?? "application/octet-stream");
                multipart.Add(fileContent, "file", photo.FileName);

                var multipartUrl = AppendFunctionKeyIfNeeded("customers");
                using var response = await _httpClient.PostAsync(multipartUrl, multipart);

                if (!response.IsSuccessStatusCode)
                {
                    var respBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("CreateCustomerAsync (multipart) failed: {Status} {Reason}. Body: {Body}", response.StatusCode, response.ReasonPhrase, respBody);
                    return null;
                }

                return await ReadFromResponseAsync<Customer>(response);
            }
            catch (HttpRequestException hex)
            {
                _logger.LogError(hex, "HTTP error in CreateCustomerAsync. BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in CreateCustomerAsync");
                return null;
            }
        }

        public async Task<List<Customer>> GetCustomersAsync()
        {
            try
            {
                var relative = AppendFunctionKeyIfNeeded("customers");
                _logger.LogInformation("GET {Url}", new Uri(_httpClient.BaseAddress, relative));

                using var resp = await _httpClient.GetAsync(relative);
                var respBody = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetCustomersAsync failed: {Status} {Reason}. Body: {Body}", resp.StatusCode, resp.ReasonPhrase, respBody);
                    return new List<Customer>();
                }

                if (string.IsNullOrWhiteSpace(respBody))
                {
                    _logger.LogWarning("GetCustomersAsync returned empty body.");
                    return new List<Customer>();
                }

                try
                {
                    return JsonSerializer.Deserialize<List<Customer>>(respBody, _jsonOptions) ?? new List<Customer>();
                }
                catch (JsonException jex)
                {
                    _logger.LogError(jex, "Failed to deserialize customers response. Body: {Body}", respBody);
                    return new List<Customer>();
                }
            }
            catch (HttpRequestException hex)
            {
                _logger.LogError(hex, "HTTP request failed in GetCustomersAsync. BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
                return new List<Customer>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetCustomersAsync");
                return new List<Customer>();
            }
        }

        public async Task<List<QueueLogViewModel>> GetMessagesAsync()
        {
            try
            {
                var relative = AppendFunctionKeyIfNeeded("messages");
                using var resp = await _httpClient.GetAsync(relative);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GetMessagesAsync failed: {Status} {Reason}. Body: {Body}", resp.StatusCode, resp.ReasonPhrase, body);
                    return new List<QueueLogViewModel>();
                }

                return JsonSerializer.Deserialize<List<QueueLogViewModel>>(body, _jsonOptions) ?? new List<QueueLogViewModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMessagesAsync");
                return new List<QueueLogViewModel>();
            }
        }

        public async Task<bool> UploadLogFileAsync(string filename, Stream fileStream)
        {
            try
            {
                using var content = new StreamContent(fileStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Headers.Add("X-Filename", filename);

                var url = AppendFunctionKeyIfNeeded("logs/upload");
                using var resp = await _httpClient.PostAsync(url, content);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    _logger.LogWarning("UploadLogFileAsync failed: {Status} {Reason}. Body: {Body}", resp.StatusCode, resp.ReasonPhrase, body);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UploadLogFileAsync");
                return false;
            }
        }

        public async Task<bool> UpdateCustomerAsync(Customer customer, IFormFile? photo)
        {
            try
            {
                var url = $"customers/{Uri.EscapeDataString(customer.PartitionKey ?? "")}/{Uri.EscapeDataString(customer.RowKey ?? "")}";
                url = AppendFunctionKeyIfNeeded(url);

                if (photo == null)
                {
                    var json = JsonSerializer.Serialize(customer, _jsonOptions);
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var resp = await _httpClient.PutAsync(url, content);
                    return resp.IsSuccessStatusCode;
                }

                using var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(JsonSerializer.Serialize(customer, _jsonOptions), Encoding.UTF8, "application/json"), "customer");

                await using var stream = photo.OpenReadStream();
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(photo.ContentType ?? "application/octet-stream");
                multipart.Add(fileContent, "file", photo.FileName);

                using var response = await _httpClient.PutAsync(url, multipart);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateCustomerAsync");
                return false;
            }
        }

        public async Task<bool> DeleteCustomerAsync(string partitionKey, string rowKey)
        {
            try
            {
                var url = $"customers/{Uri.EscapeDataString(partitionKey)}/{Uri.EscapeDataString(rowKey)}";
                url = AppendFunctionKeyIfNeeded(url);

                using var resp = await _httpClient.DeleteAsync(url);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    _logger.LogWarning("DeleteCustomerAsync failed: {Status} {Reason}. Body: {Body}", resp.StatusCode, resp.ReasonPhrase, body);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteCustomerAsync");
                return false;
            }
        }
    }
}