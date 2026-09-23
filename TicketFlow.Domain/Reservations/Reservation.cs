using TicketFlow.Domain.Common;

namespace TicketFlow.Domain.Reservations;

// Não guarda a lista de tickets reservados — essa relação já tem uma fonte
// única de verdade em Ticket.ReservationId (§10/§11 do documento). Duplicar
// aqui só criaria duas fontes de verdade pra manter sincronizadas sem
// necessidade real.
public class Reservation
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public ReservationStatus Status { get; private set; }

    private Reservation() { }

    // A duração é passada de fora (Application layer, que lê a configuração)
    // porque o documento marca a duração da reserva como configurável — o
    // Domain não deveria conhecer de onde esse valor vem.
    public Reservation(Guid customerId, TimeSpan duration)
    {
        if (customerId == Guid.Empty)
            throw new DomainException("RESERVATION_CUSTOMER_REQUIRED", "Customer is required.");

        if (duration <= TimeSpan.Zero)
            throw new DomainException("RESERVATION_DURATION_INVALID", "Duration must be greater than zero.");

        Id = Guid.NewGuid();
        CustomerId = customerId;
        ExpiresAt = DateTime.UtcNow.Add(duration);
        Status = ReservationStatus.Active;
    }

    public bool IsExpired(DateTime? now = null) =>
        Status == ReservationStatus.Active && (now ?? DateTime.UtcNow) >= ExpiresAt;

    public void Confirm()
    {
        if (Status != ReservationStatus.Active)
            throw new DomainException("RESERVATION_CANNOT_CONFIRM", $"Cannot confirm a reservation in {Status} status.");

        Status = ReservationStatus.Confirmed;
    }

    // Cenário crítico da documentação: "reserva expirada libera disponibilidade".
    public void Expire()
    {
        if (Status != ReservationStatus.Active)
            throw new DomainException("RESERVATION_CANNOT_EXPIRE", $"Cannot expire a reservation in {Status} status.");

        Status = ReservationStatus.Expired;
    }

    public void Cancel()
    {
        if (Status != ReservationStatus.Active)
            throw new DomainException("RESERVATION_CANNOT_CANCEL", $"Cannot cancel a reservation in {Status} status.");

        Status = ReservationStatus.Cancelled;
    }
}
