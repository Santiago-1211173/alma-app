using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.Domain.Rooms;

public class Room
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public int Capacity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    private Room() { } // EF

    public Room(string name, int capacity, bool isActive = true)
    {
        Id = Guid.NewGuid();
        Update(name, capacity, isActive);
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(string name, int capacity, bool isActive)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Name = name.Trim();
        Capacity = capacity;
        IsActive = isActive;
    }
}