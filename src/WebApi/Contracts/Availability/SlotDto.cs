using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.WebApi.Contracts.Availability;

/// <summary>Slot devolvido apenas em hora local de Portugal.</summary>
public sealed record SlotDto(
    DateTime StartLocal,
    DateTime EndLocal
);