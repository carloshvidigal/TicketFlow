using TicketFlow.Domain.Common;

namespace TicketFlow.Domain.Payments;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }

    // Referência do gateway externo (aqui, o provider simulado). É por ela
    // que a estratégia de idempotência de webhook (ADR-006, ainda a
    // escrever) vai reconhecer uma notificação repetida.
    public string? ProviderReference { get; private set; }

    private Payment() { }

    public Payment(Guid orderId, decimal amount)
    {
        if (orderId == Guid.Empty)
            throw new DomainException("PAYMENT_ORDER_REQUIRED", "Order is required.");

        if (amount <= 0)
            throw new DomainException("PAYMENT_AMOUNT_INVALID", "Amount must be greater than zero.");

        Id = Guid.NewGuid();
        OrderId = orderId;
        Amount = amount;
        Status = PaymentStatus.Pending;
    }

    public void Approve(string? providerReference = null)
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("PAYMENT_CANNOT_APPROVE", $"Cannot approve a payment in {Status} status.");

        Status = PaymentStatus.Approved;
        ProviderReference = providerReference;
    }

    // Cenário crítico da documentação: "pagamento recusado não transforma
    // reserva em venda".
    public void Decline(string? providerReference = null)
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("PAYMENT_CANNOT_DECLINE", $"Cannot decline a payment in {Status} status.");

        Status = PaymentStatus.Declined;
        ProviderReference = providerReference;
    }

    public void TimeOut()
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("PAYMENT_CANNOT_TIME_OUT", $"Cannot time out a payment in {Status} status.");

        Status = PaymentStatus.TimedOut;
    }
}
