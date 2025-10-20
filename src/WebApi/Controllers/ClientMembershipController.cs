using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Contracts.Memberships;
using AlmaApp.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlmaApp.WebApi.Controllers
{
    
    [Authorize]
    [ApiController]
    [Route("api/v1")]
    public sealed class ClientMembershipController : ControllerBase
    {
        private readonly IClientMembershipService _service;
        public ClientMembershipController(IClientMembershipService service)
        {
            _service = service;
        }

        /// <summary>
        /// Retrieves the current membership of the authenticated user. Returns
        /// null if the user is not a client or has no active membership.
        /// </summary>
        [HttpGet("me/membership")]
        public async Task<ActionResult<MembershipResponse?>> GetMyMembership(CancellationToken ct)
        {
            var uid = User.FindFirst("user_id")?.Value ??
                      User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(uid)) return Unauthorized();
            var membership = await _service.GetMyMembershipAsync(uid, ct);
            return Ok(membership);
        }

        /// <summary>
        /// Creates a new membership for the specified client. Only users with
        /// elevated privileges should be allowed to call this endpoint.
        /// </summary>
        [HttpPost("clients/{clientId:guid}/membership")]
        public async Task<IActionResult> CreateMembership(Guid clientId, [FromBody] CreateMembershipRequest body, CancellationToken ct)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            // Convert start to UTC; if unspecified or local, treat as UTC to preserve consistency.
            var startUtc = body.Start.Kind == DateTimeKind.Utc ? body.Start : DateTime.SpecifyKind(body.Start, DateTimeKind.Utc);
            try
            {
                var response = await _service.CreateMembershipAsync(clientId, startUtc, (Domain.Memberships.BillingPeriod)body.BillingPeriod, body.Nif, GetUid(), ct);
                return CreatedAtAction(nameof(GetMyMembership), new { id = response.MembershipId }, response);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ProblemDetails { Title = ex.Message });
            }
        }

        /// <summary>
        /// Cancels the active membership for the specified client. The membership
        /// will be marked as cancelled with the current UTC time.
        /// </summary>
        [HttpPost("clients/{clientId:guid}/membership/cancel")]
        public async Task<IActionResult> CancelMembership(Guid clientId, [FromBody] CancelMembershipRequest body, CancellationToken ct)
        {
            try
            {
                await _service.CancelMembershipAsync(clientId, GetUid(), ct);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ProblemDetails { Title = ex.Message });
            }
        }

        private string GetUid()
        {
            var uid = User.FindFirst("user_id")?.Value ??
                      User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return uid ?? throw new InvalidOperationException("Missing user id.");
        }
    }
}