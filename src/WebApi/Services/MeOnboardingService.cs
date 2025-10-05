using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Auth;
using AlmaApp.Domain.Clients;
using AlmaApp.Domain.Staff;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Common.Auth;
using AlmaApp.WebApi.Contracts.Auth;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services;

public sealed class MeOnboardingService : IMeOnboardingService
{
    private readonly AppDbContext _db;
    private readonly IUserContext _user;

    public MeOnboardingService(AppDbContext db, IUserContext user)
    {
        _db = db;
        _user = user;
    }

    public async Task<ServiceResult<OnboardingResult>> CreateClientAsync(CreateClientSelf request, CancellationToken ct)
    {
        var uid = _user.Uid;
        if (string.IsNullOrWhiteSpace(uid))
        {
            return ServiceResult<OnboardingResult>.Fail(new ServiceError(401, "Missing user id."));
        }

        var email = (_user.Email ?? request.Email).Trim().ToLowerInvariant();

        var exists = await _db.Clients.AnyAsync(c =>
            (c.FirebaseUid == uid) ||
            (c.Email == email), ct);

        if (exists)
        {
            return ServiceResult<OnboardingResult>.Fail(new ServiceError(409, "Já existe um perfil de Client para este utilizador."));
        }

        Client client;
        try
        {
            client = new Client(
                firstName: request.FirstName.Trim(),
                lastName: request.LastName.Trim(),
                email: email,
                citizenCardNumber: request.CitizenCardNumber.Trim(),
                phone: request.Phone?.Trim(),
                birthDate: request.BirthDate);
            client.LinkFirebase(uid);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult<OnboardingResult>.Fail(new ServiceError(400, ex.Message));
        }

        await _db.Clients.AddAsync(client, ct);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<OnboardingResult>.Ok(new OnboardingResult("Perfil de cliente criado.", clientId: client.Id));
    }

    public async Task<ServiceResult<OnboardingResult>> ClaimStaffAsync(ClaimStaffBody request, CancellationToken ct)
    {
        var uid = _user.Uid;
        if (string.IsNullOrWhiteSpace(uid))
        {
            return ServiceResult<OnboardingResult>.Fail(new ServiceError(401, "Missing user id."));
        }

        var staff = await _db.Staff.FirstOrDefaultAsync(s =>
            s.StaffNumber == request.StaffNumber &&
            (request.Email == null || s.Email == request.Email), ct);

        if (staff is null)
        {
            return ServiceResult<OnboardingResult>.Fail(new ServiceError(404, "Staff não encontrado."));
        }

        if (!string.IsNullOrWhiteSpace(staff.FirebaseUid))
        {
            return ServiceResult<OnboardingResult>.Fail(new ServiceError(409, "Este Staff já está associado a outra conta."));
        }

        try
        {
            staff.LinkFirebase(uid);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult<OnboardingResult>.Fail(new ServiceError(400, ex.Message));
        }

        var hasRole = await _db.RoleAssignments.AnyAsync(r =>
            r.FirebaseUid == uid && r.Role == RoleName.Staff, ct);

        if (!hasRole)
        {
            await _db.RoleAssignments.AddAsync(new RoleAssignment(uid, RoleName.Staff), ct);
        }

        await _db.SaveChangesAsync(ct);

        return ServiceResult<OnboardingResult>.Ok(new OnboardingResult("Conta associada como Staff.", staffId: staff.Id));
    }
}
