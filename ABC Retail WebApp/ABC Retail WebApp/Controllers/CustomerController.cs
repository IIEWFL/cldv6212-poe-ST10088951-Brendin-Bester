using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ABC_Retail_WebApp.Models;
using ABC_Retail_WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ABC_Retail_WebApp.Controllers
{
    public class CustomerController : Controller
    {
        private readonly FunctionsService _functions;

        public CustomerController(FunctionsService functions)
        {
            _functions = functions;
        }

        // GET: Customer/Index
        public async Task<IActionResult> Index()
        {
            var customers = await _functions.GetCustomersAsync();
            return View(customers);
        }

        // GET: Customer/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Customer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer, IFormFile? photo)
        {
            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            // Call the Function to create the customer (handles photo upload if provided)
            var created = await _functions.CreateCustomerAsync(customer, photo);

            if (created == null)
            {
                // Function returned failure; add error and show the form again
                ModelState.AddModelError(string.Empty, "Failed to create customer via Functions API.");
                return View(customer);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Customer/Details/{partitionKey}/{rowKey}
        // Note: FunctionsService exposes GetCustomersAsync; we filter locally for now
        public async Task<IActionResult> Details(string partitionKey, string rowKey)
        {
            var customers = await _functions.GetCustomersAsync();
            var customer = customers.FirstOrDefault(c => c.PartitionKey == partitionKey && c.RowKey == rowKey);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // GET: Customer/Edit/{partitionKey}/{rowKey}
        // TODO: Implement UpdateCustomer function or keep using local TableStorageService until the Function exists
        [HttpGet]
        public async Task<IActionResult> Edit(string partitionKey, string rowKey)
        {
            var customers = await _functions.GetCustomersAsync();
            var customer = customers.FirstOrDefault(c => c.PartitionKey == partitionKey && c.RowKey == rowKey);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // POST: Customer/Edit
        // NOTE: This action currently does not call a Function endpoint for update.
        // Implement an UpdateCustomer function and a corresponding FunctionsService method to fully migrate.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Customer customer, IFormFile? photo)
        {
            ModelState.Remove("Customerphoto");

            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            // Temporary approach: fetch existing customer, merge, and call CreateCustomerAsync to upsert
            // Replace with a real UpdateCustomerAsync function when available
            var existing = (await _functions.GetCustomersAsync())
                           .FirstOrDefault(c => c.PartitionKey == customer.PartitionKey && c.RowKey == customer.RowKey);

            if (existing == null)
            {
                return NotFound();
            }

            // Preserve existing photo if no new photo provided
            if (photo == null)
            {
                customer.Customerphoto = existing.Customerphoto;
            }

            // Use CreateCustomerAsync as an upsert (depends on your CreateCustomer function behavior)
            var upserted = await _functions.CreateCustomerAsync(customer, photo);
            if (upserted == null)
            {
                ModelState.AddModelError(string.Empty, "Failed to update customer via Functions API.");
                return View(customer);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Customer/Delete/{partitionKey}/{rowKey}
        // TODO: Implement DeleteCustomer function in Azure Functions and a method in FunctionsService for deletion.
        public async Task<IActionResult> Delete(string partitionKey, string rowKey)
        {
            var customers = await _functions.GetCustomersAsync();
            var customer = customers.FirstOrDefault(c => c.PartitionKey == partitionKey && c.RowKey == rowKey);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // POST: Customer/DeleteConfirmed
        // NOTE: This currently does not call an Azure Function to delete — implement Delete in Functions and FunctionsService to complete migration.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string partitionKey, string rowKey)
        {
            // TODO: Replace with await _functions.DeleteCustomerAsync(partitionKey, rowKey);
            ModelState.AddModelError(string.Empty, "Delete is not yet implemented via Functions API. Implement DeleteCustomer function and FunctionsService.DeleteCustomerAsync.");
            return RedirectToAction(nameof(Index));
        }

        // GET: Customer/Log
        public async Task<IActionResult> Log()
        {
            var logs = await _functions.GetMessagesAsync();
            return View(logs);
        }

        // POST: Customer/ExportLog
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportLog()
        {
            var logMessages = await _functions.GetMessagesAsync();

            var filename = $"log_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            using var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true))
            {
                await writer.WriteLineAsync("MessageId,InsertionTime,MessageText");
                foreach (var log in logMessages)
                {
                    var messageText = log.MessageText?.Replace("\"", "\"\"");
                    await writer.WriteLineAsync($"\"{log.MessageId}\",\"{log.InsertionTime}\",\"{messageText}\"");
                }
                await writer.FlushAsync();
                stream.Position = 0;

                // Upload the generated CSV to the functions endpoint for files (UploadLogFile function accepts raw stream)
                // Note: UploadLogFileAsync expects filename + stream
                var uploaded = await _functions.UploadLogFileAsync(filename, stream);
                if (!uploaded)
                {
                    ModelState.AddModelError(string.Empty, "Failed to upload log file via Functions API.");
                }
            }

            return RedirectToAction(nameof(Index));
        }
    }
}