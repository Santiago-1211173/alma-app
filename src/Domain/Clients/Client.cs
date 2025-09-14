using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// ATENÇÃO: a linha abaixo TEM de ser exatamente este namespace.
// NÃO pode ser "AlmaApp.Domain.Clients.Client" nem outro parecido.
namespace AlmaApp.Domain.Clients;

public sealed class Client
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string FirstName { get; private set; } = null!;
    public string LastName  { get; private set; } = null!;
    public DateOnly? BirthDate { get; private set; }

    public string CitizenCardNumber { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string Phone { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private Client() { }

    public Client(string firstName, string lastName, string cc, string email, string phone, DateOnly? birthDate = null)
    {
        FirstName = firstName;
        LastName  = lastName;
        CitizenCardNumber = cc;
        Email = email;
        Phone = phone;
        BirthDate = birthDate;
    }
}
