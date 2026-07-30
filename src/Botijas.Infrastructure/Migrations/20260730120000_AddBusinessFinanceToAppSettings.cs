using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Botijas.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(BotijasDbContext))]
[Migration("20260730120000_AddBusinessFinanceToAppSettings")]
public partial class AddBusinessFinanceToAppSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "RefillPriceEur",
            table: "AppSettings",
            type: "numeric(10,2)",
            precision: 10,
            scale: 2,
            nullable: false,
            defaultValue: 10m);

        migrationBuilder.AddColumn<decimal>(
            name: "SourceCylinderCostEur",
            table: "AppSettings",
            type: "numeric(10,2)",
            precision: 10,
            scale: 2,
            nullable: false,
            defaultValue: 90m);

        migrationBuilder.AddColumn<decimal>(
            name: "SourceCylinderGasKg",
            table: "AppSettings",
            type: "numeric(10,3)",
            precision: 10,
            scale: 3,
            nullable: false,
            defaultValue: 17m);

        migrationBuilder.AddColumn<decimal>(
            name: "ConsumerCylinderGasG",
            table: "AppSettings",
            type: "numeric(10,1)",
            precision: 10,
            scale: 1,
            nullable: false,
            defaultValue: 425m);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RefillPriceEur", table: "AppSettings");
        migrationBuilder.DropColumn(name: "SourceCylinderCostEur", table: "AppSettings");
        migrationBuilder.DropColumn(name: "SourceCylinderGasKg", table: "AppSettings");
        migrationBuilder.DropColumn(name: "ConsumerCylinderGasG", table: "AppSettings");
    }
}
