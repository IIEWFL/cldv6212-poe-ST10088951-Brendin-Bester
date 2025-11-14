using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#nullable enable

namespace ABCRetailFunctions.Models
{
    public class QueueLogViewModel
    {
        public string? MessageId { get; set; }
        public DateTimeOffset? InsertionTime { get; set; }
        public string? MessageText { get; set; }
    }
}
