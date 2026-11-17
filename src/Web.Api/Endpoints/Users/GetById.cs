using Application.Abstractions.Messaging;
using Application.Users.GetById;
using Domain.Users;
using SharedKernel.Results;
using Web.Api.Extensions;

namespace Web.Api.Endpoints.Users;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/{userId:guid}", async (
            Guid userId,
            IQueryHandler<GetUserByIdQuery, UserResponse> handler,
            CancellationToken cancellationToken) =>
        {
            CommandResult<UserResponse> result = await handler.Handle(new GetUserByIdQuery(userId), cancellationToken);

            return result.ToHttpResult();
        })
        .WithTags(Tags.Users)
        .WithName("GetUserById")
        .WithSummary("Get a user profile")
        .WithDescription("Only your own profile; requires the `users:access` permission.")
        .ProducesCommandResult<UserResponse>()
        .ProducesErrors(StatusCodes.Status404NotFound)
        .HasPermission(Permissions.UsersAccess);
    }
}
