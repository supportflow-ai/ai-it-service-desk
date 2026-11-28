using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDesk.Modules.AIAssistance.Application.Ports;
using ServiceDesk.Modules.AIAssistance.Application.Triage;
using ServiceDesk.Modules.AIAssistance.Infrastructure;
using ServiceDesk.Modules.AIAssistance.Infrastructure.Options;

namespace ServiceDesk.Modules.AIAssistance;

/// <summary>
/// Điểm vào duy nhất của module AIAssistance cho composition root.
/// Chỉ bật adapter HTTP khi được cấu hình; mặc định giữ NoOp cho môi trường chưa nối AI.
/// </summary>
public static class AIAssistanceModule
{
    /// <summary>Đăng ký toàn bộ module: use case và adapter.</summary>
    public static IServiceCollection AddAIAssistanceModule(this IServiceCollection services, IConfiguration? configuration = null) =>
        services.AddAIAssistanceApplication().AddAIAssistanceInfrastructure(configuration);

    /// <summary>Đăng ký các use case của AIAssistance.</summary>
    public static IServiceCollection AddAIAssistanceApplication(this IServiceCollection services)
    {
        services.AddScoped<IRequestAiTriage, RequestAiTriageUseCase>();
        services.AddScoped<IGetTicketTriage, GetTicketTriageUseCase>();
        services.AddScoped<IDecideAiTriage, DecideAiTriageUseCase>();
        return services;
    }

    /// <summary>Đăng ký adapter phân loại (HTTP hoặc NoOp theo <c>Ai:Enabled</c>), embedding và kho PostgreSQL (host phải gọi <c>AddPersistence</c>).</summary>
    public static IServiceCollection AddAIAssistanceInfrastructure(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var section = configuration?.GetSection(AiOptions.SectionName);
        var options = services.AddOptions<AiOptions>();
        if (section is not null) options.Bind(section);
        options.Validate(value => value.IsValid(), "Cấu hình Ai không hợp lệ: cần origin HTTP(S), token S2S và timeout 1–30 giây.").ValidateOnStart();
        services.AddSingleton<ClassificationCircuit>();
        services.AddHttpClient<HttpTicketClassificationService>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<NoOpTicketClassificationService>();
        services.AddScoped<ITicketClassificationService>(sp => sp.GetRequiredService<IOptions<AiOptions>>().Value.Enabled
            ? sp.GetRequiredService<HttpTicketClassificationService>() : sp.GetRequiredService<NoOpTicketClassificationService>());
        services.AddSingleton<IEmbeddingService, NoOpEmbeddingService>();
        services.AddScoped<IAiAnalysisStore, PostgresAiAnalysisStore>();
        return services;
    }
}
