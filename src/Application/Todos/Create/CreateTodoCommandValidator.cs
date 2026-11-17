using Domain.Todos;
using FluentValidation;

namespace Application.Todos.Create;

internal sealed class CreateTodoCommandValidator : AbstractValidator<CreateTodoCommand>
{
    public const int MaxLabels = 10;
    public const int LabelMaxLength = 50;

    public CreateTodoCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(c => c.Description).NotEmpty().MaximumLength(TodoItem.DescriptionMaxLength);
        RuleFor(c => c.Priority).IsInEnum();
        RuleFor(c => c.DueDate)
            .Must(dueDate => dueDate >= timeProvider.GetUtcNow().UtcDateTime.Date)
            .When(c => c.DueDate.HasValue)
            .WithMessage("The due date cannot be in the past.");
        RuleFor(c => c.Labels).NotNull().Must(labels => labels.Count <= MaxLabels)
            .WithMessage($"A to-do item can have at most {MaxLabels} labels.");
        RuleForEach(c => c.Labels).NotEmpty().MaximumLength(LabelMaxLength);
    }
}
