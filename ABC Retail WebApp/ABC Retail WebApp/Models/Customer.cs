using Azure;
using Azure.Data.Tables;
using System;

namespace ABC_Retail_WebApp.Models
{
    public class Customer : ITableEntity
    {
        public string? PartitionKey { get; set; }   
        public string? RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }


   
    public string? CustomerId { get; set; }
    public string? Customerphoto { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public DateTime? CreatedAt { get; set; }
    
    }
}
