using TicketFlow.Domain.Common;

namespace TicketFlow.Domain.Orders;

// Nasce da confirmação de uma Reservation no checkout. Assim como a
// Reservation não guarda a lista de tickets (ver comentário lá), o Order
// também não — os tickets são encontrados via ReservationId quando
// necessário, sem duplicar essa relação.
public class Order
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid ReservationId { get; private set; }
    public decimal Amount { get; private set; }
    public OrderStatus Status { get; private set; }

    private Order() { }

    public Order(Guid customerId, Guid reservationId, decimal amount)
    {
        if (customerId == Guid.Empty)
            throw new DomainException("ORDER_CUSTOMER_REQUIRED", "Customer is required.");

        if (reservationId == Guid.Empty)
            throw new DomainException("ORDER_RESERVATION_REQUIRED", "Reservation is required.");

        if (amount <= 0)
            throw new DomainException("ORDER_AMOUNT_INVALID", "Amount must be greater than zero.");

        Id = Guid.NewGuid();
        CustomerId = customerId;
        ReservationId = reservationId;
        Amount = amount;
        Status = OrderStatus.Pending;
    }

    public void MarkAsPaid()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("ORDER_CANNOT_MARK_PAID", $"Cannot mark as paid an order in {Status} status.");

        Status = OrderStatus.Paid;
    }

    // Um pedido já pago não pode ser cancelado por aqui — isso seria um
    // reembolso, uma operação diferente que o MVP não cobre.
    public void Cancel()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("ORDER_CANNOT_CANCEL", $"Cannot cancel an order in {Status} status.");

        Status = OrderStatus.Cancelled;
    }
}
