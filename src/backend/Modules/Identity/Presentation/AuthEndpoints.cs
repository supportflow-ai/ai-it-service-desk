using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using ServiceDesk.SharedKernel.Web;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Modules.Identity.Application.RegisterUser;
using ServiceDesk.Modules.Identity.Application.LoginUser;
using ServiceDesk.Modules.Identity.Application.GetCurrentUser;
using ServiceDesk.Modules.Identity.Application.GetUsersByRole;
using ServiceDesk.Modules.Identity.Application.RefreshAuth;
using ServiceDesk.Modules.Identity.Application.RevokeToken;
using ServiceDesk.Modules.Identity.Application.ChangePassword;
using ServiceDesk.Modules.Identity.Application.ForgotPassword;
using ServiceDesk.Modules.Identity.Application.ResetPassword;
using ServiceDesk.Modules.Identity.Application.AssignUserRoles;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.SharedKernel.Authorization;

namespace ServiceDesk.Modules.Identity.Presentation;

/// <summary>
/// Định nghĩa các API endpoints cho phân hệ định danh & xác thực người dùng (Identity & Authentication).
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // POST /api/auth/register: Đăng ký tài khoản người dùng mới (mặc định gán vai trò Requester)
        group.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            IRegisterUser registerUser,
            CancellationToken ct) =>
        {
            var result = await registerUser.ExecuteAsync(request, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { errors = result.Errors });
            }

            return Results.Created("/api/auth/me", new
            {
                token = result.Token,
                refreshToken = result.RefreshToken,
                user = result.User
            });
        })
        .WithName("Register")
        .RequireRateLimiting(AuthRateLimiting.IpPolicy)
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status429TooManyRequests)
        .AllowAnonymous();

        // POST /api/auth/login: Đăng nhập hệ thống bằng email và mật khẩu (trả về JWT Bearer Token & Refresh Token)
        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            ILoginUser loginUser,
            CancellationToken ct) =>
        {
            var result = await loginUser.ExecuteAsync(request, ct);
            if (!result.Succeeded)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                token = result.Token,
                refreshToken = result.RefreshToken,
                user = result.User
            });
        })
        .WithName("Login")
        .RequireRateLimiting(AuthRateLimiting.IpPolicy)
        .RequireEmailRateLimit<LoginRequest>(AuthEmailOperation.Login, r => r.Email)
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status429TooManyRequests)
        .AllowAnonymous();

        // POST /api/auth/refresh: Làm mới Access Token bằng Refresh Token
        group.MapPost("/refresh", async (
            [FromBody] RefreshAuthRequest request,
            IRefreshAuth refreshAuth,
            CancellationToken ct) =>
        {
            var result = await refreshAuth.ExecuteAsync(request, ct);
            if (!result.Succeeded)
            {
                return Results.Json(new { error = result.Errors.FirstOrDefault() ?? "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(new
            {
                token = result.Token,
                refreshToken = result.RefreshToken,
                user = result.User
            });
        })
        .WithName("RefreshToken")
        .RequireRateLimiting(AuthRateLimiting.RefreshPolicy)
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status429TooManyRequests)
        .AllowAnonymous();

        // POST /api/auth/revoke: Thu hồi Refresh Token (Đăng xuất)
        group.MapPost("/revoke", async (
            [FromBody] RevokeTokenRequest request,
            IRevokeToken revokeToken,
            CancellationToken ct) =>
        {
            await revokeToken.ExecuteAsync(request, ct);
            return Results.NoContent();
        })
        .WithName("RevokeToken")
        .AllowAnonymous();

        // POST /api/auth/change-password: Đổi mật khẩu tài khoản đang đăng nhập
        group.MapPost("/change-password", async (
            [FromBody] ChangePasswordRequest request,
            IChangePassword changePassword,
            CancellationToken ct) =>
        {
            var result = await changePassword.ExecuteAsync(request, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { errors = result.Errors });
            }

            return Results.Ok(new { message = "Đổi mật khẩu thành công." });
        })
        .AddEndpointFilter<ApiErrorFilter>()
        .WithName("ChangePassword")
        .RequireAuthorization();

        // POST /api/auth/forgot-password: Yêu cầu gửi OTP đặt lại mật khẩu
        group.MapPost("/forgot-password", async (
            [FromBody] ForgotPasswordRequest request,
            IForgotPassword forgotPassword,
            CancellationToken ct) =>
        {
            await forgotPassword.ExecuteAsync(request, ct);
            return Results.Ok(new { message = "Nếu email tồn tại trong hệ thống, mã OTP đã được gửi." });
        })
        .WithName("ForgotPassword")
        .RequireRateLimiting(AuthRateLimiting.IpPolicy)
        .RequireEmailRateLimit<ForgotPasswordRequest>(AuthEmailOperation.ForgotPassword, r => r.Email)
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status429TooManyRequests)
        .AllowAnonymous();

        // POST /api/auth/reset-password: Đặt lại mật khẩu bằng mã OTP
        group.MapPost("/reset-password", async (
            [FromBody] ResetPasswordRequest request,
            IResetPassword resetPassword,
            CancellationToken ct) =>
        {
            var result = await resetPassword.ExecuteAsync(request, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { errors = result.Errors });
            }

            return Results.Ok(new { message = "Đặt lại mật khẩu thành công." });
        })
        .WithName("ResetPassword")
        .RequireRateLimiting(AuthRateLimiting.IpPolicy)
        .RequireEmailRateLimit<ResetPasswordRequest>(AuthEmailOperation.ResetPassword, r => r.Email)
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status429TooManyRequests)
        .AllowAnonymous();

        // GET /api/auth/me: Lấy thông tin tài khoản và danh sách vai trò (roles) của người dùng đang đăng nhập
        group.MapGet("/me", async (
            IGetCurrentUser getCurrentUser,
            CancellationToken ct) =>
        {
            var user = await getCurrentUser.ExecuteAsync(ct);
            if (user == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(user);
        })
        .AddEndpointFilter<ApiErrorFilter>()
        .WithName("GetCurrentUser")
        .RequireAuthorization();

        // GET /api/users: Lấy danh sách người dùng theo vai trò (Agent/Admin)
        app.MapGet("/api/users", async (
            [FromQuery] string? role,
            [FromServices] IGetUsersByRole getUsersByRole,
            CancellationToken ct) =>
        {
            var targetRole = string.IsNullOrWhiteSpace(role) ? RoleNames.Agent : role;
            var users = await getUsersByRole.ExecuteAsync(new(targetRole), ct);
            return Results.Ok(users.Select(user => new { user.UserId, user.FullName }));
        })
        .AddEndpointFilter<ApiErrorFilter>()
        .WithName("GetUsers")
        .RequireAuthorization(PolicyNames.RequireAgent)
        .Produces<IReadOnlyList<object>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/users/{id}/roles: Quản trị viên phân quyền cho người dùng
        app.MapPut("/api/users/{id:guid}/roles", async (
            Guid id,
            [FromBody] AssignRolesRequest request,
            [FromServices] IAssignUserRoles assignRoles,
            CancellationToken ct) =>
        {
            var result = await assignRoles.ExecuteAsync(id, request, ct);
            if (!result.Succeeded)
            {
                return Results.BadRequest(new { errors = result.Errors });
            }

            return Results.Ok(new { message = "Cập nhật vai trò thành công." });
        })
        .AddEndpointFilter<ApiErrorFilter>()
        .WithName("AssignUserRoles")
        .RequireAuthorization(PolicyNames.RequireAdmin)
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
