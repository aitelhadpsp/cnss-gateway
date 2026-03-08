using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CnssProxy.Models;

/// <summary>
/// Stores every FSE or EP submission we send to CNSS so we can list/retrieve them later.
/// CNSS has no list-submissions API, so this is our source of truth.
/// FSE records are enriched with the full detail fetched from CNSS after creation.
/// </summary>
[BsonIgnoreExtraElements]
public class SubmissionRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string ClientId { get; set; } = "";
    public string Username { get; set; } = "";

    /// <summary>"FSE" or "EP"</summary>
    public string Type { get; set; } = "FSE";

    /// <summary>The FSE / EP number returned by CNSS (e.g. "FSE268084519183").</summary>
    public string? SubmissionNumber { get; set; }

    /// <summary>The client app's internal patient ID.</summary>
    public string AppPatientId { get; set; } = "";

    // ── CNSS response summary ────────────────────────────────────────────────

    public string? ResponseCode { get; set; }
    public string? ResponseMessage { get; set; }

    // ── Quick-access patient fields (from request — detail may have null names) ──

    public string? PatientRegistrationNumber { get; set; }
    public string? PatientLastName { get; set; }
    public string? PatientFirstName { get; set; }

    // ── FSE detail fields (populated by GET /detailFse after creation) ───────

    public string? VisitDate { get; set; }
    public string? DoctorId { get; set; }
    public string? FacilityId { get; set; }
    public bool IsAccident { get; set; }
    public string? Comment { get; set; }
    public FseDiagnosis? Diagnosis { get; set; }
    public FsePatientDetail? Patient { get; set; }
    public FsePrescriberDetail? Prescriber { get; set; }

    /// <summary>
    /// Acts from the FSE creation response (prestations).
    /// Contains unit price and session date not present in the detail response.
    /// </summary>
    public List<Prestation> Prestations { get; set; } = [];

    /// <summary>
    /// Acts from the FSE detail (acteRealises).
    /// Each FsePerformedActDetail.TechnicalId is the CNSS id needed for
    /// POST /prescription/prescriptionActe/charger-fichier.
    /// File uploads are tracked per act in Uploads.
    /// </summary>
    public List<FsePerformedActDetail> PerformedActs { get; set; } = [];

    // ── EP only ──────────────────────────────────────────────────────────────

    public List<string> EpNumbers { get; set; } = [];

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

public class TimeSeriesDataPoint
{
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class TimeSeriesResult
{
    public string GroupingType { get; set; } = "";
    public Dictionary<string, TimeSeriesDataPoint> Data { get; set; } = [];
}

public class SubmissionTimeSeriesStats
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSeriesResult Fse { get; set; } = new();
    public TimeSeriesResult Ep { get; set; } = new();
}

public class PatientStats
{
    public string AppPatientId { get; set; } = "";
    public string? PatientLastName { get; set; }
    public string? PatientFirstName { get; set; }
    public string? PatientRegistrationNumber { get; set; }
    public int FseCount { get; set; }
    public int EpCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class ActeFileUpload
{
    /// <summary>App-defined key identifying what this file represents (e.g. "xray_before").</summary>
    public string FileKey { get; set; } = "";

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
