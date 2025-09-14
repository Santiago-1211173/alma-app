using System.Threading.Tasks;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Features.Clients;
using AlmaApp.Domain.Clients;              // <— ADICIONA ISTO
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class ClientsController(AppDbContext db) : ControllerBase
{
    // GET /api/v1/clients?q=ana&page=1&pageSize=10
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientListItemDto>>> Search(
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page     = page     < 1 ? 1  : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var query = db.Clients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c =>
                EF.Functions.Like(c.FirstName, $"%{term}%") ||
                EF.Functions.Like(c.LastName,  $"%{term}%")  ||
                EF.Functions.Like(c.Email,     $"%{term}%")  ||
                EF.Functions.Like(c.Phone ?? "", $"%{term}%")); // <- null-safe
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ClientListItemDto(c.Id, c.FirstName, c.LastName, c.Email, c.Phone))
            .ToListAsync();

        return Ok(PagedResult<ClientListItemDto>.Create(items, page, pageSize, total));
    }

    // POST /api/v1/clients
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var client = new Client(
            id: Guid.NewGuid(),
            firstName: body.FirstName.Trim(),
            lastName: body.LastName.Trim(),
            email: body.Email.Trim().ToLowerInvariant(),
            citizenCardNumber: body.CitizenCardNumber.Trim(),
            phone: body.Phone?.Trim(),
            birthDate: body.BirthDate
        );

        db.Clients.Add(client);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = client.Id }, client);
    }

    // GET /api/v1/clients/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var c = await db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return c is null ? NotFound() : Ok(c);
    }
}

// Se ainda não tiveres um DTO de entrada num ficheiro próprio, mantém este:
public record CreateClientRequest(
    string FirstName, string LastName, string CitizenCardNumber,
    string Email, string Phone, DateOnly? BirthDate);
