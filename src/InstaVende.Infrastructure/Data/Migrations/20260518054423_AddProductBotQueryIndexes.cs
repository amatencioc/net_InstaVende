using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaVende.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBotQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_BusinessId",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessId_IsActive_CategoryId",
                table: "Products",
                columns: new[] { "BusinessId", "IsActive", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessId_IsActive_SortOrder",
                table: "Products",
                columns: new[] { "BusinessId", "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_BusinessId_IsActive_CategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_BusinessId_IsActive_SortOrder",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BusinessId",
                table: "Products",
                column: "BusinessId");
        }
    }
}
