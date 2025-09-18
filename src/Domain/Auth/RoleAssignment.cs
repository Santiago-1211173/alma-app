using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.Domain.Auth;

public class RoleAssignment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string FirebaseUid { get; private set; } = default!;
    public RoleName Role { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    private RoleAssignment() { } // EF

    public RoleAssignment(string firebaseUid, RoleName role)
    {
        if (string.IsNullOrWhiteSpace(firebaseUid))
            throw new ArgumentException("FirebaseUid obrigatório.", nameof(firebaseUid));

        Id = Guid.NewGuid();
        FirebaseUid = firebaseUid.Trim();
        Role = role;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
