using System.Text.Json.Serialization;

namespace CnssProxy.Models;

// ── FSE creation ─────────────────────────────────────────────────────────────

/// <summary>Typed response from POST /prescription/fse/creerFse</summary>
public class FseCreationResponse
{
    [JsonPropertyName("code")]
    public string? StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("numeroFse")]
    public string? FseNumber { get; set; }

    [JsonPropertyName("prestations")]
    public List<Prestation>? Prestations { get; set; }
}

/// <summary>Act entry from the FSE creation response (prestations list).</summary>
public class Prestation
{
    [JsonPropertyName("id")]
    public int TechnicalId { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("libelle")]
    public string Name { get; set; } = "";

    [JsonPropertyName("nombre")]
    public int Count { get; set; }

    [JsonPropertyName("prixUnitaire")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("dateRealisation")]
    public string? PerformedDate { get; set; }

    [JsonPropertyName("dateSeancePrescription")]
    public string? SessionDate { get; set; }
}

// ── FSE detail ───────────────────────────────────────────────────────────────

/// <summary>Typed response from GET /prescription/fse/detailFse/{fseId}</summary>
public class FseDetailResponse
{
    [JsonPropertyName("code")]
    public string? StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("fseDetail")]
    public FseDetail? Detail { get; set; }
}

public class FseDetail
{
    [JsonPropertyName("numeroFSE")]
    public string? FseNumber { get; set; }

    [JsonPropertyName("numeroImmatriculation")]
    public string? RegistrationNumber { get; set; }

    [JsonPropertyName("numeroIndividu")]
    public string? IndividualNumber { get; set; }

    [JsonPropertyName("inpeMedecin")]
    public string? DoctorId { get; set; }

    [JsonPropertyName("inpeEtablissement")]
    public string? FacilityId { get; set; }

    [JsonPropertyName("dateVisite")]
    public string? VisitDate { get; set; }

    [JsonPropertyName("estAccident")]
    public bool IsAccident { get; set; }

    [JsonPropertyName("commentaire")]
    public string? Comment { get; set; }

    [JsonPropertyName("diagnostic")]
    public FseDiagnosis? Diagnosis { get; set; }

    [JsonPropertyName("patient")]
    public FsePatientDetail? Patient { get; set; }

    [JsonPropertyName("prescripteur")]
    public FsePrescriberDetail? Prescriber { get; set; }

    [JsonPropertyName("acteRealises")]
    public List<FsePerformedActDetail>? PerformedActs { get; set; }
}

public class FseDiagnosis
{
    [JsonPropertyName("pathologies")]
    public List<FseCondition>? Conditions { get; set; }

    [JsonPropertyName("diagnostic")]
    public string? Comment { get; set; }
}

public class FseCondition
{
    [JsonPropertyName("codePathologie")]
    public string Code { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("provisoire")]
    public bool IsPreliminary { get; set; }
}

public class FsePatientDetail
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("nom")]
    public string? LastName { get; set; }

    [JsonPropertyName("prenom")]
    public string? FirstName { get; set; }

    [JsonPropertyName("numeroImmatriculation")]
    public string? RegistrationNumber { get; set; }

    [JsonPropertyName("numeroIndividu")]
    public string? IndividualNumber { get; set; }

    [JsonPropertyName("identifiant")]
    public string? Identifier { get; set; }

    [JsonPropertyName("dateNaissance")]
    public string? BirthDate { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("genre")]
    public string? Gender { get; set; }

    [JsonPropertyName("adresse")]
    public string? Address { get; set; }

    [JsonPropertyName("telephone")]
    public string? Phone { get; set; }
}

public class FsePrescriberDetail
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("nom")]
    public string? LastName { get; set; }

    [JsonPropertyName("prenom")]
    public string? FirstName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("telephone")]
    public string? Phone { get; set; }

    [JsonPropertyName("inpe")]
    public string? PractitionerId { get; set; }

    [JsonPropertyName("enabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("categorieMedicale")]
    public string? MedicalCategory { get; set; }

    [JsonPropertyName("dateCreation")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("unite")]
    public FsePrescriberUnit? Unit { get; set; }
}

public class FsePrescriberUnit
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("inpe")]
    public string? PractitionerId { get; set; }

    [JsonPropertyName("libelle")]
    public string? Name { get; set; }

    [JsonPropertyName("active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("telephone")]
    public string? Phone { get; set; }

    [JsonPropertyName("adresse")]
    public string? Address { get; set; }

    [JsonPropertyName("ville")]
    public string? City { get; set; }
}

/// <summary>
/// An act from the FSE detail response (acteRealises).
/// TechnicalId is the CNSS id needed when uploading result files.
/// Uploads are tracked locally per act.
/// </summary>
public class FsePerformedActDetail
{
    [JsonPropertyName("id")]
    public int TechnicalId { get; set; }

    [JsonPropertyName("nombreActes")]
    public int Count { get; set; }

    [JsonPropertyName("categorieActe")]
    public string? Category { get; set; }

    [JsonPropertyName("codeActe")]
    public string Code { get; set; } = "";

    [JsonPropertyName("libelleActe")]
    public string Name { get; set; } = "";

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("commentaire")]
    public string? Comment { get; set; }

    [JsonPropertyName("motif")]
    public string? Reason { get; set; }

    [JsonPropertyName("localisation")]
    public string? Location { get; set; }

    [JsonPropertyName("radioAvant")]
    public bool? XRayBefore { get; set; }

    [JsonPropertyName("radioApres")]
    public bool? XRayAfter { get; set; }

    [JsonPropertyName("typePeriode")]
    public string? PeriodType { get; set; }

    [JsonPropertyName("nombrePeriode")]
    public int? PeriodCount { get; set; }

    [JsonPropertyName("prescriptionActeDentaires")]
    public List<FseDentalActDetail>? DentalCodes { get; set; }

    /// <summary>Files uploaded for this act via prescriptionActe/charger-fichier. Not from CNSS.</summary>
    public List<ActeFileUpload> Uploads { get; set; } = [];
}

public class FseDentalActDetail
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("faces")]
    public List<string>? Faces { get; set; }
}

// ── EP creation ──────────────────────────────────────────────────────────────

/// <summary>Typed response from POST /ep/ep/demander</summary>
public class EpCreationResponse
{
    [JsonPropertyName("code")]
    public string? StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("epResponse")]
    public EpCreationDetail? Details { get; set; }
}

public class EpCreationDetail
{
    [JsonPropertyName("numeroFSE")]
    public string? FseNumber { get; set; }

    /// <summary>List of EP numbers generated by CNSS.</summary>
    [JsonPropertyName("numerosEP")]
    public List<string>? EpNumbers { get; set; }
}
