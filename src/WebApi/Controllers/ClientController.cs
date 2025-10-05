using System;
using System.Linq;
using System.Threading.Tasks;
using AlmaApp.Domain.Clients;                 // Entidade Client (Domain)
using AlmaApp.Infrastructure;                // AppDbContext (Infra)
using AlmaApp.WebApi.Common;                 // PagedResult
using AlmaApp.WebApi.Features.Clients;       // ClientListItemDto
using AlmaApp.WebApi.Contracts.Clients;      // CreateClientRequest, UpdateClientRequest
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers;

[Authorize(Policy = "EmailVerified")]
[ApiController]
[Route("api/v1/[controller]")]
public class ClientsController : ControllerBase{
    private readonly AppDbContext _db;
    public ClientsController(AppDbContext db) => _db = db;

    // GET /api/v1/clients?q=ana&page=1&pageSize=10
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientListItemDto>>> Search(
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        page     = page     < 1 ? 1  : page;
        pageSize = pageSize < 1 ? 10 : (pageSize > 200 ? 200 : pageSize);

        var query = _db.Clients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c =>
                EF.Functions.Like(c.FirstName, $"%{term}%") ||
                EF.Functions.Like(c.LastName,  $"%{term}%")  ||
                EF.Functions.Like(c.Email,     $"%{term}%")  ||
                EF.Functions.Like(c.Phone ?? "", $"%{term}%")); // null-safe
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

    // GET /api/v1/clients/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var c = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return c is null ? NotFound() : Ok(c);
    }

    // POST /api/v1/clients
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var client = new Client(
            id: Guid.NewGuid(),                                    // requer overload com Id no Domain
            firstName: body.FirstName.Trim(),
            lastName: body.LastName.Trim(),
            email: body.Email.Trim().ToLowerInvariant(),
            citizenCardNumber: body.CitizenCardNumber.Trim(),
            phone: body.Phone?.Trim(),
            birthDate: body.BirthDate
        );

        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = client.Id }, client);
    }

    // PUT /api/v1/clients/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest body)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var c = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        // Se adicionaste o método Update() no domínio (recomendado):
        c.Update(
            body.FirstName.Trim(),
            body.LastName.Trim(),
            body.Email.Trim().ToLowerInvariant(),
            body.CitizenCardNumber.Trim(),
            body.Phone?.Trim(),
            body.BirthDate
        );

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/v1/clients/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var c = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        _db.Clients.Remove(c);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
