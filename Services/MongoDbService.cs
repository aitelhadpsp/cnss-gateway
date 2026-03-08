using CnssProxy.Helpers;
using CnssProxy.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CnssProxy.Services;

public class MongoDbService
{
    private readonly IMongoCollection<CnssUser> _users;
    private readonly IMongoCollection<RequestLog> _logs;
    private readonly IMongoCollection<SubmissionRecord> _submissions;

    public MongoDbService(IConfiguration config)
    {
        var connectionString = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
        var databaseName = config["MongoDB:Database"] ?? "cnss_proxy";

        var client = new MongoClient(connectionString);
        var db = client.GetDatabase(databaseName);
        _users = db.GetCollection<CnssUser>("users");
        _logs = db.GetCollection<RequestLog>("request_logs");
        _submissions = db.GetCollection<SubmissionRecord>("submissions");

        var indexKeys = Builders<CnssUser>
            .IndexKeys.Ascending(u => u.ClientId)
            .Ascending(u => u.Username);
        _users.Indexes.CreateOne(
            new CreateIndexModel<CnssUser>(indexKeys, new CreateIndexOptions { Unique = true })
        );

        _logs.Indexes.CreateOne(
            new CreateIndexModel<RequestLog>(
                Builders<RequestLog>
                    .IndexKeys.Ascending(l => l.ClientId)
                    .Ascending(l => l.Username)
                    .Descending(l => l.CreatedAt)
            )
        );

        _submissions.Indexes.CreateOne(
            new CreateIndexModel<SubmissionRecord>(
                Builders<SubmissionRecord>
                    .IndexKeys.Ascending(s => s.ClientId)
                    .Ascending(s => s.Username)
                    .Ascending(s => s.Type)
                    .Descending(s => s.SubmittedAt)
            )
        );
    }

    public Task<CnssUser?> GetUserAsync(string clientId, string username) =>
        _users.Find(u => u.ClientId == clientId && u.Username == username).FirstOrDefaultAsync()!;

    public async Task UpsertAsync(CnssUser user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        var filter = Builders<CnssUser>.Filter.Where(u =>
            u.ClientId == user.ClientId && u.Username == user.Username
        );
        await _users.ReplaceOneAsync(filter, user, new ReplaceOptions { IsUpsert = true });
    }

    public Task InsertLogAsync(RequestLog log) => _logs.InsertOneAsync(log);

    /// <summary>
    /// Pushes an ActeFileUpload into the matching act's Uploads list within the submission.
    /// Uses an array filter to target the act by TechnicalId.
    /// </summary>
    public Task AddActeUploadAsync(
        string clientId,
        string username,
        string fseNumber,
        int acteId,
        ActeFileUpload upload
    )
    {
        var filter = Builders<SubmissionRecord>.Filter.Where(s =>
            s.ClientId == clientId && s.Username == username && s.SubmissionNumber == fseNumber
        );

        var update = Builders<SubmissionRecord>.Update.Push("PerformedActs.$[act].Uploads", upload);

        var arrayFilter = new BsonDocumentArrayFilterDefinition<SubmissionRecord>(
            new BsonDocument("act.TechnicalId", acteId)
        );

        return _submissions.UpdateOneAsync(
            filter,
            update,
            new UpdateOptions { ArrayFilters = [arrayFilter] }
        );
    }

    public Task InsertSubmissionAsync(SubmissionRecord record) =>
        _submissions.InsertOneAsync(record);

    public Task<List<SubmissionRecord>> GetSubmissionsAsync(
        string clientId,
        string username,
        string? type = null,
        string? appPatientId = null,
        int page = 1,
        int limit = 20
    )
    {
        var filter = Builders<SubmissionRecord>.Filter.Where(s =>
            s.ClientId == clientId && s.Username == username
        );

        if (type != null)
            filter &= Builders<SubmissionRecord>.Filter.Eq(s => s.Type, type.ToUpper());

        if (appPatientId != null)
            filter &= Builders<SubmissionRecord>.Filter.Eq(s => s.AppPatientId, appPatientId);

        return _submissions
            .Find(filter)
            .SortByDescending(s => s.SubmittedAt)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<SubmissionTimeSeriesStats> GetTimeSeriesStatsAsync(
        string clientId,
        string username,
        DateTime startDate,
        DateTime endDate
    )
    {
        var helper = new DateGroupingHelper(startDate, endDate);

        var filter = Builders<SubmissionRecord>.Filter.Where(s =>
            s.ClientId == clientId
            && s.Username == username
            && s.SubmittedAt >= startDate.Date
            && s.SubmittedAt <= endDate.Date.AddDays(1).AddTicks(-1)
        );

        var records = await _submissions.Find(filter).ToListAsync();

        var fseGrouped = records
            .Where(r => r.Type == "FSE")
            .GroupBy(r => helper.GetKey(r.SubmittedAt))
            .ToDictionary(
                g => g.Key,
                g => new TimeSeriesDataPoint
                {
                    Count = g.Count(),
                    Amount = g.SelectMany(r => r.Prestations)
                        .Sum((Prestation p) => p.UnitPrice * p.Count),
                }
            );

        var epGrouped = records
            .Where(r => r.Type == "EP")
            .GroupBy(r => helper.GetKey(r.SubmittedAt))
            .ToDictionary(
                g => g.Key,
                g => new TimeSeriesDataPoint { Count = g.Count(), Amount = 0 }
            );

        var zero = new TimeSeriesDataPoint { Count = 0, Amount = 0 };

        return new SubmissionTimeSeriesStats
        {
            StartDate = startDate,
            EndDate = endDate,
            Fse = new TimeSeriesResult
            {
                GroupingType = helper.Grouping.ToString(),
                Data = helper.FillGaps(fseGrouped, zero),
            },
            Ep = new TimeSeriesResult
            {
                GroupingType = helper.Grouping.ToString(),
                Data = helper.FillGaps(epGrouped, zero),
            },
        };
    }

    public async Task<List<PatientStats>> GetStatsAsync(
        string clientId,
        string username,
        string? appPatientId = null
    )
    {
        var filter = Builders<SubmissionRecord>.Filter.Where(s =>
            s.ClientId == clientId && s.Username == username
        );
        if (appPatientId != null)
            filter &= Builders<SubmissionRecord>.Filter.Eq(s => s.AppPatientId, appPatientId);

        var records = await _submissions.Find(filter).ToListAsync();

        return records
            .GroupBy(r => r.AppPatientId)
            .Select(g =>
            {
                var first = g.First();
                return new PatientStats
                {
                    AppPatientId = g.Key,
                    PatientLastName = first.PatientLastName,
                    PatientFirstName = first.PatientFirstName,
                    PatientRegistrationNumber = first.PatientRegistrationNumber,
                    FseCount = g.Count(r => r.Type == "FSE"),
                    EpCount = g.Count(r => r.Type == "EP"),
                    TotalAmount = g.Sum(r => r.Prestations.Sum(p => p.UnitPrice * p.Count)),
                };
            })
            .ToList();
    }

    public Task<SubmissionRecord?> GetSubmissionByNumberAsync(
        string clientId,
        string username,
        string submissionNumber
    ) =>
        _submissions
            .Find(s =>
                s.ClientId == clientId
                && s.Username == username
                && s.SubmissionNumber == submissionNumber
            )
            .FirstOrDefaultAsync()!;

    public Task UpdateTokensAsync(
        string clientId,
        string username,
        string? encAccessToken,
        string? encRefreshToken,
        DateTime? expiresAt,
        bool otpVerified,
        bool isConfigured
    )
    {
        var filter = Builders<CnssUser>.Filter.Where(u =>
            u.ClientId == clientId && u.Username == username
        );
        var update = Builders<CnssUser>
            .Update.Set(u => u.EncryptedAccessToken, encAccessToken)
            .Set(u => u.EncryptedRefreshToken, encRefreshToken)
            .Set(u => u.TokenExpiresAt, expiresAt)
            .Set(u => u.OtpVerified, otpVerified)
            .Set(u => u.IsConfigured, isConfigured)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);
        return _users.UpdateOneAsync(filter, update);
    }
}
