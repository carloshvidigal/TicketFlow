using TicketFlow.Domain.Common;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.Domain.Events;

public class Section
{
    // Cada unidade de capacidade vira uma linha de Ticket (ADR-0003); o teto evita
    // que um único pedido de criação gere milhões de linhas.
    public const int MaxCapacity = 10_000;

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Capacity { get; private set; }
    public decimal Price { get; private set; }

    private Section() { }

    public Section(Guid eventId, string name, int capacity, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("SECTION_NAME_REQUIRED", "Section name is required.");

        if (capacity <= 0 || capacity > MaxCapacity)
            throw new DomainException("SECTION_CAPACITY_INVALID", $"Capacity must be between 1 and {MaxCapacity}.");

        if (price < 0)
            throw new DomainException("SECTION_PRICE_INVALID", "Price cannot be negative.");

        Id = Guid.NewGuid();
        EventId = eventId;
        Name = name.Trim();
        Capacity = capacity;
        Price = price;
    }

    // Cria as unidades de estoque (tickets) desse setor — a decisão
    // "ticket-per-unit" fechada na modelagem de domínio.
    public List<Ticket> GenerateTickets()
    {
        var tickets = new List<Ticket>(Capacity);
        for (var i = 0; i < Capacity; i++)
            tickets.Add(new Ticket(Id));

        return tickets;
    }
}
