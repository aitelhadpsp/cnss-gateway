using System.Security.Claims;
using System.Text.Json;
using CnssProxy.Models;
using CnssProxy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CnssProxy.Controllers;

[ApiController]
[Route("api/fse/{username}")]
[Authorize]
public abstract class CnssBaseController(MongoDbService db) : ControllerBase
{
    protected MongoDbService Db { get; } = db;

    protected async Task<(bool ok, IActionResult? error, CnssUser? user)> Guard(string username)
    {
        var user = await Db.GetUserAsync(GetClientId(), username);
        if (user == null)
            return (false, NotFound(new { message = $"User '{username}' not found." }), null);

        if (!user.IsConfigured)
            return (
                false,
                StatusCode(
                    403,
                    new
                    {
                        message = "User is not fully configured. Complete OTP verification first.",
                        username,
                        otpVerified = user.OtpVerified,
                    }
                ),
                null
            );

        return (true, null, user);
    }

    protected string GetClientId() =>
        User.FindFirstValue("azp")
        ?? User.FindFirstValue("client_id")
        ?? throw new UnauthorizedAccessException("No clientId claim found in token.");

    protected static string? ExtractString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
            if (element.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String)
                return val.GetString();
        return null;
    }
}
