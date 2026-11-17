using Domain.Todos;
using FluentValidation;

namespace Application.Todos.Get;

internal sealed class GetTodosQueryValidator : AbstractValidator<GetTodosQuery>
{
    public const int MaxPageSize = 100;

    public GetTodosQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(q => q.Search).MaximumLength(TodoItem.DescriptionMaxLength);
        RuleFor(q => q.SortColumn)
            .Must(column => Enum.TryParse<TodoItemSortField>(column, ignoreCase: true, out _))
            .When(q => !string.IsNullOrWhiteSpace(q.SortColumn))
            .WithMessage($"Sort by one of: {string.Join(", ", Enum.GetNames<TodoItemSortField>())}.");
        RuleFor(q => q.SortOrder)
            .Must(order => string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase))
            .When(q => !string.IsNullOrWhiteSpace(q.SortOrder))
            .WithMessage("The sort order must be 'asc' or 'desc'.");
    }
}
