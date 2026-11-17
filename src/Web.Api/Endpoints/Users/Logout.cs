using Application.Abstractions.Messaging;
using Application.Users.Logout;
using SharedKernel.Results;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Users;

internal sealed class Logout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/logout", async (
            ICommandHandler<LogoutUserCommand> handler,
            CancellationToken cancellationToken) =>
        {
            CommandResult result = await handler.Handle(new LogoutUserCommand(), cancellationToken);

            return result.ToHttpResult();
        })
        .WithTags(Tags.Users)
        .WithName("Logout")
        .WithSummary("Log out everywhere")
        .WithDescription("Revokes every refresh token of the current user. Access tokens already issued expire on their own.")
        .ProducesCommandResult()
        .RequireAuthorization();
    }
}
