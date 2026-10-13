using FluentValidation;

namespace TechStore.Application.Search;

public sealed class SearchQueryValidator : AbstractValidator<SearchQuery>
{
    public SearchQueryValidator()
    {
        RuleFor(x => x.Q).MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(100).Matches("^[a-z0-9-]+$")
            .When(x => x.Category is not null)
            .WithMessage("'category' must be a category slug.");
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).When(x => x.MinPrice.HasValue);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0).When(x => x.MaxPrice.HasValue);
        RuleFor(x => x).Must(x => x.MinPrice <= x.MaxPrice)
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue)
            .WithName("maxPrice")
            .WithMessage("'maxPrice' must be greater than or equal to 'minPrice'.");
        RuleFor(x => x.MinRating).InclusiveBetween(0, 5).When(x => x.MinRating.HasValue);
        RuleFor(x => x.Sort).Must(SortOptions.All.Contains)
            .WithMessage($"'sort' must be one of: {string.Join(", ", SortOptions.All)}.");
        RuleFor(x => x.Page).InclusiveBetween(1, SearchQuery.MaxPage)
            .WithMessage($"'page' must be between 1 and {SearchQuery.MaxPage}. Use 'cursor' to go deeper.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, SearchQuery.MaxPageSize);
        RuleFor(x => x.Cursor).MaximumLength(4096);
        RuleForEach(x => x.Brands).MaximumLength(60);
    }
}

public sealed class AutocompleteQuery
{
    public string Q { get; init; } = "";
    public string? Category { get; init; }
    public const int MinLength = 2;
}

public sealed class AutocompleteQueryValidator : AbstractValidator<AutocompleteQuery>
{
    public AutocompleteQueryValidator()
    {
        RuleFor(x => x.Q).NotEmpty().MinimumLength(AutocompleteQuery.MinLength).MaximumLength(100);
        RuleFor(x => x.Category).Matches("^[a-z0-9-]+$").When(x => x.Category is not null);
    }
}
