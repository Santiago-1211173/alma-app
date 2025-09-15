using System.ComponentModel.DataAnnotations;

namespace AlmaApp.WebApi.Contracts.Rooms;

public sealed record RoomListItemDto(Guid Id, string Name, int Capacity, bool IsActive);
public sealed record RoomResponse(Guid Id, string Name, int Capacity, bool IsActive, DateTime CreatedAtUtc);

public sealed class CreateRoomRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = default!;
    [Range(1, int.MaxValue)] public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}

// Sem herança
public sealed class UpdateRoomRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = default!;
    [Range(1, int.MaxValue)] public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}
