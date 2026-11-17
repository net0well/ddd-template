using Application.Abstractions.Messaging;
using Application.Users.Register;
using SharedKernel.Results;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class Register : IEndpoint
{
    public sealed record Request(string Email, string FirstName, string LastName, string Password);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/register", async (
            Request request,
            ICommandHandler<RegisterUserCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new RegisterUserCommand(request.Email, request.FirstName, request.LastName, request.Password);

            CommandResult<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .WithTags(Tags.Users)
        .WithName("RegisterUser")
        .WithSummary("Register a user")
        .WithDescription("Creates a `Member` and sends a welcome e-mail in the background. Password: 8–128 characters.")
        .ProducesCommandResult<Guid>(StatusCodes.Status201Created)
        .ProducesErrors(StatusCodes.Status400BadRequest, StatusCodes.Status409Conflict)
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
