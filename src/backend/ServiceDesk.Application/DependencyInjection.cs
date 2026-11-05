using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Identity.RegisterUser;
using ServiceDesk.Application.Identity.LoginUser;
using ServiceDesk.Application.Identity.GetCurrentUser;
using ServiceDesk.Application.Identity.GetUsersByRole;
using ServiceDesk.Application.Identity.RefreshAuth;
using ServiceDesk.Application.Identity.RevokeToken;
using ServiceDesk.Application.Identity.GetDepartments;
using ServiceDesk.Application.Identity.ChangePassword;
using ServiceDesk.Application.Identity.ForgotPassword;
using ServiceDesk.Application.Identity.ResetPassword;
using ServiceDesk.Application.Identity.AssignUserRoles;
using ServiceDesk.Application.Ticketing.CreateTicket;
using ServiceDesk.Application.Ticketing.GetMyTickets;
using ServiceDesk.Application.Ticketing.GetTicketDetail;
using ServiceDesk.Application.Ticketing.UpdateTicket;
using ServiceDesk.Application.Ticketing.ClaimTicket;
using ServiceDesk.Application.Ticketing.ResolveTicket;
using ServiceDesk.Application.Ticketing.ReopenTicket;
using ServiceDesk.Application.Ticketing.CloseTicket;
using ServiceDesk.Application.Ticketing.CancelTicket;
using ServiceDesk.Application.Ticketing.GetAgentQueue;
using ServiceDesk.Application.Ticketing.AssignTicket;
using ServiceDesk.Application.Ticketing.UnassignTicket;
using ServiceDesk.Application.Ticketing.AddComment;
using ServiceDesk.Application.Ticketing.AddInternalNote;
using ServiceDesk.Application.Ticketing.GetTicketTimeline;
using ServiceDesk.Application.Ticketing.GenerateAttachmentUploadUrl;
using ServiceDesk.Application.Ticketing.GetAttachmentDownloadUrl;
using ServiceDesk.Application.Ticketing.ConfirmAttachmentUpload;
using ServiceDesk.Application.Ticketing.GetTicketAttachments;
using ServiceDesk.Application.Knowledge.CreateDraftArticle;
using ServiceDesk.Application.Knowledge.UpdateDraftArticle;
using ServiceDesk.Application.Knowledge.PublishArticle;
using ServiceDesk.Application.Knowledge.ArchiveArticle;
using ServiceDesk.Application.Knowledge.GetArticleDetail;
using ServiceDesk.Application.Knowledge.SearchKnowledge;

namespace ServiceDesk.Application;

/// <summary>
/// Registers Application layer services.
/// Called from the API composition root.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services
            .AddTicketingApplication()
            .AddIdentityApplication()
            .AddKnowledgeApplication();
    }

    public static IServiceCollection AddTicketingApplication(this IServiceCollection services)
    {
        services.AddScoped<ICreateTicket, CreateTicketUseCase>();
        services.AddScoped<IGetMyTickets, GetMyTicketsUseCase>();
        services.AddScoped<IGetTicketDetail, GetTicketDetailUseCase>();
        services.AddScoped<IUpdateTicket, UpdateTicketUseCase>();
        services.AddScoped<IClaimTicket, ClaimTicketUseCase>();
        services.AddScoped<IResolveTicket, ResolveTicketUseCase>();
        services.AddScoped<IReopenTicket, ReopenTicketUseCase>();
        services.AddScoped<ICloseTicket, CloseTicketUseCase>();
        services.AddScoped<ICancelTicket, CancelTicketUseCase>();
        services.AddScoped<IGetAgentQueue, GetAgentQueueUseCase>();
        services.AddScoped<IAssignTicket, AssignTicketUseCase>();
        services.AddScoped<IUnassignTicket, UnassignTicketUseCase>();
        services.AddScoped<IAddComment, AddCommentUseCase>();
        services.AddScoped<IAddInternalNote, AddInternalNoteUseCase>();
        services.AddScoped<IGetTicketTimeline, GetTicketTimelineUseCase>();
        services.AddScoped<IGenerateAttachmentUploadUrl, GenerateAttachmentUploadUrlUseCase>();
        services.AddScoped<IGetAttachmentDownloadUrl, GetAttachmentDownloadUrlUseCase>();
        services.AddScoped<IConfirmAttachmentUpload, ConfirmAttachmentUploadUseCase>();
        services.AddScoped<IGetTicketAttachments, GetTicketAttachmentsUseCase>();
        return services;
    }

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
}
