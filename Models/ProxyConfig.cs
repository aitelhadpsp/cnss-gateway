using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CnssProxy.Models;

[BsonIgnoreExtraElements]
public class ProxyConfig
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string? UpstreamBase { get; set; }
    public string? CnssClientId { get; set; }
    public string? CnssSecretKey { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
