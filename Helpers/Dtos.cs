namespace ABC_Retail_Functions.Helpers;

public record UpsertEntityDto(string PartitionKey, string RowKey, Dictionary<string, object>? Properties);

public record OrderQueueDto(string CustomerId, string ProductId, int Quantity);
