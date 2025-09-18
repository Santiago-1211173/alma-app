using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.WebApi.Contracts.ClassRequests;

/// <summary>
/// Resposta ao aprovar um ClassRequest: devolve o estado do pedido
/// e um resumo da Class criada (Scheduled).
/// </summary>
public sealed record ClassRequestApprovedResponse(
    // Pedido
    Guid     RequestId,
    Guid     ClientId,
    Guid     StaffId,
    DateTime ProposedStartUtc,
    int      DurationMinutes,
    string?  Notes,
    int      Status,
    string   CreatedByUid,
    DateTime CreatedAtUtc,

    // Aula criada
    Guid     ClassId,
    DateTime ClassStartUtc,
    int      ClassDurationMinutes,
    Guid     ClassRoomId,
    int      ClassStatus
);
