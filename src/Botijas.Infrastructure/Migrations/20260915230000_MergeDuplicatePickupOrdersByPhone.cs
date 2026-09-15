using Botijas.Domain.Entities;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Botijas.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(BotijasDbContext))]
[Migration("20260915230000_MergeDuplicatePickupOrdersByPhone")]
public partial class MergeDuplicatePickupOrdersByPhone : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Funde pedidos prontos para recolha duplicados pelo telefone do cliente (não só CustomerId).
        // Inclui pedidos Open com todas as botijas prontas — ficaram de fora da migração anterior.
        migrationBuilder.Sql(
            $"""
            WITH eligible_orders AS (
                SELECT
                    o."OrderId",
                    o."CreatedAt",
                    o."Status",
                    c."Phone",
                    o."FulfillmentMethod"
                FROM "Orders" o
                INNER JOIN "Customers" c ON c."CustomerId" = o."CustomerId"
                WHERE o."Status" NOT IN ('{nameof(RefillOrderStatus.Completed)}', '{nameof(RefillOrderStatus.Cancelled)}')
                  AND EXISTS (
                      SELECT 1 FROM "CylinderRefs" cr WHERE cr."OrderId" = o."OrderId"
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "CylinderRefs" cr
                      INNER JOIN "Cylinders" cyl ON cyl."CylinderId" = cr."CylinderId"
                      WHERE cr."OrderId" = o."OrderId"
                        AND cyl."State" NOT IN ('{nameof(CylinderState.Ready)}', '{nameof(CylinderState.Problem)}', '{nameof(CylinderState.Delivered)}')
                  )
            ),
            grouped AS (
                SELECT
                    eo."Phone",
                    eo."FulfillmentMethod",
                    (ARRAY_AGG(eo."OrderId" ORDER BY eo."CreatedAt", eo."OrderId"))[1] AS primary_order_id,
                    ARRAY_AGG(eo."OrderId" ORDER BY eo."CreatedAt", eo."OrderId") AS order_ids
                FROM eligible_orders eo
                GROUP BY eo."Phone", eo."FulfillmentMethod"
                HAVING COUNT(*) > 1
            ),
            duplicate_map AS (
                SELECT
                    g.primary_order_id,
                    duplicate_id
                FROM grouped g
                CROSS JOIN LATERAL unnest(g.order_ids) AS duplicate_id
                WHERE duplicate_id <> g.primary_order_id
            ),
            moved_refs AS (
                UPDATE "CylinderRefs" cr
                SET "OrderId" = dm.primary_order_id
                FROM duplicate_map dm
                WHERE cr."OrderId" = dm.duplicate_id
                RETURNING cr."CylinderId"
            ),
            promoted_primary AS (
                UPDATE "Orders" o
                SET "Status" = '{nameof(RefillOrderStatus.ReadyForPickup)}'
                FROM grouped g
                WHERE o."OrderId" = g.primary_order_id
                  AND o."Status" = '{nameof(RefillOrderStatus.Open)}'
                RETURNING o."OrderId"
            )
            DELETE FROM "Orders" o
            WHERE o."OrderId" IN (SELECT duplicate_id FROM duplicate_map);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversível: pedidos fundidos não são separados.
    }
}
