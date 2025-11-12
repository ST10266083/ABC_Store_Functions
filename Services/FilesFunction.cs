using System.Net;
using ABC_Retail_Functions.Helpers;
using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Net.Http.Headers;

namespace ABC_Retail_Functions.Services
{
    public class FilesFunction
    {
        private readonly ShareServiceClient _shares;
        public FilesFunction(ShareServiceClient ssc) => _shares = ssc;

        [Function("FilesList")]
        public async Task<HttpResponseData> ListAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "files/{share}")]
            HttpRequestData req, string share)
        {
            try

            {
                var sh = _shares.GetShareClient(share);
                await sh.CreateIfNotExistsAsync();

                var root = sh.GetRootDirectoryClient();
                var files = new List<string>();
                await foreach (ShareFileItem item in root.GetFilesAndDirectoriesAsync())
                    if (!item.IsDirectory) files.Add(item.Name);

                return await req.JsonAsync(files);
            }
            catch (Exception ex)
            {
                return await req.ProblemAsync($"List files failed: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        [Function("FilesItem")]
        public async Task<HttpResponseData> ItemAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "get", "delete",
                Route = "files/{share}/path/{*name}")]
            HttpRequestData req, string share, string name)
        {
            name = Uri.UnescapeDataString(name ?? string.Empty);

            var sh = _shares.GetShareClient(share);
            await sh.CreateIfNotExistsAsync();
            var root = sh.GetRootDirectoryClient();

            var parts = name.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return await req.ProblemAsync("Missing file name.", HttpStatusCode.BadRequest);

            var fileName = parts[^1];
            var currentDir = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var dir = currentDir.GetSubdirectoryClient(parts[i]);
                await dir.CreateIfNotExistsAsync();
                currentDir = dir;
            }

            var file = currentDir.GetFileClient(fileName);

            switch (req.Method.ToUpperInvariant())
            {
                case "POST":
                    try
                    {
                        var ms = new MemoryStream();
                        await req.Body.CopyToAsync(ms);
                        ms.Position = 0;

                        await file.CreateAsync(ms.Length);
                        await file.UploadAsync(ms);

                        return await req.JsonAsync(new { ok = true, name }, HttpStatusCode.Created);
                    }
                    catch (Exception ex)
                    {
                        return await req.ProblemAsync($"Upload failed: {ex.Message}", HttpStatusCode.InternalServerError);
                    }

                case "GET":
                    try
                    {
                        var dl = await file.DownloadAsync();
                        var ok = req.CreateResponse(HttpStatusCode.OK);
                        ok.Headers.Add("Content-Type", "application/octet-stream");
                        await dl.Value.Content.CopyToAsync(ok.Body);
                        return ok;
                    }
                    catch (RequestFailedException rfe) when (rfe.Status == 404)
                    {
                        return await req.ProblemAsync("File not found.", HttpStatusCode.NotFound);
                    }

                case "DELETE":
                    try
                    {
                        await file.DeleteIfExistsAsync();
                        return await req.JsonAsync(new { ok = true });
                    }
                    catch (Exception ex)
                    {
                        return await req.ProblemAsync($"Delete failed: {ex.Message}", HttpStatusCode.InternalServerError);
                    }

                default:
                    return await req.ProblemAsync("Method not allowed.", HttpStatusCode.MethodNotAllowed);
            }
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
       * Title : Manage your file function app
       * Author: Microsoft learn
       * Date: 2025/10/04 
       * Code version : 1.0
       * Available at : hhttps://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-use-azure-function-app-settings?tabs=azure-portal%2Cto-premium
**************************************/