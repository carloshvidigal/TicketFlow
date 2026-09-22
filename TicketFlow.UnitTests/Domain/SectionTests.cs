using TicketFlow.Domain.Common;
using TicketFlow.Domain.Events;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.UnitTests.Domain;

public class SectionTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithoutName_ThrowsDomainException(string? name)
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Section(Guid.NewGuid(), name!, capacity: 100, price: 50m));

        Assert.Equal("SECTION_NAME_REQUIRED", ex.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveCapacity_ThrowsDomainException(int capacity)
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Section(Guid.NewGuid(), "Pista", capacity, price: 50m));

        Assert.Equal("SECTION_CAPACITY_INVALID", ex.Code);
    }

    [Fact]
    public void Constructor_WithNegativePrice_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Section(Guid.NewGuid(), "Pista", capacity: 100, price: -1m));

        Assert.Equal("SECTION_PRICE_INVALID", ex.Code);
    }

    [Fact]
    public void GenerateTickets_CreatesOneAvailableTicketPerUnitOfCapacity()
    {
        var section = new Section(Guid.NewGuid(), "VIP", capacity: 3, price: 350m);

        var tickets = section.GenerateTickets();

        Assert.Equal(3, tickets.Count);
        Assert.All(tickets, t =>
        {
            Assert.Equal(section.Id, t.SectionId);
            Assert.Equal(TicketStatus.Available, t.Status);
        });
    }
}
