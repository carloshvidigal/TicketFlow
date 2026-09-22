using TicketFlow.Domain.Common;

namespace TicketFlow.Domain.Events;

public class Event
{
    public Guid Id { get; private set; }
    public Guid OrganizerId { get; private set; }
    public Guid VenueId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime Date { get; private set; }
    public EventStatus Status { get; private set; }

    // EF Core precisa de um construtor sem parâmetros (pode ser privado).
    private Event() { }

    public Event(Guid organizerId, Guid venueId, string name, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("EVENT_NAME_REQUIRED", "Event name is required.");

        if (date <= DateTime.UtcNow)
            throw new DomainException("EVENT_DATE_INVALID", "Event date must be in the future.");

        Id = Guid.NewGuid();
        OrganizerId = organizerId;
        VenueId = venueId;
        Name = name;
        Date = date;
        Status = EventStatus.Draft;
    }

    public void Publish()
    {
        if (Status != EventStatus.Draft)
            throw new DomainException("EVENT_CANNOT_PUBLISH", $"Cannot publish an event in {Status} status.");

        Status = EventStatus.Published;
    }

    public void Close()
    {
        if (Status != EventStatus.Published)
            throw new DomainException("EVENT_CANNOT_CLOSE", $"Cannot close an event in {Status} status.");

        Status = EventStatus.Closed;
    }

    public void Cancel()
    {
        if (Status is EventStatus.Closed or EventStatus.Cancelled)
            throw new DomainException("EVENT_CANNOT_CANCEL", $"Cannot cancel an event in {Status} status.");

        Status = EventStatus.Cancelled;
    }

    // Cenário crítico da documentação: "evento encerrado não aceita novas compras".
    public bool AcceptsPurchases() => Status == EventStatus.Published;
}
