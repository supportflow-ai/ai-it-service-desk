using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Identity.RegisterUser;
using ServiceDesk.Application.Identity.LoginUser;
using ServiceDesk.Application.Identity.GetCurrentUser;
using ServiceDesk.Application.Identity.GetUsersByRole;
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

namespace ServiceDesk.Application;

/// <summary>
/// Registers Application layer services.
/// Called from the API composition root.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services.AddTicketingApplication().AddIdentityApplication();
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
        return services;
    }
}
