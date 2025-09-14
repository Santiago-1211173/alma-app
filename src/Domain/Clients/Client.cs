namespace AlmaApp.Domain.Clients;

public class Client
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string? CitizenCardNumber { get; private set; } = default!;
    public string? Phone { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    // EF precisa disto
    private Client() { }

    // construtor "normal" (sem Id explícito)
    public Client(string firstName, string lastName, string email,
                  string citizenCardNumber, string? phone, DateOnly? birthDate)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        CitizenCardNumber = citizenCardNumber;
        Phone = phone;
        BirthDate = birthDate;
    }

    // **NOVO** overload com Id explícito (para testes/seeders/API)
    public Client(Guid id, string firstName, string lastName, string email,
                  string citizenCardNumber, string? phone, DateOnly? birthDate)
        : this(firstName, lastName, email, citizenCardNumber, phone, birthDate)
    {
        Id = id == default ? Guid.NewGuid() : id;
    }
    
    public void Update(string firstName, string lastName, string email,
                   string citizenCardNumber, string? phone, DateOnly? birthDate){
        FirstName = firstName;
        LastName  = lastName;
        Email     = email;
        CitizenCardNumber = citizenCardNumber;
        Phone = phone;
        BirthDate = birthDate;
    }

}
