using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Contracts.ServiceAppointments;
using AlmaApp.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlmaApp.WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/service-appointments")]
public sealed class ServiceAppointmentsController : ControllerBase
{
    private readonly IServiceAppointmentService _svc;

    public ServiceAppointmentsController(IServiceAppointmentService svc)
    {
        _svc = svc;
    }

    private string CurrentUid =>
        User.FindFirst("user_id")?.Value ??
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
        throw new InvalidOperationException("Missing user id.");


    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? staffId,
        [FromQuery] Guid? roomId,
        [FromQuery] int? serviceType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _svc.SearchAsync(clientId, staffId, roomId, serviceType, from, to, page, pageSize, ct);
        return Ok(result);
    }


    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var resp = await _svc.GetByIdAsync(id, ct);
        if (resp == null) return NotFound();
        return Ok(resp);
    }


    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceAppointmentRequestDto body, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var resp = await _svc.CreateAsync(body, CurrentUid, ct);
            return CreatedAtAction(nameof(GetById), new { id = resp.Id }, resp);
        }
        catch (ArgumentException ex)
        {
            return Problem(ex.Message, statusCode: 400);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = ex.Message });
        }
    }


    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateServiceAppointmentRequestDto body, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            await _svc.UpdateAsync(id, body, CurrentUid, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return Problem(ex.Message, statusCode: 400);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("não encontrada"))
                return NotFound(new ProblemDetails { Title = ex.Message });
            return Conflict(new ProblemDetails { Title = ex.Message });
        }
    }


    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        try
        {
            await _svc.CancelAsync(id, CurrentUid, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("não encontrada"))
                return NotFound(new ProblemDetails { Title = ex.Message });
            return Conflict(new ProblemDetails { Title = ex.Message });
        }
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        try
        {
            await _svc.CompleteAsync(id, CurrentUid, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("não encontrada"))
                return NotFound(new ProblemDetails { Title = ex.Message });
            return Conflict(new ProblemDetails { Title = ex.Message });
        }
    }
}