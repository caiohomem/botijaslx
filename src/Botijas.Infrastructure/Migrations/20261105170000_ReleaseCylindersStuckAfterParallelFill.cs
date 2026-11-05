using Botijas.Domain.Entities;
using Botijas.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Botijas.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(BotijasDbContext))]
[Migration("20261105170000_ReleaseCylindersStuckAfterParallelFill")]
public partial class ReleaseCylindersStuckAfterParallelFill : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Marcações de "cheia" em paralelo deixam as botijas Ready e o pedido Open.
        // A visita seguinte cola nesse pedido, e as botijas já cheias deixam de poder sair.
        // Cada statement é separado para o INSERT do pedido novo ser visível à FK.
        migrationBuilder.Sql(
            $"""
            CREATE TEMP TABLE stuck_ready_orders ON COMMIT DROP AS
            WITH membership AS (
                SELECT
                    o."OrderId",
                    o."CustomerId",
                    o."FulfillmentMethod",
                    o."RefillPaid",
                    o."ShippingPaid",
                    cyl."State",
                    (
                        SELECT MAX(h."Timestamp")
                        FROM "CylinderHistory" h
                        WHERE h."CylinderId" = cr."CylinderId"
                          AND h."OrderId" = o."OrderId"
                          AND h."EventType" IN (
                              '{nameof(CylinderEventType.MarkedReady)}',
                              '{nameof(CylinderEventType.ProblemReported)}',
                              '{nameof(CylinderEventType.Delivered)}')
                    ) AS ready_at,
                    (
                        SELECT MIN(h."Timestamp")
                        FROM "CylinderHistory" h
                        WHERE h."CylinderId" = cr."CylinderId"
                          AND h."OrderId" = o."OrderId"
                          AND h."EventType" = '{nameof(CylinderEventType.Received)}'
                    ) AS received_at
                FROM "Orders" o
                INNER JOIN "CylinderRefs" cr ON cr."OrderId" = o."OrderId"
                INNER JOIN "Cylinders" cyl ON cyl."CylinderId" = cr."CylinderId"
                WHERE o."Status" = '{nameof(RefillOrderStatus.Open)}'
            ),
            stuck AS (
                SELECT
                    "OrderId",
                    "CustomerId",
                    "FulfillmentMethod",
                    "RefillPaid",
                    "ShippingPaid"
                FROM membership
                GROUP BY "OrderId", "CustomerId", "FulfillmentMethod", "RefillPaid", "ShippingPaid"
                HAVING COUNT(*) FILTER (WHERE "State" IN (
                           '{nameof(CylinderState.Ready)}',
                           '{nameof(CylinderState.Problem)}',
                           '{nameof(CylinderState.Delivered)}')) > 0
                   AND COUNT(*) FILTER (WHERE "State" = '{nameof(CylinderState.Received)}') > 0
                   AND MAX(ready_at) FILTER (WHERE "State" IN (
                           '{nameof(CylinderState.Ready)}',
                           '{nameof(CylinderState.Problem)}',
                           '{nameof(CylinderState.Delivered)}'))
                       < MIN(received_at) FILTER (WHERE "State" = '{nameof(CylinderState.Received)}')
            )
            SELECT
                gen_random_uuid() AS new_order_id,
                s."OrderId" AS old_order_id,
                s."CustomerId",
                s."FulfillmentMethod",
                s."RefillPaid",
                s."ShippingPaid",
                (
                    SELECT MIN(m.received_at)
                    FROM membership m
                    WHERE m."OrderId" = s."OrderId"
                      AND m."State" = '{nameof(CylinderState.Received)}'
                ) AS created_at
            FROM stuck s;
            """);

        migrationBuilder.Sql(
            $"""
            INSERT INTO "Orders" (
                "OrderId", "CustomerId", "Status", "FulfillmentMethod",
                "RefillPaid", "ShippingPaid", "CreatedAt",
                "CompletedAt", "NotifiedAt", "ShippedAt", "CancelledAt", "CancellationNotes")
            SELECT
                new_order_id,
                "CustomerId",
                '{nameof(RefillOrderStatus.Open)}',
                "FulfillmentMethod",
                "RefillPaid",
                "ShippingPaid",
                COALESCE(created_at, NOW()),
                NULL, NULL, NULL, NULL, NULL
            FROM stuck_ready_orders;
            """);

        migrationBuilder.Sql(
            $"""
            UPDATE "CylinderRefs" cr
            SET "OrderId" = plan.new_order_id
            FROM stuck_ready_orders plan, "Cylinders" cyl
            WHERE cr."OrderId" = plan.old_order_id
              AND cyl."CylinderId" = cr."CylinderId"
              AND cyl."State" = '{nameof(CylinderState.Received)}';
            """);

        migrationBuilder.Sql(
            $"""
            UPDATE "CylinderHistory" h
            SET "OrderId" = plan.new_order_id
            FROM stuck_ready_orders plan, "Cylinders" cyl
            WHERE h."OrderId" = plan.old_order_id
              AND h."CylinderId" = cyl."CylinderId"
              AND cyl."State" = '{nameof(CylinderState.Received)}'
              AND EXISTS (
                  SELECT 1
                  FROM "CylinderRefs" cr
                  WHERE cr."OrderId" = plan.new_order_id
                    AND cr."CylinderId" = h."CylinderId");
            """);

        migrationBuilder.Sql(
            $"""
            UPDATE "Orders" o
            SET "Status" = '{nameof(RefillOrderStatus.ReadyForPickup)}'
            FROM stuck_ready_orders plan
            WHERE o."OrderId" = plan.old_order_id
              AND o."Status" = '{nameof(RefillOrderStatus.Open)}'
              AND NOT EXISTS (
                  SELECT 1
                  FROM "CylinderRefs" cr
                  INNER JOIN "Cylinders" cyl ON cyl."CylinderId" = cr."CylinderId"
                  WHERE cr."OrderId" = o."OrderId"
                    AND cyl."State" = '{nameof(CylinderState.Received)}');
            """);

        migrationBuilder.Sql(
            $"""
            UPDATE "Orders" o
            SET "Status" = '{nameof(RefillOrderStatus.ReadyForPickup)}'
            WHERE o."Status" = '{nameof(RefillOrderStatus.Open)}'
              AND EXISTS (
                  SELECT 1 FROM "CylinderRefs" cr WHERE cr."OrderId" = o."OrderId")
              AND NOT EXISTS (
                  SELECT 1
                  FROM "CylinderRefs" cr
                  INNER JOIN "Cylinders" cyl ON cyl."CylinderId" = cr."CylinderId"
                  WHERE cr."OrderId" = o."OrderId"
                    AND cyl."State" NOT IN (
                        '{nameof(CylinderState.Ready)}',
                        '{nameof(CylinderState.Problem)}',
                        '{nameof(CylinderState.Delivered)}'));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversível: a visita tardia já ficou num pedido próprio.
    }
}
