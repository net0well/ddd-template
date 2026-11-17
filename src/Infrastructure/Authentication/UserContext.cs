using Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;
using SharedKernel.DependencyInjection;

namespace Infrastructure.Authentication;

internal sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext, IInjectScoped
{
    public Guid UserId => TryGetUserId(out Guid userId) ? userId : throw new UserContextUnavailableException();

    public bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;

        return httpContextAccessor.HttpContext?.User is { } user && user.TryGetUserId(out userId);
    }
}
