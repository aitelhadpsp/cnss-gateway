using CnssProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace CnssProxy.Controllers;

public class SubmissionsController(MongoDbService db, CnssApiService cnss) : CnssBaseController(db)
{
    /// <summary>
    /// List FSE / EP submissions stored locally.
    /// Filter by type (FSE|EP) and/or patientId (app's internal patient ID).
    /// </summary>
    [HttpGet("submissions")]
    public async Task<IActionResult> ListSubmissions(
        string username,
        [FromQuery] string? type = null,
        [FromQuery] string? patientId = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20
    )
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var records = await Db.GetSubmissionsAsync(
            GetClientId(),
            username,
            type,
            patientId,
            Math.Max(1, page),
            Math.Clamp(limit, 1, 100)
        );

        return Ok(
            records.Select(r => new
            {
                id = r.Id,
                type = r.Type,
                submissionNumber = r.SubmissionNumber,
                responseCode = r.ResponseCode,
                responseMessage = r.ResponseMessage,
                appPatientId = r.AppPatientId,
                patient = new
                {
                    registrationNumber = r.PatientRegistrationNumber,
                    lastName = r.PatientLastName,
                    firstName = r.PatientFirstName,
                },
                doctorId = r.DoctorId,
                visitDate = r.VisitDate,
                submittedAt = r.SubmittedAt,
            })
        );
    }

    /// <summary>
    /// Get submission stats (count + total amount) grouped by patient.
    /// Optionally filter by patientId.
    /// </summary>
    [HttpGet("submissions/stats")]
    public async Task<IActionResult> GetStats(
        string username,
        [FromQuery] string? patientId = null
    )
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var stats = await Db.GetStatsAsync(GetClientId(), username, patientId);

        return Ok(new
        {
            patients = stats.Select(s => new
            {
                appPatientId = s.AppPatientId,
                patient = new
                {
                    registrationNumber = s.PatientRegistrationNumber,
                    lastName = s.PatientLastName,
                    firstName = s.PatientFirstName,
                },
                fseCount = s.FseCount,
                epCount = s.EpCount,
                totalSubmissions = s.FseCount + s.EpCount,
                totalAmount = s.TotalAmount,
            }),
            totals = new
            {
                fseCount = stats.Sum(s => s.FseCount),
                epCount = stats.Sum(s => s.EpCount),
                totalSubmissions = stats.Sum(s => s.FseCount + s.EpCount),
                totalAmount = stats.Sum(s => s.TotalAmount),
            },
        });
    }

    /// <summary>
    /// Get a specific submission from local DB by its CNSS number (e.g. "FSE268084519183").
    /// Falls back to live CNSS if not found locally.
    /// </summary>
    [HttpGet("submissions/{submissionNumber}")]
    public async Task<IActionResult> GetSubmission(string username, string submissionNumber)
    {
        var (ok, error, _) = await Guard(username);
        if (!ok)
            return error!;

        var record = await Db.GetSubmissionByNumberAsync(GetClientId(), username, submissionNumber);
        if (record != null)
            return Ok(
                new
                {
                    source = "local",
                    type = record.Type,
                    submissionNumber = record.SubmissionNumber,
                    responseCode = record.ResponseCode,
                    responseMessage = record.ResponseMessage,
                    appPatientId = record.AppPatientId,
                    patientName = new
                    {
                        registrationNumber = record.PatientRegistrationNumber,
                        lastName = record.PatientLastName,
                        firstName = record.PatientFirstName,
                    },
                    doctorId = record.DoctorId,
                    visitDate = record.VisitDate,
                    isAccident = record.IsAccident,
                    comment = record.Comment,
                    diagnosis = record.Diagnosis,
                    patient = record.Patient,
                    prescriber = record.Prescriber,
                    prestations = record.Prestations,
                    performedActs = record.PerformedActs,
                    epNumbers = record.EpNumbers,
                    submittedAt = record.SubmittedAt,
                }
            );

        // Not in local DB — fetch from CNSS directly
        var cnssResult = await cnss.GetAsync(
            GetClientId(),
            username,
            $"prescription/fse/detailFse/{submissionNumber}"
        );
        return Ok(new { source = "cnss", data = cnssResult });
    }
}
