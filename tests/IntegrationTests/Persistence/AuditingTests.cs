using System.Net.Http.Json;
using Domain.Todos;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Persistence;

public sealed class AuditingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task AuditableEntity_Should_RecordWhoCreatedAndWhoLastChangedIt()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        DateTime before = DateTime.UtcNow.AddSeconds(-5);

        // Act
        HttpResponseMessage created = await HttpClient.PostAsJsonAsync("todos", new { description = "Audited", priority = "Normal" });
        Guid todoId = await ReadDataAsync<Guid>(created);
        TodoItem afterCreate = await LoadAsync<TodoItem>(todoId);

        (await HttpClient.PutAsync($"todos/{todoId}/complete", null)).EnsureSuccessStatusCode();
        TodoItem afterChange = await LoadAsync<TodoItem>(todoId);

        // Assert
        afterCreate.CreatedBy.ShouldBe(userId);
        afterCreate.CreatedOnUtc.ShouldBeGreaterThan(before);
        afterCreate.ModifiedOnUtc.ShouldBeNull();
        afterCreate.ModifiedBy.ShouldBeNull();

        afterChange.CreatedOnUtc.ShouldBe(afterCreate.CreatedOnUtc);
        afterChange.CreatedBy.ShouldBe(userId);
        afterChange.ModifiedOnUtc.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(afterCreate.CreatedOnUtc);
        afterChange.ModifiedBy.ShouldBe(userId);
    }

    [Fact]
    public async Task AnonymousRequest_Should_RecordTheTime_WithoutAUser()
    {
        // Arrange
        DateTime before = DateTime.UtcNow.AddSeconds(-5);

        // Act
        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Assert
        User user = await LoadAsync<User>(userId);
        user.CreatedOnUtc.ShouldBeGreaterThan(before);
        user.CreatedBy.ShouldBeNull();
    }

    private async Task<TEntity> LoadAsync<TEntity>(Guid id)
        where TEntity : SharedKernel.Primitives.Entity
    {
        using IServiceScope scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<TEntity>().AsNoTracking().SingleAsync(entity => entity.Id == id);
    }
}
