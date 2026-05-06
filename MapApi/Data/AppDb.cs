using MapApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Data;

public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Poi> Pois => Set<Poi>();
    public DbSet<PoiLanguage> PoiLanguages => Set<PoiLanguage>();
    public DbSet<PoiMedia> PoiMedia => Set<PoiMedia>();
    public DbSet<Users> Users => Set<Users>();
    public DbSet<HistoryPoi> HistoryPoi => Set<HistoryPoi>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourPoi> TourPois => Set<TourPoi>();
    public DbSet<PoiTicket> PoiTickets => Set<PoiTicket>();
    public DbSet<AnalyticsVisit> AnalyticsVisits => Set<AnalyticsVisit>();
    public DbSet<AnalyticsRoute> AnalyticsRoutes => Set<AnalyticsRoute>();
    public DbSet<AnalyticsListenDuration> AnalyticsListenDurations => Set<AnalyticsListenDuration>();
    public DbSet<GuestDevice> GuestDevices => Set<GuestDevice>();
    public DbSet<PoiImage> PoiImages => Set<PoiImage>();
    public DbSet<DailyUsageTracking> DailyUsageTrackings => Set<DailyUsageTracking>();
    public DbSet<SupportedLanguage> SupportedLanguages => Set<SupportedLanguage>();
    public DbSet<AppDownloadSource> AppDownloadSources => Set<AppDownloadSource>();
    public DbSet<AnalyticsAppDownloadScan> AnalyticsAppDownloadScans => Set<AnalyticsAppDownloadScan>();
    public DbSet<WebNarrationUsage> WebNarrationUsages => Set<WebNarrationUsage>();

    // Freemium / Paywall
    public DbSet<Area>               Areas               => Set<Area>();
    public DbSet<AreaPoi>            AreaPois            => Set<AreaPoi>();
    public DbSet<Product>            Products            => Set<Product>();
    public DbSet<ProductArea>        ProductAreas        => Set<ProductArea>();
    public DbSet<UserEntitlement>    UserEntitlements    => Set<UserEntitlement>();
    public DbSet<PurchaseTransaction> PurchaseTransactions => Set<PurchaseTransaction>();
    public DbSet<UsageEvent>         UsageEvents         => Set<UsageEvent>();

    // Keyless SP result types
    public DbSet<AccessCheckRow>     AccessCheckRows     => Set<AccessCheckRow>();
    public DbSet<UsageStatusRow>     UsageStatusRows     => Set<UsageStatusRow>();
    public DbSet<UserEntitlementRow> UserEntitlementRows => Set<UserEntitlementRow>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Poi>(e =>
        {
            e.ToTable("Pois");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.RadiusMeters).HasDefaultValue(120);
            e.Property(x => x.CooldownSeconds).HasDefaultValue(30);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.PriorityLevel).HasDefaultValue(0);
            e.Property(x => x.ConflictPolicy).HasMaxLength(30).HasDefaultValue("PRIORITY_ONLY");
            e.Property(x => x.AllowQueueWhenConflict).HasDefaultValue(false);
            e.Property(x => x.AudioSourceMode).HasMaxLength(20).HasDefaultValue("AUDIO_FIRST");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => new { x.IsActive, x.Name })
             .HasDatabaseName("IX_Pois_Active");
        });

        b.Entity<PoiLanguage>(e =>
        {
            e.ToTable("PoiLanguage");
            e.HasKey(x => x.IdLang);
            e.Property(x => x.IdPoi).IsRequired();
            e.Property(x => x.LanguageTag).HasMaxLength(10).IsRequired();
            e.Property(x => x.TextToSpeech).HasMaxLength(4000);
            e.Property(x => x.ProPodcastScript).HasColumnType("nvarchar(max)");
            e.Property(x => x.ProAudioUrl).HasMaxLength(1000);
            e.HasOne<Poi>().WithMany()
             .HasForeignKey(x => x.IdPoi).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.IdPoi, x.LanguageTag })
             .IsUnique().HasDatabaseName("UX_PoiLanguage_Key");
        });

        b.Entity<PoiMedia>(e =>
        {
            e.ToTable("PoiMedia");
            e.HasKey(x => x.Idm);
            e.Property(x => x.IdPoi).IsRequired();
            e.Property(x => x.Image).HasMaxLength(1000);
            e.Property(x => x.MapLink).HasMaxLength(1000);
            e.HasOne<Poi>().WithMany()
             .HasForeignKey(x => x.IdPoi).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.IdPoi).HasDatabaseName("IX_PoiMedia_Poi");
        });

        b.Entity<Users>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasDefaultValueSql("NEWID()");
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.Mail).HasMaxLength(200).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
            e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.AvatarUrl).HasMaxLength(1000).HasDefaultValue("GpsGeoFenceApp/Application/Resources/Image/default-avatar.png");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.PlanType).HasMaxLength(20).HasDefaultValue("FREE");
            e.Property(x => x.ProExpiryDate).IsRequired(false);
            e.HasIndex(x => x.Username).IsUnique().HasDatabaseName("UX_Users_Username");
            e.HasIndex(x => x.Mail).IsUnique().HasDatabaseName("UX_Users_Mail");
        });

        b.Entity<HistoryPoi>(e =>
        {
            e.ToTable("HistoryPoi");
            e.HasKey(x => x.Id);
            e.Property(x => x.IdPoi).IsRequired();
            e.Property(x => x.PoiName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Quantity).HasDefaultValue(1);
            e.Property(x => x.LastVisitedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne<Poi>().WithMany()
             .HasForeignKey(x => x.IdPoi).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Users>().WithMany()
             .HasForeignKey(x => x.IdUser).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.IdPoi, x.LastVisitedAt })
             .HasDatabaseName("IX_History_Poi");
            e.HasIndex(x => new { x.IdUser, x.LastVisitedAt })
             .HasDatabaseName("IX_History_User");
        });

        b.Entity<Tour>(e =>
        {
            e.ToTable("Tours");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasMany(x => x.TourPois).WithOne().HasForeignKey(x => x.TourId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TourPoi>(e =>
        {
            e.ToTable("TourPois");
            e.HasKey(x => new { x.TourId, x.PoiId });
            e.Property(x => x.SortOrder).HasDefaultValue(0);
            e.HasOne<Poi>().WithMany().HasForeignKey(x => x.PoiId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PoiTicket>(e =>
        {
            e.ToTable("PoiTickets");
            e.HasKey(x => x.TicketCode);
            e.Property(x => x.MaxUses).HasDefaultValue(5);
            e.Property(x => x.CurrentUses).HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });
        b.Entity<AnalyticsVisit>(e =>      { e.ToTable("Analytics_Visit"); e.HasKey(x => x.Id); });
        b.Entity<AnalyticsRoute>(e =>      { e.ToTable("Analytics_Route"); e.HasKey(x => x.Id); });
        b.Entity<AnalyticsListenDuration>(e => { e.ToTable("Analytics_ListenDuration"); e.HasKey(x => x.Id); });

        b.Entity<PoiImage>(e =>
        {
            e.ToTable("PoiImages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.ImageUrl).HasMaxLength(1000).IsRequired();
            e.Property(x => x.SortOrder).HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne<Poi>().WithMany()
             .HasForeignKey(x => x.IdPoi).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DailyUsageTracking>(e =>
        {
            e.ToTable("DailyUsageTracking");
            e.HasKey(x => new { x.EntityId, x.ActionType });
            e.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
            e.Property(x => x.ActionType).HasMaxLength(20).IsRequired();
            e.Property(x => x.UsedCount).HasDefaultValue(0);
            e.Property(x => x.LastResetAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        b.Entity<SupportedLanguage>(e =>
        {
            e.ToTable("SupportedLanguages");
            e.HasKey(x => x.LanguageTag);
            e.Property(x => x.LanguageTag).HasMaxLength(10).IsRequired();
            e.Property(x => x.LanguageName).HasMaxLength(50).IsRequired();
            e.Property(x => x.IsPremium).HasDefaultValue(false);
            e.Property(x => x.IsActive).HasDefaultValue(true);
        });

        b.Entity<GuestDevice>(e =>
        {
            e.ToTable("GuestDevices");
            e.HasKey(x => x.DeviceId);
            e.Property(x => x.DeviceId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Platform).HasMaxLength(20);
            e.Property(x => x.AppVersion).HasMaxLength(20);
            e.Property(x => x.FirstSeenAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.LastActiveAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => x.LastActiveAt)
             .IsDescending()
             .HasDatabaseName("IX_GuestDevices_LastActive");
        });

        b.Entity<AppDownloadSource>(e =>
        {
            e.ToTable("AppDownloadSources");
            e.HasKey(x => x.SourceId);
            e.Property(x => x.SourceId).ValueGeneratedOnAdd();
            e.Property(x => x.LocationName).HasMaxLength(200).IsRequired();
            e.Property(x => x.CampaignCode).HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        b.Entity<AnalyticsAppDownloadScan>(e =>
        {
            e.ToTable("Analytics_AppDownloadScans");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.SourceId).IsRequired();
            e.Property(x => x.Platform).HasMaxLength(50).IsRequired();
            e.Property(x => x.ScannedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            e.HasOne<AppDownloadSource>().WithMany()
             .HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.SourceId).HasDatabaseName("IX_Analytics_AppDownloadScans_SourceId");
        });

        b.Entity<WebNarrationUsage>(e =>
        {
            e.ToTable("WebNarrationUsage");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.PoiId).IsRequired();
            e.Property(x => x.DeviceKey).HasMaxLength(200).IsRequired();
            e.Property(x => x.PlayCount).HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne<Poi>().WithMany()
             .HasForeignKey(x => x.PoiId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PoiId, x.DeviceKey })
             .IsUnique().HasDatabaseName("UX_WebNarrationUsage_Key");
        });

        // ── Freemium / Paywall ─────────────────────────────────────────────

        b.Entity<Area>(e =>
        {
            e.ToTable("Areas");
            e.HasKey(x => x.AreaId);
            e.Property(x => x.AreaId).ValueGeneratedOnAdd();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.Province).HasMaxLength(100);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => x.Code).IsUnique();
        });

        b.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.HasKey(x => x.ProductId);
            e.Property(x => x.ProductId).ValueGeneratedOnAdd();
            e.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            e.Property(x => x.ProductType).HasMaxLength(30).IsRequired();
            e.Property(x => x.Price).HasColumnType("decimal(18,2)");
            e.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("VND");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => x.ProductCode).IsUnique();
        });

        b.Entity<AreaPoi>(e =>
        {
            e.ToTable("AreaPois");
            e.HasKey(x => new { x.AreaId, x.PoiId });
            e.Property(x => x.SortOrder).HasDefaultValue(0);
            e.Property(x => x.IsPrimaryArea).HasDefaultValue(false);
            e.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Poi>().WithMany().HasForeignKey(x => x.PoiId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProductArea>(e =>
        {
            e.ToTable("ProductAreas");
            e.HasKey(x => new { x.ProductId, x.AreaId });
        });

        b.Entity<UserEntitlement>(e =>
        {
            e.ToTable("UserEntitlements");
            e.HasKey(x => x.EntitlementId);
            e.Property(x => x.EntitlementId).ValueGeneratedOnAdd();
            e.Property(x => x.EntitlementType).HasMaxLength(30).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("ACTIVE");
            e.Property(x => x.StartsAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne<Users>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.Status, x.ExpiresAt })
             .HasDatabaseName("IX_UserEntitlements_User_Status_Expiry");
        });

        b.Entity<PurchaseTransaction>(e =>
        {
            e.ToTable("PurchaseTransactions");
            e.HasKey(x => x.TransactionId);
            e.Property(x => x.TransactionId).ValueGeneratedOnAdd();
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("VND");
            e.Property(x => x.PaymentProvider).HasMaxLength(50);
            e.Property(x => x.PaymentRef).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("PAID");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasOne<Users>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<UsageEvent>(e =>
        {
            e.ToTable("UsageEvents");
            e.HasKey(x => x.UsageEventId);
            e.Property(x => x.UsageEventId).ValueGeneratedOnAdd();
            e.Property(x => x.SubjectType).HasMaxLength(20).IsRequired();
            e.Property(x => x.SubjectId).HasMaxLength(100).IsRequired();
            e.Property(x => x.ActionType).HasMaxLength(30).IsRequired();
            e.Property(x => x.OccurredAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.HasIndex(x => new { x.SubjectType, x.SubjectId, x.ActionType, x.OccurredAt })
             .HasDatabaseName("IX_UsageEvents_Subject_Action_Time");
        });

        // Keyless SP result types – không map table, chỉ dùng cho FromSqlRaw
        b.Entity<AccessCheckRow>().HasNoKey().ToView(null);
        b.Entity<UsageStatusRow>().HasNoKey().ToView(null);
        b.Entity<UserEntitlementRow>().HasNoKey().ToView(null);
    }
}
