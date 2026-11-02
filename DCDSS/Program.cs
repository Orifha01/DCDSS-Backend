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
using Microsoft.OpenApi.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── MVC / Filters ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuditActionFilter>();
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<AuditActionFilter>();
});

// ── Swagger ──────────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DCDSS API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── Database ────────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
var serverVersion = new MySqlServerVersion(new Version(8, 0, 34));

builder.Services.AddDbContextFactory<DcdssContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// ── Configuration binding & validation ─────────────────────────────────────────
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("CorsSettings"));
builder.Services.Configure<AdminSeedSettings>(builder.Configuration.GetSection("AdminSeed"));
builder.Services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("JwtSettings"))
    .ValidateOnStart();

// ── Authentication ──────────────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
                Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

// ── Authorization ───────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ── CORS ────────────────────────────────────────────────────────────────────────
var cors = builder.Configuration.GetSection("CorsSettings").Get<CorsSettings>()!;
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        policy.WithOrigins(cors.AllowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ── Health Checks ───────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DcdssContext>();

// ── HTTP Context ────────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

// ── Repositories ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IPatientAllergyRepository, PatientAllergyRepository>();
builder.Services.AddScoped<IMedicalRecordRepository, MedicalRecordRepository>();
builder.Services.AddScoped<ILabResultRepository, LabResultRepository>();
builder.Services.AddScoped<IMedicationRepository, MedicationRepository>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// ── Services (scoped) ───────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IMedicalRecordService, MedicalRecordService>();
builder.Services.AddScoped<ILabService, LabService>();
builder.Services.AddScoped<IMedicationService, MedicationService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISafetyCheckService, SafetyCheckService>();
builder.Services.AddScoped<ICdsEngine, CdsEngine>();
builder.Services.AddScoped<IAdminSeeder, AdminSeeder>();
builder.Services.AddScoped<ICdsRuleEvaluator, CdsRuleEvaluator>();

// ── Services (singleton) ────────────────────────────────────────────────────────
builder.Services.AddSingleton<IClinicalRuleStore, ClinicalRuleStore>();
builder.Services.AddSingleton<IReferenceRangeParser, ReferenceRangeParser>();
builder.Services.AddSingleton<ITokenService, TokenService>();

// ── Security helpers ────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddTransient<PasswordHasher<User>>();

// ── Build ────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Seed admin ───────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IAdminSeeder>().SeedAsync();
}

// ── Pipeline ─────────────────────────────────────────────────────────────────────
// GlobalExceptionMiddleware MUST be first in the pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
