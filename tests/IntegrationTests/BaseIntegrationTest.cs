using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseIntegrationTest
{
    protected const string Password = "Password123";

    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        Factory = factory;
        HttpClient = factory.CreateClient();
    }

    protected IntegrationTestWebAppFactory Factory { get; }

    protected HttpClient HttpClient { get; }

    protected sealed record AccessTokens(string AccessToken, string RefreshToken);

    /// <summary>The response envelope every endpoint returns.</summary>
    protected sealed record Envelope<T>(bool Success, T? Data, List<NotificationDto> Notifications, string? TraceId);

    protected sealed record NotificationDto(string Key, string Message, string Type);

    protected static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.com";

    protected static async Task<Envelope<T>> ReadEnvelopeAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions))!;

    protected static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        Envelope<T> envelope = await ReadEnvelopeAsync<T>(response);

        envelope.Success.ShouldBeTrue();

        return envelope.Data!;
    }

    protected async Task<Guid> RegisterUserAsync(string email)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/register",
            new { email, firstName = "Test", lastName = "User", password = Password });

        response.EnsureSuccessStatusCode();

        return await ReadDataAsync<Guid>(response);
    }

    protected async Task<AccessTokens> LoginAsync(string email)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login", new { email, password = Password });

        response.EnsureSuccessStatusCode();

        return await ReadDataAsync<AccessTokens>(response);
    }

    protected async Task<(Guid UserId, AccessTokens Tokens)> RegisterAndLoginAsync()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);

        return (userId, tokens);
    }

    /// <summary>Delivers the pending outbox messages now (the background processor is off in tests).</summary>
    protected async Task ProcessOutboxAsync(IServiceProvider? services = null)
    {
        IServiceProvider provider = services ?? Factory.Services;
        int claimed;

        do
        {
            using IServiceScope scope = provider.CreateScope();
            claimed = await scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>().ProcessAsync(CancellationToken.None);
        }
        while (claimed > 0);
    }

    protected void Authenticate(string accessToken)
    {
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
