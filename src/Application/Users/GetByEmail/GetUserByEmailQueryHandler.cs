using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel.Results;

namespace Application.Users.GetByEmail;

internal sealed class GetUserByEmailQueryHandler(IUsersSearchableRepository users, IUserContext userContext)
    : IQueryHandler<GetUserByEmailQuery, UserResponse>
{
    public async Task<CommandResult<UserResponse>> Handle(GetUserByEmailQuery query, CancellationToken cancellationToken)
    {
        var email = Email.Create(query.Email);

        User? user = email.IsValid ? await users.GetByEmailAsync(email, cancellationToken) : null;

        if (user is null)
        {
            return CommandResult.Fail<UserResponse>(UserErrors.NotFoundByEmail);
        }

        if (user.Id != userContext.UserId)
        {
            return CommandResult.Fail<UserResponse>(UserErrors.Forbidden);
        }

        return CommandResult.Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email.Value,
            FirstName = user.FirstName,
            LastName = user.LastName
        });
    }
}
