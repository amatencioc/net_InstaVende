using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaVende.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationBusinessCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Conversations_BusinessId_CreatedAt",
                table: "Conversations",
                columns: new[] { "BusinessId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conversations_BusinessId_CreatedAt",
                table: "Conversations");
        }
    }
}
