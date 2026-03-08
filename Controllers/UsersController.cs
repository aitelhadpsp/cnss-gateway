using System.Security.Claims;
using CnssProxy.Models;
using CnssProxy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CnssProxy.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(MongoDbService db, EncryptionService enc, CnssAuthService cnssAuth)
    : ControllerBase
{
    /// <summary>
    /// Register or update a practitioner's CNSS credentials.
    /// This triggers an initial CNSS authentication; the user must then verify OTP.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterUserRequest req)
    {
        var clientId = GetClientId();

        var user = new CnssUser
        {
            ClientId = clientId,
            Username = req.Username,
            EncryptedPractitionerId = enc.Encrypt(req.PractitionerId),
            EncryptedPassword = enc.Encrypt(req.Password),
        };

        await db.UpsertAsync(user);

        try
        {
            await cnssAuth.AuthenticateAsync(user);
            return Ok(
                new
                {
                    message = "User registered and authenticated with CNSS. Please verify OTP to complete setup.",
                    otpRequired = true,
                }
            );
        }
        catch (Exception ex)
        {
            return BadRequest(
                new
                {
                    message = "Credentials saved but CNSS authentication failed.",
                    error = ex.Message,
                }
            );
        }
    }

    /// <summary>Returns whether the user is configured and ready to make FSE calls.</summary>
    [HttpGet("{username}/status")]
    public async Task<IActionResult> GetStatus(string username)
    {
        var user = await db.GetUserAsync(GetClientId(), username);
        if (user == null)
            return NotFound(new { message = "User not found." });

        return Ok(
            new
            {
                username = user.Username,
                isConfigured = user.IsConfigured,
                otpVerified = user.OtpVerified,
                hasActiveToken = user.EncryptedAccessToken != null,
                tokenExpiresAt = user.TokenExpiresAt,
                tokenExpired = user.TokenExpiresAt <= DateTime.UtcNow,
            }
        );
    }

    /// <summary>
    /// Prepares the user for FSE calls:
    /// - If no token / expired → re-authenticates with CNSS automatically.
    /// - If token valid but OTP not done → tells the caller OTP is needed.
    /// - If all good → returns ready=true, next FSE call will succeed.
    /// Call this before any FSE operation to avoid auth surprises.
    /// </summary>
    [HttpPost("{username}/prepare")]
    public async Task<IActionResult> Prepare(string username)
    {
        var result = await cnssAuth.PrepareAsync(GetClientId(), username);

        var statusCode = result switch
        {
            { Ready: true } => 200,
            { NeedsOtp: true } => 202,
            { NeedsRegistration: true } => 404,
            _ => 500,
        };

        return StatusCode(
            statusCode,
            new
            {
                ready = result.Ready,
                needsOtp = result.NeedsOtp,
                needsRegistration = result.NeedsRegistration,
                message = result.Message,
            }
        );
    }

    private string GetClientId() =>
        User.FindFirstValue("azp")
        ?? User.FindFirstValue("client_id")
        ?? throw new UnauthorizedAccessException("No clientId claim found in token.");
}

public record RegisterUserRequest(string Username, string PractitionerId, string Password);
