using ABC_Retail_WebApp.Models;
using Azure;
using Azure.Data.Tables;

namespace ABC_Retail_WebApp.Services
{
    public class TableStorageService
    {
        private readonly TableClient _tableClient;

        public TableStorageService(string storageConnectionString, string tableName)
        {
        var serviceClient = new TableServiceClient(storageConnectionString);
            _tableClient = serviceClient.GetTableClient(tableName);
            _tableClient.CreateIfNotExists();

        }
        //get all Customer INDEX

        public async Task<List<Customer>>GetCustomersAsync()
        {
            var customers = new List<Customer>();
            await foreach (var customer in _tableClient.QueryAsync<Customer>())
            {
                customers.Add(customer);
            }
            return customers;
        }


        //get Customer via ID, ROWKEY Details.cshtml
        public async Task<Customer?> GetCustomerAsync(string partitionKey, String rowkey)
        {
            try
            {
                var response = await _tableClient.GetEntityAsync<Customer>(partitionKey, rowkey);
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }
        //add Customer Create.cshtml

        public async Task AddCustomerAsync(Customer customer) 
        {
            customer.PartitionKey = customer.CustomerId;
            customer.RowKey = Guid.NewGuid().ToString();

            await _tableClient.AddEntityAsync(customer);

        }


        //edit Customer Edit.cshtml

        public async Task UpdateCustomerAsync(Customer customer)
        {
            await _tableClient.UpdateEntityAsync(customer, ETag.All, TableUpdateMode.Replace);
        }

        //delete Customer Delete.cshtml

        public async Task DeleteCustomerAsync(string partitionkey, string rowkey)
        {
            await _tableClient.DeleteEntityAsync(partitionkey, rowkey);
        }
    }
}
