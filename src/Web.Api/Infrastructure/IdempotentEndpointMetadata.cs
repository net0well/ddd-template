namespace Web.Api.Infrastructure;

/// <summary>Marks endpoints that honor the <c>Idempotency-Key</c> header, so the API documentation can show it.</summary>
internal sealed class IdempotentEndpointMetadata
{
    public const string HeaderName = "Idempotency-Key";
}
