using TicketFlow.Domain.Common;

namespace TicketFlow.Domain.Events;

public class Event
{
    // Teto de setores por evento: junto com Section.MaxCapacity, limita quantas
    // linhas de Ticket um único evento consegue gerar (ADR-0003: um Ticket por
    // unidade de capacidade).
    public const int MaxSections = 20;

    public Guid Id { get; private set; }
    public Guid OrganizerId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    // Texto livre ("Arena XYZ, São Paulo"). Não há entidade Venue no MVP (ADR-0013).
    public string Location { get; private set; } = string.Empty;

    // Sempre em UTC.
    public DateTime Date { get; private set; }
    public EventStatus Status { get; private set; }

    // EF Core precisa de um construtor sem parâmetros (pode ser privado).
    private Event() { }

    public Event(Guid organizerId, string name, string location, DateTime date)
    {
        if (organizerId == Guid.Empty)
            throw new DomainException("EVENT_ORGANIZER_REQUIRED", "Organizer is required.");

        ValidateDetails(name, location, date);

        Id = Guid.NewGuid();
        OrganizerId = organizerId;
        Name = name.Trim();
        Location = location.Trim();
        Date = date;
        Status = EventStatus.Draft;
    }

    // Só enquanto o evento ainda é rascunho: depois de publicado há quem já
    // tenha visto (e, em breve, comprado) com base nesses dados.
    public void Update(string name, string location, DateTime date)
    {
        EnsureEditable();
        ValidateDetails(name, location, date);

        Name = name.Trim();
        Location = location.Trim();
        Date = date;
    }

    // Setores (e, portanto, o estoque de ingressos) só mudam enquanto o evento é rascunho.
    public void EnsureEditable()
    {
        if (Status != EventStatus.Draft)
            throw new DomainException("EVENT_NOT_EDITABLE", $"An event in {Status} status can no longer be edited.");
    }

    public void Publish()
    {
        if (Status != EventStatus.Draft)
            throw new DomainException("EVENT_CANNOT_PUBLISH", $"Cannot publish an event in {Status} status.");

        // O rascunho pode ter ficado parado até a data passar.
        if (Date <= DateTime.UtcNow)
            throw new DomainException("EVENT_DATE_INVALID", "Event date must be in the future.");

        Status = EventStatus.Published;
    }

    public void Close()
    {
        if (Status != EventStatus.Published)
            throw new DomainException("EVENT_CANNOT_CLOSE", $"Cannot close an event in {Status} status.");

        Status = EventStatus.Closed;
    }

    public void Cancel()
    {
        if (Status is EventStatus.Closed or EventStatus.Cancelled)
            throw new DomainException("EVENT_CANNOT_CANCEL", $"Cannot cancel an event in {Status} status.");

        Status = EventStatus.Cancelled;
    }

    // Cenário crítico da documentação: "evento encerrado não aceita novas compras".
    // Um evento publicado cuja data já passou também não vende mais, mesmo que
    // o organizador não tenha chegado a encerrá-lo.
    public bool AcceptsPurchases(DateTime? now = null) =>
        Status == EventStatus.Published && Date > (now ?? DateTime.UtcNow);

    private static void ValidateDetails(string name, string location, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("EVENT_NAME_REQUIRED", "Event name is required.");

        if (string.IsNullOrWhiteSpace(location))
            throw new DomainException("EVENT_LOCATION_REQUIRED", "Event location is required.");

        if (date <= DateTime.UtcNow)
            throw new DomainException("EVENT_DATE_INVALID", "Event date must be in the future.");
    }
}
