using CnssProxy.Models;
using CnssProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace CnssProxy.Controllers;

public class PrescriptionController(MongoDbService db, CnssApiService cnss) : CnssBaseController(db)
{
    [HttpGet("{fseId}/prescriptions")]
    public async Task<IActionResult> GetPrescriptions(string username, string fseId)
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.GetAsync(
            GetClientId(),
            username,
            $"prescription/prescriptionPharmacie/{fseId}"
        );
        return Ok(result);
    }

    [HttpPost("{fseId}/prescriptions/dispense")]
    public async Task<IActionResult> DispensePrescription(
        string username,
        [FromBody] DispensePrescriptionRequest body
    )
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.PostAsync(
            GetClientId(),
            username,
            "prescription/prescriptionPharmacie/dispenser",
            body
        );
        return Ok(result);
    }

    [HttpGet("{fseId}/devices")]
    public async Task<IActionResult> GetDevices(string username, string fseId)
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.GetAsync(
            GetClientId(),
            username,
            $"prescription/prescriptionDispositifMedical/{fseId}"
        );
        return Ok(result);
    }

    [HttpPost("{fseId}/acte/execute")]
    public async Task<IActionResult> ExecuteActe(string username, [FromBody] ExecuteActRequest body)
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.PostAsync(
            GetClientId(),
            username,
            "prescription/prescriptionActe/executer",
            body
        );
        return Ok(result);
    }

    [HttpPost("{fseId}/acte/upload")]
    public async Task<IActionResult> UploadActeFile(
        string username,
        string fseId,
        [FromForm] string acteId,
        [FromForm] string fileKey,
        IFormFile file
    )
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var documentUploadJson = $"{{\"id\":\"{acteId}\",\"numeroFse\":\"{fseId}\"}}";

        var result = await cnss.UploadFileAsync(
            GetClientId(),
            username,
            "prescription/prescriptionActe/charger-fichier",
            documentUploadJson,
            file
        );

        _ = Db.AddActeUploadAsync(
            GetClientId(),
            username,
            fseId,
            int.Parse(acteId),
            new ActeFileUpload { FileKey = fileKey }
        );

        return Ok(result);
    }
}
