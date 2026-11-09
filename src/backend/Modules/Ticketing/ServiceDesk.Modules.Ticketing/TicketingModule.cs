using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Ticketing.Application.AddComment;
using ServiceDesk.Modules.Ticketing.Application.AddInternalNote;
using ServiceDesk.Modules.Ticketing.Application.AssignTicket;
using ServiceDesk.Modules.Ticketing.Application.CancelTicket;
using ServiceDesk.Modules.Ticketing.Application.ClaimTicket;
using ServiceDesk.Modules.Ticketing.Application.CloseTicket;
using ServiceDesk.Modules.Ticketing.Application.ConfirmAttachmentUpload;
using ServiceDesk.Modules.Ticketing.Application.CreateTicket;
using ServiceDesk.Modules.Ticketing.Application.GenerateAttachmentUploadUrl;
using ServiceDesk.Modules.Ticketing.Application.GetAgentQueue;
using ServiceDesk.Modules.Ticketing.Application.GetAttachmentDownloadUrl;
using ServiceDesk.Modules.Ticketing.Application.GetMyTickets;
using ServiceDesk.Modules.Ticketing.Application.GetTicketAttachments;
using ServiceDesk.Modules.Ticketing.Application.GetTicketDetail;
using ServiceDesk.Modules.Ticketing.Application.GetTicketTimeline;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Application.ReopenTicket;
using ServiceDesk.Modules.Ticketing.Application.ResolveTicket;
using ServiceDesk.Modules.Ticketing.Application.UnassignTicket;
using ServiceDesk.Modules.Ticketing.Application.UpdateTicket;
using ServiceDesk.Modules.Ticketing.Infrastructure;

namespace ServiceDesk.Modules.Ticketing;

/// <summary>
/// Điểm vào duy nhất của module Ticketing cho composition root (API, Worker).
/// Module tự đăng ký use case và adapter của mình; host chỉ gọi <see cref="AddTicketingModule"/>
/// và <c>MapTicketEndpoints</c>, không cần biết lớp triển khai bên trong.
/// Adapter persistence nhận <c>DbContext</c> dùng chung do host đăng ký (xem ServiceDesk.Infrastructure).
/// </summary>
public static class TicketingModule
{
    /// <summary>Đăng ký toàn bộ module: use case và adapter.</summary>
    public static IServiceCollection AddTicketingModule(this IServiceCollection services) =>
        services.AddTicketingApplication().AddTicketingInfrastructure();

    /// <summary>Đăng ký use case. Mỗi use case là một lớp internal thực hiện một thao tác.</summary>
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

    /// <summary>Đăng ký adapter PostgreSQL cho các cổng dữ liệu của Ticketing.</summary>
    public static IServiceCollection AddTicketingInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ITicketWriter, PostgresTicketWriter>();
        services.AddScoped<ITicketQueries, PostgresTicketQueries>();
        services.AddScoped<ITicketTimelineStore, PostgresTicketTimelineStore>();
        services.AddScoped<ITicketAttachmentStore, PostgresTicketAttachmentStore>();
        services.AddScoped<IAgentDirectory, AgentDirectory>();
        services.AddScoped<ISupportTeamMembershipReader, PostgresSupportTeamMembershipReader>();
        return services;
    }
}
