namespace MapApi.Contracts;

public sealed record LoginRequest(string Identifier, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AnalyticsVisitRequest(Guid SessionId, int PoiId, string Action);
public sealed record AnalyticsRouteRequest(Guid SessionId, double Latitude, double Longitude);
public sealed record AnalyticsListenRequest(Guid SessionId, int PoiId, int DurationSeconds);
public sealed record AdminUserUpdateRequest(string? PhoneNumber, string? PlanType, DateTime? ProExpiryDate);
public sealed record UsageCheckRequest(string EntityId, string ActionType);

public sealed class HistoryRequest
{
    public int PoiId { get; set; }
    public Guid UserId { get; set; }
    public int? DurationSeconds { get; set; }
}

public sealed class ProfileHistoryUpsertRequest
{
    public int PoiId { get; set; }
    public int? DurationSeconds { get; set; }
}

public sealed class PoiUpdateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? RadiusMeters { get; set; }
    public int? CooldownSeconds { get; set; }
    public int? PriorityLevel { get; set; }
}
