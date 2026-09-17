using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Application;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Options;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration binding ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection(MinioOptions.SectionName));

// --- Layer registration ---
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// --- Authentication (JWT Bearer) ---
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? new JwtOptions();

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
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.SecretKey))
    };
});

builder.Services.AddAuthorization();

// --- Health checks ---
// Use a factory delegate so the connection string is resolved lazily from IOptions<DatabaseOptions>
// at health-check execution time — not captured from IConfiguration at startup.
// This allows WebApplicationFactory to override Database:ConnectionString via ConfigureAppConfiguration
// for integration tests without modifying service registrations.
builder.Services.AddHealthChecks()
    .AddNpgSql(
        sp => sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString,
        name: "postgresql",
        tags: ["db", "ready"]);

// --- API / Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AI IT Service Desk API",
        Version = "v1",
        Description = "Internal IT Service Request Management System with AI Assistance"
    });
});

// --- CORS (development) ---
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// --- Forwarded headers (behind NGINX) ---
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// --- Problem Details ---
builder.Services.AddProblemDetails();

var app = builder.Build();

// --- Middleware pipeline ---
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// --- Health check endpoint ---
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

// --- API info endpoint ---
app.MapGet("/api", () => Results.Ok(new
{
    service = "AI IT Service Desk API",
    version = "1.0.0-foundation",
    status = "running"
}))
.WithTags("System")
.WithName("GetApiInfo");

app.Run();

// Make Program accessible for integration tests
namespace ServiceDesk.Api
{
    public partial class Program;
}
