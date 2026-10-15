using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstaVende.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlatformHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reminders_BusinessId_Status",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_ScheduledAt",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_BusinessId_Status",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_BusinessUsers_BusinessId",
                table: "BusinessUsers");

            // Business.BusinessSector y Business.Sector eran duplicados: se conserva el dato en Sector.
            // EXEC(...) evita errores de compilación del lote al reejecutar el script idempotente.
            migrationBuilder.Sql("EXEC(N'UPDATE [Businesses] SET [Sector] = [BusinessSector] WHERE [Sector] IS NULL AND [BusinessSector] IS NOT NULL;')");

            migrationBuilder.DropColumn(
                name: "BusinessSector",
                table: "Businesses");

            // Recorta textos que excedan los nuevos límites antes de reducir las columnas
            migrationBuilder.Sql("EXEC(N'UPDATE [Reminders] SET [Message] = LEFT([Message], 1000) WHERE LEN([Message]) > 1000;')");
            migrationBuilder.Sql("EXEC(N'UPDATE [Messages] SET [MediaType] = LEFT([MediaType], 50) WHERE LEN([MediaType]) > 50;')");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Reminders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "MediaType",
                table: "Messages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExternalMessageId",
                table: "Messages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // Los mensajes existentes ya fueron entregados (1 = MessageDeliveryStatus.Sent)
            migrationBuilder.AddColumn<int>(
                name: "DeliveryStatus",
                table: "Messages",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Xml = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_BusinessId_ScheduledAt",
                table: "Reminders",
                columns: new[] { "BusinessId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_Status_ScheduledAt",
                table: "Reminders",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BusinessId_OrderNumber",
                table: "Orders",
                columns: new[] { "BusinessId", "OrderNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BusinessId_SubStatus",
                table: "Orders",
                columns: new[] { "BusinessId", "SubStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ExternalMessageId",
                table: "Messages",
                column: "ExternalMessageId",
                unique: true,
                filter: "[ExternalMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessUsers_BusinessId_UserId",
                table: "BusinessUsers",
                columns: new[] { "BusinessId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_BusinessId_ScheduledAt",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_Status_ScheduledAt",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BusinessId_OrderNumber",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BusinessId_SubStatus",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ExternalMessageId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_BusinessUsers_BusinessId_UserId",
                table: "BusinessUsers");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "Messages");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Reminders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "MediaType",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExternalMessageId",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessSector",
                table: "Businesses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_BusinessId_Status",
                table: "Reminders",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_ScheduledAt",
                table: "Reminders",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_BusinessId_Status",
                table: "Conversations",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessUsers_BusinessId",
                table: "BusinessUsers",
                column: "BusinessId");
        }
    }
}
