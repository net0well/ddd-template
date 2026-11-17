namespace Application.Abstractions.Data;

/// <summary>
/// The database rejected the save because of a conflict the use case could not prevent: a unique index won a
/// race (two sign-ups with the same e-mail) or another request changed the same row. The API answers 409.
/// </summary>
public sealed class DataConflictException(string message, Exception innerException)
    : Exception(message, innerException);
