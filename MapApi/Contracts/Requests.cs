namespace MapApi.Contracts;

public sealed record LoginRequest(string Identifier, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AnalyticsVisitRequest(Guid SessionId, int PoiId, string Action);
public sealed record AnalyticsRouteRequest(Guid SessionId, double Latitude, double Longitude);
public sealed record AnalyticsListenRequest(Guid SessionId, int PoiId, int DurationSeconds);
public sealed record AdminUserUpdateRequest(string? PhoneNumber, string? PlanType, DateTime? ProExpiryDate);
public sealed record UsageCheckRequest(string EntityId, string ActionType);

// Freemium / Access Control
public sealed record AccessCheckPoiRequest(int PoiId, string? DeviceId);
public sealed record ConsumePoisListenRequest(int PoiId, int? AreaId, string? DeviceId, string? MetadataJson);
public sealed record PurchaseRequest(string ProductCode, string? PaymentMethod);

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
    public string? ImageUrl { get; set; }
}

public sealed record PoiAreaAssignRequest(int AreaId, int SortOrder = 0, bool IsPrimaryArea = false);

public sealed record PoiImageAddRequest(string ImageUrl, int? SortOrder);

public sealed record PaymentCallbackRequest(
    string PaymentRef, string Status, string Provider, decimal Amount, string Currency);

public sealed record AdminProductUpdateRequest(
    string? ProductName, decimal? Price, bool? IsActive, int? DurationHours);
