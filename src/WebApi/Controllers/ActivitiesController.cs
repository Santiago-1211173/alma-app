using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.Activities;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Activities;
using AlmaApp.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlmaApp.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/activities")]
    public sealed class ActivitiesController : ControllerBase
    {
        private readonly IActivitiesService _service;

        public ActivitiesController(IActivitiesService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<PagedResult<ActivityListItemDto>>> Search(
            [FromQuery] Guid? roomId,
            [FromQuery] Guid? instructorId,
            [FromQuery] ActivityCategory? category,
            [FromQuery] DateTime? fromLocal,
            [FromQuery] DateTime? toLocal,
            [FromQuery] ActivityStatus? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            var result = await _service.SearchAsync(roomId, instructorId, category,
                                                    fromLocal, toLocal, status,
                                                    page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ActivityResponse>> GetById(Guid id, CancellationToken ct = default)
        {
            var a = await _service.GetByIdAsync(id, ct);
            return a == null ? NotFound() : Ok(a);
        }

        [HttpPost]
        public async Task<ActionResult<ActivityResponse>> Create([FromBody] CreateActivityRequestDto dto, CancellationToken ct = default)
        {
            var uid = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            try
            {
                var created = await _service.CreateAsync(dto, uid, ct);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(Problem(detail: ex.Message, statusCode: 409));
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ActivityResponse>> Update(Guid id, [FromBody] UpdateActivityRequestDto dto, CancellationToken ct = default)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto, ct);
                return Ok(updated);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(Problem(detail: "Concorrência: RowVersion desactualizado.", statusCode: 409));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(Problem(detail: ex.Message, statusCode: 409));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken ct = default)
        {
            await _service.CancelAsync(id, ct);
            return NoContent();
        }

        [HttpPost("{id:guid}/complete")]
        public async Task<IActionResult> Complete(Guid id, CancellationToken ct = default)
        {
            await _service.CompleteAsync(id, ct);
            return NoContent();
        }

        [HttpPost("{id:guid}/participants")]
        public async Task<IActionResult> Join(Guid id, [FromBody] JoinActivityRequestDto dto, CancellationToken ct = default)
        {
            try
            {
                await _service.JoinAsync(id, dto.ClientId, DateTime.Now, ct);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(Problem(detail: ex.Message, statusCode: 409));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{id:guid}/participants/{clientId:guid}")]
        public async Task<IActionResult> Leave(Guid id, Guid clientId, CancellationToken ct = default)
        {
            await _service.LeaveAsync(id, clientId, DateTime.Now, ct);
            return NoContent();
        }
    }
}
