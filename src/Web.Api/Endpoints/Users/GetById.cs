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
        .ProducesCommandResult<UserResponse>()
        .HasPermission(Permissions.UsersAccess);
    }
}
