namespace Botijas.Domain.Services;

/// <summary>
/// Resolve a data de receção do ciclo atual (não a criação original da botija).
/// </summary>
public static class CylinderReceivedAt
{
    /// <summary>
    /// Prefere o timestamp do evento Received do pedido; se faltar (dados legados),
    /// usa a criação do pedido — nunca a CreatedAt antiga de botijas reutilizadas.
    /// </summary>
    public static DateTime Resolve(DateTime? receivedEventAt, DateTime orderCreatedAt)
        => receivedEventAt ?? orderCreatedAt;
}
