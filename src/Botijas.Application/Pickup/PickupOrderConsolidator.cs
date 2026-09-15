namespace Botijas.Application.Pickup;

/// <summary>
/// Um cliente só deve aparecer uma vez na Out por modo de entrega.
/// Pedidos ReadyForPickup duplicados (legado do fluxo antigo) são agregados num único cartão.
/// </summary>
public static class PickupOrderConsolidator
{
    public static List<PickupOrderDto> ConsolidateByCustomer(IEnumerable<PickupOrderDto> orders)
    {
        return orders
            .GroupBy(o => (NormalizePhone(o.CustomerPhone), NormalizeFulfillment(o.FulfillmentMethod)))
            .Select(BuildConsolidatedOrder)
            .OrderBy(o => o.ReadyAt ?? o.CreatedAt)
            .ToList();
    }

    internal static string NormalizePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());

    internal static string NormalizeFulfillment(string fulfillment) =>
        string.IsNullOrWhiteSpace(fulfillment) ? "Pickup" : fulfillment.Trim();

    private static PickupOrderDto BuildConsolidatedOrder(IEnumerable<PickupOrderDto> group)
    {
        var orders = group
            .OrderBy(o => o.ReadyAt ?? o.CreatedAt)
            .ToList();

        var primary = orders[0];
        var cylinders = orders
            .SelectMany(order => order.Cylinders.Select(cylinder => new PickupCylinderDto
            {
                CylinderId = cylinder.CylinderId,
                SequentialNumber = cylinder.SequentialNumber,
                LabelToken = cylinder.LabelToken,
                State = cylinder.State,
                OccurrenceNotes = cylinder.OccurrenceNotes,
                IsDelivered = cylinder.IsDelivered,
                OrderId = order.OrderId,
            }))
            .OrderBy(c => c.SequentialNumber)
            .ToList();

        return new PickupOrderDto
        {
            OrderId = primary.OrderId,
            CustomerId = primary.CustomerId,
            CustomerName = primary.CustomerName,
            CustomerPhone = primary.CustomerPhone,
            CustomerPhoneType = primary.CustomerPhoneType,
            Status = primary.Status,
            FulfillmentMethod = primary.FulfillmentMethod,
            RefillPaid = orders.Any(o => o.RefillPaid),
            ShippingPaid = orders.Any(o => o.ShippingPaid),
            CreatedAt = orders.Min(o => o.CreatedAt),
            ReadyAt = orders.Min(o => o.ReadyAt ?? o.CreatedAt),
            NotifiedAt = orders.All(o => o.NotifiedAt.HasValue)
                ? orders.Max(o => o.NotifiedAt)
                : primary.NotifiedAt,
            ShippedAt = orders.All(o => o.ShippedAt.HasValue)
                ? orders.Max(o => o.ShippedAt)
                : primary.ShippedAt,
            NeedsNotification = orders.Any(o => o.NeedsNotification),
            TotalCylinders = cylinders.Count,
            DeliveredCylinders = cylinders.Count(c => c.IsDelivered),
            Cylinders = cylinders,
        };
    }
}
