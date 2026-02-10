using Botijas.Domain.DomainEvents;
using Botijas.Domain.ValueObjects;

namespace Botijas.Domain.Entities;

public enum CylinderState
{
    Received,
    Ready,
    Delivered,
    Problem
}

public class Cylinder
{
    public Guid CylinderId { get; private set; }
    public long SequentialNumber { get; private set; } // Auto-incrementing sequential ID for display
    public LabelToken? LabelToken { get; private set; }
    public CylinderState State { get; private set; }
    public string? OccurrenceNotes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Cylinder() // EF Core
    {
        State = CylinderState.Received;
        CreatedAt = DateTime.UtcNow;
    }

    private Cylinder(Guid cylinderId, long sequentialNumber, LabelToken? labelToken = null)
    {
        CylinderId = cylinderId;
        SequentialNumber = sequentialNumber;
        LabelToken = labelToken;
        State = CylinderState.Received;
        CreatedAt = DateTime.UtcNow;
    }

    public static Cylinder Create(long sequentialNumber, LabelToken? labelToken = null)
    {
        var cylinder = new Cylinder(Guid.NewGuid(), sequentialNumber, labelToken);
        cylinder._domainEvents.Add(new CylinderReceived(cylinder.CylinderId, cylinder.LabelToken?.Value));
        return cylinder;
    }

    public void AssignLabel(LabelToken labelToken)
    {
        if (LabelToken != null && LabelToken.Value == labelToken.Value)
        {
            return; // Já tem essa etiqueta
        }

        LabelToken = labelToken;
        _domainEvents.Add(new LabelAssigned(CylinderId, labelToken.Value));
    }

    public void MarkAsReady()
    {
        if (State != CylinderState.Received)
        {
            throw new InvalidOperationException($"Cannot mark cylinder as Ready. Current state: {State}");
        }

        State = CylinderState.Ready;
        _domainEvents.Add(new CylinderMarkedReady(CylinderId));
    }

    public void MarkAsDelivered()
    {
        if (State != CylinderState.Ready)
        {
            throw new InvalidOperationException($"Cannot mark cylinder as Delivered. Current state: {State}");
        }

        State = CylinderState.Delivered;
        _domainEvents.Add(new CylinderDelivered(CylinderId));
    }

    public void ReportProblem(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new ArgumentException("Problem notes cannot be empty", nameof(notes));
        }

        State = CylinderState.Problem;
        OccurrenceNotes = notes;
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
