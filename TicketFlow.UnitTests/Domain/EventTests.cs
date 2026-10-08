using TicketFlow.Domain.Common;
using TicketFlow.Domain.Events;

namespace TicketFlow.UnitTests.Domain;

public class EventTests
{
    private static Event CreateValidEvent(DateTime? date = null) =>
        new(Guid.NewGuid(), "Rock Festival 2027", "Arena XYZ, São Paulo", date ?? DateTime.UtcNow.AddDays(30));

    [Fact]
    public void Constructor_WithoutOrganizer_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Event(Guid.Empty, "Rock Festival", "Arena XYZ", DateTime.UtcNow.AddDays(1)));

        Assert.Equal("EVENT_ORGANIZER_REQUIRED", ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithoutName_ThrowsDomainException(string? name)
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Event(Guid.NewGuid(), name!, "Arena XYZ", DateTime.UtcNow.AddDays(1)));

        Assert.Equal("EVENT_NAME_REQUIRED", ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithoutLocation_ThrowsDomainException(string? location)
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Event(Guid.NewGuid(), "Rock Festival", location!, DateTime.UtcNow.AddDays(1)));

        Assert.Equal("EVENT_LOCATION_REQUIRED", ex.Code);
    }

    [Fact]
    public void Constructor_WithPastDate_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Event(Guid.NewGuid(), "Rock Festival", "Arena XYZ", DateTime.UtcNow.AddDays(-1)));

        Assert.Equal("EVENT_DATE_INVALID", ex.Code);
    }

    [Fact]
    public void Constructor_WithValidData_StartsAsDraftAndTrimsTexts()
    {
        var organizerId = Guid.NewGuid();

        var @event = new Event(organizerId, "  Rock Festival 2027 ", " Arena XYZ ", DateTime.UtcNow.AddDays(30));

        Assert.Equal(EventStatus.Draft, @event.Status);
        Assert.Equal(organizerId, @event.OrganizerId);
        Assert.Equal("Rock Festival 2027", @event.Name);
        Assert.Equal("Arena XYZ", @event.Location);
    }

    [Fact]
    public void Update_WhenDraft_ChangesNameLocationAndDate()
    {
        var @event = CreateValidEvent();
        var newDate = DateTime.UtcNow.AddDays(90);

        @event.Update("Rock Festival 2028", "Estádio ABC", newDate);

        Assert.Equal("Rock Festival 2028", @event.Name);
        Assert.Equal("Estádio ABC", @event.Location);
        Assert.Equal(newDate, @event.Date);
        Assert.Equal(EventStatus.Draft, @event.Status);
    }

    [Fact]
    public void Update_WithInvalidData_ThrowsAndKeepsTheOldValues()
    {
        var @event = CreateValidEvent();
        var originalDate = @event.Date;

        Assert.Equal("EVENT_NAME_REQUIRED",
            Assert.Throws<DomainException>(() => @event.Update(" ", "Estádio ABC", DateTime.UtcNow.AddDays(90))).Code);
        Assert.Equal("EVENT_LOCATION_REQUIRED",
            Assert.Throws<DomainException>(() => @event.Update("Novo nome", "", DateTime.UtcNow.AddDays(90))).Code);
        Assert.Equal("EVENT_DATE_INVALID",
            Assert.Throws<DomainException>(() => @event.Update("Novo nome", "Estádio ABC", DateTime.UtcNow.AddDays(-1))).Code);

        Assert.Equal("Rock Festival 2027", @event.Name);
        Assert.Equal("Arena XYZ, São Paulo", @event.Location);
        Assert.Equal(originalDate, @event.Date);
    }

    [Theory]
    [InlineData(EventStatus.Published)]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public void Update_WhenNotDraft_ThrowsDomainException(EventStatus status)
    {
        var @event = InStatus(status);

        var ex = Assert.Throws<DomainException>(() =>
            @event.Update("Outro nome", "Outro local", DateTime.UtcNow.AddDays(90)));

        Assert.Equal("EVENT_NOT_EDITABLE", ex.Code);
    }

    [Fact]
    public void EnsureEditable_OnlyPassesWhileDraft()
    {
        var draft = CreateValidEvent();
        draft.EnsureEditable();

        draft.Publish();

        Assert.Equal("EVENT_NOT_EDITABLE", Assert.Throws<DomainException>(() => draft.EnsureEditable()).Code);
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

    // O rascunho pode ter ficado parado até a data passar.
    [Fact]
    public void Publish_WhenTheDateAlreadyPassed_ThrowsDomainException()
    {
        var @event = CreateValidEvent(DateTime.UtcNow.AddMilliseconds(50));
        Thread.Sleep(100);

        var ex = Assert.Throws<DomainException>(() => @event.Publish());

        Assert.Equal("EVENT_DATE_INVALID", ex.Code);
        Assert.Equal(EventStatus.Draft, @event.Status);
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
    [InlineData(EventStatus.Draft)]
    [InlineData(EventStatus.Published)]
    public void Cancel_FromDraftOrPublished_MovesToCancelled(EventStatus status)
    {
        var @event = InStatus(status);

        @event.Cancel();

        Assert.Equal(EventStatus.Cancelled, @event.Status);
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public void Cancel_WhenClosedOrCancelled_ThrowsDomainException(EventStatus status)
    {
        var @event = InStatus(status);

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

    [Fact]
    public void AcceptsPurchases_IsFalseOnceTheEventDateHasPassed_EvenIfStillPublished()
    {
        var @event = CreateValidEvent(DateTime.UtcNow.AddDays(10));
        @event.Publish();

        Assert.True(@event.AcceptsPurchases(DateTime.UtcNow.AddDays(9)));
        Assert.False(@event.AcceptsPurchases(DateTime.UtcNow.AddDays(11)));
    }

    private static Event InStatus(EventStatus status)
    {
        var @event = CreateValidEvent();

        switch (status)
        {
            case EventStatus.Published:
                @event.Publish();
                break;
            case EventStatus.Closed:
                @event.Publish();
                @event.Close();
                break;
            case EventStatus.Cancelled:
                @event.Cancel();
                break;
        }

        return @event;
    }
}
