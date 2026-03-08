using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CnssProxy.Models;

namespace CnssProxy.Services;

/// <summary>
/// Token lifecycle: one CNSS JWT per user.
/// - Valid + OTP verified   → use it
/// - Expired / missing      → re-authenticate (sets otpVerified=false), caller must do OTP
/// - Valid but no OTP       → throw OtpRequiredException
/// </summary>
public class CnssAuthService(
    IHttpClientFactory httpClientFactory,
    MongoDbService db,
    EncryptionService enc,
    IConfiguration config,
    ILogger<CnssAuthService> logger
)
{
    private readonly HttpClient _http = httpClientFactory.CreateClient("cnss");
    private readonly MongoDbService _db = db;
    private readonly EncryptionService _enc = enc;
    private readonly ILogger<CnssAuthService> _logger = logger;
    private readonly string _cnssBase = config["Cnss:BaseUrl"] ?? "https://sandboxfse-dev.cnss.ma";
    private readonly string _cnssClientId = config["Cnss:ClientId"] ?? "";
    private readonly string _cnssSecretKey = config["Cnss:SecretKey"] ?? "";

    // ── public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a valid, OTP-verified CNSS access token.
    /// Throws <see cref="OtpRequiredException"/> if OTP is needed.
    /// Throws <see cref="KeyNotFoundException"/> if the user is not registered.
    /// </summary>
    public async Task<string> GetValidTokenAsync(string clientId, string username)
    {
        var user =
            await _db.GetUserAsync(clientId, username)
            ?? throw new KeyNotFoundException(
                $"User '{username}' not found for client '{clientId}'."
            );

        if (IsTokenExpired(user))
        {
            _logger.LogInformation(
                "Token expired for {ClientId}/{Username} — re-authenticating",
                clientId,
                username
            );
            await AuthenticateAsync(user);
            throw new OtpRequiredException(
                $"Session renewed for '{username}'. OTP verification required before FSE calls."
            );
        }

        if (!user.OtpVerified)
            throw new OtpRequiredException(
                $"OTP not verified for '{username}'. Complete OTP verification to proceed."
            );

        return _enc.Decrypt(user.EncryptedAccessToken!);
    }

    /// <summary>
    /// Proactively ensures the user has a valid token.
    /// If expired → re-authenticates automatically (OTP will be needed afterwards).
    /// Returns a <see cref="PrepareResult"/> describing what still needs to happen.
    /// </summary>
    public async Task<PrepareResult> PrepareAsync(string clientId, string username)
    {
        var user = await _db.GetUserAsync(clientId, username);
        if (user == null)
            return new PrepareResult(
                Ready: false,
                NeedsOtp: false,
                NeedsRegistration: true,
                Message: "User not found. Register CNSS credentials first."
            );

        if (IsTokenExpired(user))
        {
            try
            {
                _logger.LogInformation(
                    "Prepare: re-authenticating {ClientId}/{Username}",
                    clientId,
                    username
                );
                await AuthenticateAsync(user);
                return new PrepareResult(
                    Ready: false,
                    NeedsOtp: true,
                    NeedsRegistration: false,
                    Message: "Session renewed. OTP verification required to complete setup."
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Prepare: authentication failed for {ClientId}/{Username}",
                    clientId,
                    username
                );
                return new PrepareResult(
                    Ready: false,
                    NeedsOtp: false,
                    NeedsRegistration: true,
                    Message: $"Authentication failed — verify credentials are still valid. ({ex.Message})"
                );
            }
        }

        if (!user.OtpVerified)
            return new PrepareResult(
                Ready: false,
                NeedsOtp: true,
                NeedsRegistration: false,
                Message: "Token is valid but OTP has not been verified yet."
            );

        return new PrepareResult(
            Ready: true,
            NeedsOtp: false,
            NeedsRegistration: false,
            Message: "User is ready. FSE calls will succeed."
        );
    }

    /// <summary>Authenticates the user with CNSS and stores the JWT. OtpVerified is reset to false.</summary>
    public async Task<string> AuthenticateAsync(CnssUser user)
    {
        var body = new
        {
            inpe = _enc.Decrypt(user.EncryptedPractitionerId),
            motDePasse = _enc.Decrypt(user.EncryptedPassword),
            clientId = _cnssClientId,
            secretKey = _cnssSecretKey,
        };

        var response = await _http.PostAsJsonAsync($"{_cnssBase}/adhesion/auth/authenticate", body);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken =
            json.GetProperty("token").GetString() ?? throw new InvalidOperationException(
                "No accessToken in CNSS auth response."
            );

        var expiry = ParseExpiry(accessToken) ?? DateTime.UtcNow.AddHours(1);
        // OTP is always reset on a fresh authentication
        await _db.UpdateTokensAsync(
            user.ClientId,
            user.Username,
            _enc.Encrypt(accessToken),
            null,
            expiry,
            otpVerified: false,
            isConfigured: false
        );

        return accessToken;
    }

    /// <summary>Verifies the OTP and marks the user as fully configured.</summary>
    public async Task VerifyOtpAsync(CnssUser user, string otp)
    {
        if (user.EncryptedAccessToken == null)
            throw new InvalidOperationException("No active CNSS session. Authenticate first.");

        if (IsTokenExpired(user))
            throw new InvalidOperationException(
                "Session expired. Call /prepare to renew before verifying OTP."
            );

        var accessToken = _enc.Decrypt(user.EncryptedAccessToken);

        var req = new HttpRequestMessage(HttpMethod.Post, $"{_cnssBase}/gw/otp/verify");
        req.Headers.Authorization = new("Bearer", accessToken);
        req.Content = JsonContent.Create(new { username = user.Username, otp });

        var response = await _http.SendAsync(req);
        response.EnsureSuccessStatusCode();

        // OTP verify may return a fresh token
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var finalToken =
            json.TryGetProperty("accessToken", out var t) && t.GetString() is { } s
                ? s
                : accessToken;
        var expiry = ParseExpiry(finalToken) ?? user.TokenExpiresAt ?? DateTime.UtcNow.AddHours(1);

        await _db.UpdateTokensAsync(
            user.ClientId,
            user.Username,
            _enc.Encrypt(finalToken),
            null,
            expiry,
            otpVerified: true,
            isConfigured: true
        );
    }

    /// <summary>Resends the OTP for the current session.</summary>
    public async Task ResendOtpAsync(CnssUser user)
    {
        if (user.EncryptedAccessToken == null)
            throw new InvalidOperationException("No active CNSS session. Authenticate first.");

        if (IsTokenExpired(user))
            throw new InvalidOperationException(
                "Session expired. Call /prepare to renew before resending OTP."
            );

        var req = new HttpRequestMessage(HttpMethod.Post, $"{_cnssBase}/gw/otp/resend");
        req.Headers.Authorization = new("Bearer", _enc.Decrypt(user.EncryptedAccessToken));
        (await _http.SendAsync(req)).EnsureSuccessStatusCode();
    }

    // ── private helpers ────────────────────────────────────────────────────

    private static bool IsTokenExpired(CnssUser user) =>
        user.EncryptedAccessToken == null || user.TokenExpiresAt <= DateTime.UtcNow;

    private static DateTime? ParseExpiry(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return null;
            var payload = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
            var json = JsonDocument.Parse(
                Encoding.UTF8.GetString(Convert.FromBase64String(payload))
            );
            if (json.RootElement.TryGetProperty("exp", out var exp))
                return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()).UtcDateTime;
        }
        catch { }
        return null;
    }
}

/// <summary>Thrown when a CNSS FSE call is attempted but OTP has not been verified.</summary>
public class OtpRequiredException(string message) : Exception(message);

/// <summary>Result of a prepare check.</summary>
public record PrepareResult(bool Ready, bool NeedsOtp, bool NeedsRegistration, string Message);
