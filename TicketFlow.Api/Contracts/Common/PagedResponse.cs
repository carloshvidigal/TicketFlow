using FluentValidation;

namespace TicketFlow.Api.Contracts.Common;

// Paginação por offset (ADR-0012): ?page=1&pageSize=20.
public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record PageQuery(int Page = 1, int PageSize = 20);

public class PageQueryValidator : AbstractValidator<PageQuery>
{
    public const int MaxPageSize = 100;

    // O teto da página também impede um offset gigante (page * pageSize
    // estourando o int) de virar erro 500 no banco.
    public const int MaxPage = 100_000;

    public PageQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, MaxPage);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
