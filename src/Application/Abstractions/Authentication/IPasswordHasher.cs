namespace Application.Abstractions.Authentication;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <param name="passwordHash">
    /// <see langword="null"/> when the account does not exist: a dummy verification still runs (and fails)
    /// so the response time does not reveal which e-mails are registered.
    /// </param>
    PasswordVerificationResult Verify(string password, string? passwordHash);
}

public enum PasswordVerificationResult
{
    Failed = 0,
    Success = 1,

    /// <summary>The password is right but was hashed with an older format or cost: store a new hash.</summary>
    SuccessRehashNeeded = 2
}
