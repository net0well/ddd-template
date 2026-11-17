using Application.Abstractions.Behaviors;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using FluentValidation;
using SharedKernel.Notifications;
using SharedKernel.Results;

namespace Application.UnitTests.Behaviors;

public sealed class DecoratorTests
{
    private readonly ICommandHandler<PlainCommand> _plainHandler = Substitute.For<ICommandHandler<PlainCommand>>();
    private readonly ICommandHandler<TransactionalCommand> _transactionalHandler = Substitute.For<ICommandHandler<TransactionalCommand>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Validation_Should_ReturnOneNotificationPerFailure_AndSkipTheHandler()
    {
        // Arrange
        var decorator = new ValidationDecorator.CommandBaseHandler<PlainCommand>(_plainHandler, [new PlainCommandValidator()]);

        // Act
        CommandResult result = await decorator.Handle(new PlainCommand(string.Empty), CancellationToken.None);

        // Assert
        Notification notification = result.Notifications.ShouldHaveSingleItem();
        notification.Key.ShouldBe(nameof(PlainCommand.Name));
        notification.Type.ShouldBe(NotificationType.Validation);
        await _plainHandler.DidNotReceiveWithAnyArgs().Handle(default!, default);
    }

    [Fact]
    public async Task Validation_Should_CallTheHandler_WhenValid()
    {
        // Arrange
        _plainHandler.Handle(Arg.Any<PlainCommand>(), Arg.Any<CancellationToken>()).Returns(CommandResult.Ok());
        var decorator = new ValidationDecorator.CommandBaseHandler<PlainCommand>(_plainHandler, [new PlainCommandValidator()]);

        // Act
        CommandResult result = await decorator.Handle(new PlainCommand("ok"), CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Transaction_Should_WrapOnlyTransactionalCommands()
    {
        // Arrange
        _plainHandler.Handle(Arg.Any<PlainCommand>(), Arg.Any<CancellationToken>()).Returns(CommandResult.Ok());
        var decorator = new TransactionDecorator.CommandBaseHandler<PlainCommand>(_plainHandler, _unitOfWork);

        // Act
        await decorator.Handle(new PlainCommand("ok"), CancellationToken.None);

        // Assert
        await _unitOfWork.DidNotReceiveWithAnyArgs().ExecuteInTransactionAsync<CommandResult>(default!, default);
    }

    [Fact]
    public async Task Transaction_Should_RunTransactionalCommandsThroughTheUnitOfWork()
    {
        // Arrange
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<CommandResult>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<CommandResult>>>()(CancellationToken.None));
        _transactionalHandler.Handle(Arg.Any<TransactionalCommand>(), Arg.Any<CancellationToken>()).Returns(CommandResult.Ok());
        var decorator = new TransactionDecorator.CommandBaseHandler<TransactionalCommand>(_transactionalHandler, _unitOfWork);

        // Act
        CommandResult result = await decorator.Handle(new TransactionalCommand(), CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<CommandResult>>>(),
            Arg.Any<CancellationToken>());
        await _transactionalHandler.Received(1).Handle(Arg.Any<TransactionalCommand>(), Arg.Any<CancellationToken>());
    }

    public sealed record PlainCommand(string Name) : ICommand;

    public sealed record TransactionalCommand : ICommand, ITransactionalCommand;

    private sealed class PlainCommandValidator : AbstractValidator<PlainCommand>
    {
        public PlainCommandValidator()
        {
            RuleFor(c => c.Name).NotEmpty();
        }
    }
}
