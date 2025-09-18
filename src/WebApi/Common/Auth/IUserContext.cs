using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Auth;

namespace AlmaApp.WebApi.Common.Auth;

public interface IUserContext
{
    string? Uid { get; }
    string? Email { get; }
    string? DisplayName { get; }   // <-- ADICIONADO
    bool EmailVerified { get; }

    Task<bool> IsInRoleAsync(RoleName role, CancellationToken ct = default);
    Task<IReadOnlyList<RoleName>> GetRolesAsync(CancellationToken ct = default);
}

