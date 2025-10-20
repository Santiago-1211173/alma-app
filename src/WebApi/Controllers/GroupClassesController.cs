using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.Domain.GroupClasses;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.GroupClasses;
using AlmaApp.WebApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlmaApp.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/group-classes")]
    public sealed class GroupClassesController : ControllerBase
    {
        private readonly IGroupClassService _service;

        public GroupClassesController(IGroupClassService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<PagedResult<GroupClassListItemDto>>> Search(
            [FromQuery] GroupClassCategory? category,
            [FromQuery] Guid? instructorId,
            [FromQuery] Guid? roomId,
            [FromQuery] DateTime? fromLocal,
            [FromQuery] DateTime? toLocal,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            var result = await _service.SearchAsync(category, instructorId, roomId, fromLocal, toLocal, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GroupClassResponse>> GetById(Guid id, CancellationToken ct = default)
        {
            var r = await _service.GetByIdAsync(id, ct);
            return r == null ? NotFound() : Ok(r);
        }

        [HttpPost]
        public async Task<ActionResult<GroupClassResponse>> Create([FromBody] CreateGroupClassRequestDto req, CancellationToken ct = default)
        {
            var uid = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            try
            {
                var created = await _service.CreateAsync(req, uid, ct);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(Problem(detail: ex.Message, statusCode: 409));
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<GroupClassResponse>> Update(Guid id, [FromBody] UpdateGroupClassRequestDto req, CancellationToken ct = default)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, req, ct);
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
        public async Task<IActionResult> Join(Guid id, [FromBody] JoinGroupClassRequestDto req, CancellationToken ct = default)
        {
            try
            {
                await _service.JoinAsync(id, req.ClientId, DateTime.Now, ct);
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
