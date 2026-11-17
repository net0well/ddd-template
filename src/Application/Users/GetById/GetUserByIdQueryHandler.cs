using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel.Results;

namespace Application.Users.GetById;

internal sealed class GetUserByIdQueryHandler(IUsersSearchableRepository users, IUserContext userContext)
    : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    public async Task<CommandResult<UserResponse>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        if (query.UserId != userContext.UserId)
        {
            return CommandResult.Fail<UserResponse>(UserErrors.Forbidden);
        }

        User? user = await users.GetByIdAsync(query.UserId, cancellationToken);

        if (user is null)
        {
            return CommandResult.Fail<UserResponse>(UserErrors.NotFound(query.UserId));
        }

        return CommandResult.Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email.Value,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = [.. user.Roles]
        });
    }
}
