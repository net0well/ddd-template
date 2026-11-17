using Domain.Users;
using Infrastructure.Database;
using IntegrationTests.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Users;

public sealed class PasswordRehashTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Login_Should_UpgradeAHashStoredInTheLegacyFormat()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await SetPasswordHashAsync(userId, PasswordHasherTests.LegacyHash(Password));

        // Act
        await LoginAsync(email);

        // Assert: upgraded to the current format and cost, and still accepted
        (await GetPasswordHashAsync(userId)).ShouldStartWith("v1$100000$");
        (await LoginAsync(email)).AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    private async Task SetPasswordHashAsync(Guid userId, string passwordHash)
    {
        using IServiceScope scope = Factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.PasswordHash, passwordHash));
    }

    private async Task<string> GetPasswordHashAsync(Guid userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(user => user.Id == userId)
            .Select(user => user.PasswordHash)
            .SingleAsync();
    }
}
