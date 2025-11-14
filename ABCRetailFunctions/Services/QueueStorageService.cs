using ABCRetailFunctions.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ABCRetailFunctions.Services
{
    public class QueueStorageService
    {
        private readonly QueueClient _queueClient;

        public QueueStorageService(string storageConnectionString, string queueName)
        {
            var queueServiceClient = new QueueServiceClient(storageConnectionString);
            _queueClient = queueServiceClient.GetQueueClient(queueName);
            _queueClient.CreateIfNotExists();

        }

        //send message to queue

        public async Task SendMessageAsync(object message)
        {
            var messageJson = JsonSerializer.Serialize(message);
            await _queueClient.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(messageJson)));
        }



        //get messages for log
        public async Task<List<QueueLogViewModel>> GetMessagesAsync()
        {
            var messagesList = new List<QueueLogViewModel>();
            var messages = await _queueClient.PeekMessagesAsync(maxMessages: 32);

            foreach (PeekedMessage message in messages.Value)
            {
                messagesList.Add(new QueueLogViewModel
                {
                    MessageId = message.MessageId,
                    InsertionTime = message.InsertedOn,
                    MessageText = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(message.MessageText))
                });
                // Optionally, delete the message after processing
                // await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
            }
            return messagesList;
        }

    }
}
