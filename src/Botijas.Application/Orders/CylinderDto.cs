namespace Botijas.Application.Orders;

public class CylinderDto
{
    public Guid CylinderId { get; set; }
    public long SequentialNumber { get; set; }
    public string? LabelToken { get; set; }
    public string State { get; set; } = string.Empty;
}
