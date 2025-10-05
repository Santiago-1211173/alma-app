using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Staff;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Staff;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Services;

public sealed class StaffService : IStaffService
{
    private readonly AppDbContext _db;

    public StaffService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<PagedResult<StaffListItemDto>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var staff = _db.Staff.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            staff = staff.Where(s =>
                EF.Functions.Like(s.FirstName, $"%{term}%") ||
                EF.Functions.Like(s.LastName, $"%{term}%") ||
                EF.Functions.Like(s.Email, $"%{term}%") ||
                EF.Functions.Like(s.Phone, $"%{term}%") ||
                EF.Functions.Like(s.StaffNumber, $"%{term}%"));
        }

        var total = await staff.CountAsync(ct);

        var items = await staff
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StaffListItemDto(
                s.Id,
                s.FirstName,
                s.LastName,
                s.Email,
                s.Phone,
                s.StaffNumber,
                s.Speciality))
            .ToListAsync(ct);

        var paged = PagedResult<StaffListItemDto>.Create(items, page, pageSize, total);
        return ServiceResult<PagedResult<StaffListItemDto>>.Ok(paged);
    }

    public async Task<ServiceResult<StaffResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var staff = await _db.Staff.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (staff is null)
        {
            return ServiceResult<StaffResponse>.Fail(new ServiceError(404, "Staff not found"));
        }

        return ServiceResult<StaffResponse>.Ok(MapToResponse(staff));
    }

    public async Task<ServiceResult<StaffResponse>> CreateAsync(CreateStaffRequest request, CancellationToken ct)
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
            return ServiceResult<StaffResponse>.Fail(new ServiceError(400, ex.Message));
        }

        await _db.Staff.AddAsync(staff, ct);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<StaffResponse>.Ok(MapToResponse(staff));
    }

    public async Task<ServiceResult> UpdateAsync(Guid id, UpdateStaffRequest request, CancellationToken ct)
    {
        var staff = await _db.Staff.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (staff is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Staff not found"));
        }

        try
        {
            staff.Update(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Phone,
                request.StaffNumber,
                request.Speciality);
        }
        catch (ArgumentException ex)
        {
            return ServiceResult.Fail(new ServiceError(400, ex.Message));
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var staff = await _db.Staff.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (staff is null)
        {
            return ServiceResult.Fail(new ServiceError(404, "Staff not found"));
        }

        _db.Staff.Remove(staff);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static StaffResponse MapToResponse(Staff staff)
        => new(
            staff.Id,
            staff.FirstName,
            staff.LastName,
            staff.Email,
            staff.Phone,
            staff.StaffNumber,
            staff.Speciality,
            staff.CreatedAtUtc);
}
