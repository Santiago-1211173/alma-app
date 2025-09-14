using System.Security.Claims;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// -------------------------
// 1) SERVICES (antes do Build)
// -------------------------

builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("EmailVerified", p => p.RequireClaim("email_verified", "true"));
});

// EF Core (SQL Server)
var cs = builder.Configuration.GetConnectionString("DefaultConnection")
         ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (string.IsNullOrWhiteSpace(cs))
    throw new InvalidOperationException("Missing connection string 'DefaultConnection'. Configure em appsettings(.Development).json ou via env var ConnectionStrings__DefaultConnection.");

builder.Services.AddDbContext<AppDbContext>(opts => opts.UseSqlServer(cs));

// Auth (Firebase)
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

builder.Services.AddAuthorization();

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Alma API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Cole aqui o Firebase **ID token** (sem 'Bearer ')",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { securityScheme, Array.Empty<string>() } });
});

// CORS (para o teu front em dev)
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
// 2) BUILD
// -------------------------
var app = builder.Build();

// -------------------------
// 3) MIDDLEWARES
// -------------------------
app.UseMiddleware<ProblemDetailsMiddleware>(); // mapeia violação de unique -> 409

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Seed de dados DEV
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await AlmaApp.WebApi.DevSeeder.SeedAsync(db);
}

app.UseHttpsRedirection();
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
