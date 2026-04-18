using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MapApi.Migrations
{
    public partial class AddUserProfileFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Users",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "GpsGeoFenceApp/Application/Resources/Image/default-avatar.png");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PhoneNumber", table: "Users");
            migrationBuilder.DropColumn(name: "AvatarUrl", table: "Users");
        }
    }
}