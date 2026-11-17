using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Domain.Users;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Users;

public sealed class RefreshTokenConcurrencyTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task SecondRotation_OfAStaleCopy_Should_BeRejected()
    {
        // Arrange: two requests loaded the same token before either saved
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        string hash = Factory.Services.GetRequiredService<ITokenProvider>().HashRefreshToken(tokens.RefreshToken);

        using IServiceScope first = Factory.Services.CreateScope();
        using IServiceScope second = Factory.Services.CreateScope();
        await RotateAsync(first, hash);
        await RotateAsync(second, hash);

        // Act
        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        await Should.ThrowAsync<DataConflictException>(
            () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrentRefreshRequests_WithTheSameToken_Should_IssueOnlyOneNewPair()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken = tokens.RefreshToken })));

        // Assert: the losers lost the race or arrived just after the rotation; both are a 409 that keeps the session
        responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses
            .Where(response => response.StatusCode != HttpStatusCode.OK)
            .ShouldAllBe(response => response.StatusCode == HttpStatusCode.Conflict);
    }

    private static async Task RotateAsync(IServiceScope scope, string hash)
    {
        IRefreshTokensWritableRepository refreshTokens =
            scope.ServiceProvider.GetRequiredService<IRefreshTokensWritableRepository>();

        RefreshToken current = (await refreshTokens.GetByTokenHashAsync(hash)).ShouldNotBeNull();

        refreshTokens.Add(current.Rotate($"rotated-{Guid.NewGuid():N}", DateTime.UtcNow, TimeSpan.FromDays(1)));
    }
}
