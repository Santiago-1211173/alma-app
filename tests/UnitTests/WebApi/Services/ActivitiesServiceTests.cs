using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Auth;
using AlmaApp.Domain.Rooms;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common.Auth;
using AlmaApp.WebApi.Contracts.Activities;
using AlmaApp.WebApi.Services;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.UnitTests.WebApi.Services;

public sealed class ActivitiesServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ActivitiesService _sut;
    private readonly FakeUserContext _userContext;

    public ActivitiesServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _db.ChangeTracker.Tracked += (_, e) =>
        {
            if (e.Entry.State == EntityState.Added && e.Entry.Metadata.FindProperty("RowVersion") is not null)
            {
                e.Entry.Property("RowVersion").CurrentValue ??= Guid.NewGuid().ToByteArray();
            }
        };

        _userContext = new FakeUserContext("uid-test");
        _sut = new ActivitiesService(_db, _userContext);

        // ensure there is at least one room for tests
        var room = new Room("Sala 1", 10);
        _db.Rooms.Add(room);
        _db.Entry(room).Property<byte[]>("RowVersion").CurrentValue = new byte[] { 1 };
        _db.SaveChanges();
        DefaultRoomId = room.Id;
    }

    private Guid DefaultRoomId { get; }

    [Fact]
    public async Task CreateAsync_ShouldPersistActivity()
    {
        var start = new DateTime(2025, 1, 1, 9, 0, 0);
        var request = new CreateActivityRequestDto
        {
            RoomId = DefaultRoomId,
            Title = "Yoga",
            Start = start,
            DurationMinutes = 60
        };

        var result = await _sut.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal("Yoga", result.Value!.Title);
        Assert.Equal(start, result.Value!.StartLocal);

        var saved = await _db.Activities.FirstOrDefaultAsync(x => x.Id == result.Value.Id);
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnConflict_WhenRoomBusyWithActivity()
    {
        var start = new DateTime(2025, 1, 1, 9, 0, 0);
        var first = new CreateActivityRequestDto
        {
            RoomId = DefaultRoomId,
            Title = "Yoga",
            Start = start,
            DurationMinutes = 60
        };
        await _sut.CreateAsync(first, CancellationToken.None);

        var overlapping = new CreateActivityRequestDto
        {
            RoomId = DefaultRoomId,
            Title = "Pilates",
            Start = start.AddMinutes(30),
            DurationMinutes = 45
        };

        var result = await _sut.CreateAsync(overlapping, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(409, result.Error!.StatusCode);
        Assert.Equal("Conflito de agenda (Activity/Room)", result.Error.Title);
    }

    [Fact]
    public async Task CompleteAsync_ShouldReturnConflict_WhenActivityNotScheduled()
    {
        var request = new CreateActivityRequestDto
        {
            RoomId = DefaultRoomId,
            Title = "Yoga",
            Start = new DateTime(2025, 1, 1, 9, 0, 0),
            DurationMinutes = 60
        };

        var created = await _sut.CreateAsync(request, CancellationToken.None);
        Assert.True(created.Success);
        var id = created.Value!.Id;

        var cancelResult = await _sut.CancelAsync(id, CancellationToken.None);
        Assert.True(cancelResult.Success);

        var completeResult = await _sut.CompleteAsync(id, CancellationToken.None);

        Assert.False(completeResult.Success);
        Assert.NotNull(completeResult.Error);
        Assert.Equal(409, completeResult.Error!.StatusCode);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private sealed class FakeUserContext : IUserContext
    {
        public FakeUserContext(string? uid) => Uid = uid;

        public string? Uid { get; }
        public string? Email => null;
        public string? DisplayName => null;
        public bool EmailVerified => true;

        public Task<bool> IsInRoleAsync(RoleName role, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<RoleName>> GetRolesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RoleName>>(Array.Empty<RoleName>());
    }
}
