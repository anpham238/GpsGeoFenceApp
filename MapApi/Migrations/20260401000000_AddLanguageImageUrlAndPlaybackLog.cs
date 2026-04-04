using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace MapApi.Migrations
{
    public partial class AddLanguageImageUrlAndPlaybackLog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Them cot Language vao Pois
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Pois",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                defaultValue: "vi-VN");

            // 2) Them cot ImageUrl vao Pois
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Pois",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // 3) Cap nhat AudioUrl co max length
            migrationBuilder.AlterColumn<string>(
                name: "AudioUrl",
                table: "Pois",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // 4) Tao bang PlaybackLogs
            migrationBuilder.CreateTable(
                name: "PlaybackLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                                            .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PoiId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlayedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationListened = table.Column<int>(type: "int", nullable: true),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybackLogs_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex("IX_PlaybackLogs_PoiId", "PlaybackLogs", "PoiId");
            migrationBuilder.CreateIndex("IX_PlaybackLogs_PlayedAt", "PlaybackLogs", "PlayedAt");

            // 5) Seed 7 diem TPHCM
            migrationBuilder.InsertData("Pois",
                columns: new[] { "Id", "Name", "Description", "Latitude", "Longitude", "RadiusMeters", "NearRadiusMeters", "DebounceSeconds", "CooldownSeconds", "Priority", "Language", "NarrationText", "AudioUrl", "ImageUrl", "MapLink", "IsActive", "UpdatedAt" },
                values: new object[,]
                {
                    { "poi-hcm-001",        "Trung tâm TP.HCM",           "Trái tim kinh tế và văn hoá Việt Nam",         10.776889, 106.700806, 150f, 300f, 3, 30, 1, "vi-VN", "Chào mừng đến Thành phố Hồ Chí Minh, trái tim kinh tế năng động của Việt Nam.",    null, null, "https://maps.google.com/?q=10.776889,106.700806", true, new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc) },
                    { "poi-benthanh-001",   "Chợ Bến Thành",              "Biểu tượng lịch sử hơn 100 năm của Sài Gòn",  10.772450, 106.698060, 100f, 200f, 3, 30, 2, "vi-VN", "Bạn đang đến Chợ Bến Thành, biểu tượng văn hoá lịch sử trên 100 năm của Sài Gòn.", null, null, "https://maps.google.com/?q=10.77245,106.69806",   true, new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc) },
                    { "poi-notredame-001",  "Nhà thờ Đức Bà",             "Kiến trúc Gothic xây từ 1863",                 10.779930, 106.699330, 80f,  160f, 3, 30, 3, "vi-VN", "Trước mặt bạn là Nhà thờ Đức Bà Sài Gòn, công trình Gothic ấn tượng xây dựng từ năm 1863.", null, null, "https://maps.google.com/?q=10.77993,106.69933",  true, new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc) },
                    { "poi-postoffice-001", "Bưu điện Trung tâm Sài Gòn", "Biệt thự Pháp đẹp nhất TP, xây 1886",         10.779760, 106.699600, 80f,  160f, 3, 30, 4, "vi-VN", "Đây là Bưu điện Trung tâm Sài Gòn, biệt thự Pháp tuyệt đẹp được xây dựng năm 1886.",   null, null, "https://maps.google.com/?q=10.77976,106.6996",   true, new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc) },
                    { "poi-park304-001",    "Công viên 30/4",              "Công viên lịch sử trước Dinh Độc Lập",         10.777600, 106.695400, 100f, 200f, 3, 30, 5, "vi-VN", "Bạn đang ở Công viên 30 tháng 4. Phía sau là Dinh Độc Lập, di tích lịch sử quan trọng.", null, null, "https://maps.google.com/?q=10.7776,106.6954",    true, new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc) },
                    { "poi-reunif-001",     "Dinh Độc Lập",                "Nơi ghi dấu sự kiện lịch sử quan trọng",      10.776900, 106.695400, 100f, 200f, 3, 30, 6, "vi-VN", "Đây là Dinh Độc Lập, chứng nhân lịch sử của đất nước. Hiện là bảo tàng tham quan.",    null, null, "https://maps.google.com/?q=10.7769,106.6954",    true, new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc) },
                    { "poi-ntmk-001",       "Công viên NTMK",              "Công viên xanh mát giữa lòng thành phố",       10.787000, 106.700000, 120f, 240f, 3, 30, 7, "vi-VN", "Bạn đang đến công viên Nguyễn Thị Minh Khai, điểm xanh yên bình giữa lòng thành phố.", null, null, "https://maps.google.com/?q=10.787,106.700",      true, new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc) },
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PlaybackLogs");
            migrationBuilder.DropColumn(name: "Language", table: "Pois");
            migrationBuilder.DropColumn(name: "ImageUrl", table: "Pois");
            migrationBuilder.DeleteData("Pois", "Id", new object[] {
                "poi-hcm-001","poi-benthanh-001","poi-notredame-001",
                "poi-postoffice-001","poi-park304-001","poi-reunif-001","poi-ntmk-001"
            });
        }
    }
}