using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Botijas.Infrastructure.Migrations;

/// <inheritdoc />
public partial class DropCylinderRefState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "State",
            table: "CylinderRefs");
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
