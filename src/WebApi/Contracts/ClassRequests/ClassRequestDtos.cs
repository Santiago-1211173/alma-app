using System;

namespace AlmaApp.WebApi.Contracts.ClassRequests
{
    // Listagem
    public record ClassRequestListItemDto(
        Guid Id,
        Guid ClientId,
        Guid StaffId,
        Guid RoomId,
        DateTime ProposedStartUtc,
        int DurationMinutes,
        string? Notes,
        int Status
    );

    // Detalhe
    public record ClassRequestResponse(
        Guid Id,
        Guid ClientId,
        Guid StaffId,
        Guid RoomId,
        DateTime ProposedStartUtc,
        int DurationMinutes,
        string? Notes,
        int Status,
        string CreatedByUid,
        DateTime CreatedAtUtc
    );

    // Criação (pelo Staff) — RoomId obrigatório
    public record CreateClassRequestByStaff(
        Guid?   ClientId,
        string? ClientEmail,
        string? ClientUid,
        DateTime ProposedStartUtc,
        int      DurationMinutes,
        Guid     RoomId,
        string?  Notes
    );

    // Atualização — RoomId **opcional**
    public record UpdateClassRequest(
        Guid     ClientId,
        Guid     StaffId,
        DateTime ProposedStartUtc,
        int      DurationMinutes,
        Guid?    RoomId,
        string?  Notes
    );

    // Mantido apenas por compatibilidade (o approve já não usa body)
    public record ApproveClassRequest();
}
