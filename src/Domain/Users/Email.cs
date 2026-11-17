using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using SharedKernel.Notifications;
using SharedKernel.Primitives;

namespace Domain.Users;

/// <summary>
/// E-mail address, normalized (trimmed, lower case) so uniqueness and log-in ignore how it was typed.
/// </summary>
public sealed class Email : ValueObject
{
    public const int MaxLength = 254;

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>Always returns an instance; check <see cref="Notifiable.IsValid"/>.</summary>
    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "E-mail addresses are canonicalized in lower case by convention.")]
    [SuppressMessage(
        "Minor Code Smell",
        "S4040:Strings should be normalized to uppercase",
        Justification = "E-mail addresses are canonicalized in lower case by convention.")]
    public static Email Create(string? value)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        var email = new Email(normalized);

        if (normalized.Length == 0)
        {
            email.AddNotification(UserErrors.EmailRequired);
        }
        else if (normalized.Length > MaxLength)
        {
            email.AddNotification(UserErrors.EmailTooLong);
        }
        else if (!IsWellFormed(normalized))
        {
            email.AddNotification(UserErrors.EmailInvalid);
        }

        return email;
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    // MailAddress also accepts "Name <a@b.c>"; requiring the parsed address to equal the input rejects it.
    private static bool IsWellFormed(string value) =>
        MailAddress.TryCreate(value, out MailAddress? address) &&
        string.Equals(address.Address, value, StringComparison.Ordinal);
}
