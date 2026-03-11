using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Botijas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersistAppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    AppSettingsId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StorePhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StoreLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AppTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WhatsAppMessageTemplate = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    WelcomeMessageTemplate = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ThankYouMessageTemplate = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LabelTemplate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PrinterType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LabelWidthMm = table.Column<int>(type: "integer", nullable: false),
                    LabelHeightMm = table.Column<int>(type: "integer", nullable: false),
                    MaxPhoneDigits = table.Column<int>(type: "integer", nullable: false),
                    SoundNotificationsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.AppSettingsId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");
        }
    }
}
