using TicketFlow.Domain.Common;
using TicketFlow.Domain.Reservations;

namespace TicketFlow.UnitTests.Domain;

public class ReservationTests
{
    [Fact]
    public void Constructor_WithoutCustomer_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Reservation(Guid.Empty, TimeSpan.FromMinutes(10)));

        Assert.Equal("RESERVATION_CUSTOMER_REQUIRED", ex.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveDuration_ThrowsDomainException(int minutes)
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Reservation(Guid.NewGuid(), TimeSpan.FromMinutes(minutes)));

        Assert.Equal("RESERVATION_DURATION_INVALID", ex.Code);
    }

    [Fact]
    public void Constructor_WithValidData_StartsActiveAndSetsExpiration()
    {
        var before = DateTime.UtcNow;
        var reservation = new Reservation(Guid.NewGuid(), TimeSpan.FromMinutes(10));
        var after = DateTime.UtcNow;

        Assert.Equal(ReservationStatus.Active, reservation.Status);
        Assert.InRange(reservation.ExpiresAt, before.AddMinutes(10), after.AddMinutes(10));
    }

    // Cenário crítico da documentação: "reserva expirada libera disponibilidade".
    [Fact]
    public void IsExpired_WhenPastExpiresAt_ReturnsTrue()
    {
        var reservation = new Reservation(Guid.NewGuid(), TimeSpan.FromMinutes(10));

        Assert.False(reservation.IsExpired(DateTime.UtcNow));
        Assert.True(reservation.IsExpired(DateTime.UtcNow.AddMinutes(11)));
    }

    [Fact]
    public void Confirm_WhenActive_MovesToConfirmed()
    {
        var reservation = new Reservation(Guid.NewGuid(), TimeSpan.FromMinutes(10));

        reservation.Confirm();

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public void Confirm_WhenNotActive_ThrowsDomainException()
    {
        var reservation = new Reservation(Guid.NewGuid(), TimeSpan.FromMinutes(10));
        reservation.Confirm();

        var ex = Assert.Throws<DomainException>(() => reservation.Confirm());

        Assert.Equal("RESERVATION_CANNOT_CONFIRM", ex.Code);
    }

    [Fact]
    public void Expire_WhenActive_MovesToExpired()
    {
        var reservation = new Reservation(Guid.NewGuid(), TimeSpan.FromMinutes(10));

        reservation.Expire();

        Assert.Equal(ReservationStatus.Expired, reservation.Status);
    }

    [Fact]
    public void Expire_WhenNotActive_ThrowsDomainException()
    {
        var reservation = new Reservation(Guid.NewGuid(), TimeSpan.FromMinutes(10));
        reservation.Cancel();

        var ex = Assert.Throws<DomainException>(() => reservation.Expire());

        Assert.Equal("RESERVATION_CANNOT_EXPIRE", ex.Code);
    }

    [Fact]
    public void Cancel_WhenActive_MovesToCancelled()
    {
        var reservation = new Reservation(Guid.NewGuid(), TimeSpan.FromMinutes(10));

        reservation.Cancel();

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
    }

    [Fact]
    public void Cancel_WhenNotActive_ThrowsDomainException()
    {
        var reservation = new Reservation(Guid.NewGuid(), TimeSpan.FromMinutes(10));
        reservation.Expire();

        var ex = Assert.Throws<DomainException>(() => reservation.Cancel());

        Assert.Equal("RESERVATION_CANNOT_CANCEL", ex.Code);
    }
}
