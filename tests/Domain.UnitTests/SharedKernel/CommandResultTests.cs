using System.Diagnostics;
using SharedKernel.Notifications;
using SharedKernel.Results;

namespace Domain.UnitTests.SharedKernel;

public sealed class CommandResultTests
{
    private static readonly Notification SomeNotification = Notification.Validation("Field", "Message");

    [Fact]
    public void Ok_Should_BeSuccessfulWithoutNotifications()
    {
        // Act
        var result = CommandResult.Ok(42);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldBe(42);
        result.Notifications.ShouldBeEmpty();
    }

    [Fact]
    public void Fail_Should_CarryTheNotifications()
    {
        // Act
        var result = CommandResult.Fail<int>(SomeNotification);

        // Assert
        result.Success.ShouldBeFalse();
        result.Data.ShouldBe(default);
        result.Notifications.ShouldBe([SomeNotification]);
    }

    [Fact]
    public void Fail_Should_Throw_WhenThereAreNoNotifications()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => CommandResult.Fail(Array.Empty<Notification>()));
    }

    [Fact]
    public void Fail_Should_CopyTheNotifications_SoTheResultCannotChangeAfterwards()
    {
        // Arrange
        var notifications = new List<Notification> { SomeNotification };

        // Act
        var result = CommandResult.Fail(notifications);
        notifications.Add(Notification.Validation("Other", "Message"));

        // Assert
        result.Notifications.Count.ShouldBe(1);
    }

    [Fact]
    public void Fail_Should_TakeEveryNotificationOfANotifiable()
    {
        // Arrange
        var notifiable = new TestNotifiable();
        notifiable.AddNotification(SomeNotification);
        notifiable.AddNotification(Notification.NotFound("Thing.NotFound", "Missing"));

        // Act
        var result = CommandResult.Fail(notifiable);

        // Assert
        result.Notifications.Count.ShouldBe(2);
    }

    [Fact]
    public void TraceId_Should_BeTheTraceOfTheCurrentOperation()
    {
        // Arrange
        using var activity = new Activity("test-operation");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        // Act
        var result = CommandResult.Fail(SomeNotification);

        // Assert
        result.TraceId.ShouldBe(activity.TraceId.ToHexString());
    }

    [Fact]
    public void TraceId_Should_BeNull_OutsideATracedOperation()
    {
        // Act
        var result = CommandResult.Ok();

        // Assert
        result.TraceId.ShouldBeNull();
    }

    private sealed class TestNotifiable : Notifiable;
}
