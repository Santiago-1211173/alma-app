using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Clients;
using AlmaApp.Domain.Memberships;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlmaApp.UnitTests;

public sealed class ClientMembershipServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ClientMembershipServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = new AppDbContext(_options);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task CreateMembershipAsync_SetsCurrentMembershipId()
    {
        await using var db = new AppDbContext(_options);
        var client = new Client(Guid.NewGuid(), "Ana", "Silva", "ana@example.com", "123456789", "910000000", null);
        await db.Clients.AddAsync(client);
        await db.SaveChangesAsync();

        var service = new ClientMembershipService(db);
        var response = await service.CreateMembershipAsync(client.Id, DateTime.UtcNow, BillingPeriod.Month, null, "admin", CancellationToken.None);

        var reloaded = await db.Clients.AsNoTracking().SingleAsync(c => c.Id == client.Id);
        Assert.Equal(response.MembershipId, reloaded.CurrentMembershipId);
        Assert.NotNull(response.MembershipId);

        var membership = await db.ClientMemberships.AsNoTracking().SingleAsync(m => m.Id == response.MembershipId);
        Assert.Equal(MembershipStatus.Active, membership.Status);
    }

    [Fact]
    public async Task CancelMembershipAsync_ClearsCurrentMembershipId()
    {
        await using var db = new AppDbContext(_options);
        var client = new Client(Guid.NewGuid(), "Beatriz", "Costa", "bia@example.com", "987654321", "920000000", null);
        await db.Clients.AddAsync(client);
        await db.SaveChangesAsync();

        var service = new ClientMembershipService(db);
        var response = await service.CreateMembershipAsync(client.Id, DateTime.UtcNow, BillingPeriod.Month, null, "admin", CancellationToken.None);

        await service.CancelMembershipAsync(client.Id, "admin", CancellationToken.None);

        var reloaded = await db.Clients.AsNoTracking().SingleAsync(c => c.Id == client.Id);
        Assert.Null(reloaded.CurrentMembershipId);

        var membership = await db.ClientMemberships.AsNoTracking().SingleAsync(m => m.Id == response.MembershipId);
        Assert.Equal(MembershipStatus.Cancelled, membership.Status);
    }

    [Fact]
    public async Task GetMyMembershipAsync_UsesCurrentMembershipReference()
    {
        await using var db = new AppDbContext(_options);
        var client = new Client(Guid.NewGuid(), "Carlos", "Ribeiro", "carlos@example.com", "111111111", "930000000", null);
        client.LinkFirebase("firebase-123");
        await db.Clients.AddAsync(client);
        await db.SaveChangesAsync();

        var service = new ClientMembershipService(db);
        var createResponse = await service.CreateMembershipAsync(client.Id, DateTime.UtcNow, BillingPeriod.Month, null, "admin", CancellationToken.None);

        var response = await service.GetMyMembershipAsync("firebase-123", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(createResponse.MembershipId, response!.MembershipId);

        // Expire membership and ensure lookup stops returning it
        await service.ExpireMembershipAsync(createResponse.MembershipId!.Value, CancellationToken.None);

        var afterExpiry = await service.GetMyMembershipAsync("firebase-123", CancellationToken.None);
        Assert.Null(afterExpiry);

        var reloadedClient = await db.Clients.AsNoTracking().SingleAsync(c => c.Id == client.Id);
        Assert.Null(reloadedClient.CurrentMembershipId);

        var membership = await db.ClientMemberships.AsNoTracking().SingleAsync(m => m.Id == createResponse.MembershipId);
        Assert.Equal(MembershipStatus.Expired, membership.Status);
    }
}
