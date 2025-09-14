using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlmaApp.WebApi.Middleware;

public sealed class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _log;

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> log)
        => (_next, _log) = (next, log);

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _log.LogWarning(ex, "Unique constraint violation");

            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.Conflict;
                ctx.Response.ContentType = "application/problem+json";
                await ctx.Response.WriteAsJsonAsync(new
                {
                    type = "https://httpstatuses.com/409",
                    title = "Conflict",
                    detail = "Já existe um registo com o mesmo Email/Phone/CitizenCardNumber.",
                    status = 409
                });
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // SQL Server: 2601 (duplicate key) / 2627 (unique index)
        if (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            return true;

        if (ex.InnerException?.InnerException is SqlException sql2 && (sql2.Number == 2601 || sql2.Number == 2627))
            return true;

        return false;
    }
}
