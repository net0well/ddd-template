using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Users;

public sealed class UsersTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record UserDto(Guid Id, string Email, string FirstName, string LastName, List<string> Roles);

    [Fact]
    public async Task Register_Should_ReturnCreatedWithTheUserId()
    {
        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/register",
            new { email = UniqueEmail(), firstName = "Test", lastName = "User", password = Password }, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ReadDataAsync<Guid>(response)).ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Register_Should_ReturnConflict_WhenEmailIsTaken_IgnoringCase()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/register",
            new { email = email.ToUpperInvariant(), firstName = "Test", lastName = "User", password = Password }, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        Envelope<Guid?> envelope = await ReadEnvelopeAsync<Guid?>(response);
        envelope.Notifications.ShouldHaveSingleItem().Key.ShouldBe("Users.EmailNotUnique");
        envelope.Data.ShouldBeNull();
    }

    [Fact]
    public async Task Login_Should_ReturnAccessAndRefreshTokens()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        // Act
        AccessTokens tokens = await LoginAsync(email);

        // Assert
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("WrongPassword1")]
    [InlineData(null)]
    public async Task Login_Should_ReturnUnauthorizedWithTheSameError_ForWrongPasswordOrUnknownEmail(string? password)
    {
        // Arrange
        string email = UniqueEmail();

        if (password is not null)
        {
            await RegisterUserAsync(email);
        }

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/login",
            new { email, password = password ?? Password }, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadEnvelopeAsync<AccessTokens>(response)).Notifications.ShouldHaveSingleItem().Key.ShouldBe("Users.InvalidCredentials");
    }

    [Theory]
    [InlineData("not-an-email", "Password123")]
    [InlineData("test@example.com", "")]
    public async Task Login_Should_ReturnBadRequest_WhenInputIsInvalid(string email, string password)
    {
        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login", new { email, password }, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_Should_RotateTheToken()
    {
        // Arrange
        AccessTokens tokens = await LoginAsync(await RegisterNewUserAsync());

        // Act
        HttpResponseMessage response = await RefreshAsync(tokens.RefreshToken);

        // Assert
        response.EnsureSuccessStatusCode();
        AccessTokens rotated = await ReadDataAsync<AccessTokens>(response);
        rotated.AccessToken.ShouldNotBeNullOrWhiteSpace();
        rotated.RefreshToken.ShouldNotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_Should_AnswerConflict_AndKeepTheSession_WhenTheTokenWasJustRotated()
    {
        // Arrange: a second tab refreshes with the token the first one has just rotated
        AccessTokens tokens = await LoginAsync(await RegisterNewUserAsync());
        AccessTokens rotated = await ReadDataAsync<AccessTokens>(await RefreshAsync(tokens.RefreshToken));

        // Act
        HttpResponseMessage secondTab = await RefreshAsync(tokens.RefreshToken);
        HttpResponseMessage withNewestToken = await RefreshAsync(rotated.RefreshToken);

        // Assert
        secondTab.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadEnvelopeAsync<AccessTokens>(secondTab)).Notifications.ShouldHaveSingleItem().Key.ShouldBe("Users.RefreshTokenConflict");
        withNewestToken.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RefreshToken_Should_RevokeTheWholeSession_WhenARotatedTokenIsReusedAfterTheGracePeriod()
    {
        // Arrange
        AccessTokens tokens = await LoginAsync(await RegisterNewUserAsync());
        AccessTokens rotated = await ReadDataAsync<AccessTokens>(await RefreshAsync(tokens.RefreshToken));
        await MoveRotationBackAsync(tokens.RefreshToken, TimeSpan.FromMinutes(1));

        // Act
        HttpResponseMessage reuse = await RefreshAsync(tokens.RefreshToken);
        HttpResponseMessage afterReuse = await RefreshAsync(rotated.RefreshToken);

        // Assert
        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        afterReuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnUnauthorized_WhenTokenIsInvalid()
    {
        // Act
        HttpResponseMessage response = await RefreshAsync("this-token-does-not-exist");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Should_RevokeTheRefreshTokens()
    {
        // Arrange
        AccessTokens tokens = await LoginAsync(await RegisterNewUserAsync());
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage logout = await HttpClient.PostAsync("users/logout", null, TestContext.Current.CancellationToken);
        HttpResponseMessage refresh = await RefreshAsync(tokens.RefreshToken);

        // Assert
        logout.StatusCode.ShouldBe(HttpStatusCode.OK);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUser_Should_ReturnTheProfileWithTheMemberRole()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/{userId}", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        UserDto user = await ReadDataAsync<UserDto>(response);
        user.Id.ShouldBe(userId);
        user.Roles.ShouldBe(["Member"]);
    }

    [Fact]
    public async Task GetUser_Should_ReturnForbidden_ForAnotherUser()
    {
        // Arrange
        (Guid otherUserId, _) = await RegisterAndLoginAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/{otherUserId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUser_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<string> RegisterNewUserAsync()
    {
        string email = UniqueEmail();

        await RegisterUserAsync(email);

        return email;
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        HttpClient.PostAsJsonAsync("users/refresh-token", new { refreshToken });

    // Simulates time passing: the rotation happened longer ago than the grace period.
    private async Task MoveRotationBackAsync(string refreshToken, TimeSpan by)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        string hash = scope.ServiceProvider.GetRequiredService<ITokenProvider>().HashRefreshToken(refreshToken);

        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RefreshTokens
            .Where(token => token.TokenHash == hash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedOnUtc, token => token.RevokedOnUtc - by));
    }
}
