namespace TicketFlow.Domain.Payments;

// Espelha os resultados possíveis do Payment Provider simulado (§14 do documento).
public enum PaymentStatus
{
    Pending,
    Approved,
    Declined,
    TimedOut
}
