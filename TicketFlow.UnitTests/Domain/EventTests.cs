using TicketFlow.Domain.Common;
using TicketFlow.Domain.Events;

namespace TicketFlow.UnitTests.Domain;

public class EventTests
{
    private static Event CreateValidEvent(DateTime? date = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Rock Festival 2027", date ?? DateTime.UtcNow.AddDays(30));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithoutName_ThrowsDomainException(string? name)
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Event(Guid.NewGuid(), Guid.NewGuid(), name!, DateTime.UtcNow.AddDays(1)));

        Assert.Equal("EVENT_NAME_REQUIRED", ex.Code);
    }

    [Fact]
    public void Constructor_WithPastDate_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Event(Guid.NewGuid(), Guid.NewGuid(), "Rock Festival", DateTime.UtcNow.AddDays(-1)));

        Assert.Equal("EVENT_DATE_INVALID", ex.Code);
    }

    [Fact]
    public void Constructor_WithValidData_StartsAsDraft()
    {
        var @event = CreateValidEvent();

        Assert.Equal(EventStatus.Draft, @event.Status);
    }

    [Fact]
    public void Publish_FromDraft_MovesToPublished()
    {
        var @event = CreateValidEvent();

        @event.Publish();

        Assert.Equal(EventStatus.Published, @event.Status);
    }

    [Fact]
    public void Publish_WhenAlreadyPublished_ThrowsDomainException()
    {
        var @event = CreateValidEvent();
        @event.Publish();

        var ex = Assert.Throws<DomainException>(() => @event.Publish());

        Assert.Equal("EVENT_CANNOT_PUBLISH", ex.Code);
    }

    [Fact]
    public void Close_FromPublished_MovesToClosed()
    {
        var @event = CreateValidEvent();
        @event.Publish();

        @event.Close();

        Assert.Equal(EventStatus.Closed, @event.Status);
    }

    [Fact]
    public void Close_FromDraft_ThrowsDomainException()
    {
        var @event = CreateValidEvent();

        var ex = Assert.Throws<DomainException>(() => @event.Close());

        Assert.Equal("EVENT_CANNOT_CLOSE", ex.Code);
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public void Cancel_WhenClosedOrCancelled_ThrowsDomainException(EventStatus status)
    {
        var @event = CreateValidEvent();
        @event.Publish();
        if (status == EventStatus.Closed)
            @event.Close();
        else
            @event.Cancel();

        var ex = Assert.Throws<DomainException>(() => @event.Cancel());

        Assert.Equal("EVENT_CANNOT_CANCEL", ex.Code);
    }

    // Cenário crítico da documentação: "evento encerrado não aceita novas compras".
    [Fact]
    public void AcceptsPurchases_OnlyWhenPublished()
    {
        var @event = CreateValidEvent();
        Assert.False(@event.AcceptsPurchases());

        @event.Publish();
        Assert.True(@event.AcceptsPurchases());

        @event.Close();
        Assert.False(@event.AcceptsPurchases());
    }
}
