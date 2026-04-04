using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapApi.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelToDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Pois",
                table: "Pois");

            migrationBuilder.RenameTable(
                name: "Pois",
                newName: "Poi");

            migrationBuilder.RenameIndex(
                name: "IX_Pois_IsActive_Priority",
                table: "Poi",
                newName: "IX_Poi_IsActive_Priority");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Poi",
                table: "Poi",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Poi",
                table: "Poi");

            migrationBuilder.RenameTable(
                name: "Poi",
                newName: "Pois");

            migrationBuilder.RenameIndex(
                name: "IX_Poi_IsActive_Priority",
                table: "Pois",
                newName: "IX_Pois_IsActive_Priority");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Pois",
                table: "Pois",
                column: "Id");
        }
    }
}
