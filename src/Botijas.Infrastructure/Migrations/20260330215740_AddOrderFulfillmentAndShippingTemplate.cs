using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Botijas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderFulfillmentAndShippingTemplate : Migration
    {
        private const string DefaultShippingReadyMessageTemplate =
            "Olá {name}! As suas {count} botija(s) de CO₂ estão prontas e vamos enviar para a morada indicada. {link}";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FulfillmentMethod",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "Pickup");

            migrationBuilder.AddColumn<bool>(
                name: "RefillPaid",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShippingPaid",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShippingReadyMessageTemplate",
                table: "AppSettings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: DefaultShippingReadyMessageTemplate);

            migrationBuilder.Sql("""
                UPDATE "Orders"
                SET "FulfillmentMethod" = 'Pickup'
                WHERE "FulfillmentMethod" IS NULL OR "FulfillmentMethod" = '';
                """);

            migrationBuilder.Sql($"""
                UPDATE "AppSettings"
                SET "ShippingReadyMessageTemplate" = '{DefaultShippingReadyMessageTemplate.Replace("'", "''")}'
                WHERE "ShippingReadyMessageTemplate" IS NULL OR "ShippingReadyMessageTemplate" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FulfillmentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RefillPaid",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingPaid",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingReadyMessageTemplate",
                table: "AppSettings");
        }
    }
}
