using Application.Todos.Create;
using Application.Todos.Get;
using Application.Todos.Update;
using Domain.Todos;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Time.Testing;

namespace Application.UnitTests.Todos;

public sealed class TodoValidatorsTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly CreateTodoCommandValidator _createValidator;
    private readonly UpdateTodoCommandValidator _updateValidator = new();
    private readonly GetTodosQueryValidator _getValidator = new();

    public TodoValidatorsTests()
    {
        _createValidator = new CreateTodoCommandValidator(new FakeTimeProvider(new DateTimeOffset(UtcNow)));
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDescriptionIsEmpty()
    {
        TestValidationResult<CreateTodoCommand> result =
            _createValidator.TestValidate(new CreateTodoCommand(string.Empty, null, [], Priority.Low));

        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDueDateIsInThePast()
    {
        TestValidationResult<CreateTodoCommand> result =
            _createValidator.TestValidate(new CreateTodoCommand("Late", UtcNow.AddDays(-1), [], Priority.Low));

        result.ShouldHaveValidationErrorFor(c => c.DueDate);
    }

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        TestValidationResult<CreateTodoCommand> result =
            _createValidator.TestValidate(new CreateTodoCommand("Buy groceries", UtcNow, ["home"], Priority.Medium));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenDescriptionExceedsMaxLength()
    {
        TestValidationResult<UpdateTodoCommand> result = _updateValidator.TestValidate(
            new UpdateTodoCommand(Guid.CreateVersion7(), new string('a', TodoItem.DescriptionMaxLength + 1)));

        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Theory]
    [InlineData(0, 10, null, null)]
    [InlineData(1, 101, null, null)]
    [InlineData(1, 10, "unknown", null)]
    [InlineData(1, 10, null, "sideways")]
    public void GetValidator_Should_RejectInvalidPagingAndSorting(int page, int pageSize, string? sortColumn, string? sortOrder)
    {
        TestValidationResult<GetTodosQuery> result =
            _getValidator.TestValidate(new GetTodosQuery(null, sortColumn, sortOrder, page, pageSize));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void GetValidator_Should_AcceptSortColumnsCaseInsensitively()
    {
        TestValidationResult<GetTodosQuery> result =
            _getValidator.TestValidate(new GetTodosQuery("x", "dueDate", "asc", 2, 50));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
