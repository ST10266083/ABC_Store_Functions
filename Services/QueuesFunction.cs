using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using ABC_Retail_Functions.Helpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

// This is the QueuesFunction which helps the mvc with backend operations
namespace ABC_Retail_Functions.Services
{
    public class QueueFunctions
    {
        private readonly QueueServiceClient _queues;
        private readonly TableServiceClient _tables;
        private const string OrdersTable = "Orders";
        private const string ProductsTable = "Products";

        private static readonly JsonSerializerOptions CaseInsensitive = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public QueueFunctions(QueueServiceClient qsc, TableServiceClient tsc)
        {
            _queues = qsc;
            _tables = tsc;
        }

        [Function("QueuesPeek")]
        public async Task<HttpResponseData> PeekAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "queues/{queue}/peek")]
            HttpRequestData req, string queue, int count = 10)
        {
            var qc = _queues.GetQueueClient(queue);
            await qc.CreateIfNotExistsAsync();

            if (count <= 0 || count > 32) count = 10;
            var msgs = await qc.PeekMessagesAsync(count);

            var list = msgs.Value.Select(m => new
            {
                m.MessageText,
                m.InsertedOn
            }).ToList();

            return await req.JsonAsync(list);
        }


        [Function("QueuesEnqueue")]
        public async Task<HttpResponseData> EnqueueAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "queues/{queue}")]
            HttpRequestData req, string queue)
        {
            var dto = await JsonSerializer.DeserializeAsync<OrderQueueDto>(req.Body, CaseInsensitive);

            if (dto is null ||
                string.IsNullOrWhiteSpace(dto.CustomerId) ||
                string.IsNullOrWhiteSpace(dto.ProductId) ||
                dto.Quantity < 1)
            {
                return await req.ProblemAsync("Invalid payload");
            }

            var payload = JsonSerializer.Serialize(dto);
            var qc = _queues.GetQueueClient(queue);
            await qc.CreateIfNotExistsAsync();
            await qc.SendMessageAsync(payload);
            if (string.Equals(queue, "orderqueue", StringComparison.OrdinalIgnoreCase))
            {
                var preview = _queues.GetQueueClient("orderqueue-preview");
                await preview.CreateIfNotExistsAsync();
                await preview.SendMessageAsync(payload, timeToLive: TimeSpan.FromMinutes(10));
            }

            return await req.JsonAsync(new { queued = true });
        }

        [Function("OrderQueueProcessor")]
        public async Task ProcessOrderAsync(
            [QueueTrigger("orderqueue", Connection = "AzureWebJobsStorage")] string msg)
        {
            var dto = JsonSerializer.Deserialize<OrderQueueDto>(msg, CaseInsensitive);
            if (dto is null) return;

            var products = _tables.GetTableClient(ProductsTable);
            await products.CreateIfNotExistsAsync();

            double price = 0d;

            await foreach (var p in products.QueryAsync<TableEntity>(
                               $"PartitionKey eq 'Product' and RowKey eq '{dto.ProductId}'"))
            {
                if (p.TryGetValue("Price", out var v) && v is not null)
                {
                    price = Convert.ToDouble(v);
                }
                break;
            }

            var total = dto.Quantity * price;

            var orders = _tables.GetTableClient(OrdersTable);
            await orders.CreateIfNotExistsAsync();

            var order = new TableEntity("Order", Guid.NewGuid().ToString("N"))
            {
                ["CustomerId"] = dto.CustomerId,
                ["ProductId"] = dto.ProductId,
                ["Quantity"] = dto.Quantity,
                ["TotalPrice"] = total,
                ["Status"] = "Processed",
                ["ProcessedOn"] = DateTimeOffset.UtcNow
            };

            await orders.AddEntityAsync(order);
        }
    }
}
/**************************************
       * Reference list  
       * Title : Help with templates and code
       * Author: OPENAI CHATGPT
       * Date: 2025/10/04 
       * Code version : 1.0
       * Available at : https://chatgpt.com/c/68e1bf30-6718-8327-ab34-b913a4d53122
**************************************/

/**************************************
       * Reference list  
       * Title : Azure Queue storage trigger for Azure Functions
       * Author: Microsoft learn
       * Date: 2025/10/04 
       * Code version : 1.0
       * Available at : https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue-trigger?tabs=python-v2%2Cisolated-process%2Cnodejs-v4%2Cextensionv5&pivots=programming-language-csharp
**************************************/