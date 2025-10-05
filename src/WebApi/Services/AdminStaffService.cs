using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Staff;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Staff;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services;

public sealed class AdminStaffService : IAdminStaffService
{
    private readonly AppDbContext _db;

    public AdminStaffService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<AdminStaffDto>> CreateAsync(CreateAdminStaffRequest request, CancellationToken ct)
    {
        Staff staff;
        try
        {
            staff = new Staff(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Phone,
                request.StaffNumber,
                request.Speciality);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult<AdminStaffDto>.Fail(new ServiceError(400, ex.Message));
        }

        await _db.Staff.AddAsync(staff, ct);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<AdminStaffDto>.Ok(MapToDto(staff));
    }

    public async Task<ServiceResult<AdminStaffDto>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var staff = await _db.Staff.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (staff is null)
        {
            return ServiceResult<AdminStaffDto>.Fail(new ServiceError(404, "Staff not found"));
        }

        return ServiceResult<AdminStaffDto>.Ok(MapToDto(staff));
    }

    public async Task<ServiceResult> LinkFirebaseAsync(Guid id, LinkFirebaseRequest request, CancellationToken ct)
    {
        var staff = await _db.Staff.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (staff is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Staff not found"));
        }

        var exists = await _db.Staff.AnyAsync(x => x.FirebaseUid == request.Uid && x.Id != id, ct);
        if (exists)
        {
            return ServiceResult.Fail(new ServiceError(409, "UID já ligado a outro Staff"));
        }

        try
        {
            staff.LinkFirebase(request.Uid);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult.Fail(new ServiceError(400, ex.Message));
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static AdminStaffDto MapToDto(Staff staff)
        => new(
            staff.Id,
            staff.FirstName,
            staff.LastName,
            staff.Email,
            staff.Phone,
            staff.StaffNumber,
            staff.Speciality,
            staff.FirebaseUid);
}
