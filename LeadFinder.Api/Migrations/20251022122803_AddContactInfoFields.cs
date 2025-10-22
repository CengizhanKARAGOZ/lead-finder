using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadFinder.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContactInfoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailsCsv",
                table: "Websites",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PhonesCsv",
                table: "Websites",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Businesses",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Businesses",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Businesses",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Websites_BusinessId",
                table: "Websites",
                column: "BusinessId");

            migrationBuilder.AddForeignKey(
                name: "FK_Websites_Businesses_BusinessId",
                table: "Websites",
                column: "BusinessId",
                principalTable: "Businesses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Websites_Businesses_BusinessId",
                table: "Websites");

            migrationBuilder.DropIndex(
                name: "IX_Websites_BusinessId",
                table: "Websites");

            migrationBuilder.DropColumn(
                name: "EmailsCsv",
                table: "Websites");

            migrationBuilder.DropColumn(
                name: "PhonesCsv",
                table: "Websites");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Businesses");
        }
    }
}
