using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CnssProxy.Models;

public class RequestLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string ClientId { get; set; } = "";
    public string Username { get; set; } = "";

    public string Method { get; set; } = "";        // GET / POST
    public string CnssPath { get; set; } = "";      // e.g. "prescription/fse/creerFse"
    public string GatewayRoute { get; set; } = "";  // e.g. "/api/fse/dr-ali/create"

    /// <summary>Raw request body stored as BSON for queryability.</summary>
    public BsonDocument? RequestBody { get; set; }

    /// <summary>Raw CNSS response body stored as BSON.</summary>
    public BsonDocument? ResponseBody { get; set; }

    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public bool FromCache { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
