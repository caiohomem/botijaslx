using Botijas.Domain.Entities;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Botijas.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(BotijasDbContext))]
[Migration("20260915220000_MergeDuplicateReadyForPickupOrders")]
public partial class MergeDuplicateReadyForPickupOrders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Pedidos ReadyForPickup duplicados do mesmo cliente/modo (legado) → um só pedido.
        migrationBuilder.Sql(
            $"""
            WITH grouped AS (
                SELECT
                    "CustomerId",
                    "FulfillmentMethod",
                    (ARRAY_AGG("OrderId" ORDER BY "CreatedAt", "OrderId"))[1] AS primary_order_id,
                    ARRAY_AGG("OrderId" ORDER BY "CreatedAt", "OrderId") AS order_ids
                FROM "Orders"
                WHERE "Status" = '{nameof(RefillOrderStatus.ReadyForPickup)}'
                GROUP BY "CustomerId", "FulfillmentMethod"
                HAVING COUNT(*) > 1
            ),
            duplicate_map AS (
                SELECT
                    g.primary_order_id,
                    duplicate_id
                FROM grouped g
                CROSS JOIN LATERAL unnest(g.order_ids) AS duplicate_id
                WHERE duplicate_id <> g.primary_order_id
            )
            UPDATE "CylinderRefs" cr
            SET "OrderId" = dm.primary_order_id
            FROM duplicate_map dm
            WHERE cr."OrderId" = dm.duplicate_id;

            DELETE FROM "Orders" o
            USING grouped g
            WHERE o."Status" = '{nameof(RefillOrderStatus.ReadyForPickup)}'
              AND o."CustomerId" = g."CustomerId"
              AND o."FulfillmentMethod" = g."FulfillmentMethod"
              AND o."OrderId" <> g.primary_order_id;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversível: pedidos fundidos não são separados.
    }
}
