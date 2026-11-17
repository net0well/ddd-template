using Application.Abstractions.Messaging;
using Application.Users;
using Application.Users.Refresh;
using SharedKernel.Results;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class RefreshToken : IEndpoint
{
    public sealed record Request(string RefreshToken);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/refresh-token", async (
            Request request,
            ICommandHandler<RefreshTokenCommand, AccessTokensResponse> handler,
            CancellationToken cancellationToken) =>
        {
            CommandResult<AccessTokensResponse> result = await handler.Handle(
                new RefreshTokenCommand(request.RefreshToken),
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithTags(Tags.Users)
        .WithName("RefreshToken")
        .WithSummary("Exchange a refresh token")
        .WithDescription("Rotates the refresh token: the old one stops working, and reusing it revokes every session of the user.")
        .ProducesCommandResult<AccessTokensResponse>()
        .ProducesErrors(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized)
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
