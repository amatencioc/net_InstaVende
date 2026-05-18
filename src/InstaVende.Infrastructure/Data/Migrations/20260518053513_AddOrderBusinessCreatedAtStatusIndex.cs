using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaVende.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderBusinessCreatedAtStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReminderTemplates_BusinessId",
                table: "ReminderTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_BusinessId",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BusinessId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ConversationId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_BusinessId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ContactId",
                table: "Conversations");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ProductCategories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ConversationLabels",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "IntentName",
                table: "BotIntents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderTemplates_BusinessId_Segment",
                table: "ReminderTemplates",
                columns: new[] { "BusinessId", "Segment" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_BusinessId_Status",
                table: "Reminders",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_ScheduledAt",
                table: "Reminders",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BusinessId_CreatedAt_Status",
                table: "Orders",
                columns: new[] { "BusinessId", "CreatedAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BusinessId_Status",
                table: "Orders",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ConversationId_Status",
                table: "Orders",
                columns: new[] { "ConversationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_SentAt",
                table: "Messages",
                columns: new[] { "ConversationId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_BusinessId_Status",
                table: "Conversations",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ContactId_Status",
                table: "Conversations",
                columns: new[] { "ContactId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReminderTemplates_BusinessId_Segment",
                table: "ReminderTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_BusinessId_Status",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_ScheduledAt",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BusinessId_CreatedAt_Status",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BusinessId_Status",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ConversationId_Status",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId_SentAt",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_BusinessId_Status",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ContactId_Status",
                table: "Conversations");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ProductCategories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ConversationLabels",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "IntentName",
                table: "BotIntents",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_ReminderTemplates_BusinessId",
                table: "ReminderTemplates",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_BusinessId",
                table: "Reminders",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BusinessId",
                table: "Orders",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ConversationId",
                table: "Orders",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_BusinessId",
                table: "Conversations",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ContactId",
                table: "Conversations",
                column: "ContactId");
        }
    }
}
