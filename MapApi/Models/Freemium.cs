using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Models;

// ── Bảng dữ liệu ─────────────────────────────────────────────────────────────

public sealed class Area
{
    public int     AreaId      { get; set; }
    public string  Code        { get; set; } = "";
    public string  Name        { get; set; } = "";
    public string? Description { get; set; }
    public string? City        { get; set; }
    public string? Province    { get; set; }
    public bool    IsActive    { get; set; } = true;
    public DateTime CreatedAt  { get; set; }
    public DateTime UpdatedAt  { get; set; }
}

public sealed class Product
{
    public long    ProductId        { get; set; }
    public string  ProductCode      { get; set; } = "";
    public string  ProductName      { get; set; } = "";
    public string  ProductType      { get; set; } = "";   // PRO | AREA_PACK
    public decimal Price            { get; set; }
    public string  Currency         { get; set; } = "VND";
    public bool    UnlockNarration  { get; set; }
    public bool    UnlockLanguages  { get; set; }
    public bool    UnlockQr         { get; set; }
    public bool    UnlockOffline    { get; set; }
    public int?    DurationHours    { get; set; }
    public bool    IsActive         { get; set; } = true;
    public DateTime CreatedAt       { get; set; }
}

public sealed class ProductArea
{
    public long ProductId { get; set; }
    public int  AreaId    { get; set; }
}

public sealed class AreaPoi
{
    public int  AreaId        { get; set; }
    public int  PoiId         { get; set; }
    public int  SortOrder     { get; set; }
    public bool IsPrimaryArea { get; set; }
}

public sealed class UserEntitlement
{
    public long      EntitlementId   { get; set; }
    public Guid      UserId          { get; set; }
    public long      ProductId       { get; set; }
    public string    EntitlementType { get; set; } = "";   // PRO | AREA_PACK
    public DateTime  StartsAt        { get; set; }
    public DateTime? ExpiresAt       { get; set; }
    public string    Status          { get; set; } = "ACTIVE";
    public DateTime  CreatedAt       { get; set; }
}

public sealed class PurchaseTransaction
{
    public long      TransactionId    { get; set; }
    public Guid      UserId           { get; set; }
    public long      ProductId        { get; set; }
    public decimal   Amount           { get; set; }
    public string    Currency         { get; set; } = "VND";
    public string?   PaymentProvider  { get; set; }
    public string?   PaymentRef       { get; set; }
    public string    Status           { get; set; } = "PAID";
    public DateTime? PaidAt           { get; set; }
    public DateTime  CreatedAt        { get; set; }
}

public sealed class UsageEvent
{
    public long      UsageEventId  { get; set; }
    public string    SubjectType   { get; set; } = "";   // USER | GUEST_DEVICE
    public string    SubjectId     { get; set; } = "";
    public string    ActionType    { get; set; } = "";   // POI_LISTEN
    public int?      PoiId         { get; set; }
    public int?      AreaId        { get; set; }
    public DateTime  OccurredAt    { get; set; }
    public string?   MetadataJson  { get; set; }
}

// ── Keyless types – kết quả stored procedure ─────────────────────────────────

[Keyless]
public sealed class AccessCheckRow
{
    public bool   AccessGranted      { get; set; }
    public string AccessReason       { get; set; } = "";
    public int    RemainingFreeUses  { get; set; }
    public int?   MatchedAreaId      { get; set; }
    public int    PoiId              { get; set; }
}

[Keyless]
public sealed class UsageStatusRow
{
    public string SubjectType         { get; set; } = "";
    public string SubjectId           { get; set; } = "";
    public int    UsedLast24h         { get; set; }
    public int?   RemainingFreeUses   { get; set; }
    public bool   HasActivePro        { get; set; }
    public bool   IsFreeLimitExceeded { get; set; }
}

[Keyless]
public sealed class UserEntitlementRow
{
    public long      EntitlementId   { get; set; }
    public long      ProductId       { get; set; }
    public string    ProductCode     { get; set; } = "";
    public string    ProductName     { get; set; } = "";
    public string    ProductType     { get; set; } = "";
    public string    EntitlementType { get; set; } = "";
    public DateTime  StartsAt        { get; set; }
    public DateTime? ExpiresAt       { get; set; }
    public string    Status          { get; set; } = "";
    public bool      IsValid         { get; set; }
    public string?   AreaIds         { get; set; }
    public string?   AreaCodes       { get; set; }
}
