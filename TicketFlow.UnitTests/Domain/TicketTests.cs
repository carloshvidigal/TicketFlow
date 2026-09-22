using TicketFlow.Domain.Common;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.UnitTests.Domain;

public class TicketTests
{
    [Fact]
    public void Constructor_CreatesAvailableTicket()
    {
        var ticket = new Ticket(Guid.NewGuid());

        Assert.Equal(TicketStatus.Available, ticket.Status);
        Assert.Null(ticket.ReservationId);
    }

    [Fact]
    public void Reserve_WhenAvailable_MovesToReservedAndLinksReservation()
    {
        var ticket = new Ticket(Guid.NewGuid());
        var reservationId = Guid.NewGuid();

        ticket.Reserve(reservationId);

        Assert.Equal(TicketStatus.Reserved, ticket.Status);
        Assert.Equal(reservationId, ticket.ReservationId);
    }

    // Cenário crítico da documentação: "ingresso indisponível não pode ser reservado".
    [Fact]
    public void Reserve_WhenNotAvailable_ThrowsDomainException()
    {
        var ticket = new Ticket(Guid.NewGuid());
        ticket.Reserve(Guid.NewGuid());

        var ex = Assert.Throws<DomainException>(() => ticket.Reserve(Guid.NewGuid()));

        Assert.Equal("TICKET_UNAVAILABLE", ex.Code);
    }

    [Fact]
    public void Confirm_WhenReserved_MovesToSold()
    {
        var ticket = new Ticket(Guid.NewGuid());
        ticket.Reserve(Guid.NewGuid());

        ticket.Confirm();

        Assert.Equal(TicketStatus.Sold, ticket.Status);
    }

    // Cenário crítico da documentação: "reserva confirmada não pode ser reutilizada".
    [Fact]
    public void Confirm_WhenNotReserved_ThrowsDomainException()
    {
        var ticket = new Ticket(Guid.NewGuid());

        var ex = Assert.Throws<DomainException>(() => ticket.Confirm());

        Assert.Equal("TICKET_NOT_RESERVED", ex.Code);
    }

    // Cenário crítico da documentação: "reserva expirada libera disponibilidade".
    [Fact]
    public void Release_WhenReserved_MovesBackToAvailableAndClearsReservation()
    {
        var ticket = new Ticket(Guid.NewGuid());
        ticket.Reserve(Guid.NewGuid());

        ticket.Release();

        Assert.Equal(TicketStatus.Available, ticket.Status);
        Assert.Null(ticket.ReservationId);
    }

    [Fact]
    public void Release_WhenNotReserved_ThrowsDomainException()
    {
        var ticket = new Ticket(Guid.NewGuid());

        var ex = Assert.Throws<DomainException>(() => ticket.Release());

        Assert.Equal("TICKET_NOT_RESERVED", ex.Code);
    }

    [Fact]
    public void Cancel_WhenNotAlreadyCancelled_MovesToCancelledAndClearsReservation()
    {
        var ticket = new Ticket(Guid.NewGuid());
        ticket.Reserve(Guid.NewGuid());

        ticket.Cancel();

        Assert.Equal(TicketStatus.Cancelled, ticket.Status);
        Assert.Null(ticket.ReservationId);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsDomainException()
    {
        var ticket = new Ticket(Guid.NewGuid());
        ticket.Cancel();

        var ex = Assert.Throws<DomainException>(() => ticket.Cancel());

        Assert.Equal("TICKET_ALREADY_CANCELLED", ex.Code);
    }
}
