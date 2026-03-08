using CnssProxy.Models;
using CnssProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace CnssProxy.Controllers;

public class RejetController(MongoDbService db, CnssApiService cnss) : CnssBaseController(db)
{
    // ── Rejet ──────────────────────────────────────────────────────────────

    [HttpGet("{fseId}/rejet")]
    public async Task<IActionResult> GetRejet(string username, string fseId)
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.GetAsync(
            GetClientId(),
            username,
            $"prescription/fse/rejet?numeroFse={fseId}"
        );
        return Ok(result);
    }

    [HttpPost("{fseId}/rejet/reponse")]
    public async Task<IActionResult> RepondreRejet(string username, [FromBody] FseResponseRequest body)
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.PostAsync(
            GetClientId(),
            username,
            "prescription/fse/rejet/reponse",
            body
        );
        return Ok(result);
    }

    [HttpPost("{fseId}/rejet/upload")]
    public async Task<IActionResult> UploadRejetFile(
        string username,
        string fseId,
        [FromForm] string documentId,
        IFormFile file
    )
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var documentUploadJson = $"{{\"documentId\":\"{documentId}\",\"numeroFse\":\"{fseId}\"}}";

        var result = await cnss.UploadFileAsync(
            GetClientId(),
            username,
            "prescription/fse/rejet/charger-fichier",
            documentUploadJson,
            file
        );
        return Ok(result);
    }

    // ── Demande de complément ───────────────────────────────────────────────

    [HttpGet("{fseId}/complement")]
    public async Task<IActionResult> GetComplement(string username, string fseId)
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.GetAsync(
            GetClientId(),
            username,
            $"prescription/fse/demandeComplement?numeroFse={fseId}"
        );
        return Ok(result);
    }

    [HttpPost("{fseId}/complement/reponse")]
    public async Task<IActionResult> RepondreComplement(
        string username,
        [FromBody] FseResponseRequest body
    )
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var result = await cnss.PostAsync(
            GetClientId(),
            username,
            "prescription/fse/demandeComplement/reponse",
            body
        );
        return Ok(result);
    }

    [HttpPost("{fseId}/complement/upload")]
    public async Task<IActionResult> UploadComplementFile(
        string username,
        string fseId,
        [FromForm] string documentId,
        IFormFile file
    )
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var documentUploadJson = $"{{\"documentId\":\"{documentId}\",\"numeroFse\":\"{fseId}\"}}";

        var result = await cnss.UploadFileAsync(
            GetClientId(),
            username,
            "prescription/fse/demandeComplement/charger-fichier",
            documentUploadJson,
            file
        );
        return Ok(result);
    }
}
