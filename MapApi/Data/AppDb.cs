using MapApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Data;

public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Poi> Pois => Set<Poi>();
    public DbSet<PoiNarration> PoiNarrations => Set<PoiNarration>();
    public DbSet<PoiMedia> PoiMedia => Set<PoiMedia>();
    public DbSet<PlaybackLog> PoiPlaybackLog => Set<PlaybackLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
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

            // Option A: default language
            e.Property(x => x.Language).HasMaxLength(10);

            e.HasIndex(x => new { x.IsActive, x.Priority, x.Name })
             .HasDatabaseName("IX_Pois_ActivePriority");
        }); 
        b.Entity<PoiNarration>(e =>
        {
            e.ToTable("PoiNarration");
            e.HasKey(x => x.Id);

            e.Property(x => x.PoiId).HasMaxLength(64).IsRequired();
            e.Property(x => x.LanguageTag).HasMaxLength(10).IsRequired();
            e.Property(x => x.NarrationText).HasMaxLength(4000);

            e.HasOne<Poi>()
             .WithMany()
             .HasForeignKey(x => x.PoiId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.PoiId, x.EventType, x.LanguageTag })
             .IsUnique()
             .HasDatabaseName("UX_PoiNarration_Key");
        }); 
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

            e.HasIndex(x => new { x.PoiId, x.MediaType, x.IsPrimary, x.SortOrder })
             .HasDatabaseName("IX_PoiMedia_PoiType");
        }); 
        b.Entity<PlaybackLog>(e =>
        {
            e.ToTable("PoiPlaybackLog");
            e.HasKey(x => x.Id);
            e.Property(x => x.PoiId).HasMaxLength(64).IsRequired();
            e.Property(x => x.DeviceId).HasMaxLength(64);
        }); 
    }
}