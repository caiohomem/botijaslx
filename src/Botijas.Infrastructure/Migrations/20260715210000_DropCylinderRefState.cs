using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Botijas.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(BotijasDbContext))]
[Migration("20260715210000_DropCylinderRefState")]
public partial class DropCylinderRefState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // IF EXISTS: safe if column already removed manually / partial deploy.
        migrationBuilder.Sql(
            """
            ALTER TABLE "CylinderRefs" DROP COLUMN IF EXISTS "State";
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "State",
            table: "CylinderRefs",
            type: "text",
            nullable: false,
            defaultValue: "Received");
    }
}
