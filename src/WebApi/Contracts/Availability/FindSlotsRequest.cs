using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.WebApi.Contracts.Availability;

/// <summary>
/// Procura de slots em HORA LOCAL de Portugal (Europe/Lisbon).
/// Envia <see cref="FromLocal"/> e <see cref="ToLocal"/> SEM 'Z'.
/// </summary>
public sealed record FindSlotsRequest(
    Guid     StaffId,
    Guid?    RoomId,
    DateTime FromLocal,
    DateTime ToLocal,
    int      DurationMinutes,
    int      SlotMinutes = 15
);