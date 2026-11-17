using Application.Abstractions.Messaging;
using Application.Users;
using Application.Users.Login;
using SharedKernel.Results;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class Login : IEndpoint
{
    public sealed record Request(string Email, string Password);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/login", async (
            Request request,
            ICommandHandler<LoginUserCommand, AccessTokensResponse> handler,
            CancellationToken cancellationToken) =>
        {
            CommandResult<AccessTokensResponse> result = await handler.Handle(
                new LoginUserCommand(request.Email, request.Password),
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithTags(Tags.Users)
        .WithName("Login")
        .WithSummary("Log in")
        .WithDescription("Returns a short-lived access token (JWT) and a refresh token.")
        .ProducesCommandResult<AccessTokensResponse>()
        .ProducesErrors(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized)
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
