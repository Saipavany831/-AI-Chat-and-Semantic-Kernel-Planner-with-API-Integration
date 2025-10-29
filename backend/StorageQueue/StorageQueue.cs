// using Azure;
// using Azure.Storage.Queues;
// using Azure.Storage.Queues.Models;

using System;
using System.IO;
using System.Threading.Tasks;
using backend.api.Dtos;
// using Microsoft.AspNetCore.DataProtection;
// using backend.api.Credentials;
// using System.Net;

namespace backend.api.StorageQueue
{
    public class StorageQueue
    {
        public async Task sendMessageToQueue(QueueData queueData)
        {
            try
            {
                // 👇 ORIGINAL AZURE CODE (COMMENTED OUT)
                /*
                string queueName = "chatqueue";

                QueueClient queueClient = new QueueClient($"{Secrets.storageConnectionString!}", queueName, new QueueClientOptions
                {
                    MessageEncoding = QueueMessageEncoding.Base64
                });

                bool queueExists = await queueClient.ExistsAsync();

                if (!queueExists)
                {
                    await queueClient.CreateAsync();
                }

                string Id = queueData.Id!;
                string Date = queueData.Date!;
                string UserQuery = queueData.UserQuery!;
                string AssistantResponse = queueData.AssistantResponse!;

                string message = @$" {{
                    ""Id"": ""{Id}"",
                    ""Date"": ""{Date}"",
                    ""UserQuery"": ""{UserQuery}"",
                    ""AssistantResponse"": ""{AssistantResponse}""
                }}";

                await queueClient.SendMessageAsync(message);
                */

                // 👇 LOCAL MODE — WORKS WITHOUT AZURE
                string IdLocal = queueData.Id ?? Guid.NewGuid().ToString();
                string DateLocal = queueData.Date ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string UserQueryLocal = queueData.UserQuery ?? "";
                string AssistantResponseLocal = queueData.AssistantResponse ?? "";

                string messageLocal = @$"{{
                    ""Id"": ""{IdLocal}"",
                    ""Date"": ""{DateLocal}"",
                    ""UserQuery"": ""{UserQueryLocal}"",
                    ""AssistantResponse"": ""{AssistantResponseLocal}""
                }}";

                // Save locally (instead of Azure queue)
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "local_queue_log.txt");
                await File.AppendAllTextAsync(filePath, messageLocal + Environment.NewLine);

                Console.WriteLine($"[LOCAL QUEUE MOCK] Message logged to {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in sendMessageToQueue: {ex.Message}");
            }
        }
    }
}
