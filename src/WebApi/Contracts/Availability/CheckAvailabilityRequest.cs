using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.WebApi.Contracts.Availability;

    /// <summary>
    /// Pedido para validar disponibilidade em HORA LOCAL de Portugal (Europe/Lisbon).
    /// Envia <see cref="StartLocal"/> SEM sufixo 'Z'.
    /// </summary>
    public sealed record CheckAvailabilityRequest(
        Guid StaffId,
        Guid?    RoomId,
        DateTime StartLocal,
        int DurationMinutes
    );