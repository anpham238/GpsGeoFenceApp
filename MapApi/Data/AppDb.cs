using Microsoft.EntityFrameworkCore;
using MapApi.Models;

namespace MapApi.Data;

public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Poi> Pois => Set<Poi>();
    public DbSet<PlaybackLog> PlaybackLogs => Set<PlaybackLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ── Poi ──────────────────────────────────────────────────────────
        b.Entity<Poi>(e =>
        {
            e.ToTable("Pois");               // khop ten bang trong SQL
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(100);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Language).HasMaxLength(10);
            e.Property(x => x.AudioUrl).HasMaxLength(500);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            e.Property(x => x.MapLink).HasMaxLength(500);
            // RadiusMeters/NearRadiusMeters la float trong C# -> REAL trong SQL
            e.HasIndex(x => new { x.IsActive, x.Priority })
             .HasDatabaseName("IX_Pois_IsActive_Priority");
        });

        // ── PlaybackLog ───────────────────────────────────────────────────
        b.Entity<PlaybackLog>(e =>
        {
            e.ToTable("PlaybackLogs");       // khop ten bang trong SQL
            e.HasKey(x => x.Id);
            e.Property(x => x.Id)
             .UseIdentityColumn();           // BIGINT IDENTITY(1,1)
            e.Property(x => x.DeviceId).HasMaxLength(100).IsRequired();
            e.Property(x => x.PoiId).HasMaxLength(100).IsRequired();
            e.Property(x => x.TriggerType).HasMaxLength(20).IsRequired();
            e.Property(x => x.IsSuccess).HasDefaultValue(true);

            e.HasOne(x => x.Poi)
             .WithMany()
             .HasForeignKey(x => x.PoiId)
             .OnDelete(DeleteBehavior.NoAction);  // khop SQL ON DELETE NO ACTION

            e.HasIndex(x => x.PoiId)
             .HasDatabaseName("IX_PlaybackLogs_PoiId");
            e.HasIndex(x => x.PlayedAt)
             .HasDatabaseName("IX_PlaybackLogs_PlayedAt");
        });

        // ── Seed 7 diem TPHCM ────────────────────────────────────────────
        // Chi seed qua EF neu KHONG chay SQL file truoc.
        // Neu da chay SQL file -> du lieu da co, EF se bao loi duplicate.
        // Giai phap: dung InsertOrIgnore (xem Program.cs) hoac comment out HasData.
        b.Entity<Poi>().HasData(
            new Poi { Id = "poi-hcm-001", Name = "Trung tâm TP.HCM", Description = "Trái tim kinh tế và văn hoá Việt Nam", Latitude = 10.776889, Longitude = 106.700806, RadiusMeters = 150, NearRadiusMeters = 300, Language = "vi-VN", NarrationText = "Chào mừng đến Thành phố Hồ Chí Minh, trái tim kinh tế năng động của Việt Nam.", MapLink = "https://maps.google.com/?q=10.776889,106.700806", Priority = 1, UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Poi { Id = "poi-benthanh-001", Name = "Chợ Bến Thành", Description = "Biểu tượng lịch sử hơn 100 năm của Sài Gòn", Latitude = 10.772450, Longitude = 106.698060, RadiusMeters = 100, NearRadiusMeters = 200, Language = "vi-VN", NarrationText = "Bạn đang đến Chợ Bến Thành, biểu tượng văn hoá lịch sử trên 100 năm của Sài Gòn.", MapLink = "https://maps.google.com/?q=10.77245,106.69806", Priority = 2, UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Poi { Id = "poi-notredame-001", Name = "Nhà thờ Đức Bà", Description = "Kiến trúc Gothic xây từ 1863", Latitude = 10.779930, Longitude = 106.699330, RadiusMeters = 80, NearRadiusMeters = 160, Language = "vi-VN", NarrationText = "Trước mặt bạn là Nhà thờ Đức Bà Sài Gòn, công trình Gothic ấn tượng xây dựng từ năm 1863.", MapLink = "https://maps.google.com/?q=10.77993,106.69933", Priority = 3, UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Poi { Id = "poi-postoffice-001", Name = "Bưu điện Trung tâm Sài Gòn", Description = "Biệt thự Pháp đẹp nhất TP, xây 1886", Latitude = 10.779760, Longitude = 106.699600, RadiusMeters = 80, NearRadiusMeters = 160, Language = "vi-VN", NarrationText = "Đây là Bưu điện Trung tâm Sài Gòn, biệt thự Pháp tuyệt đẹp được xây dựng năm 1886.", MapLink = "https://maps.google.com/?q=10.77976,106.6996", Priority = 4, UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Poi { Id = "poi-park304-001", Name = "Công viên 30/4", Description = "Công viên lịch sử trước Dinh Độc Lập", Latitude = 10.777600, Longitude = 106.695400, RadiusMeters = 100, NearRadiusMeters = 200, Language = "vi-VN", NarrationText = "Bạn đang ở Công viên 30 tháng 4. Phía sau là Dinh Độc Lập, di tích lịch sử quan trọng.", MapLink = "https://maps.google.com/?q=10.7776,106.6954", Priority = 5, UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Poi { Id = "poi-reunif-001", Name = "Dinh Độc Lập", Description = "Nơi ghi dấu sự kiện lịch sử quan trọng", Latitude = 10.776900, Longitude = 106.695400, RadiusMeters = 100, NearRadiusMeters = 200, Language = "vi-VN", NarrationText = "Đây là Dinh Độc Lập, chứng nhân lịch sử của đất nước. Hiện là bảo tàng tham quan.", MapLink = "https://maps.google.com/?q=10.7769,106.6954", Priority = 6, UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Poi { Id = "poi-ntmk-001", Name = "Công viên NTMK", Description = "Công viên xanh mát giữa lòng thành phố", Latitude = 10.787000, Longitude = 106.700000, RadiusMeters = 120, NearRadiusMeters = 240, Language = "vi-VN", NarrationText = "Bạn đang đến công viên Nguyễn Thị Minh Khai, điểm xanh yên bình giữa lòng thành phố.", MapLink = "https://maps.google.com/?q=10.787,106.700", Priority = 7, UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}