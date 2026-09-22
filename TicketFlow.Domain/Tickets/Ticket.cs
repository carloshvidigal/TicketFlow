using TicketFlow.Domain.Common;

namespace TicketFlow.Domain.Tickets;

public class Ticket
{
    public Guid Id { get; private set; }
    public Guid SectionId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public TicketStatus Status { get; private set; }

    private Ticket() { }

    public Ticket(Guid sectionId)
    {
        Id = Guid.NewGuid();
        SectionId = sectionId;
        Status = TicketStatus.Available;
    }

    public void Reserve(Guid reservationId)
    {
        if (Status != TicketStatus.Available)
            throw new DomainException("TICKET_UNAVAILABLE", $"Ticket {Id} is not available for reservation.");

        Status = TicketStatus.Reserved;
        ReservationId = reservationId;
    }

    public void Confirm()
    {
        if (Status != TicketStatus.Reserved)
            throw new DomainException("TICKET_NOT_RESERVED", $"Ticket {Id} must be reserved before it can be confirmed.");

        Status = TicketStatus.Sold;
    }

    public void Release()
    {
        if (Status != TicketStatus.Reserved)
            throw new DomainException("TICKET_NOT_RESERVED", $"Ticket {Id} is not currently reserved.");

        Status = TicketStatus.Available;
        ReservationId = null;
    }

    public void Cancel()
    {
        if (Status == TicketStatus.Cancelled)
            throw new DomainException("TICKET_ALREADY_CANCELLED", $"Ticket {Id} is already cancelled.");

        Status = TicketStatus.Cancelled;
        ReservationId = null;
    }
}
