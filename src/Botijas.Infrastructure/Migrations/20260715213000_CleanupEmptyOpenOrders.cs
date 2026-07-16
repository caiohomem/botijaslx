using Botijas.Domain.Entities;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Botijas.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(BotijasDbContext))]
[Migration("20260715213000_CleanupEmptyOpenOrders")]
public partial class CleanupEmptyOpenOrders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Apaga pedidos Open sem CylinderRefs (órfãos do fluxo create-then-add / delete última botija).
        migrationBuilder.Sql(
            $"""
            DELETE FROM "Orders" o
            WHERE o."Status" = '{nameof(RefillOrderStatus.Open)}'
              AND NOT EXISTS (
                  SELECT 1
                  FROM "CylinderRefs" cr
                  WHERE cr."OrderId" = o."OrderId"
              );
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversível: pedidos órfãos não são recriados.
    }
}
