using System.Text.Json;
using System.Text.Json.Serialization;
using CnssProxy.Models;
using CnssProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace CnssProxy.Controllers;

public class FseController(MongoDbService db, CnssApiService cnss) : CnssBaseController(db)
{
    /// <summary>
    /// Verify an FSE before submission (dry-run, not stored).
    /// CNSS: POST /prescription/fse/verifierFse
    /// </summary>
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyFse(string username, [FromBody] FseSubmitRequest body)
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.PostAsync(
            GetClientId(),
            username,
            "prescription/fse/verifierFse",
            body.Fse.ToCnss()
        );
        return Ok(result);
    }

    /// <summary>
    /// Create a new FSE and persist it locally.
    /// Body: { "appPatientId": "your-db-id", "fse": { ...FSE fields... } }
    /// CNSS: POST /prescription/fse/creerFse
    /// </summary>
    [HttpPost("create")]
    public async Task<IActionResult> CreateFse(string username, [FromBody] FseSubmitRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.AppPatientId))
            return BadRequest(new { message = "appPatientId is required." });

        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.PostAsync(
            GetClientId(),
            username,
            "prescription/fse/creerFse",
            req.Fse.ToCnss()
        );
        var createResponse = Deserialize<FseCreationResponse>(result);

        // Fetch full FSE detail from CNSS right after creation
        FseDetail? detail = null;
        if (createResponse?.FseNumber != null)
        {
            try
            {
                var detailResult = await cnss.GetAsync(
                    GetClientId(),
                    username,
                    $"prescription/fse/detailFse/{createResponse.FseNumber}"
                );
                detail = Deserialize<FseDetailResponse>(detailResult)?.Detail;
            }
            catch
            { /* best-effort — submission is still saved without detail */
            }
        }

        await Db.InsertSubmissionAsync(
            new SubmissionRecord
            {
                ClientId = GetClientId(),
                Username = username,
                Type = "FSE",
                AppPatientId = req.AppPatientId,
                SubmissionNumber = createResponse?.FseNumber,
                ResponseCode = createResponse?.StatusCode,
                ResponseMessage = createResponse?.Message,
                Prestations = createResponse?.Prestations ?? [],
                PatientRegistrationNumber = req.Fse.Patient?.RegistrationNumber,
                PatientLastName = req.Fse.Patient?.LastName,
                PatientFirstName = req.Fse.Patient?.FirstName,
                VisitDate = detail?.VisitDate ?? req.Fse.VisitDate,
                DoctorId = detail?.DoctorId ?? req.Fse.DoctorId,
                FacilityId = detail?.FacilityId,
                IsAccident = detail?.IsAccident ?? req.Fse.IsAccident,
                Comment = detail?.Comment,
                Diagnosis = detail?.Diagnosis,
                Patient = detail?.Patient,
                Prescriber = detail?.Prescriber,
                PerformedActs = detail?.PerformedActs ?? [],
            }
        );

        return Ok(result);
    }

    /// <summary>
    /// Create a new EP (Entente Préalable) and persist it locally.
    /// Body: { "appPatientId": "your-db-id", "ep": { ...EP fields... } }
    /// CNSS: POST /ep/ep/demander
    /// </summary>
    [HttpPost("ep/create")]
    public async Task<IActionResult> CreateEp(string username, [FromBody] EpSubmitRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.AppPatientId))
            return BadRequest(new { message = "appPatientId is required." });

        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.PostAsync(
            GetClientId(),
            username,
            "ep/ep/demander",
            req.Ep.ToCnss()
        );
        var cnssResponse = Deserialize<EpCreationResponse>(result);

        await Db.InsertSubmissionAsync(
            new SubmissionRecord
            {
                ClientId = GetClientId(),
                Username = username,
                Type = "EP",
                AppPatientId = req.AppPatientId,
                SubmissionNumber =
                    cnssResponse?.Details?.EpNumbers?.FirstOrDefault()
                    ?? cnssResponse?.Details?.FseNumber,
                PatientRegistrationNumber = req.Ep.RegistrationNumber,
                PatientLastName = req.Ep.LastName,
                PatientFirstName = req.Ep.FirstName,
                VisitDate = req.Ep.RequestDate,
                DoctorId = req.Ep.DoctorId,
                ResponseCode = cnssResponse?.StatusCode,
                ResponseMessage = cnssResponse?.Message,
                EpNumbers = cnssResponse?.Details?.EpNumbers ?? [],
            }
        );

        return Ok(result);
    }

    /// <summary>
    /// Get FSE details from CNSS.
    /// CNSS: GET /prescription/fse/detailFse/{fseId}
    /// </summary>
    [HttpGet("{fseId}/detail")]
    public async Task<IActionResult> GetFseDetails(string username, string fseId)
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.GetAsync(
            GetClientId(),
            username,
            $"prescription/fse/detailFse/{fseId}"
        );
        return Ok(result);
    }

    private static T? Deserialize<T>(JsonElement element)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        catch
        {
            return default;
        }
    }
}

/// <summary>Wrapper for FSE creation — keeps gateway fields separate from the CNSS body.</summary>
public class FseSubmitRequest
{
    [JsonPropertyName("appPatientId")]
    public string AppPatientId { get; set; } = "";

    [JsonPropertyName("fse")]
    public FseRequest Fse { get; set; } = new();
}

/// <summary>Wrapper for EP creation — keeps gateway fields separate from the CNSS body.</summary>
public class EpSubmitRequest
{
    [JsonPropertyName("appPatientId")]
    public string AppPatientId { get; set; } = "";

    [JsonPropertyName("ep")]
    public EpRequest Ep { get; set; } = new();
}
