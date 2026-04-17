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
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Poi>(e =>
        {
            e.ToTable("Pois");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();          // IDENTITY(1,1)
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.RadiusMeters).HasDefaultValue(120);
            e.Property(x => x.CooldownSeconds).HasDefaultValue(30);
            e.Property(x => x.IsActive).HasDefaultValue(true);
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
            e.Property(x => x.Audio).HasMaxLength(1000);
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
            e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
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
    }
}
