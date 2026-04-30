using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapApi.Migrations
{
    public partial class AddPriorityAndWebNarration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PriorityLevel",
                table: "Pois",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WebNarrationUsage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoiId = table.Column<int>(type: "int", nullable: false),
                    DeviceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlayCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastPlayedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebNarrationUsage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebNarrationUsage_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_WebNarrationUsage_Key",
                table: "WebNarrationUsage",
                columns: new[] { "PoiId", "DeviceKey" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "WebNarrationUsage");
            migrationBuilder.DropColumn(name: "PriorityLevel", table: "Pois");
        }
    }
}
