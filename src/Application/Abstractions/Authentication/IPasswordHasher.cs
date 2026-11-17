namespace Application.Abstractions.Authentication;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <param name="passwordHash">
    /// <see langword="null"/> when the account does not exist: a dummy verification still runs (and returns
    /// <see langword="false"/>) so the response time does not reveal which e-mails are registered.
    /// </param>
    bool Verify(string password, string? passwordHash);
}
