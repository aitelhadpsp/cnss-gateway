using CnssProxy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CnssProxy.Controllers;

[ApiController]
[Route("api/config")]
[Authorize]
public class ConfigController(MongoDbService db, IConfiguration configuration) : ControllerBase
{
    [HttpGet("proxy")]
    public async Task<IActionResult> GetProxyConfig()
    {
        var config = await db.GetProxyConfigAsync();
        return Ok(
            new
            {
                upstreamBase = config?.UpstreamBase ?? configuration["Cnss:BaseUrl"],
                cnssClientId = config?.CnssClientId ?? configuration["Cnss:ClientId"],
                cnssSecretKey = config?.CnssSecretKey ?? configuration["Cnss:SecretKey"],
                isCustom = config != null,
                updatedAt = config?.UpdatedAt,
            }
        );
    }

    [HttpPut("proxy")]
    public async Task<IActionResult> SetProxyConfig([FromBody] SetProxyConfigRequest req)
    {
        await db.SetProxyConfigAsync(req.UpstreamBase, req.CnssClientId, req.CnssSecretKey);
        return Ok(new { message = "Proxy config updated." });
    }

    [HttpDelete("proxy")]
    public async Task<IActionResult> ResetProxyConfig()
    {
        await db.SetProxyConfigAsync(null, null, null);
        return Ok(new { message = "Proxy config reset to defaults." });
    }
}

public record SetProxyConfigRequest(
    string? UpstreamBase,
    string? CnssClientId,
    string? CnssSecretKey
);
