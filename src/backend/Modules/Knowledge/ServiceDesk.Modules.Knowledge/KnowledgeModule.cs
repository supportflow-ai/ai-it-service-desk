using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Knowledge.Application.ArchiveArticle;
using ServiceDesk.Modules.Knowledge.Application.CreateDraftArticle;
using ServiceDesk.Modules.Knowledge.Application.GetArticleDetail;
using ServiceDesk.Modules.Knowledge.Application.Ports;
using ServiceDesk.Modules.Knowledge.Application.PublishArticle;
using ServiceDesk.Modules.Knowledge.Application.SearchKnowledge;
using ServiceDesk.Modules.Knowledge.Application.UpdateDraftArticle;
using ServiceDesk.Modules.Knowledge.Infrastructure;

namespace ServiceDesk.Modules.Knowledge;

/// <summary>
/// Điểm vào duy nhất của module Knowledge cho composition root.
/// Host gọi <see cref="AddKnowledgeModule"/> và <c>MapKnowledgeEndpoints</c>.
/// </summary>
public static class KnowledgeModule
{
    /// <summary>Đăng ký toàn bộ module: use case và adapter.</summary>
    public static IServiceCollection AddKnowledgeModule(this IServiceCollection services) =>
        services.AddKnowledgeApplication().AddKnowledgeInfrastructure();

    /// <summary>Đăng ký use case của Knowledge.</summary>
    public static IServiceCollection AddKnowledgeApplication(this IServiceCollection services)
    {
        services.AddScoped<ICreateDraftArticle, CreateDraftArticleUseCase>();
        services.AddScoped<IUpdateDraftArticle, UpdateDraftArticleUseCase>();
        services.AddScoped<IPublishArticle, PublishArticleUseCase>();
        services.AddScoped<IArchiveArticle, ArchiveArticleUseCase>();
        services.AddScoped<IGetArticleDetail, GetArticleDetailUseCase>();
        services.AddScoped<ISearchKnowledge, SearchKnowledgeUseCase>();
        return services;
    }

    /// <summary>Đăng ký adapter PostgreSQL cho các cổng dữ liệu của Knowledge.</summary>
    public static IServiceCollection AddKnowledgeInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IKnowledgeWriter, PostgresKnowledgeWriter>();
        services.AddScoped<IKnowledgeQueries, PostgresKnowledgeQueries>();
        return services;
    }
}
