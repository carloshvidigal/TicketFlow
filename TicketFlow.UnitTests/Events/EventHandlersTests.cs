using TicketFlow.Application.Common;
using TicketFlow.Application.Events;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Events;
using TicketFlow.UnitTests.Support;

namespace TicketFlow.UnitTests.Events;

public class CreateEventHandlerTests
{
    private readonly EventTestContext _ctx = new();

    private Task<EventSummary> CreateAsync(Actor actor, string name = "Rock Festival 2027") =>
        _ctx.CreateEvent.HandleAsync(
            new CreateEventCommand(actor, name, "Arena XYZ", EventTestContext.InTheFuture), CancellationToken.None);

    [Fact]
    public async Task Organizer_CreatesADraftOwnedByThemselves()
    {
        var created = await CreateAsync(_ctx.Owner);

        var saved = Assert.Single(_ctx.Repository.Events);
        Assert.Equal(created.Id, saved.Id);
        Assert.Equal(_ctx.Owner.UserId, saved.OrganizerId);
        Assert.Equal(EventStatus.Draft, saved.Status);
        Assert.Equal(EventStatus.Draft, created.Status);
    }

    [Fact]
    public async Task Admin_CanCreateEventsToo_AndBecomesTheirOrganizer()
    {
        await CreateAsync(_ctx.Admin);

        Assert.Equal(_ctx.Admin.UserId, Assert.Single(_ctx.Repository.Events).OrganizerId);
    }

    // Cenário da documentação: um customer não pode criar eventos.
    [Fact]
    public async Task Customer_CannotCreateEvents()
    {
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => CreateAsync(_ctx.Customer));

        Assert.Equal("FORBIDDEN", ex.Code);
        Assert.Empty(_ctx.Repository.Events);
    }

    [Fact]
    public async Task InvalidData_ThrowsDomainExceptionAndSavesNothing()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateAsync(_ctx.Owner, name: " "));

        Assert.Empty(_ctx.Repository.Events);
    }
}

public class UpdateEventHandlerTests
{
    private readonly EventTestContext _ctx = new();

    private Task<EventSummary> UpdateAsync(Actor actor, Guid eventId, string name = "Novo nome") =>
        _ctx.UpdateEvent.HandleAsync(
            new UpdateEventCommand(actor, eventId, name, "Novo local", DateTime.UtcNow.AddDays(60)), CancellationToken.None);

    [Fact]
    public async Task Owner_UpdatesTheirDraft()
    {
        var @event = _ctx.AddEvent();

        var updated = await UpdateAsync(_ctx.Owner, @event.Id);

        Assert.Equal("Novo nome", updated.Name);
        Assert.Equal("Novo local", @event.Location);
    }

    [Fact]
    public async Task Admin_UpdatesAnyDraft()
    {
        var @event = _ctx.AddEvent();

        await UpdateAsync(_ctx.Admin, @event.Id);

        Assert.Equal("Novo nome", @event.Name);
    }

    // Um rascunho alheio é invisível: 404, não 403 — não revela que existe.
    [Theory]
    [InlineData("OtherOrganizer")]
    [InlineData("Customer")]
    public async Task SomeoneElse_CannotEvenSeeAnotherOrganizersDraft_GetsNotFound(string who)
    {
        var @event = _ctx.AddEvent();
        var actor = who == "Customer" ? _ctx.Customer : _ctx.OtherOrganizer;

        await Assert.ThrowsAsync<NotFoundException>(() => UpdateAsync(actor, @event.Id));

        Assert.Equal("Rock Festival 2027", @event.Name);
    }

    [Fact]
    public async Task AnUnknownEvent_IsNotFound()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => UpdateAsync(_ctx.Owner, Guid.NewGuid()));

        Assert.Equal("EVENT_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task AnEventThatIsAlreadyPublished_CannotBeEdited()
    {
        var @event = _ctx.AddEvent(EventStatus.Published);

        var ex = await Assert.ThrowsAsync<DomainException>(() => UpdateAsync(_ctx.Owner, @event.Id));

        Assert.Equal("EVENT_NOT_EDITABLE", ex.Code);
    }

    [Fact]
    public async Task WhenAnotherRequestChangedTheEventFirst_ReportsAConflict()
    {
        var @event = _ctx.AddEvent();
        _ctx.Repository.ThrowConcurrencyOnSave = true;

        var ex = await Assert.ThrowsAsync<EventModifiedException>(() => UpdateAsync(_ctx.Owner, @event.Id));

        Assert.Equal("EVENT_MODIFIED", ex.Code);
    }
}

public class EventLifecycleHandlerTests
{
    private readonly EventTestContext _ctx = new();

    [Fact]
    public async Task Publish_WithASection_PublishesTheEvent()
    {
        var @event = _ctx.AddEvent(EventStatus.Draft);
        await _ctx.AddSection.HandleAsync(
            new AddSectionCommand(_ctx.Owner, @event.Id, "Pista", 10, 100m), CancellationToken.None);

        var published = await _ctx.Lifecycle.PublishAsync(_ctx.Owner, @event.Id, CancellationToken.None);

        Assert.Equal(EventStatus.Published, published.Status);
        Assert.Equal(EventStatus.Published, @event.Status);
    }

    [Fact]
    public async Task Publish_WithoutSections_IsRejected()
    {
        var @event = _ctx.AddEvent(EventStatus.Draft);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _ctx.Lifecycle.PublishAsync(_ctx.Owner, @event.Id, CancellationToken.None));

        Assert.Equal("EVENT_NEEDS_SECTIONS", ex.Code);
        Assert.Equal(EventStatus.Draft, @event.Status);
    }

    [Fact]
    public async Task Close_ClosesAPublishedEvent()
    {
        var @event = _ctx.AddEvent(EventStatus.Published);

        var closed = await _ctx.Lifecycle.CloseAsync(_ctx.Owner, @event.Id, CancellationToken.None);

        Assert.Equal(EventStatus.Closed, closed.Status);
    }

    [Fact]
    public async Task Close_OfADraft_IsRejectedByTheDomain()
    {
        var @event = _ctx.AddEvent(EventStatus.Draft);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _ctx.Lifecycle.CloseAsync(_ctx.Owner, @event.Id, CancellationToken.None));

        Assert.Equal("EVENT_CANNOT_CLOSE", ex.Code);
    }

    [Theory]
    [InlineData(EventStatus.Draft)]
    [InlineData(EventStatus.Published)]
    public async Task Cancel_CancelsADraftOrPublishedEvent(EventStatus status)
    {
        var @event = _ctx.AddEvent(status);

        var cancelled = await _ctx.Lifecycle.CancelAsync(_ctx.Owner, @event.Id, CancellationToken.None);

        Assert.Equal(EventStatus.Cancelled, cancelled.Status);
    }

    // Matriz de permissão sobre um evento PUBLICADO (visível para todos):
    // só o dono e o admin gerenciam; os demais recebem 403.
    [Theory]
    [InlineData("OtherOrganizer")]
    [InlineData("Customer")]
    public async Task OnAPublishedEvent_ThoseWhoCannotManageItGetForbidden(string who)
    {
        var @event = _ctx.AddEvent(EventStatus.Published);
        var actor = who == "Customer" ? _ctx.Customer : _ctx.OtherOrganizer;

        await Assert.ThrowsAsync<ForbiddenException>(() => _ctx.Lifecycle.CloseAsync(actor, @event.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => _ctx.Lifecycle.CancelAsync(actor, @event.Id, CancellationToken.None));

        Assert.Equal(EventStatus.Published, @event.Status);
    }

    [Fact]
    public async Task OnAnotherOrganizersDraft_PublishingIsNotFound()
    {
        var @event = _ctx.AddEvent(EventStatus.Draft);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _ctx.Lifecycle.PublishAsync(_ctx.OtherOrganizer, @event.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Admin_ManagesAnyEvent()
    {
        var @event = _ctx.AddEvent(EventStatus.Published);

        var closed = await _ctx.Lifecycle.CloseAsync(_ctx.Admin, @event.Id, CancellationToken.None);

        Assert.Equal(EventStatus.Closed, closed.Status);
    }

    [Fact]
    public async Task WhenAnotherRequestChangedTheEventFirst_ReportsAConflict()
    {
        var @event = _ctx.AddEvent(EventStatus.Published);
        _ctx.Repository.ThrowConcurrencyOnSave = true;

        await Assert.ThrowsAsync<EventModifiedException>(() =>
            _ctx.Lifecycle.CloseAsync(_ctx.Owner, @event.Id, CancellationToken.None));
    }
}

public class AddSectionHandlerTests
{
    private readonly EventTestContext _ctx = new();

    private Task<SectionAvailability> AddAsync(Actor actor, Guid eventId, int capacity = 5, string name = "Pista") =>
        _ctx.AddSection.HandleAsync(new AddSectionCommand(actor, eventId, name, capacity, 150m), CancellationToken.None);

    [Fact]
    public async Task CreatesTheSectionAndOneAvailableTicketPerUnitOfCapacity()
    {
        var @event = _ctx.AddEvent();

        var section = await AddAsync(_ctx.Owner, @event.Id, capacity: 7);

        Assert.Equal(7, section.Capacity);
        Assert.Equal(7, section.Available);
        var saved = Assert.Single(_ctx.Repository.Sections);
        Assert.Equal(@event.Id, saved.EventId);
        Assert.Equal(7, _ctx.Repository.Tickets.Count(t => t.SectionId == saved.Id));
    }

    [Fact]
    public async Task OnAPublishedEvent_IsRejected_AndCreatesNoTickets()
    {
        var @event = _ctx.AddEvent(EventStatus.Published);
        var ticketsBefore = _ctx.Repository.Tickets.Count;

        var ex = await Assert.ThrowsAsync<DomainException>(() => AddAsync(_ctx.Owner, @event.Id));

        Assert.Equal("EVENT_NOT_EDITABLE", ex.Code);
        Assert.Equal(ticketsBefore, _ctx.Repository.Tickets.Count);
    }

    [Fact]
    public async Task WithInvalidSectionData_ThrowsDomainExceptionAndCreatesNothing()
    {
        var @event = _ctx.AddEvent();

        await Assert.ThrowsAsync<DomainException>(() => AddAsync(_ctx.Owner, @event.Id, capacity: 0));
        await Assert.ThrowsAsync<DomainException>(() => AddAsync(_ctx.Owner, @event.Id, name: " "));

        Assert.Empty(_ctx.Repository.Sections);
        Assert.Empty(_ctx.Repository.Tickets);
    }

    [Fact]
    public async Task OnAnotherOrganizersDraft_IsNotFound_AndOnTheirPublishedEvent_IsForbidden()
    {
        var draft = _ctx.AddEvent();
        var published = _ctx.AddEvent(EventStatus.Published);

        await Assert.ThrowsAsync<NotFoundException>(() => AddAsync(_ctx.OtherOrganizer, draft.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => AddAsync(_ctx.OtherOrganizer, published.Id));
    }

    [Fact]
    public async Task Customer_CannotAddSections()
    {
        var published = _ctx.AddEvent(EventStatus.Published);

        await Assert.ThrowsAsync<ForbiddenException>(() => AddAsync(_ctx.Customer, published.Id));
    }
}

public class EventQueriesTests
{
    private readonly EventTestContext _ctx = new();

    private Task<EventDetail> GetAsync(Actor? actor, Guid id) => _ctx.Queries.GetAsync(actor, id, CancellationToken.None);

    // Matriz de visibilidade: status do evento x quem pergunta.
    // Publicado e encerrado: todos veem. Rascunho e cancelado: só dono e admin.
    [Theory]
    [InlineData(EventStatus.Draft, "Anonymous", false)]
    [InlineData(EventStatus.Draft, "Customer", false)]
    [InlineData(EventStatus.Draft, "OtherOrganizer", false)]
    [InlineData(EventStatus.Draft, "Owner", true)]
    [InlineData(EventStatus.Draft, "Admin", true)]
    [InlineData(EventStatus.Published, "Anonymous", true)]
    [InlineData(EventStatus.Published, "Customer", true)]
    [InlineData(EventStatus.Published, "OtherOrganizer", true)]
    [InlineData(EventStatus.Closed, "Anonymous", true)]
    [InlineData(EventStatus.Closed, "Customer", true)]
    [InlineData(EventStatus.Cancelled, "Anonymous", false)]
    [InlineData(EventStatus.Cancelled, "Customer", false)]
    [InlineData(EventStatus.Cancelled, "OtherOrganizer", false)]
    [InlineData(EventStatus.Cancelled, "Owner", true)]
    [InlineData(EventStatus.Cancelled, "Admin", true)]
    public async Task Get_RespectsTheVisibilityMatrix(EventStatus status, string who, bool visible)
    {
        var @event = _ctx.AddEvent(status);
        Actor? actor = who switch
        {
            "Customer" => _ctx.Customer,
            "OtherOrganizer" => _ctx.OtherOrganizer,
            "Owner" => _ctx.Owner,
            "Admin" => _ctx.Admin,
            _ => null
        };

        if (visible)
            Assert.Equal(@event.Id, (await GetAsync(actor, @event.Id)).Id);
        else
            await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(actor, @event.Id));
    }

    [Fact]
    public async Task Get_ForAnUnknownEvent_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(null, Guid.NewGuid()));
    }

    [Fact]
    public async Task Get_ShowsEachSectionWithItsAvailability()
    {
        var @event = _ctx.AddEvent(EventStatus.Published);
        var section = _ctx.Repository.Sections.Single();
        _ctx.Repository.Tickets.First(t => t.SectionId == section.Id).Reserve(Guid.NewGuid());

        var detail = await GetAsync(null, @event.Id);

        var shown = Assert.Single(detail.Sections);
        Assert.Equal(5, shown.Capacity);
        Assert.Equal(4, shown.Available);
    }

    [Fact]
    public async Task List_UsesTheClockForTheCurrentTime_AndPassesThePaging()
    {
        await _ctx.Queries.ListPublishedAsync(page: 3, pageSize: 10, CancellationToken.None);

        Assert.Equal(_ctx.Clock.Now.UtcDateTime, _ctx.Repository.LastListNow);
        Assert.Equal((3, 10), _ctx.Repository.LastListPaging);
    }

    [Fact]
    public async Task List_OnlyReturnsPublishedEvents()
    {
        _ctx.AddEvent(EventStatus.Draft);
        _ctx.AddEvent(EventStatus.Cancelled);
        _ctx.AddEvent(EventStatus.Closed);
        var published = _ctx.AddEvent(EventStatus.Published);

        var result = await _ctx.Queries.ListPublishedAsync(1, 20, CancellationToken.None);

        Assert.Equal(published.Id, Assert.Single(result.Items).Id);
        Assert.Equal(1, result.TotalCount);
    }
}
