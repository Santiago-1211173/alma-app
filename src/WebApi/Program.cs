using System.Security.Claims;
using AlmaApp.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- EF Core ----
var cs =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

if (string.IsNullOrWhiteSpace(cs))
    throw new InvalidOperationException("Missing connection string 'DefaultConnection'. Configure em appsettings(.Development).json ou via env var ConnectionStrings__DefaultConnection.");

builder.Services.AddDbContext<AppDbContext>(opts => opts.UseSqlServer(cs));

// ---- Auth (Firebase) ----
var projectId = builder.Configuration["Firebase:ProjectId"]
    ?? throw new InvalidOperationException("Firebase:ProjectId não configurado. Define em appsettings.Development.json.");
var authority = $"https://securetoken.google.com/{projectId}";

// logs detalhados de validação (DEV)
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
        o.IncludeErrorDetails = true; // devolve motivo no WWW-Authenticate

        // DEV ONLY: ver a exceção no corpo em caso de falha
        o.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                ctx.NoResult();
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "text/plain";
                return ctx.Response.WriteAsync(ctx.Exception.ToString());
            }
        };
    });

builder.Services.AddAuthorization();

// ---- Controllers & Swagger ----
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

builder.Services.AddHealthChecks();
// (opcional) logs de auth detalhados
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.IdentityModel", LogLevel.Debug);

var app = builder.Build();

// ---- Dev: Swagger + seeder ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await AlmaApp.WebApi.DevSeeder.SeedAsync(db);
}

app.UseHttpsRedirection();
app.UseAuthentication(); // <- antes do Authorization
app.UseAuthorization();

app.MapHealthChecks("/healthz");
app.MapControllers();

// /me: inspeciona o token autenticado
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

// qualidade de vida: raiz → Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

public partial class Program { }
