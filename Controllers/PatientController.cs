using CnssProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace CnssProxy.Controllers;

public class PatientController(MongoDbService db, CnssApiService cnss) : CnssBaseController(db)
{
    /// <summary>
    /// Fetch patient info (signalétique) by CNSS immatriculation number.
    /// CNSS: POST /prescription/patient/signaletique
    /// </summary>
    [HttpPost("patient")]
    public async Task<IActionResult> GetPatient(string username, [FromBody] PatientRequest req)
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.PostAsync(
            GetClientId(),
            username,
            "prescription/patient/signaletique",
            new
            {
                numeroImmatriculation = req.RegistrationNumber,
                identifiant = req.Identifier ?? "",
            }
        );
        return Ok(result);
    }
}

public record PatientRequest(string RegistrationNumber, string? Identifier);
