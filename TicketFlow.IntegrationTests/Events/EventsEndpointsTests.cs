using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Events;
using TicketFlow.Domain.Tickets;
using TicketFlow.Domain.Users;
using TicketFlow.IntegrationTests.Database;
using TicketFlow.IntegrationTests.Support;

namespace TicketFlow.IntegrationTests.Events;

[Collection(DatabaseCollection.Name)]
public class EventsEndpointsTests(DatabaseFixture fixture)
{
    private readonly HttpClient _anonymous = fixture.CreateApiClient();

    private Task<TestUser> NewUserAsync(UserRole role) => TestUsers.CreateAsync(fixture, role);

    private static Task<string> ErrorCodeAsync(HttpResponseMessage response) => EventsApi.ErrorCodeAsync(response);

    // ---- Criação e permissões --------------------------------------------

    [Fact]
    public async Task Organizer_CreatesADraftEventOwnedByThemselves()
    {
        var organizer = await NewUserAsync(UserRole.Organizer);

        var response = await EventsApi.CreateRawAsync(organizer.CreateClient(fixture));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", body.GetProperty("status").GetString());
        Assert.Equal(organizer.Id, body.GetProperty("organizerId").GetGuid());
        Assert.EndsWith($"/events/{body.GetProperty("id").GetGuid()}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Admin_AlsoCreatesEvents()
    {
        var admin = await NewUserAsync(UserRole.Admin);

        var response = await EventsApi.CreateRawAsync(admin.CreateClient(fixture));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // Cenário da documentação: um customer não pode criar eventos (POST /events).
    [Fact]
    public async Task Customer_CannotCreateEvents_Gets403InTheStandardFormat()
    {
        var customer = await NewUserAsync(UserRole.Customer);

        var response = await EventsApi.CreateRawAsync(customer.CreateClient(fixture), name: "Evento do customer");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("FORBIDDEN", await ErrorCodeAsync(response));

        await using var db = fixture.CreateContext();
        Assert.False(await db.Events.AnyAsync(e => e.Name == "Evento do customer"));
    }

    [Fact]
    public async Task WithoutAToken_ManagementEndpointsReturn401()
    {
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await EventsApi.CreateRawAsync(_anonymous)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _anonymous.PutAsJsonAsync($"/events/{id}", new { name = "x", location = "y", date = DateTimeOffset.UtcNow.AddDays(5) })).StatusCode);
        foreach (var action in new[] { "publish", "close", "cancel" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await EventsApi.PostActionAsync(_anonymous, id, action)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await EventsApi.AddSectionRawAsync(_anonymous, id)).StatusCode);
    }

    [Fact]
    public async Task WithAnExpiredToken_ManagementEndpointsReturnTokenExpired()
    {
        var client = fixture.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", TestTokens.Create(role: "Organizer", expiresAt: DateTime.UtcNow.AddHours(-1)));

        var response = await EventsApi.CreateRawAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("TOKEN_EXPIRED", await ErrorCodeAsync(response));
    }

    // Mass assignment: o corpo não escolhe dono nem status.
    [Fact]
    public async Task CreatingAnEvent_IgnoresOrganizerAndStatusSentInTheBody()
    {
        var organizer = await NewUserAsync(UserRole.Organizer);
        var someoneElse = Guid.NewGuid();

        var response = await organizer.CreateClient(fixture).PostAsJsonAsync("/events", new
        {
            name = "Evento com campos extras",
            location = "Arena XYZ",
            date = DateTimeOffset.UtcNow.AddDays(30),
            organizerId = someoneElse,
            status = "Published",
            id = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(organizer.Id, body.GetProperty("organizerId").GetGuid());
        Assert.Equal("Draft", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task TheDateIsStoredInUtc_EvenWhenSentWithAnotherOffset()
    {
        var organizer = await NewUserAsync(UserRole.Organizer);
        var date = new DateTimeOffset(
            DateTime.SpecifyKind(DateTime.UtcNow.AddDays(40).Date.AddHours(20), DateTimeKind.Unspecified),
            TimeSpan.FromHours(-3));

        var response = await EventsApi.CreateRawAsync(organizer.CreateClient(fixture), date: date);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(date.UtcDateTime, body.GetProperty("date").GetDateTime().ToUniversalTime());
        Assert.EndsWith("Z", body.GetProperty("date").GetString());
    }

    [Theory]
    [InlineData("", "Arena", 30, "name")]
    [InlineData("Show", "", 30, "location")]
    [InlineData("Show", "Arena", -1, "date")]
    public async Task CreatingWithInvalidData_ReturnsValidationErrorPointingAtTheField(
        string name, string location, int daysAhead, string field)
    {
        var organizer = await NewUserAsync(UserRole.Organizer);

        var response = await EventsApi.CreateRawAsync(
            organizer.CreateClient(fixture), name, location, DateTimeOffset.UtcNow.AddDays(daysAhead));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
        Assert.Contains(error.GetProperty("details").EnumerateArray(),
            d => d.GetProperty("field").GetString() == field);
    }

    [Fact]
    public async Task CreatingWithATooLongName_IsRejectedBeforeReachingTheDatabase()
    {
        var organizer = await NewUserAsync(UserRole.Organizer);

        var response = await EventsApi.CreateRawAsync(organizer.CreateClient(fixture), name: new string('x', 201));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Ciclo de vida completo ------------------------------------------

    [Fact]
    public async Task FullLifecycle_CreateAddSectionPublishListGetClose()
    {
        var organizer = await NewUserAsync(UserRole.Organizer);
        var client = organizer.CreateClient(fixture);
        var name = $"Ciclo completo {Guid.NewGuid():N}";

        var id = await EventsApi.CreateAsync(client, name);
        await EventsApi.AddSectionAsync(client, id, "Pista", capacity: 100, price: 150m);
        await EventsApi.AddSectionAsync(client, id, "VIP", capacity: 20, price: 350.50m);

        // Rascunho: ainda não aparece na listagem pública.
        Assert.DoesNotContain(await ListAllNamesAsync(), n => n == name);

        var publish = await EventsApi.PostActionAsync(client, id, "publish");
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        Assert.Equal("Published", (await publish.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        // Publicado: qualquer um vê na listagem e no detalhe, com a disponibilidade.
        Assert.Contains(await ListAllNamesAsync(), n => n == name);

        var detail = await (await _anonymous.GetAsync($"/events/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Published", detail.GetProperty("status").GetString());
        var sections = detail.GetProperty("sections").EnumerateArray().ToList();
        Assert.Equal(2, sections.Count);
        Assert.Equal(["Pista", "VIP"], sections.Select(s => s.GetProperty("name").GetString()));
        Assert.Equal([100, 20], sections.Select(s => s.GetProperty("capacity").GetInt32()));
        Assert.Equal([100, 20], sections.Select(s => s.GetProperty("available").GetInt32()));
        Assert.Equal(350.50m, sections[1].GetProperty("price").GetDecimal());

        // Encerrado: some da listagem, mas o detalhe continua público.
        Assert.Equal(HttpStatusCode.OK, (await EventsApi.PostActionAsync(client, id, "close")).StatusCode);
        Assert.DoesNotContain(await ListAllNamesAsync(), n => n == name);
        var closed = await _anonymous.GetAsync($"/events/{id}");
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.Equal("Closed", (await closed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Publishing_AnEventWithoutSections_IsRejected()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var id = await EventsApi.CreateAsync(client);

        var response = await EventsApi.PostActionAsync(client, id, "publish");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("EVENT_NEEDS_SECTIONS", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task InvalidTransitions_Return422WithTheDomainCode()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var draft = await EventsApi.CreateAsync(client);

        var closeDraft = await EventsApi.PostActionAsync(client, draft, "close");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, closeDraft.StatusCode);
        Assert.Equal("EVENT_CANNOT_CLOSE", await ErrorCodeAsync(closeDraft));

        var published = await EventsApi.CreatePublishedAsync(client);
        var publishAgain = await EventsApi.PostActionAsync(client, published, "publish");
        Assert.Equal("EVENT_CANNOT_PUBLISH", await ErrorCodeAsync(publishAgain));

        await EventsApi.PostActionAsync(client, published, "cancel");
        var cancelAgain = await EventsApi.PostActionAsync(client, published, "cancel");
        Assert.Equal("EVENT_CANNOT_CANCEL", await ErrorCodeAsync(cancelAgain));
    }

    [Fact]
    public async Task Update_ChangesADraft_ButNotAPublishedEvent()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var id = await EventsApi.CreateAsync(client);
        var newDate = DateTimeOffset.UtcNow.AddDays(90);

        var update = await client.PutAsJsonAsync($"/events/{id}", new { name = "Nome novo", location = "Local novo", date = newDate });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var body = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Nome novo", body.GetProperty("name").GetString());
        Assert.Equal("Local novo", body.GetProperty("location").GetString());

        await EventsApi.AddSectionAsync(client, id);
        await EventsApi.PostActionAsync(client, id, "publish");

        var afterPublish = await client.PutAsJsonAsync($"/events/{id}", new { name = "Tarde demais", location = "x", date = newDate });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, afterPublish.StatusCode);
        Assert.Equal("EVENT_NOT_EDITABLE", await ErrorCodeAsync(afterPublish));
    }

    [Fact]
    public async Task Update_WithInvalidData_ReturnsValidationError()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var id = await EventsApi.CreateAsync(client);

        var response = await client.PutAsJsonAsync($"/events/{id}", new { name = "", location = "", date = DateTimeOffset.UtcNow.AddDays(-2) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("error").GetProperty("details").EnumerateArray()
            .Select(d => d.GetProperty("field").GetString()).ToList();
        Assert.Contains("name", fields);
        Assert.Contains("location", fields);
        Assert.Contains("date", fields);
    }

    // ---- Visibilidade e propriedade --------------------------------------

    [Fact]
    public async Task ADraft_IsInvisibleToEveryoneExceptItsOwnerAndAdmins()
    {
        var owner = await NewUserAsync(UserRole.Organizer);
        var id = await EventsApi.CreateAsync(owner.CreateClient(fixture));
        var other = await NewUserAsync(UserRole.Organizer);
        var customer = await NewUserAsync(UserRole.Customer);
        var admin = await NewUserAsync(UserRole.Admin);

        var anonymous = await _anonymous.GetAsync($"/events/{id}");
        Assert.Equal(HttpStatusCode.NotFound, anonymous.StatusCode);
        Assert.Equal("EVENT_NOT_FOUND", await ErrorCodeAsync(anonymous));
        Assert.Equal(HttpStatusCode.NotFound, (await customer.CreateClient(fixture).GetAsync($"/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.CreateClient(fixture).GetAsync($"/events/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await owner.CreateClient(fixture).GetAsync($"/events/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.CreateClient(fixture).GetAsync($"/events/{id}")).StatusCode);
    }

    [Fact]
    public async Task ACancelledEvent_IsHiddenFromThePublic_ButVisibleToItsOwner()
    {
        var owner = await NewUserAsync(UserRole.Organizer);
        var client = owner.CreateClient(fixture);
        var name = $"Cancelado {Guid.NewGuid():N}";
        var id = await EventsApi.CreatePublishedAsync(client, name);

        await EventsApi.PostActionAsync(client, id, "cancel");

        Assert.DoesNotContain(await ListAllNamesAsync(), n => n == name);
        Assert.Equal(HttpStatusCode.NotFound, (await _anonymous.GetAsync($"/events/{id}")).StatusCode);
        var ownerView = await client.GetAsync($"/events/{id}");
        Assert.Equal("Cancelled", (await ownerView.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    // Um organizador gerencia só os próprios eventos (§18 do documento).
    [Fact]
    public async Task AnotherOrganizer_CannotManageSomeoneElsesEvents()
    {
        var owner = await NewUserAsync(UserRole.Organizer);
        var other = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var draft = await EventsApi.CreateAsync(owner.CreateClient(fixture));
        var published = await EventsApi.CreatePublishedAsync(owner.CreateClient(fixture));

        // Rascunho alheio: nem a existência é revelada (404).
        Assert.Equal(HttpStatusCode.NotFound, (await EventsApi.PostActionAsync(other, draft, "publish")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await EventsApi.AddSectionRawAsync(other, draft)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.PutAsJsonAsync($"/events/{draft}", new { name = "x", location = "y", date = DateTimeOffset.UtcNow.AddDays(5) })).StatusCode);

        // Evento público alheio: ele existe e é visível, mas não é seu (403).
        foreach (var action in new[] { "close", "cancel" })
        {
            var response = await EventsApi.PostActionAsync(other, published, action);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("FORBIDDEN", await ErrorCodeAsync(response));
        }

        // Nada mudou.
        var detail = await (await _anonymous.GetAsync($"/events/{published}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Published", detail.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Admin_CanManageAnyEvent()
    {
        var owner = await NewUserAsync(UserRole.Organizer);
        var admin = (await NewUserAsync(UserRole.Admin)).CreateClient(fixture);
        var id = await EventsApi.CreateAsync(owner.CreateClient(fixture));

        Assert.Equal(HttpStatusCode.Created, (await EventsApi.AddSectionRawAsync(admin, id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await EventsApi.PostActionAsync(admin, id, "publish")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await EventsApi.PostActionAsync(admin, id, "cancel")).StatusCode);
    }

    [Fact]
    public async Task AnUnknownEvent_Returns404()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);

        Assert.Equal(HttpStatusCode.NotFound, (await _anonymous.GetAsync($"/events/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await EventsApi.PostActionAsync(client, Guid.NewGuid(), "publish")).StatusCode);
    }

    // Um id malformado numa rota pública é erro de validação no formato padrão (400),
    // não um 401 nem um 404 sem corpo.
    [Fact]
    public async Task AMalformedEventId_Returns400WithTheOffendingField()
    {
        var response = await _anonymous.GetAsync("/events/isto-nao-e-um-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
        Assert.Contains(error.GetProperty("details").EnumerateArray(), d => d.GetProperty("field").GetString() == "id");
    }

    // Efeito da política "segura por padrão": até uma rota que não existe responde
    // 401 para quem não está autenticado, e 404 para quem está.
    [Fact]
    public async Task AnUnknownRoute_Returns401ToAnonymousCallers_And404ToAuthenticatedOnes()
    {
        var user = await NewUserAsync(UserRole.Customer);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.GetAsync("/nao-existe")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.CreateClient(fixture).GetAsync("/nao-existe")).StatusCode);
    }

    // ---- Setores e estoque -----------------------------------------------

    [Fact]
    public async Task AddingASection_GeneratesOneAvailableTicketPerUnitOfCapacity()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var id = await EventsApi.CreateAsync(client);

        var response = await EventsApi.AddSectionRawAsync(client, id, "Pista", capacity: 250, price: 99.90m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(250, body.GetProperty("capacity").GetInt32());
        Assert.Equal(250, body.GetProperty("available").GetInt32());

        await using var db = fixture.CreateContext();
        var sectionId = body.GetProperty("id").GetGuid();
        var tickets = await db.Tickets.Where(t => t.SectionId == sectionId).ToListAsync();
        Assert.Equal(250, tickets.Count);
        Assert.All(tickets, t => Assert.Equal(TicketStatus.Available, t.Status));
    }

    [Fact]
    public async Task TheAvailabilityShownPublicly_FollowsTheTicketStatus()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var id = await EventsApi.CreatePublishedAsync(client, capacity: 10);

        // Simula 3 ingressos reservados e 2 vendidos (a reserva real chega na Fase 4).
        await using (var db = fixture.CreateContext())
        {
            var sectionId = (await db.Sections.SingleAsync(s => s.EventId == id)).Id;
            var tickets = await db.Tickets.Where(t => t.SectionId == sectionId).Take(5).ToListAsync();
            var reserved = tickets.Take(3).Select(t => t.Id).ToArray();
            var sold = tickets.Skip(3).Select(t => t.Id).ToArray();
            await db.Tickets.Where(t => reserved.Contains(t.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Reserved));
            await db.Tickets.Where(t => sold.Contains(t.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Sold));
        }

        var detail = await (await _anonymous.GetAsync($"/events/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        var section = detail.GetProperty("sections")[0];
        Assert.Equal(10, section.GetProperty("capacity").GetInt32());
        Assert.Equal(5, section.GetProperty("available").GetInt32());
    }

    [Fact]
    public async Task AddingASection_ToAPublishedEvent_IsRejectedAndCreatesNoTickets()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var id = await EventsApi.CreatePublishedAsync(client, capacity: 10);

        var response = await EventsApi.AddSectionRawAsync(client, id, "Camarote", capacity: 5);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("EVENT_NOT_EDITABLE", await ErrorCodeAsync(response));
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.Sections.CountAsync(s => s.EventId == id));
        Assert.Equal(10, await db.Tickets.CountAsync(t => db.Sections.Any(s => s.Id == t.SectionId && s.EventId == id)));
    }

    [Theory]
    [InlineData("", 10, 100, "name")]
    [InlineData("Pista", 0, 100, "capacity")]
    [InlineData("Pista", -5, 100, "capacity")]
    [InlineData("Pista", Section.MaxCapacity + 1, 100, "capacity")]
    [InlineData("Pista", 10, -1, "price")]
    [InlineData("Pista", 10, 10.999, "price")]
    [InlineData("Pista", 10, 100000000, "price")]
    public async Task AddingASection_WithInvalidData_ReturnsValidationError(string name, int capacity, double price, string field)
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var id = await EventsApi.CreateAsync(client);

        var response = await EventsApi.AddSectionRawAsync(client, id, name, capacity, (decimal)price);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var details = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("error").GetProperty("details").EnumerateArray();
        Assert.Contains(details, d => d.GetProperty("field").GetString() == field);
    }

    [Fact]
    public async Task AFreeSection_PriceZero_IsAllowed()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var id = await EventsApi.CreateAsync(client);

        Assert.Equal(HttpStatusCode.Created, (await EventsApi.AddSectionRawAsync(client, id, "Cortesia", 5, 0m)).StatusCode);
    }

    [Fact]
    public async Task SectionNames_AreUniquePerEvent_IgnoringCase()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var id = await EventsApi.CreateAsync(client);
        await EventsApi.AddSectionAsync(client, id, "Pista");

        var duplicate = await EventsApi.AddSectionRawAsync(client, id, "  PISTA ");

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("SECTION_NAME_ALREADY_EXISTS", await ErrorCodeAsync(duplicate));

        // O mesmo nome em outro evento é permitido.
        var another = await EventsApi.CreateAsync(client);
        Assert.Equal(HttpStatusCode.Created, (await EventsApi.AddSectionRawAsync(client, another, "Pista")).StatusCode);
    }

    // ---- Concorrência ------------------------------------------------------

    // Várias requisições criando o MESMO nome de setor ao mesmo tempo: a trava
    // do evento serializa as transações, então só uma passa.
    [Fact]
    public async Task AddingTheSameSectionNameInParallel_CreatesExactlyOne()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var id = await EventsApi.CreateAsync(client);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => EventsApi.AddSectionRawAsync(client, id, "Pista", capacity: 5)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.Sections.CountAsync(s => s.EventId == id));
    }

    // O limite de setores também vale sob concorrência: 25 pedidos simultâneos
    // com nomes diferentes resultam em exatamente MaxSections setores.
    [Fact]
    public async Task TheSectionLimit_HoldsEvenWhenManyAreAddedAtTheSameTime()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var id = await EventsApi.CreateAsync(client);

        var responses = await Task.WhenAll(Enumerable.Range(0, Event.MaxSections + 5)
            .Select(i => EventsApi.AddSectionRawAsync(client, id, $"Setor {i}", capacity: 1)));

        Assert.Equal(Event.MaxSections, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var rejected = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();
        Assert.Equal(5, rejected.Count);
        Assert.All(rejected, r => Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode));
        Assert.Equal("EVENT_SECTION_LIMIT_REACHED", await ErrorCodeAsync(rejected[0]));

        await using var db = fixture.CreateContext();
        Assert.Equal(Event.MaxSections, await db.Sections.CountAsync(s => s.EventId == id));
    }

    // Corrida publicar x adicionar setor, reproduzida de forma determinística
    // com dois contextos: quem leu o evento ANTES de o estoque mudar não
    // consegue publicá-lo depois (concorrência otimista, xmin).
    [Fact]
    public async Task APublishBasedOnStaleData_FailsWhenASectionWasAddedInTheMeantime()
    {
        var organizer = await NewUserAsync(UserRole.Organizer);
        var id = await EventsApi.CreateAsync(organizer.CreateClient(fixture));

        await using var contextA = fixture.CreateContext();
        var repositoryA = new TicketFlow.Infrastructure.Events.EventRepository(contextA);
        var staleEvent = (await repositoryA.FindByIdAsync(id, CancellationToken.None))!;

        // Enquanto A decidia, outra requisição adicionou um setor.
        await EventsApi.AddSectionAsync(organizer.CreateClient(fixture), id);

        staleEvent.Publish();
        await Assert.ThrowsAsync<TicketFlow.Application.Common.ConcurrentUpdateException>(
            () => repositoryA.SaveChangesAsync(CancellationToken.None));

        var detail = await (await organizer.CreateClient(fixture).GetAsync($"/events/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", detail.GetProperty("status").GetString());
    }

    // O outro lado da corrida: quem decidiu adicionar o setor com o evento
    // ainda em rascunho não consegue gravá-lo se ele foi publicado antes.
    [Fact]
    public async Task AddingASection_BasedOnStaleData_FailsWhenTheEventWasPublishedInTheMeantime()
    {
        var organizer = await NewUserAsync(UserRole.Organizer);
        var client = organizer.CreateClient(fixture);
        var id = await EventsApi.CreateAsync(client);
        await EventsApi.AddSectionAsync(client, id, "Pista");

        await using var contextA = fixture.CreateContext();
        var repositoryA = new TicketFlow.Infrastructure.Events.EventRepository(contextA);
        var staleEvent = (await repositoryA.FindByIdAsync(id, CancellationToken.None))!;
        staleEvent.EnsureEditable(); // A ainda o vê como rascunho

        await EventsApi.PostActionAsync(client, id, "publish");

        var section = new Section(id, "Camarote", 5, 500m);
        var ex = await Assert.ThrowsAsync<TicketFlow.Domain.Common.DomainException>(() =>
            repositoryA.AddSectionAsync(id, section, section.GenerateTickets(), CancellationToken.None));

        Assert.Equal("EVENT_NOT_EDITABLE", ex.Code);
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.Sections.CountAsync(s => s.EventId == id));
    }

    // ---- Listagem pública e paginação ------------------------------------

    [Fact]
    public async Task List_IsPaginatedOrderedByDateAndStable_WithoutDuplicates()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var prefix = $"Paginação {Guid.NewGuid():N}";
        var baseDate = DateTimeOffset.UtcNow.AddDays(500);

        // Criados fora de ordem de propósito.
        foreach (var offset in new[] { 3, 1, 4, 0, 2 })
            await EventsApi.CreatePublishedAsync(client, $"{prefix} #{offset}", baseDate.AddDays(offset));

        var collected = new List<(string Name, DateTime Date)>();
        for (var page = 1; ; page++)
        {
            var response = await _anonymous.GetAsync($"/events?page={page}&pageSize=2");
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(page, body.GetProperty("page").GetInt32());
            Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
            Assert.True(body.GetProperty("totalCount").GetInt32() >= 5);

            var items = body.GetProperty("items").EnumerateArray().ToList();
            Assert.True(items.Count <= 2);
            collected.AddRange(items.Select(i => (i.GetProperty("name").GetString()!, i.GetProperty("date").GetDateTime())));

            if (items.Count < 2 || collected.Count >= body.GetProperty("totalCount").GetInt32())
                break;
        }

        // Sem repetição entre páginas e na ordem global de data.
        Assert.Equal(collected.Count, collected.Select(c => (c.Name, c.Date)).Distinct().Count());
        Assert.Equal(collected.OrderBy(c => c.Date).Select(c => c.Date), collected.Select(c => c.Date));
        Assert.Equal(
            [$"{prefix} #0", $"{prefix} #1", $"{prefix} #2", $"{prefix} #3", $"{prefix} #4"],
            collected.Where(c => c.Name.StartsWith(prefix)).Select(c => c.Name));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=100001")]
    [InlineData("page=abc")]
    public async Task List_WithInvalidPaging_ReturnsValidationError(string query)
    {
        var response = await _anonymous.GetAsync($"/events?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task List_WithoutParameters_UsesTheDefaults()
    {
        var body = await (await _anonymous.GetAsync("/events")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task APublishedEventWhoseDateHasPassed_NoLongerAppearsInTheList()
    {
        var client = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);
        var name = $"Já aconteceu {Guid.NewGuid():N}";
        var id = await EventsApi.CreatePublishedAsync(client, name);

        await using (var db = fixture.CreateContext())
            await db.Events.Where(e => e.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Date, DateTime.UtcNow.AddHours(-1)));

        Assert.DoesNotContain(await ListAllNamesAsync(), n => n == name);
        Assert.Equal(HttpStatusCode.OK, (await _anonymous.GetAsync($"/events/{id}")).StatusCode);
    }

    // ---- Documentação interativa -------------------------------------------

    [Fact]
    public async Task TheOpenApiDocument_DescribesTheEventsEndpointsAndTheBearerScheme()
    {
        var response = await _anonymous.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/events", out _));
        Assert.True(paths.TryGetProperty("/events/{id}/sections", out _));
        Assert.True(paths.TryGetProperty("/auth/login", out _));
        Assert.True(paths.TryGetProperty("/me", out _));
        Assert.Equal("bearer",
            document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
    }

    [Fact]
    public async Task OnlyTheEndpointsThatNeedAToken_AreMarkedAsSecuredInTheDocument()
    {
        var document = await (await _anonymous.GetAsync("/openapi/v1.json")).Content.ReadFromJsonAsync<JsonElement>();
        var paths = document.GetProperty("paths");

        static bool IsSecured(JsonElement operation) =>
            operation.TryGetProperty("security", out var security) && security.GetArrayLength() > 0;

        Assert.False(IsSecured(paths.GetProperty("/events").GetProperty("get")));
        Assert.False(IsSecured(paths.GetProperty("/auth/login").GetProperty("post")));
        Assert.True(IsSecured(paths.GetProperty("/events").GetProperty("post")));
        Assert.True(IsSecured(paths.GetProperty("/me").GetProperty("get")));
    }

    // ---- Auxiliares --------------------------------------------------------

    // Percorre todas as páginas da listagem pública e devolve os nomes.
    private async Task<List<string>> ListAllNamesAsync()
    {
        var names = new List<string>();
        for (var page = 1; ; page++)
        {
            var body = await (await _anonymous.GetAsync($"/events?page={page}&pageSize=100"))
                .Content.ReadFromJsonAsync<JsonElement>();
            var items = body.GetProperty("items").EnumerateArray().ToList();
            names.AddRange(items.Select(i => i.GetProperty("name").GetString()!));

            if (items.Count < 100)
                return names;
        }
    }
}
