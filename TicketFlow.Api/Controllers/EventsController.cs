using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Api.Authentication;
using TicketFlow.Api.Contracts.Common;
using TicketFlow.Api.Contracts.Events;
using TicketFlow.Api.Errors;
using TicketFlow.Application.Events;

namespace TicketFlow.Api.Controllers;

[ApiController]
[Route("events")]
public class EventsController(
    IValidator<CreateEventRequest> createValidator,
    IValidator<UpdateEventRequest> updateValidator,
    IValidator<CreateSectionRequest> sectionValidator,
    IValidator<PageQuery> pageValidator,
    CreateEventHandler createEvent,
    UpdateEventHandler updateEvent,
    EventLifecycleHandler lifecycle,
    AddSectionHandler addSection,
    EventQueries queries) : ControllerBase
{
    // ---- Leitura pública --------------------------------------------------

    /// <summary>Lista os eventos publicados que ainda vão acontecer, do mais próximo ao mais distante.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResponse<EventResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] PageQuery query, CancellationToken cancellationToken)
    {
        await pageValidator.ValidateAndThrowAsync(query, cancellationToken);

        var result = await queries.ListPublishedAsync(query.Page, query.PageSize, cancellationToken);

        return Ok(new PagedResponse<EventResponse>(
            result.Items.Select(EventResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    /// <summary>Detalhe de um evento com a disponibilidade de ingressos de cada setor.</summary>
    /// <remarks>Rascunhos e eventos cancelados só aparecem para o organizador dono e para administradores.</remarks>
    [HttpGet("{id}", Name = nameof(Get))]
    [AllowAnonymous]
    [ProducesResponseType<EventDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var detail = await queries.GetAsync(User.ToActorOrNull(), id, cancellationToken);

        return Ok(EventDetailResponse.From(detail));
    }

    // ---- Gestão (organizador dono ou administrador) -----------------------

    /// <summary>Cria um evento em rascunho. O organizador é o usuário autenticado.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.OrganizerOrAdmin)]
    [ProducesResponseType<EventResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(CreateEventRequest request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var created = await createEvent.HandleAsync(
            new CreateEventCommand(User.ToActor(), request.Name, request.Location, request.Date.UtcDateTime),
            cancellationToken);

        return CreatedAtRoute(nameof(Get), new { id = created.Id }, EventResponse.From(created));
    }

    /// <summary>Altera nome, local e data. Só enquanto o evento é rascunho.</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = Policies.OrganizerOrAdmin)]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, UpdateEventRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var updated = await updateEvent.HandleAsync(
            new UpdateEventCommand(User.ToActor(), id, request.Name, request.Location, request.Date.UtcDateTime),
            cancellationToken);

        return Ok(EventResponse.From(updated));
    }

    /// <summary>Publica o evento (precisa de ao menos um setor). A partir daqui ele aparece na listagem pública.</summary>
    [HttpPost("{id}/publish")]
    [Authorize(Policy = Policies.OrganizerOrAdmin)]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken) =>
        Ok(EventResponse.From(await lifecycle.PublishAsync(User.ToActor(), id, cancellationToken)));

    /// <summary>Encerra as vendas de um evento publicado.</summary>
    [HttpPost("{id}/close")]
    [Authorize(Policy = Policies.OrganizerOrAdmin)]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken) =>
        Ok(EventResponse.From(await lifecycle.CloseAsync(User.ToActor(), id, cancellationToken)));

    /// <summary>Cancela um evento em rascunho ou publicado.</summary>
    [HttpPost("{id}/cancel")]
    [Authorize(Policy = Policies.OrganizerOrAdmin)]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok(EventResponse.From(await lifecycle.CancelAsync(User.ToActor(), id, cancellationToken)));

    /// <summary>Cria um setor e gera um ingresso para cada unidade de capacidade. Só enquanto o evento é rascunho.</summary>
    [HttpPost("{id}/sections")]
    [Authorize(Policy = Policies.OrganizerOrAdmin)]
    [ProducesResponseType<SectionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddSection(Guid id, CreateSectionRequest request, CancellationToken cancellationToken)
    {
        await sectionValidator.ValidateAndThrowAsync(request, cancellationToken);

        var section = await addSection.HandleAsync(
            new AddSectionCommand(User.ToActor(), id, request.Name, request.Capacity, request.Price),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, SectionResponse.From(section));
    }
}
