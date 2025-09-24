using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.WebApi.Contracts.Availability;

// ===== Staff Availability Rules =====
public sealed record StaffAvailabilityRuleDto(
    Guid Id,
    Guid StaffId,
    int DayOfWeek,     // 0=Sunday .. 6=Saturday (igual a System.DayOfWeek)
    string StartTimeUtc, // "HH:mm"
    string EndTimeUtc,   // "HH:mm"
    bool Active
);

public sealed record UpsertStaffAvailabilityRuleDto(
    int DayOfWeek,
    string StartTimeUtc,   // "HH:mm"
    string EndTimeUtc,     // "HH:mm"
    bool Active = true
);

// ===== Staff Time Off =====
public sealed record StaffTimeOffDto(
    Guid Id,
    Guid StaffId,
    DateTime FromUtc,
    DateTime ToUtc,
    string? Reason
);

public sealed record UpsertStaffTimeOffDto(
    DateTime FromUtc,
    DateTime ToUtc,
    string? Reason
);

// ===== Room Closures =====
public sealed record RoomClosureDto(
    Guid Id,
    Guid RoomId,
    DateTime FromUtc,
    DateTime ToUtc,
    string? Reason
);

public sealed record UpsertRoomClosureDto(
    DateTime FromUtc,
    DateTime ToUtc,
    string? Reason
);


public sealed record CheckAvailabilityResponse(
    bool Available,
    string? Reason
);
