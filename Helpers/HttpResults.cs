using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace ABC_Retail_Functions.Helpers;

public static class HttpResults
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static async Task<HttpResponseData> JsonAsync(this HttpRequestData req, object body, HttpStatusCode code = HttpStatusCode.OK)
    {
        var res = req.CreateResponse(code);
        res.Headers.Add("Content-Type", "application/json");
        await res.WriteStringAsync(JsonSerializer.Serialize(body, JsonOpts));
        return res;
    }

    public static Task<HttpResponseData> ProblemAsync(this HttpRequestData req, string message, HttpStatusCode code = HttpStatusCode.BadRequest)
        => req.JsonAsync(new { error = message }, code);
}
/**************************************
       * Reference list  
       * Title : Help with templates and code
       * Author: OPENAI CHATGPT
       * Date: 2025/10/04 
       * Code version : 1.0
       * Available at : https://chatgpt.com/c/68e1bf30-6718-8327-ab34-b913a4d53122
**************************************/