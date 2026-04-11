using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapApi.Migrations
{
    /// <inheritdoc />
    public partial class PostRedesign2026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Poi",
                table: "Poi");

            migrationBuilder.DropIndex(
                name: "IX_Poi_IsActive_Priority",
                table: "Poi");

            migrationBuilder.DropColumn(
                name: "AudioUrl",
                table: "Poi");

            migrationBuilder.DropColumn(
                name: "DebounceSeconds",
                table: "Poi");

            migrationBuilder.DropColumn(
                name: "MapLink",
                table: "Poi");

            migrationBuilder.DropColumn(
                name: "NarrationText",
                table: "Poi");

            migrationBuilder.DropColumn(
                name: "NearRadiusMeters",
                table: "Poi");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Poi");

            migrationBuilder.RenameTable(
                name: "Poi",
                newName: "Pois");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Pois",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<int>(
                name: "RadiusMeters",
                table: "Pois",
                type: "int",
                nullable: false,
                defaultValue: 120,
                oldClrType: typeof(float),
                oldType: "real");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Pois",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Pois",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Pois",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Pois",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<int>(
                name: "CooldownSeconds",
                table: "Pois",
                type: "int",
                nullable: false,
                defaultValue: 30,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Pois",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Pois",
                table: "Pois",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "PoiLanguage",
                columns: table => new
                {
                    IdLang = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPoi = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NamePoi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NarTTS = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LanguageTag = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoiLanguage", x => x.IdLang);
                    table.ForeignKey(
                        name: "FK_PoiLanguage_Pois_IdPoi",
                        column: x => x.IdPoi,
                        principalTable: "Pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoiMedia",
                columns: table => new
                {
                    Idm = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPoi = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Image = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MapLink = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoiMedia", x => x.Idm);
                    table.ForeignKey(
                        name: "FK_PoiMedia_Pois_IdPoi",
                        column: x => x.IdPoi,
                        principalTable: "Pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Mail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "HistoryPoi",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPoi = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IdUser = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PoiName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    LastVisitedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    TotalDurationSeconds = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryPoi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryPoi_Pois_IdPoi",
                        column: x => x.IdPoi,
                        principalTable: "Pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistoryPoi_Users_IdUser",
                        column: x => x.IdUser,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pois_Active",
                table: "Pois",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_History_Poi",
                table: "HistoryPoi",
                columns: new[] { "IdPoi", "LastVisitedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_History_User",
                table: "HistoryPoi",
                columns: new[] { "IdUser", "LastVisitedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_PoiLanguage_Key",
                table: "PoiLanguage",
                columns: new[] { "IdPoi", "LanguageTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoiMedia_Poi",
                table: "PoiMedia",
                column: "IdPoi");

            migrationBuilder.CreateIndex(
                name: "UX_Users_Mail",
                table: "Users",
                column: "Mail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoryPoi");

            migrationBuilder.DropTable(
                name: "PoiLanguage");

            migrationBuilder.DropTable(
                name: "PoiMedia");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Pois",
                table: "Pois");

            migrationBuilder.DropIndex(
                name: "IX_Pois_Active",
                table: "Pois");

            migrationBuilder.RenameTable(
                name: "Pois",
                newName: "Poi");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Poi",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<float>(
                name: "RadiusMeters",
                table: "Poi",
                type: "real",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Poi",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Poi",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Poi",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Poi",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<int>(
                name: "CooldownSeconds",
                table: "Poi",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Poi",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<string>(
                name: "AudioUrl",
                table: "Poi",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DebounceSeconds",
                table: "Poi",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MapLink",
                table: "Poi",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NarrationText",
                table: "Poi",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "NearRadiusMeters",
                table: "Poi",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Poi",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Poi",
                table: "Poi",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Poi_IsActive_Priority",
                table: "Poi",
                columns: new[] { "IsActive", "Priority" });
        }
    }
}
