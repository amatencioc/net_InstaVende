using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaVende.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVendedorIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KnowledgeEntries_BusinessId",
                table: "KnowledgeEntries");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryZones_BusinessId",
                table: "DeliveryZones");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEntries_BusinessId_IsFavorite_CreatedAt",
                table: "KnowledgeEntries",
                columns: new[] { "BusinessId", "IsFavorite", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_BusinessId_SortOrder",
                table: "DeliveryZones",
                columns: new[] { "BusinessId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KnowledgeEntries_BusinessId_IsFavorite_CreatedAt",
                table: "KnowledgeEntries");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryZones_BusinessId_SortOrder",
                table: "DeliveryZones");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEntries_BusinessId",
                table: "KnowledgeEntries",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_BusinessId",
                table: "DeliveryZones",
                column: "BusinessId");
        }
    }
}
