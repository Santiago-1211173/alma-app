using System;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Clients;
using AlmaApp.WebApi.Features.Clients;
using AlmaApp.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlmaApp.WebApi.Controllers;

[Authorize(Policy = "EmailVerified")]
[ApiController]
[Route("api/v1/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly IClientsService _clients;

    public ClientsController(IClientsService clients)
        => _clients = clients;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientListItemDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _clients.SearchAsync(q, page, pageSize, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _clients.GetByIdAsync(id, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _clients.CreateAsync(body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest body, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _clients.UpdateAsync(id, body, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _clients.DeleteAsync(id, ct);
        if (!result.Success)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    private ActionResult MapError(ServiceError error)
    {
        var problem = error.ToProblemDetails();

        return error.StatusCode switch
        {
            400 => BadRequest(problem),
            401 => Unauthorized(problem),
            404 => NotFound(problem),
            409 => Conflict(problem),
            _ => StatusCode(error.StatusCode, problem)
        };
    }
}
