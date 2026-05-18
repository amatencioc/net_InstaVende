using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaVende.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountConfigIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserInvitations_BusinessId",
                table: "UserInvitations");

            migrationBuilder.DropIndex(
                name: "IX_PaymentMethods_BusinessId",
                table: "PaymentMethods");

            migrationBuilder.CreateIndex(
                name: "IX_UserInvitations_BusinessId_Status",
                table: "UserInvitations",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_BusinessId_SortOrder",
                table: "PaymentMethods",
                columns: new[] { "BusinessId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserInvitations_BusinessId_Status",
                table: "UserInvitations");

            migrationBuilder.DropIndex(
                name: "IX_PaymentMethods_BusinessId_SortOrder",
                table: "PaymentMethods");

            migrationBuilder.CreateIndex(
                name: "IX_UserInvitations_BusinessId",
                table: "UserInvitations",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_BusinessId",
                table: "PaymentMethods",
                column: "BusinessId");
        }
    }
}
