using System.Text;
using DCDSS.Configuration;
using DCDSS.Data;
using DCDSS.Entities;
using DCDSS.Middleware;
using DCDSS.Repositories;
using DCDSS.Repositories.Interfaces;
using DCDSS.Security;
using DCDSS.Services;
using DCDSS.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────────
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("CorsSettings"));
builder.Services.Configure<AdminSeedSettings>(builder.Configuration.GetSection("AdminSeed"));

// Validate JWT settings on startup
builder.Services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();

// ── Database ────────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContextFactory<DcdssContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mysqlOptions => mysqlOptions.SchemaBehavior(MySqlSchemaBehavior.Ignore)));

// ── Repositories ────────────────────────────────────────────────────────────────
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IPatientAllergyRepository, PatientAllergyRepository>();
builder.Services.AddScoped<IMedicalRecordRepository, MedicalRecordRepository>();
builder.Services.AddScoped<ILabResultRepository, LabResultRepository>();
builder.Services.AddScoped<IMedicationRepository, MedicationRepository>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// ── Services ────────────────────────────────────────────────────────────────────
// Singletons
builder.Services.AddSingleton<IClinicalRuleStore, ClinicalRuleStore>();
builder.Services.AddSingleton<IReferenceRangeParser, ReferenceRangeParser>();
builder.Services.AddSingleton<ITokenService, TokenService>();

// Scoped services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IMedicalRecordService, MedicalRecordService>();
builder.Services.AddScoped<ICdsRuleEvaluator, CdsRuleEvaluator>();
builder.Services.AddScoped<ICdsEngine, CdsEngine>();
builder.Services.AddScoped<ILabService, LabService>();
builder.Services.AddScoped<IMedicationService, MedicationService>();
builder.Services.AddScoped<ISafetyCheckService, SafetyCheckService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAdminSeeder, AdminSeeder>();

// Security helpers
builder.Services.AddTransient<PasswordHasher<User>>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ── Authentication & Authorization ─────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
    ?? new JwtSettings();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.SigningKey))
    };
});

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// ── MVC / Filters ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuditActionFilter>();

builder.Services.AddControllers(options =>
{
    options.Filters.AddService<AuditActionFilter>();
});

// ── CORS ────────────────────────────────────────────────────────────────────────
var corsSettings = builder.Configuration.GetSection("CorsSettings").Get<CorsSettings>();
if (corsSettings?.AllowedOrigins is { Length: > 0 } origins)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });
}

// ── Swagger ──────────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "DCDSS API", Version = "v1" });

    // JWT bearer scheme for Swagger UI
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token."
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── Build ────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Pipeline ─────────────────────────────────────────────────────────────────────
// GlobalExceptionMiddleware must be FIRST
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

if (corsSettings?.AllowedOrigins is { Length: > 0 })
{
    app.UseCors();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ── Seed Admin ────────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IAdminSeeder>();
    await seeder.SeedAsync();
}

app.Run();
