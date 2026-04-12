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
    }
}
