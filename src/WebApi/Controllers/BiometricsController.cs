using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AlmaApp.WebApi.Common;
using AlmaApp.WebApi.Contracts.Biometrics;
using AlmaApp.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlmaApp.WebApi.Controllers
{
    
    [Authorize]
    [ApiController]
    [Route("api/v1")]
    public sealed class BiometricsController : ControllerBase
    {
        private readonly IBiometricSnapshotService _service;
        public BiometricsController(IBiometricSnapshotService service)
        {
            _service = service;
        }

        
        [HttpPost("me/biometrics")]
        public async Task<ActionResult<BiometricSnapshotDto>> CreateMySnapshot([FromBody] CreateBiometricSnapshotRequest body, CancellationToken ct)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            var uid = GetUid();
            try
            {
                var snapshot = await _service.CreateSnapshotAsync(uid, body, ct);
                return CreatedAtAction(nameof(GetMySnapshots), new { id = snapshot.Id }, snapshot);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new ProblemDetails { Title = ex.Message });
            }
        }

        
        [HttpGet("me/biometrics")]
        public async Task<ActionResult<PagedResult<BiometricSnapshotDto>>> GetMySnapshots([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            var uid = GetUid();
            var result = await _service.GetMySnapshotsAsync(uid, from, to, page, pageSize, ct);
            return Ok(result);
        }

        
        [HttpGet("clients/{clientId:guid}/biometrics")]
        public async Task<ActionResult<PagedResult<BiometricSnapshotDto>>> GetSnapshotsByClient(Guid clientId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            var result = await _service.GetSnapshotsByClientIdAsync(clientId, from, to, page, pageSize, ct);
            return Ok(result);
        }

        private string GetUid()
        {
            var uid = User.FindFirst("user_id")?.Value ??
                      User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return uid ?? throw new InvalidOperationException("Missing user id.");
        }
    }
}