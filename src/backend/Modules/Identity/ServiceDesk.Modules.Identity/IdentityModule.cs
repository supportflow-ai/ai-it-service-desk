using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.Identity.Application.AssignUserRoles;
using ServiceDesk.Modules.Identity.Application.ChangePassword;
using ServiceDesk.Modules.Identity.Application.ForgotPassword;
using ServiceDesk.Modules.Identity.Application.GetCurrentUser;
using ServiceDesk.Modules.Identity.Application.GetDepartments;
using ServiceDesk.Modules.Identity.Application.GetUsersByRole;
using ServiceDesk.Modules.Identity.Application.LoginUser;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Modules.Identity.Application.RefreshAuth;
using ServiceDesk.Modules.Identity.Application.RegisterUser;
using ServiceDesk.Modules.Identity.Application.ResetPassword;
using ServiceDesk.Modules.Identity.Application.RevokeToken;
using ServiceDesk.Modules.Identity.Infrastructure;
using ServiceDesk.Modules.Identity.Infrastructure.Options;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Identity;

/// <summary>
/// Điểm vào duy nhất của module Identity cho composition root.
/// <list type="bullet">
/// <item><see cref="AddIdentityModule"/>: use case và cấp phát JWT.</item>
/// <item><see cref="AddIdentityPersistence{TContext}"/>: kho ASP.NET Core Identity trên DbContext dùng chung
/// và gateway tài khoản. Tách riêng vì module không biết kiểu DbContext cụ thể; host truyền vào.</item>
/// </list>
/// </summary>
public static class IdentityModule
{
    /// <summary>Đăng ký use case và hạ tầng cấp token của Identity.</summary>
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration) =>
        services.AddIdentityApplication().AddIdentityInfrastructure(configuration);

    /// <summary>Đăng ký use case của Identity.</summary>
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<IRegisterUser, RegisterUserUseCase>();
        services.AddScoped<ILoginUser, LoginUserUseCase>();
        services.AddScoped<IGetCurrentUser, GetCurrentUserUseCase>();
        services.AddScoped<IGetUsersByRole, GetUsersByRoleUseCase>();
        services.AddScoped<IRefreshAuth, RefreshAuthUseCase>();
        services.AddScoped<IRevokeToken, RevokeTokenUseCase>();
        services.AddScoped<IGetDepartments, GetDepartmentsUseCase>();
        services.AddScoped<IChangePassword, ChangePasswordUseCase>();
        services.AddScoped<IForgotPassword, ForgotPasswordUseCase>();
        services.AddScoped<IResetPassword, ResetPasswordUseCase>();
        services.AddScoped<IAssignUserRoles, AssignUserRolesUseCase>();
        return services;
    }

    /// <summary>
    /// Đăng ký trình cấp JWT, cổng gửi OTP và kiểm tra cấu hình <see cref="JwtOptions"/>, <see cref="SmtpEmailOptions"/> lúc khởi động.
    /// Có <c>Email:Smtp:Host</c> thì gửi OTP qua <see cref="SmtpOtpSender"/>; chưa cấu hình thì dùng <see cref="NoOpOtpSender"/>.
    /// </summary>
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAccessTokenIssuer, JwtTokenGenerator>();
        var smtpSection = configuration.GetSection(SmtpEmailOptions.SectionName);
        services.AddOptions<SmtpEmailOptions>().Bind(smtpSection).ValidateOnStart();
        services.AddSingleton<IValidateOptions<SmtpEmailOptions>, SmtpEmailOptionsValidator>();
        if (string.IsNullOrWhiteSpace(smtpSection[nameof(SmtpEmailOptions.Host)]))
        {
            services.AddSingleton<IOtpSender, NoOpOtpSender>();
        }
        else
        {
            services.AddSingleton<IOtpSender, SmtpOtpSender>();
        }

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        return services;
    }

    /// <summary>
    /// Đăng ký danh bạ người dùng (<see cref="IUserDirectory"/>) mà module khác dùng để tra vai trò và tên hiển thị.
    /// Chỉ cần bí danh <see cref="DbContext"/>, không cần UserManager, nên host nền cũng gọi được.
    /// </summary>
    public static IServiceCollection AddIdentityUserDirectory(this IServiceCollection services)
    {
        services.TryAddScoped<IUserDirectory, IdentityUserDirectory>();
        return services;
    }

    /// <summary>
    /// Đăng ký kho người dùng/vai trò ASP.NET Core Identity trên <typeparamref name="TContext"/>,
    /// bí danh <see cref="DbContext"/> cho adapter của module, gateway quản lý tài khoản và kho token/OTP/phòng ban.
    /// </summary>
    public static IServiceCollection AddIdentityPersistence<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddDataProtection();
        services.AddIdentityCore<IdentityUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<TContext>().AddDefaultTokenProviders();
        services.TryAddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddIdentityUserDirectory();
        services.AddScoped<IUserAccountGateway, IdentityAccountGateway>();
        services.AddScoped<IPasswordResetGateway, PasswordResetGateway>();
        services.AddScoped<IRefreshTokenStore, PostgresRefreshTokenStore>();
        services.AddScoped<IOtpCodeStore, PostgresOtpCodeStore>();
        services.AddScoped<IDepartmentQueries, PostgresDepartmentQueries>();
        return services;
    }
}
