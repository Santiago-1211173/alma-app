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
        catch (DbUpdateException ex) when (IsDataTruncation(ex, out var hint))
        {
            _log.LogWarning(ex, "String or binary data would be truncated");

            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                ctx.Response.ContentType = "application/problem+json";
                await ctx.Response.WriteAsJsonAsync(new
                {
                    type = "https://httpstatuses.com/400",
                    title = "Dados inválidos",
                    detail = hint ?? "Um dos campos enviados excede o tamanho máximo permitido.",
                    status = 400
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

    private static bool IsDataTruncation(DbUpdateException ex, out string? hint)
    {
        hint = null;

        // SQL Server: 2628 - String or binary data would be truncated in table '...', column '...'
        SqlException? sql = ex.InnerException as SqlException
                            ?? ex.InnerException?.InnerException as SqlException;

        if (sql is null || sql.Number != 2628) return false;

        // A mensagem geralmente inclui tabela e coluna; damos uma dica específica quando possível.
        var message = sql.Message ?? string.Empty;
        if (message.Contains("RoleAssignments", StringComparison.OrdinalIgnoreCase)
            && message.Contains("FirebaseUid", StringComparison.OrdinalIgnoreCase))
        {
            hint = "O valor para FirebaseUid é demasiado longo. Verifica se não estás a enviar o token JWT inteiro em vez do Firebase UID (usa o UID devolvido por /whoami).";
        }
        else
        {
            hint = "Um dos campos enviados excede o tamanho máximo permitido pela base de dados.";
        }

        return true;
    }
}
