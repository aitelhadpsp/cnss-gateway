using System.Security.Claims;
using CnssProxy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CnssProxy.Controllers;

[ApiController]
[Route("api/users/{username}/otp")]
[Authorize]
public class OtpController(MongoDbService db, CnssAuthService cnssAuth) : ControllerBase
{
    /// <summary>
    /// Verify the OTP sent by CNSS after initial authentication.
    /// Must be called once before FSE calls are allowed.
    /// </summary>
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyOtp(string username, [FromBody] VerifyOtpRequest req)
    {
        var user = await db.GetUserAsync(GetClientId(), username);
        if (user == null)
            return NotFound(new { message = "User not found." });

        try
        {
            await cnssAuth.VerifyOtpAsync(user, req.Otp);
            return Ok(
                new { message = "OTP verified. User is fully configured and ready for FSE calls." }
            );
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "OTP verification failed.", error = ex.Message });
        }
    }

    /// <summary>Resends the OTP to the user's registered phone/email.</summary>
    [HttpPost("resend")]
    public async Task<IActionResult> ResendOtp(string username)
    {
        var user = await db.GetUserAsync(GetClientId(), username);
        if (user == null)
            return NotFound(new { message = "User not found." });

        try
        {
            await cnssAuth.ResendOtpAsync(user);
            return Ok(new { message = "OTP resent." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Failed to resend OTP.", error = ex.Message });
        }
    }

    private string GetClientId() =>
        User.FindFirstValue("azp")
        ?? User.FindFirstValue("client_id")
        ?? throw new UnauthorizedAccessException("No clientId claim found in token.");
}

public record VerifyOtpRequest(string Otp);
