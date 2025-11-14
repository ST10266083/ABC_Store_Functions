using ABC_Retail_Functions.Helpers;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Net.Http.Headers;
using System.Net;

namespace ABC_Retail_Functions.Services;

// This is the blob function which helps the mvc with backend operations
public class BlobsFunction
{
    private readonly BlobServiceClient _blobs;
    public BlobsFunction(BlobServiceClient bsc) => _blobs = bsc;

    [Function("BlobsList")]
    public async Task<HttpResponseData> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "blobs/{container}")] HttpRequestData req,
        string container)
    {
        try
        {
            var cont = _blobs.GetBlobContainerClient(container);
            await cont.CreateIfNotExistsAsync();

            var names = new List<string>();
            await foreach (var b in cont.GetBlobsAsync()) names.Add(b.Name);

            var res = await req.JsonAsync(names);
            res.Headers.Add("X-Blob-Server", "v3");
            return res;
        }
        catch (Exception ex)
        {
            var err = req.CreateResponse(HttpStatusCode.InternalServerError);
            await err.WriteStringAsync($"List failed: {ex}");
            err.Headers.Add("X-Blob-Server", "v3");
            return err;
        }
    }

    [Function("BlobsItem")]
    public async Task<HttpResponseData> ItemAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "delete", Route = "blobs/{container}/{*name}")]
        HttpRequestData req, string container, string name)
    {
        name = Uri.UnescapeDataString(name ?? string.Empty);
        var cont = _blobs.GetBlobContainerClient(container);
        await cont.CreateIfNotExistsAsync();
        var blob = cont.GetBlobClient(name);

        return req.Method.ToUpper() switch
        {
            "GET" => await HandleGetAsync(req, blob, name),
            "DELETE" => await HandleDeleteAsync(req, blob),
            "POST" => await HandleUploadAsync(req, blob, name),
            _ => await req.ProblemAsync($"Unsupported method: {req.Method}", HttpStatusCode.MethodNotAllowed)
        };
    }

    private static async Task<HttpResponseData> HandleGetAsync(HttpRequestData req, BlobClient blob, string name)
    {
        if (!await blob.ExistsAsync())
            return await req.ProblemAsync($"Blob '{name}' not found", HttpStatusCode.NotFound);

        var dl = await blob.DownloadAsync();
        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", dl.Value.Details.ContentType ?? "application/octet-stream");
        res.Headers.Add("Cache-Control", "public, max-age=300");
        res.Headers.Add("X-Blob-Server", "v3");
        await dl.Value.Content.CopyToAsync(res.Body);
        return res;
    }

    private static async Task<HttpResponseData> HandleDeleteAsync(HttpRequestData req, BlobClient blob)
    {
        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
        var res = await req.JsonAsync(new { ok = true });
        res.Headers.Add("X-Blob-Server", "v3");
        return res;
    }

    private static async Task<HttpResponseData> HandleUploadAsync(HttpRequestData req, BlobClient blob, string name)
    {
        Stream payload;
        string? contentType;

        if (req.Headers.TryGetValues("Content-Type", out var ctVals) &&
            (ctVals.FirstOrDefault() ?? "").StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            var boundary = GetBoundary(ctVals.First());
            using var all = new MemoryStream();
            await req.Body.CopyToAsync(all);
            all.Position = 0;

            var reader = new MultipartReader(boundary, all);
            payload = null!;
            contentType = null;

            for (MultipartSection? section = await reader.ReadNextSectionAsync(); section is not null; section = await reader.ReadNextSectionAsync())
            {
                var cd = section.GetContentDispositionHeader();
                if (cd?.IsFileDisposition() == true && cd.Name.Value == "file")
                {
                    var ms = new MemoryStream();
                    await section.Body.CopyToAsync(ms);
                    ms.Position = 0;
                    payload = ms;
                    section.Headers.TryGetValue("Content-Type", out var partCt);
                    contentType = partCt;
                    break;
                }
            }

            if (payload is null)
                return await req.ProblemAsync("No 'file' field found in form-data.", HttpStatusCode.BadRequest);
        }
        else
        {
            payload = new MemoryStream();
            await req.Body.CopyToAsync(payload);
            payload.Position = 0;
            req.Headers.TryGetValues("Content-Type", out var rawCts);
            contentType = rawCts?.FirstOrDefault();
        }

        await blob.UploadAsync(payload, overwrite: true);
        await blob.SetHttpHeadersAsync(new BlobHttpHeaders { ContentType = string.IsNullOrWhiteSpace(contentType) ? GuessMimeType(name) : contentType! });

        var baseUrl = $"{req.Url.Scheme}://{req.Url.Host}{(req.Url.IsDefaultPort ? "" : ":" + req.Url.Port)}";
        var safeName = string.Join("/", name.Split('/').Select(Uri.EscapeDataString));
        var functionUrl = $"{baseUrl}/api/blobs/{blob.BlobContainerName}/{safeName}";

        var ok = await req.JsonAsync(new { url = functionUrl }, HttpStatusCode.Created);
        ok.Headers.Add("X-Blob-Server", "v3");
        return ok;
    }

    private static string GetBoundary(string contentType)
    {
        const string key = "boundary=";
        var idx = contentType.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) throw new InvalidOperationException("Missing boundary in Content-Type.");
        return contentType[(idx + key.Length)..].Trim('"');
    }

    private static string GuessMimeType(string pathOrName) => Path.GetExtension(pathOrName).ToLowerInvariant() switch
    {
        ".avif" => "image/avif",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        ".csv" => "text/csv",
        ".json" => "application/json",
        _ => "application/octet-stream"
    };
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
       * Title : Azure Blob storage bindings for Azure Functions overview
       * Author: Microsoft Ignite
       * Date: 2025/10/04 
       * Code version : 1.0
       * Available at : https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-blob?tabs=isolated-process%2Cextensionv5&pivots=programming-language-csharp
**************************************/