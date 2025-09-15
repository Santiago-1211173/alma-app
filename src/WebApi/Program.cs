using System.Security.Claims;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// -------------------------
// 1) SERVICES (antes do Build)
// -------------------------

// Policies
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("EmailVerified", p => p.RequireClaim("email_verified", "true"));
    // papéis principais
    opts.AddPolicy("AdminOnly",
        p => p.RequireClaim("role", "admin", "Admin"));

    opts.AddPolicy("StaffOrAdmin",
        p => p.RequireClaim("role", "staff", "admin", "Staff", "Admin"));

    opts.AddPolicy("ClientOnly",
        p => p.RequireClaim("role", "client", "Client"));

    // exemplo: papel + email verificado
    opts.AddPolicy("StaffOrAdminVerified", p =>
        p.RequireClaim("email_verified", "true", "True", "1")
         .RequireClaim("role", "staff", "admin", "Staff", "Admin"));
});

// EF Core (SQL Server)
var cs = builder.Configuration.GetConnectionString("DefaultConnection")
         ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (string.IsNullOrWhiteSpace(cs))
    throw new InvalidOperationException("Missing connection string 'DefaultConnection'. Configure em appsettings(.Development).json ou via env var ConnectionStrings__DefaultConnection.");

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(cs, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 5,
                                 maxRetryDelay: TimeSpan.FromSeconds(10),
                                 errorNumbersToAdd: null);
    }));

// Auth (Firebase) — Bearer
var projectId = builder.Configuration["Firebase:ProjectId"]
    ?? throw new InvalidOperationException("Firebase:ProjectId não configurado. Define em appsettings.Development.json.");
var authority = $"https://securetoken.google.com/{projectId}";

// Logs detalhados de validação (DEV)
IdentityModelEventSource.ShowPII = true;

builder.Services
    .AddAuthentication(opts =>
    {
        opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opts.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o =>
    {
        o.Authority = authority;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer   = true,  ValidIssuer   = authority,
            ValidateAudience = true,  ValidAudience = projectId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        o.IncludeErrorDetails = true; // motivo no header WWW-Authenticate (DEV)
    });

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Alma API", Version = "v1" });

    // --- Bearer no Swagger ---
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "eyJhbGciOiJSUzI1NiIsImtpZCI6IjUwMDZlMjc5MTVhMTcwYWIyNmIxZWUzYjgxZDExNjU0MmYxMjRmMjAiLCJ0eXAiOiJKV1QifQ.eyJpc3MiOiJodHRwczovL3NlY3VyZXRva2VuLmdvb2dsZS5jb20vYWxtYWFwcC1hdXRoIiwiYXVkIjoiYWxtYWFwcC1hdXRoIiwiYXV0aF90aW1lIjoxNzU3OTQ0ODQ3LCJ1c2VyX2lkIjoiT3VIaWxRNWlQNGY3cFR3Z1I4Y3FybXVJSVVyMiIsInN1YiI6Ik91SGlsUTVpUDRmN3BUd2dSOGNxcm11SUlVcjIiLCJpYXQiOjE3NTc5NDQ4NDcsImV4cCI6MTc1Nzk0ODQ0NywiZW1haWwiOiJzYW50LmZyZTIxQGdtYWlsLmNvbSIsImVtYWlsX3ZlcmlmaWVkIjpmYWxzZSwiZmlyZWJhc2UiOnsiaWRlbnRpdGllcyI6eyJlbWFpbCI6WyJzYW50LmZyZTIxQGdtYWlsLmNvbSJdfSwic2lnbl9pbl9wcm92aWRlciI6InBhc3N3b3JkIn19.c8RmJj3_uC4mn2MVwdbGVsiJVvOT0lNqwpTKAvL6FAmSLCfjCFhFrOECyJcW7nVOz2vswgb9vl7pMPw0bc-bwz3qlZWkaHPUrlRfSPjRarJvLKveVQPtRrfAqgKvGp0v6jLtPO6RQ5jpnEzSprsRUo5KTf55XwNMZw9cJArFhX5wThiZ7H83xj3lnTjWl9nDpmnKZHdJcmaOJVNiSrscfrLFytb5BudckY5eEahQzdItMyZDdeqBXlcqwujZct3nsWDZATphfIQvtoZqo3HnLjP_VoINfqOETu60Fg8ndcRNz1D9diMim9OnP8PpFCHFr-5n7KzNN1c04sRiD1v3_A",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { securityScheme, Array.Empty<string>() } });

    // --- ajustes para evitar 500 ao gerar swagger.json ---
    c.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
    c.MapType<TimeOnly>(() => new OpenApiSchema { Type = "string", Format = "time" });
    c.CustomSchemaIds(t => t.FullName!.Replace('+', '.'));
});

// CORS (para o front em dev)
builder.Services.AddCors(o => o.AddPolicy("dev", p =>
    p.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://localhost:4200")
     .AllowAnyHeader()
     .AllowAnyMethod()
));

// Health checks
builder.Services.AddHealthChecks();

// (opcional) filtros de logging para auth
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.IdentityModel", LogLevel.Debug);

// -------------------------
// Helper local: garantir BD + migrações
// -------------------------
async Task EnsureDatabaseExistsAndMigrateAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    var connString = cfg.GetConnectionString("DefaultConnection")
                   ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                   ?? throw new InvalidOperationException("Connection string 'DefaultConnection' em falta.");

    var csb = new SqlConnectionStringBuilder(connString);
    var dbName = csb.InitialCatalog;

    try
    {
        await using var tryConn = new SqlConnection(connString);
        await tryConn.OpenAsync(); // Se abrir, a BD existe
    }
    catch (SqlException ex) when (ex.Number == 4060 || ex.Number == 18456)
    {
        // 4060: DB inexistente; 18456 pode aparecer no 1.º arranque
        csb.InitialCatalog = "master";
        await using var master = new SqlConnection(csb.ConnectionString);
        await master.OpenAsync();

        await using var cmd = master.CreateCommand();
        cmd.CommandText = $"IF DB_ID(N'{dbName}') IS NULL CREATE DATABASE [{dbName}]";
        await cmd.ExecuteNonQueryAsync();
    }

    // aplicar migrações
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// -------------------------
// 2) BUILD
// -------------------------
var app = builder.Build();

// -------------------------
// 3) PIPELINE (middlewares)
// -------------------------
app.UseMiddleware<ProblemDetailsMiddleware>(); // mapeia violação de unique -> 409

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker") || app.Environment.IsEnvironment("CI"))
{
    app.UseDeveloperExceptionPage();

    // Swagger em /swagger
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Alma API v1");
        c.RoutePrefix = "swagger";
    });

    // Garantir BD também em dev/docker
    await EnsureDatabaseExistsAndMigrateAsync(app);
}
else
{
    // Em outros ambientes, pelo menos garantir migrações
    await EnsureDatabaseExistsAndMigrateAsync(app);
}

// Em Docker normalmente só expões HTTP; evitar redirecionar para HTTPS
if (!app.Environment.IsEnvironment("Docker") && !app.Environment.IsEnvironment("CI"))
{
    app.UseHttpsRedirection();
}

app.UseCors("dev");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz");
app.MapControllers();

// /me: devolve info básica do token autenticado
app.MapGet("/me", (ClaimsPrincipal user) =>
{
    if (!user.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
    return Results.Ok(new
    {
        Sub   = user.FindFirst("user_id")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        Email = user.FindFirst(ClaimTypes.Email)?.Value,
        Name  = user.FindFirst(ClaimTypes.Name)?.Value
    });
}).RequireAuthorization();

// Qualidade de vida: raiz -> Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

// Necessário para WebApplicationFactory nos testes
public partial class Program { }
