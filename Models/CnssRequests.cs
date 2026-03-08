namespace CnssProxy.Models;

// ── FSE / Verify ────────────────────────────────────────────────────────────

/// <summary>
/// Body for POST /fse/{username}/verify and /fse/{username}/create.
/// All properties use English names. ToCnss() maps them to CNSS field names.
/// </summary>
public class FseRequest
{
    public FsePatient? Patient { get; set; }
    public bool IsAccident { get; set; }

    /// <summary>A=Other, 0=No third party, 1=Work, 3=School, 4=Sport, 6=Road, 8=Foreign body</summary>
    public string? AccidentType { get; set; }
    public string DoctorId { get; set; } = "";
    public string? FacilityId { get; set; }
    public DiagnosisInfo? Diagnosis { get; set; }
    public List<VitalSign>? VitalSigns { get; set; }
    public string? Comment { get; set; }

    /// <summary>Format: dd/MM/yyyy HH:mm:ss</summary>
    public string VisitDate { get; set; } = "";
    public List<Prescription>? Prescriptions { get; set; }
    public List<ReferredAct>? ReferredActs { get; set; }
    public List<PerformedAct> PerformedActs { get; set; } = [];
    public List<MedicalDevice>? MedicalDevices { get; set; }

    public object ToCnss() =>
        new
        {
            patient = Patient?.ToCnss(),
            estAccident = IsAccident,
            typeAccident = AccidentType,
            inpeMedecin = DoctorId,
            inpeEtablissement = FacilityId,
            diagnostic = Diagnosis?.ToCnss(),
            constantes = VitalSigns?.Select(v => v.ToCnss()),
            commentaire = Comment,
            dateVisite = VisitDate,
            ordonnances = Prescriptions?.Select(p => p.ToCnss()),
            actesAdresses = ReferredActs?.Select(a => a.ToCnss()),
            acteRealises = PerformedActs.Select(a => a.ToCnss()),
            dispositifMedicaux = MedicalDevices?.Select(d => d.ToCnss()),
        };
}

public class FsePatient
{
    public string? RegistrationNumber { get; set; }
    public string? IndividualNumber { get; set; }
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";

    /// <summary>Format: dd/MM/yyyy</summary>
    public string? BirthDate { get; set; }

    /// <summary>H = Male, F = Female</summary>
    public string? Gender { get; set; }

    public object ToCnss() =>
        new
        {
            numeroImmatriculation = RegistrationNumber,
            numeroIndividu = IndividualNumber,
            nom = LastName,
            prenom = FirstName,
            dateNaissance = BirthDate,
            genre = Gender,
        };
}

public class DiagnosisInfo
{
    public List<Condition> Conditions { get; set; } = [];
    public string? Comment { get; set; }

    public object ToCnss() =>
        new { pathologies = Conditions.Select(c => c.ToCnss()), diagnostic = Comment };
}

public class Condition
{
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public bool IsPreliminary { get; set; }

    public object ToCnss() =>
        new
        {
            codePathologie = Code,
            description = Description,
            provisoire = IsPreliminary,
        };
}

public class VitalSign
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";

    public object ToCnss() => new { nom = Name, valeur = Value };
}

public class Prescription
{
    /// <summary>Format: dd/MM/yyyy HH:mm:ss</summary>
    public string Date { get; set; } = "";
    public List<Medication> Medications { get; set; } = [];

    public object ToCnss() =>
        new { dateOrdonnance = Date, listMedicament = Medications.Select(m => m.ToCnss()) };
}

public class Medication
{
    public string? Code { get; set; }
    public string Name { get; set; } = "";
    public int DosesPerUnit { get; set; }

    /// <summary>J = Day, S = Week, M = Month</summary>
    public string DoseUnit { get; set; } = "";
    public string Dosage { get; set; } = "";
    public string? Form { get; set; }
    public string DosageUnit { get; set; } = "";
    public int Duration { get; set; }

    /// <summary>J = Day, S = Week, M = Month</summary>
    public string DurationUnit { get; set; } = "";
    public string? Comment { get; set; }
    public string? Reason { get; set; }
    public bool IsContinuous { get; set; }
    public bool? IsFromReferential { get; set; }

    public object ToCnss() =>
        new
        {
            code = Code,
            libelle = Name,
            nombrePrise = DosesPerUnit,
            uniteNombrePrise = DoseUnit,
            dosage = Dosage,
            forme = Form,
            uniteDosage = DosageUnit,
            dureeTraitement = Duration,
            uniteDureeTraitement = DurationUnit,
            commentaire = Comment,
            motif = Reason,
            traitementContinu = IsContinuous,
            isReferentiel = IsFromReferential,
        };
}

public class ReferredAct
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Location { get; set; }

    /// <summary>LAB = Biology, RAD = Radiology, PAR = Paramedical</summary>
    public string Category { get; set; } = "";
    public bool RequiresPriorApproval { get; set; }
    public int Count { get; set; }
    public string? Comment { get; set; }
    public string? Reason { get; set; }
    public List<DentalCode>? DentalCodes { get; set; }

    public object ToCnss() =>
        new
        {
            code = Code,
            libelle = Name,
            localisation = Location,
            categorieActe = Category,
            isEP = RequiresPriorApproval,
            nombreActes = Count,
            commentaire = Comment,
            motif = Reason,
            dentaireCodes = DentalCodes?.Select(d => d.ToCnss()),
        };
}

public class PerformedAct
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Location { get; set; }
    public string Category { get; set; } = "";
    public int Count { get; set; }
    public string? Comment { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>Format: dd/MM/yyyy HH:mm:ss</summary>
    public string PerformedDate { get; set; } = "";
    public string? Reason { get; set; }
    public bool? RequiresPriorApproval { get; set; }

    /// <summary>1 = Year, 2 = Month, 3 = Semester, 4 = Quarter — dental only</summary>
    public string? PeriodType { get; set; }
    public int? PeriodCount { get; set; }
    public bool? XRayBefore { get; set; }
    public bool? XRayAfter { get; set; }
    public List<DentalCode>? DentalCodes { get; set; }

    public object ToCnss() =>
        new
        {
            code = Code,
            libelle = Name,
            localisation = Location,
            categorieActe = Category,
            nombreActes = Count,
            commentaire = Comment,
            prixUnitaire = UnitPrice,
            dateRealisation = PerformedDate,
            motif = Reason,
            isEP = RequiresPriorApproval,
            typePeriode = PeriodType,
            nombrePeriode = PeriodCount,
            radioAvant = XRayBefore,
            radioApres = XRayAfter,
            dentaireCodes = DentalCodes?.Select(d => d.ToCnss()),
        };
}

public class MedicalDevice
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public bool RequiresPriorApproval { get; set; }
    public string? Comment { get; set; }
    public string? Reason { get; set; }

    public object ToCnss() =>
        new
        {
            code = Code,
            libelle = Name,
            nombre = Count,
            isEP = RequiresPriorApproval,
            commentaire = Comment,
            motif = Reason,
        };
}

public class DentalCode
{
    public string Code { get; set; } = "";

    /// <summary>A = Arch, S = Segment, DA = Adult tooth, DE = Child tooth</summary>
    public string Type { get; set; } = "";
    public List<string>? Faces { get; set; }

    public object ToCnss() =>
        new
        {
            code = Code,
            type = Type,
            faces = Faces?.Select(f => new { face = f }),
        };
}

// ── EP ──────────────────────────────────────────────────────────────────────

/// <summary>Body for POST /fse/{username}/ep/create → CNSS: POST /ep/ep/demander</summary>
public class EpRequest
{
    public string? FseNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? IndividualNumber { get; set; }
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string DoctorId { get; set; } = "";
    public string? FacilityId { get; set; }

    /// <summary>Format: dd/MM/yyyy HH:mm:ss</summary>
    public string RequestDate { get; set; } = "";
    public bool IsDental { get; set; }
    public List<EpItem> Items { get; set; } = [];

    public object ToCnss() =>
        new
        {
            numeroFSE = FseNumber,
            numeroImmatriculation = RegistrationNumber,
            numeroIndividu = IndividualNumber,
            nom = LastName,
            prenom = FirstName,
            inpeMedecin = DoctorId,
            inpeEtablissement = FacilityId,
            dateDemande = RequestDate,
            dentaire = IsDental,
            listEP = Items.Select(i => i.ToCnss()),
        };
}

public class EpItem
{
    public string Code { get; set; } = "";
    public string? Type { get; set; }
    public string? Comment { get; set; }

    /// <summary>Format: dd/MM/yyyy HH:mm:ss</summary>
    public string? CareDate { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Location { get; set; }

    /// <summary>1 = Year, 2 = Month, 3 = Semester, 4 = Quarter — dental only</summary>
    public string? PeriodType { get; set; }
    public int? PeriodCount { get; set; }
    public List<DentalCode>? DentalCodes { get; set; }

    public object ToCnss() =>
        new
        {
            code = Code,
            type = Type,
            commentaire = Comment,
            dateSoin = CareDate,
            quantite = Quantity,
            montant = Amount,
            localisation = Location,
            typePeriode = PeriodType,
            nombrePeriode = PeriodCount,
            dentaireCodes = DentalCodes?.Select(d => d.ToCnss()),
        };
}

// ── Acte execution ──────────────────────────────────────────────────────────

/// <summary>Body for POST /fse/{username}/{fseId}/acte/execute → CNSS: POST /prescription/prescriptionActe/executer</summary>
public class ExecuteActRequest
{
    public string FseNumber { get; set; } = "";
    public int PrescriptionId { get; set; }
    public string ActCode { get; set; } = "";
    public int Count { get; set; }
    public decimal Price { get; set; }

    /// <summary>Required for clinics/facilities</summary>
    public string? PractitionerId { get; set; }
    public string? Comment { get; set; }
    public List<ActSupplement>? Supplements { get; set; }

    public object ToCnss() =>
        new
        {
            numeroFse = FseNumber,
            id = PrescriptionId,
            code = ActCode,
            nombre = Count,
            prix = Price,
            inpe = PractitionerId,
            commentaire = Comment,
            prescriptionActeSupplements = Supplements?.Select(s => s.ToCnss()),
        };
}

public class ActSupplement
{
    public string Code { get; set; } = "";
    public int Count { get; set; }
    public decimal Price { get; set; }

    public object ToCnss() =>
        new
        {
            code = Code,
            nombre = Count,
            prix = Price,
        };
}

// ── Pharmacy dispensation ───────────────────────────────────────────────────

/// <summary>Body for POST /fse/{username}/{fseId}/prescriptions/dispense → CNSS: POST /prescription/prescriptionPharmacie/dispenser</summary>
public class DispensePrescriptionRequest
{
    public string FseNumber { get; set; } = "";
    public List<PharmacyDispense> Prescriptions { get; set; } = [];

    public object ToCnss() =>
        new
        {
            numeroFse = FseNumber,
            prescriptionPharmacies = Prescriptions.Select(p => p.ToCnss()),
        };
}

public class PharmacyDispense
{
    public string PrescriptionId { get; set; } = "";
    public List<DispensedItem> DispensedItems { get; set; } = [];

    public object ToCnss() =>
        new
        {
            prescriptionPharmacieId = PrescriptionId,
            dispensationOfficines = DispensedItems.Select(d => d.ToCnss()),
        };
}

public class DispensedItem
{
    public string MedicationCode { get; set; } = "";
    public int Count { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsPartial { get; set; }
    public bool? IsNonReimbursable { get; set; }

    public object ToCnss() =>
        new
        {
            codeMedicament = MedicationCode,
            nombre = Count,
            prixUnitaire = UnitPrice,
            excecutionPartielle = IsPartial,
            isMNR = IsNonReimbursable,
        };
}

// ── Rejet / Complement responses ────────────────────────────────────────────

/// <summary>Body for POST rejet/reponse and demandeComplement/reponse</summary>
public class FseResponseRequest
{
    public string FseNumber { get; set; } = "";
    public List<ResponseItem> Responses { get; set; } = [];

    public object ToCnss() =>
        new
        {
            numeroFse = FseNumber,
            demandesComplement = Responses.Select(r => new { id = r.Id, reponse = r.Response }),
        };
}

public class ResponseItem
{
    public int Id { get; set; }
    public string Response { get; set; } = "";
}
