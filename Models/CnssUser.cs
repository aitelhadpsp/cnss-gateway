using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CnssProxy.Models;

public class CnssUser
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>The Keycloak clientId of the app that owns this user.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>The practitioner's username / identifier within the client app.</summary>
    public string Username { get; set; } = "";

    // CNSS credentials — all encrypted at rest
    public string EncryptedPractitionerId { get; set; } = "";
    public string EncryptedPassword { get; set; } = "";

    // CNSS session tokens — encrypted
    public string? EncryptedAccessToken { get; set; }
    public string? EncryptedRefreshToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>True after OTP has been successfully verified at least once.</summary>
    public bool OtpVerified { get; set; } = false;

    /// <summary>
    /// True when the user has valid credentials and has completed OTP.
    /// Only when true are FSE calls allowed.
    /// </summary>
    public bool IsConfigured { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
