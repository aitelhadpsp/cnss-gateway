using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CnssProxy.Models;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;

namespace CnssProxy.Services;

/// <summary>
/// Makes authenticated calls to the CNSS API with automatic:
/// - Token management (via CnssAuthService)
/// - Response caching (IMemoryCache, keyed by request fingerprint)
/// - Full request/response logging to MongoDB
/// </summary>
public class CnssApiService(
    IHttpClientFactory httpClientFactory,
    CnssAuthService auth,
    MongoDbService db,
    IMemoryCache cache,
    IHttpContextAccessor httpContext,
    IConfiguration config,
    ILogger<CnssApiService> logger
)
{
    private readonly HttpClient _http = httpClientFactory.CreateClient("cnss");
    private readonly CnssAuthService _auth = auth;
    private readonly MongoDbService _db = db;
    private readonly IMemoryCache _cache = cache;
    private readonly IHttpContextAccessor _httpContext = httpContext;
    private readonly ILogger<CnssApiService> _logger = logger;
    private readonly string _cnssBase = config["Cnss:BaseUrl"] ?? "https://sandboxfse-dev.cnss.ma";

    // TTL per operation type: write ops get 0s (never cached), read ops get configurable TTL
    private readonly TimeSpan _readCacheTtl = TimeSpan.FromSeconds(
        config.GetValue<int>("Cache:ReadTtlSeconds", 300)
    );

    // Paths that mutate state — never serve from cache
    private static readonly HashSet<string> _noCachePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "prescription/fse/creerFse",
        "prescription/fse/creerEP",
        "prescription/fse/verifierFse",
        "prescription/fse/rejet/reponse",
        "prescription/fse/rejet/charger-fichier",
        "prescription/fse/demandeComplement/reponse",
        "prescription/fse/demandeComplement/charger-fichier",
        "prescription/prescriptionActe/executer",
        "prescription/prescriptionActe/charger-fichier",
        "prescription/prescriptionPharmacie/dispenser",
        "prescription/prescriptionPharmacie/modifier",
        "prescription/prescriptionDispositifMedical/executer",
        "adhesion/auth/authenticate",
        "adhesion/auth/refresh-token",
        "gw/otp/verify",
        "gw/otp/resend",
    };

    /// <summary>
    /// Sends a multipart file upload to CNSS.
    /// CNSS expects: documentUpload (JSON string) + files (the file).
    /// </summary>
    public async Task<JsonElement> UploadFileAsync(
        string clientId,
        string username,
        string path,
        string documentUploadJson,
        IFormFile file
    )
    {
        var sw = Stopwatch.StartNew();
        var log = new RequestLog
        {
            ClientId = clientId,
            Username = username,
            Method = "POST",
            CnssPath = path,
            GatewayRoute = _httpContext.HttpContext?.Request.Path ?? "",
            RequestBody = BsonDocument.Parse(documentUploadJson),
        };

        try
        {
            var token = await _auth.GetValidTokenAsync(clientId, username);
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_cnssBase}/{path}");
            req.Headers.Authorization = new("Bearer", token);

            var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent(documentUploadJson), "documentUpload");

            var fileContent = new StreamContent(file.OpenReadStream());
            fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(
                file.ContentType ?? "application/octet-stream"
            );
            multipart.Add(fileContent, "files", file.FileName);

            req.Content = multipart;

            var response = await _http.SendAsync(req);
            sw.Stop();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(responseJson).RootElement;

            response.EnsureSuccessStatusCode();

            log.ResponseBody = BsonDocument.Parse(responseJson);
            log.StatusCode = (int)response.StatusCode;
            log.DurationMs = sw.ElapsedMilliseconds;
            _ = _db.InsertLogAsync(log);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            log.StatusCode =
                ex is HttpRequestException hre && hre.StatusCode.HasValue
                    ? (int)hre.StatusCode.Value
                    : 500;
            log.DurationMs = sw.ElapsedMilliseconds;
            log.ErrorMessage = ex.Message;
            _ = _db.InsertLogAsync(log);
            throw;
        }
    }

    public Task<JsonElement> PostAsync(
        string clientId,
        string username,
        string path,
        object body
    ) => ExecuteAsync(clientId, username, HttpMethod.Post, path, body);

    public Task<JsonElement> GetAsync(string clientId, string username, string path) =>
        ExecuteAsync(clientId, username, HttpMethod.Get, path, null);

    // ── core ───────────────────────────────────────────────────────────────

    private async Task<JsonElement> ExecuteAsync(
        string clientId,
        string username,
        HttpMethod method,
        string path,
        object? body
    )
    {
        var bodyJson = body != null ? SerializeStripNulls(body) : null;
        var cacheKey = BuildCacheKey(clientId, username, method, path, bodyJson);
        var canCache = !_noCachePaths.Contains(path);

        // Serve from cache if available
        if (canCache && _cache.TryGetValue(cacheKey, out JsonElement cached))
        {
            _logger.LogInformation(
                "[Cache HIT] {Method} {Path} for {ClientId}/{Username}",
                method,
                path,
                clientId,
                username
            );
            _ = LogAsync(
                clientId,
                username,
                method.Method,
                path,
                bodyJson,
                cached,
                200,
                0,
                fromCache: true
            );
            return cached;
        }

        // Call CNSS
        var sw = Stopwatch.StartNew();
        var log = new RequestLog
        {
            ClientId = clientId,
            Username = username,
            Method = method.Method,
            CnssPath = path,
            GatewayRoute = _httpContext.HttpContext?.Request.Path ?? "",
            RequestBody = bodyJson != null ? BsonDocument.Parse(bodyJson) : null,
        };

        try
        {
            var token = await _auth.GetValidTokenAsync(clientId, username);
            var req = new HttpRequestMessage(method, $"{_cnssBase}/{path}");
            req.Headers.Authorization = new("Bearer", token);
            if (bodyJson != null)
                req.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(req);
            sw.Stop();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(responseJson).RootElement;

            response.EnsureSuccessStatusCode();

            // Cache successful read responses
            if (canCache)
                _cache.Set(cacheKey, result, _readCacheTtl);

            log.ResponseBody = BsonDocument.Parse(responseJson);
            log.StatusCode = (int)response.StatusCode;
            log.DurationMs = sw.ElapsedMilliseconds;
            _ = _db.InsertLogAsync(log);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            log.StatusCode =
                ex is HttpRequestException hre && hre.StatusCode.HasValue
                    ? (int)hre.StatusCode.Value
                    : 500;
            log.DurationMs = sw.ElapsedMilliseconds;
            log.ErrorMessage = ex.Message;
            _ = _db.InsertLogAsync(log);
            throw;
        }
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static string SerializeStripNulls(object body)
    {
        var raw = JsonSerializer.Serialize(body, body.GetType());
        using var doc = JsonDocument.Parse(raw);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        StripNulls(doc.RootElement, writer);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void StripNulls(JsonElement el, Utf8JsonWriter w)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject();
                foreach (var p in el.EnumerateObject())
                    if (p.Value.ValueKind != JsonValueKind.Null)
                    {
                        w.WritePropertyName(p.Name);
                        StripNulls(p.Value, w);
                    }
                w.WriteEndObject();
                break;
            case JsonValueKind.Array:
                w.WriteStartArray();
                foreach (var item in el.EnumerateArray())
                    StripNulls(item, w);
                w.WriteEndArray();
                break;
            default:
                el.WriteTo(w);
                break;
        }
    }

    private static string BuildCacheKey(
        string clientId,
        string username,
        HttpMethod method,
        string path,
        string? bodyJson
    )
    {
        var raw = $"{clientId}:{username}:{method}:{path}:{bodyJson ?? ""}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"cnss:{Convert.ToHexString(hash)[..16]}"; // first 16 hex chars is enough
    }

    private Task LogAsync(
        string clientId,
        string username,
        string method,
        string path,
        string? bodyJson,
        JsonElement response,
        int status,
        long durationMs,
        bool fromCache
    )
    {
        var log = new RequestLog
        {
            ClientId = clientId,
            Username = username,
            Method = method,
            CnssPath = path,
            GatewayRoute = _httpContext.HttpContext?.Request.Path ?? "",
            RequestBody = bodyJson != null ? BsonDocument.Parse(bodyJson) : null,
            ResponseBody = BsonDocument.Parse(response.GetRawText()),
            StatusCode = status,
            DurationMs = durationMs,
            FromCache = fromCache,
        };
        return _db.InsertLogAsync(log);
    }
}
