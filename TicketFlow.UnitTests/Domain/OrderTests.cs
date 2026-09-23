using TicketFlow.Domain.Common;
using TicketFlow.Domain.Orders;

namespace TicketFlow.UnitTests.Domain;

public class OrderTests
{
    [Fact]
    public void Constructor_WithoutCustomer_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Order(Guid.Empty, Guid.NewGuid(), 150m));

        Assert.Equal("ORDER_CUSTOMER_REQUIRED", ex.Code);
    }

    [Fact]
    public void Constructor_WithoutReservation_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Order(Guid.NewGuid(), Guid.Empty, 150m));

        Assert.Equal("ORDER_RESERVATION_REQUIRED", ex.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveAmount_ThrowsDomainException(decimal amount)
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Order(Guid.NewGuid(), Guid.NewGuid(), amount));

        Assert.Equal("ORDER_AMOUNT_INVALID", ex.Code);
    }

    [Fact]
    public void Constructor_WithValidData_StartsPending()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), 150m);

        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void MarkAsPaid_WhenPending_MovesToPaid()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), 150m);

        order.MarkAsPaid();

        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void MarkAsPaid_WhenNotPending_ThrowsDomainException()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), 150m);
        order.MarkAsPaid();

        var ex = Assert.Throws<DomainException>(() => order.MarkAsPaid());

        Assert.Equal("ORDER_CANNOT_MARK_PAID", ex.Code);
    }

    [Fact]
    public void Cancel_WhenPending_MovesToCancelled()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), 150m);

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_WhenAlreadyPaid_ThrowsDomainException()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), 150m);
        order.MarkAsPaid();

        var ex = Assert.Throws<DomainException>(() => order.Cancel());

        Assert.Equal("ORDER_CANNOT_CANCEL", ex.Code);
    }
}
