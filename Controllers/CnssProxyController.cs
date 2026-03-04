using Microsoft.AspNetCore.Mvc;

namespace CnssProxy.Controllers;

[ApiController]
public class CnssProxyController(
    IHttpClientFactory httpClientFactory,
    ILogger<CnssProxyController> logger
) : ControllerBase
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("cnss");
    private readonly ILogger<CnssProxyController> _logger = logger;
    private const string UpstreamBase = "https://sandboxfse-dev.cnss.ma";

    [Route("{**path}")]
    public async Task ProxyAsync(string path = "")
    {
        var query = Request.QueryString.Value ?? "";
        var targetUrl = string.IsNullOrEmpty(path)
            ? $"{UpstreamBase}/{query}"
            : $"{UpstreamBase}/{path}{query}";

        _logger.LogInformation("[CNSS Proxy] {Method} {TargetUrl}", Request.Method, targetUrl);

        var upstreamRequest = new HttpRequestMessage
        {
            Method = new HttpMethod(Request.Method),
            RequestUri = new Uri(targetUrl),
        };

        // Pass Authorization header directly from client
        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            upstreamRequest.Headers.TryAddWithoutValidation("Authorization", authHeader.ToArray());
        }

        // Forward request body
        if (Request.ContentLength > 0 || Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync();
                var multipart = new MultipartFormDataContent();

                foreach (var field in form)
                {
                    foreach (var val in field.Value)
                    {
                        multipart.Add(new StringContent(val ?? ""), field.Key);
                    }
                }

                foreach (var file in form.Files)
                {
                    var fileContent = new StreamContent(file.OpenReadStream());
                    fileContent.Headers.ContentType =
                        System.Net.Http.Headers.MediaTypeHeaderValue.Parse(
                            file.ContentType ?? "application/octet-stream"
                        );
                    multipart.Add(fileContent, file.Name, file.FileName);
                }

                upstreamRequest.Content = multipart;
            }
            else
            {
                upstreamRequest.Content = new StreamContent(Request.Body);
                if (Request.ContentType != null)
                {
                    upstreamRequest.Content.Headers.ContentType =
                        System.Net.Http.Headers.MediaTypeHeaderValue.Parse(Request.ContentType);
                }
            }
        }

        HttpResponseMessage upstreamResponse;

        try
        {
            upstreamResponse = await _httpClient.SendAsync(
                upstreamRequest,
                HttpCompletionOption.ResponseHeadersRead
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CNSS Proxy] Upstream call failed for {TargetUrl}", targetUrl);
            Response.StatusCode = 502;
            await Response.WriteAsync($"Upstream error: {ex.Message}");
            return;
        }

        _logger.LogInformation(
            "[CNSS Proxy] Upstream responded {StatusCode}",
            (int)upstreamResponse.StatusCode
        );

        Response.StatusCode = (int)upstreamResponse.StatusCode;

        var skipResponseHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Transfer-Encoding",
            "Connection",
            "Keep-Alive",
        };

        foreach (var header in upstreamResponse.Headers)
        {
            if (skipResponseHeaders.Contains(header.Key))
                continue;
            Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in upstreamResponse.Content.Headers)
        {
            if (skipResponseHeaders.Contains(header.Key))
                continue;
            Response.Headers[header.Key] = header.Value.ToArray();
        }

        await upstreamResponse.Content.CopyToAsync(Response.Body);
    }
}
