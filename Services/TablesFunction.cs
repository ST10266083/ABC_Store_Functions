using System.Net;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using ABC_Retail_Functions.Helpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace ABC_Retail_Functions.Services

{
    public class TablesFunction

    {
        private readonly TableServiceClient _tables;
        public TablesFunction(TableServiceClient tsc) => _tables = tsc;

        [Function("TablesUpsert")]
        public async Task<HttpResponseData> UpsertAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tables/{table}")]

            HttpRequestData req, string table)
        {

            var dto = await JsonSerializer.DeserializeAsync<UpsertEntityDto>(req.Body);

            if (dto is null || string.IsNullOrWhiteSpace(dto.PartitionKey) || string.IsNullOrWhiteSpace(dto.RowKey))
                return await req.ProblemAsync("PartitionKey and RowKey required");

            var client = _tables.GetTableClient(table);
            await client.CreateIfNotExistsAsync();

            var entity = new TableEntity(dto.PartitionKey, dto.RowKey);

            if (dto.Properties is not null)
                foreach (var kv in dto.Properties) entity[kv.Key] = kv.Value;

            await client.UpsertEntityAsync(entity);
            return await req.JsonAsync(new { ok = true }, HttpStatusCode.Created);
        }

    }
}
