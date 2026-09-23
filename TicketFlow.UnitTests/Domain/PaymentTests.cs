using TicketFlow.Domain.Common;
using TicketFlow.Domain.Payments;

namespace TicketFlow.UnitTests.Domain;

public class PaymentTests
{
    [Fact]
    public void Constructor_WithoutOrder_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() => new Payment(Guid.Empty, 150m));

        Assert.Equal("PAYMENT_ORDER_REQUIRED", ex.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveAmount_ThrowsDomainException(decimal amount)
    {
        var ex = Assert.Throws<DomainException>(() => new Payment(Guid.NewGuid(), amount));

        Assert.Equal("PAYMENT_AMOUNT_INVALID", ex.Code);
    }

    [Fact]
    public void Constructor_WithValidData_StartsPending()
    {
        var payment = new Payment(Guid.NewGuid(), 150m);

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.ProviderReference);
    }

    [Fact]
    public void Approve_WhenPending_MovesToApprovedAndStoresReference()
    {
        var payment = new Payment(Guid.NewGuid(), 150m);

        payment.Approve("provider-ref-123");

        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.Equal("provider-ref-123", payment.ProviderReference);
    }

    [Fact]
    public void Approve_WhenNotPending_ThrowsDomainException()
    {
        var payment = new Payment(Guid.NewGuid(), 150m);
        payment.Approve();

        var ex = Assert.Throws<DomainException>(() => payment.Approve());

        Assert.Equal("PAYMENT_CANNOT_APPROVE", ex.Code);
    }

    // Cenário crítico da documentação: "pagamento recusado não transforma
    // reserva em venda" — aqui garantimos ao menos a transição de estado do
    // pagamento em si; a orquestração com Reservation/Ticket é da Application.
    [Fact]
    public void Decline_WhenPending_MovesToDeclined()
    {
        var payment = new Payment(Guid.NewGuid(), 150m);

        payment.Decline("provider-ref-456");

        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Equal("provider-ref-456", payment.ProviderReference);
    }

    [Fact]
    public void Decline_WhenNotPending_ThrowsDomainException()
    {
        var payment = new Payment(Guid.NewGuid(), 150m);
        payment.Decline();

        var ex = Assert.Throws<DomainException>(() => payment.Decline());

        Assert.Equal("PAYMENT_CANNOT_DECLINE", ex.Code);
    }

    [Fact]
    public void TimeOut_WhenPending_MovesToTimedOut()
    {
        var payment = new Payment(Guid.NewGuid(), 150m);

        payment.TimeOut();

        Assert.Equal(PaymentStatus.TimedOut, payment.Status);
    }

    [Fact]
    public void TimeOut_WhenNotPending_ThrowsDomainException()
    {
        var payment = new Payment(Guid.NewGuid(), 150m);
        payment.TimeOut();

        var ex = Assert.Throws<DomainException>(() => payment.TimeOut());

        Assert.Equal("PAYMENT_CANNOT_TIME_OUT", ex.Code);
    }
}
