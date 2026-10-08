using FluentValidation;
using TicketFlow.Application.Events;
using TicketFlow.Domain.Events;

namespace TicketFlow.Api.Contracts.Events;

public interface IEventDetailsRequest
{
    string Name { get; }
    string Location { get; }
    DateTimeOffset Date { get; }
}

// A data chega com fuso (DateTimeOffset) e é convertida para UTC antes de
// chegar ao domínio, que trabalha sempre em UTC.
public record CreateEventRequest(string Name, string Location, DateTimeOffset Date) : IEventDetailsRequest;

public record UpdateEventRequest(string Name, string Location, DateTimeOffset Date) : IEventDetailsRequest;

public record CreateSectionRequest(string Name, int Capacity, decimal Price);

public abstract class EventDetailsValidator<T> : AbstractValidator<T> where T : IEventDetailsRequest
{
    protected EventDetailsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Date)
            .Must(date => date.UtcDateTime > DateTime.UtcNow)
            .WithMessage("'Date' must be in the future.");
    }
}

public class CreateEventRequestValidator : EventDetailsValidator<CreateEventRequest>;

public class UpdateEventRequestValidator : EventDetailsValidator<UpdateEventRequest>;

public class CreateSectionRequestValidator : AbstractValidator<CreateSectionRequest>
{
    public CreateSectionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Capacity).InclusiveBetween(1, Section.MaxCapacity);

        // numeric(10,2) no banco: até 99.999.999,99.
        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(10, 2, ignoreTrailingZeros: true);
    }
}

public record EventResponse(Guid Id, Guid OrganizerId, string Name, string Location, DateTime Date, string Status)
{
    public static EventResponse From(EventSummary e) =>
        new(e.Id, e.OrganizerId, e.Name, e.Location, e.Date, e.Status.ToString());
}

public record SectionResponse(Guid Id, string Name, int Capacity, decimal Price, int Available)
{
    public static SectionResponse From(SectionAvailability s) =>
        new(s.Id, s.Name, s.Capacity, s.Price, s.Available);
}

public record EventDetailResponse(
    Guid Id,
    Guid OrganizerId,
    string Name,
    string Location,
    DateTime Date,
    string Status,
    IReadOnlyList<SectionResponse> Sections)
{
    public static EventDetailResponse From(EventDetail e) => new(
        e.Id, e.OrganizerId, e.Name, e.Location, e.Date, e.Status.ToString(),
        e.Sections.Select(SectionResponse.From).ToList());
}
