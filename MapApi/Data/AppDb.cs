using Microsoft.EntityFrameworkCore;
using MapApi.Models;

namespace MapApi.Data;

public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Poi> Pois => Set<Poi>();
    public DbSet<PoiNarration> PoiNarrations => Set<PoiNarration>();
    public DbSet<PoiMedia> PoiMedia => Set<PoiMedia>();
    public DbSet<PlaybackLog> PlaybackLogs => Set<PlaybackLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ── Pois (dbo.Pois) ─────────────────────────────────────────────
        b.Entity<Poi>(e =>
        {
            e.ToTable("Pois");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.MapLink).HasMaxLength(1000);
            e.Property(x => x.NarrationText).HasMaxLength(4000);
            e.Property(x => x.AudioUrl).HasMaxLength(1000);
            e.Property(x => x.ImageUrl).HasMaxLength(1000);

            // ❌ BỎ Language vì dbo.Pois không có cột Language [1](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/GpsApp.sql)[2](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/AppDb.cs)
            // e.Ignore(x => x.Language);

            // Index đúng theo script DB: IX_Pois_ActivePriority (IsActive, Priority, Name) [1](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/GpsApp.sql)
            e.HasIndex(x => new { x.IsActive, x.Priority, x.Name })
             .HasDatabaseName("IX_Pois_ActivePriority");
        });

        // ── PoiNarration (dbo.PoiNarration) ────────────────────────────
        b.Entity<PoiNarration>(e =>
        {
            e.ToTable("PoiNarration");
            e.HasKey(x => x.Id);

            e.Property(x => x.PoiId).HasMaxLength(64).IsRequired();
            e.Property(x => x.LanguageTag).HasMaxLength(10).IsRequired();
            e.Property(x => x.NarrationText).HasMaxLength(4000);

            // FK dbo.PoiNarration -> dbo.Pois ON DELETE CASCADE trong script [1](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/GpsApp.sql)
            e.HasOne<Poi>()
             .WithMany()
             .HasForeignKey(x => x.PoiId)
             .OnDelete(DeleteBehavior.Cascade);

            // Unique index UX_PoiNarration_Key (PoiId, EventType, LanguageTag) [1](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/GpsApp.sql)
            e.HasIndex(x => new { x.PoiId, x.EventType, x.LanguageTag })
             .IsUnique()
             .HasDatabaseName("UX_PoiNarration_Key");
        });

        // ── PoiMedia (dbo.PoiMedia) ────────────────────────────────────
        b.Entity<PoiMedia>(e =>
        {
            e.ToTable("PoiMedia");
            e.HasKey(x => x.Id);

            e.Property(x => x.PoiId).HasMaxLength(64).IsRequired();
            e.Property(x => x.LanguageTag).HasMaxLength(10);
            e.Property(x => x.Url).HasMaxLength(1000).IsRequired();
            e.Property(x => x.MimeType).HasMaxLength(50);

            e.HasOne<Poi>()
             .WithMany()
             .HasForeignKey(x => x.PoiId)
             .OnDelete(DeleteBehavior.Cascade);

            // Index IX_PoiMedia_PoiType (PoiId, MediaType, IsPrimary, SortOrder) [1](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/GpsApp.sql)
            e.HasIndex(x => new { x.PoiId, x.MediaType, x.IsPrimary, x.SortOrder })
             .HasDatabaseName("IX_PoiMedia_PoiType");
        });

        // ── PlaybackLog (dbo.PoiPlaybackLog) ───────────────────────────
        b.Entity<PlaybackLog>(e =>
        {
            e.ToTable("PoiPlaybackLog"); // ✅ đúng tên bảng trong script [1](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/GpsApp.sql)
            e.HasKey(x => x.Id);
            e.Property(x => x.PoiId).HasMaxLength(64).IsRequired();
            e.Property(x => x.DeviceId).HasMaxLength(64);
        });
    }
}