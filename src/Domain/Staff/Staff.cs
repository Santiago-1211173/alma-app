using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlmaApp.Domain.Staff;

public class Staff
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public string StaffNumber { get; private set; } = default!;
    public string? Speciality { get; private set; }
    public string? FirebaseUid { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = default!; // Concurrency

    private Staff() { } // EF

    public Staff(string firstName, string lastName, string email, string phone, string staffNumber, string? speciality)
    {
        Id = Guid.NewGuid();
        Update(firstName, lastName, email, phone, staffNumber, speciality);
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(string firstName, string lastName, string email, string phone, string staffNumber, string? speciality)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Phone = phone.Trim();
        StaffNumber = staffNumber.Trim();
        Speciality = string.IsNullOrWhiteSpace(speciality) ? null : speciality.Trim();
    }
    
    public void LinkFirebase(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) throw new ArgumentException(nameof(uid));
        FirebaseUid = uid.Trim();
    }
}
