using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ServiceDesk.Api.Endpoints;
using ServiceDesk.Api.Identity;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Options;

var builder = WebApplication.CreateBuilder(args);

// --- Đăng ký các tầng kiến trúc (Clean Architecture layers) ---
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// --- Cấu hình xác thực người dùng (JWT Bearer Authentication) ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
.Configure<IOptions<JwtOptions>>((options, configured) =>
{
    var jwtOptions = configured.Value;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.SecretKey))
    };
});

// --- Cấu hình chính sách phân quyền theo vai trò (Authorization Policies) ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyNames.RequireAdmin, p => p.RequireRole(RoleNames.Admin));
    options.AddPolicy(PolicyNames.RequireAgent, p => p.RequireRole(RoleNames.Agent, RoleNames.Admin));
    options.AddPolicy(PolicyNames.RequireRequester, p => p.RequireRole(RoleNames.Requester, RoleNames.Agent, RoleNames.Admin));
});

// --- Kiểm tra sức khỏe dịch vụ (Health Checks) ---
builder.Services.AddBackendHealthChecks();

// --- Tài liệu hóa API với Swagger / OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AI IT Service Desk API",
        Version = "v1",
        Description = "Hệ thống Quản lý và Tự động hóa Xử lý Yêu cầu IT Nội bộ Ứng dụng AI"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập token JWT Bearer: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// --- Cấu hình CORS cho môi trường phát triển ---
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

// --- Cấu hình Forwarded Headers khi chạy sau Reverse Proxy (NGINX) ---
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    foreach (var network in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
});

// --- Chuẩn hóa thông báo lỗi theo chuẩn RFC 7807 Problem Details ---
builder.Services.AddProblemDetails();

var app = builder.Build();

// --- Pipeline Middleware HTTP ---
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

// Đăng ký các nhóm route API
app.MapAuthEndpoints();
app.MapDiagnosticEndpoints();
app.MapTicketEndpoints();

// Endpoints kiểm tra sức khỏe
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

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

// Endpoint trả về thông tin cơ bản của API
app.MapGet("/api", () => Results.Ok(new
{
    service = "AI IT Service Desk API",
    version = "1.0.0-foundation",
    status = "running"
}))
.WithTags("System")
.WithName("GetApiInfo");

app.Run();

// Cho phép WebApplicationFactory truy cập Program class trong Integration Tests
namespace ServiceDesk.Api
{
    public partial class Program;
}
