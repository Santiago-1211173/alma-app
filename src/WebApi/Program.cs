using System.Security.Claims;
using AlmaApp.Infrastructure;
using AlmaApp.WebApi.Middleware;
using AlmaApp.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using AlmaApp.WebApi.Common.Auth;
using AlmaApp.Domain.Auth;

var builder = WebApplication.CreateBuilder(args);

// -------------------------
// 1) SERVICES (antes do Build)
// -------------------------

// HttpContext (necessário para IUserContext)
builder.Services.AddHttpContextAccessor();

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

// ---- RBAC / UserContext & Authorization ----
builder.Services.AddScoped<IUserContext, UserContext>();
builder.Services.AddScoped<IActivitiesService, ActivitiesService>();
builder.Services.AddScoped<IRoomsService, RoomsService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<IClientsService, ClientsService>();
builder.Services.AddScoped<IClassesService, ClassesService>();
builder.Services.AddScoped<IClassRequestsService, ClassRequestsService>();
builder.Services.AddScoped<IAdminStaffService, AdminStaffService>();
builder.Services.AddScoped<IAdminRbacService, AdminRbacService>();
builder.Services.AddScoped<IMeService, MeService>();
builder.Services.AddScoped<IMeOnboardingService, MeOnboardingService>();
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IAuthorizationHandler, RoleAuthorizationHandler>();       // was Singleton -> Scoped
builder.Services.AddScoped<IAuthorizationHandler, RolesAnyAuthorizationHandler>();   // was Singleton -> Scoped

builder.Services.AddAuthorization(opt =>
{
    // do token Firebase
    opt.AddPolicy("EmailVerified", p =>
        p.RequireClaim("email_verified", "true", "True", "1"));

    // da BD (RoleAssignments) via IUserContext
    opt.AddPolicy("Admin",  p => p.Requirements.Add(new RoleRequirement(RoleName.Admin)));
    opt.AddPolicy("Staff",  p => p.Requirements.Add(new RoleRequirement(RoleName.Staff)));
    opt.AddPolicy("Client", p => p.Requirements.Add(new RoleRequirement(RoleName.Client)));

    // OR entre roles
    opt.AddPolicy("AdminOrStaff",
        p => p.Requirements.Add(new RolesAnyRequirement(RoleName.Admin, RoleName.Staff)));
});

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
        o.IncludeErrorDetails = true; // detalhes no header WWW-Authenticate (DEV)
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
        Description = "Cole aqui o Firebase ID token (sem 'Bearer ')",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { securityScheme, Array.Empty<string>() } });

    // --- tipos 'novos' para o Swagger ---
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
// Helpers locais
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

// Semeia o Admin configurado em Seed:AdminFirebaseUid
static async Task EnsureSeedAdminAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var uid = cfg["Seed:AdminFirebaseUid"];
    if (string.IsNullOrWhiteSpace(uid))
        return; // nada a fazer

    var hasAdmin = await db.RoleAssignments
        .AnyAsync(r => r.FirebaseUid == uid && r.Role == RoleName.Admin);

    if (!hasAdmin)
    {
        db.RoleAssignments.Add(new RoleAssignment(uid, RoleName.Admin));
        await db.SaveChangesAsync();
        Console.WriteLine($"[SEED] Atribuído role Admin ao UID '{uid}'.");
    }
    else
    {
        Console.WriteLine($"[SEED] UID '{uid}' já tem role Admin.");
    }
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

    await EnsureDatabaseExistsAndMigrateAsync(app);
    await EnsureSeedAdminAsync(app);   // <= seed do Admin em dev/docker/ci
}
else
{
    await EnsureDatabaseExistsAndMigrateAsync(app);
    await EnsureSeedAdminAsync(app);   // <= seed também noutros ambientes
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

// Quem sou eu + roles da BD
app.MapGet("/whoami", async (IUserContext user) =>
{
    var roles = await user.GetRolesAsync();
    return Results.Ok(new { uid = user.Uid, roles });
}).RequireAuthorization();

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
