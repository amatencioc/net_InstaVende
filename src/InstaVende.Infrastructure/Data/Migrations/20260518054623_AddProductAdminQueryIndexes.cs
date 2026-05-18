using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaVende.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductAdminQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessId_CreatedAt",
                table: "Products",
                columns: new[] { "BusinessId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessId_IsActive_Name",
                table: "Products",
                columns: new[] { "BusinessId", "IsActive", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_BusinessId_CreatedAt",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_BusinessId_IsActive_Name",
                table: "Products");
        }
    }
}
