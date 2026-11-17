namespace Application.Abstractions.Authentication;

public interface IUserContext
{
    /// <summary>The authenticated user. Throws when there is none — for authorized endpoints only.</summary>
    Guid UserId { get; }

    bool TryGetUserId(out Guid userId);
}
