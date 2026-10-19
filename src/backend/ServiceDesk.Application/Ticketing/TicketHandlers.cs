using ServiceDesk.Application.Common.Exceptions;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Domain.Ticketing;

namespace ServiceDesk.Application.Ticketing;

public sealed class TicketHandlers(
    ITicketStore ticketStore,
    IIdentityService identityService,
    IClock clock,
    ICurrentUser currentUser)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public async Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        ValidateCreateRequest(request);

        var ticket = Ticket.Create(
            userId,
            request.Title,
            request.Description,
            request.Category,
            request.Impact,
            request.Urgency,
            clock.UtcNow);

        await ticketStore.AddAsync(ticket, cancellationToken);
        await ticketStore.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket);
    }

    public async Task<TicketResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var ticket = await ticketStore.GetByIdAsync(id, cancellationToken);
        if (ticket is null)
            throw new NotFoundException($"Ticket with id '{id}' was not found.");

        EnsureCanReadTicket(ticket, userId);

        return ToResponse(ticket);
    }

    public async Task<PageResponse<TicketResponse>> ListAsync(TicketListRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();

        if (request.Page < 1)
            throw new ValidationException("Page must be at least 1.");
        if (request.PageSize < 1 || request.PageSize > MaximumPageSize)
            throw new ValidationException($"PageSize must be between 1 and {MaximumPageSize}.");

        string? requesterId;
        string? assigneeId;

        if (!IsAgent() && !IsAdmin())
        {
            // Requesters can only see their own tickets; ignore incoming requester/assignee filter
            requesterId = userId;
            assigneeId = null;
        }
        else
        {
            // Agents and Admins see workspace and can filter
            requesterId = request.RequesterId;
            assigneeId = request.AssigneeId;
        }

        var filter = new TicketListFilter(
            requesterId,
            assigneeId,
            request.Status,
            request.Page,
            request.PageSize);

        var paged = await ticketStore.ListAsync(filter, cancellationToken);

        return new PageResponse<TicketResponse>(
            paged.Items.Select(ToResponse).ToList(),
            request.Page,
            request.PageSize,
            paged.TotalCount);
    }

    public async Task<TicketResponse> UpdateAsync(Guid id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        ValidateUpdateRequest(request);

        var ticket = await ticketStore.GetByIdAsync(id, cancellationToken);
        if (ticket is null)
            throw new NotFoundException($"Ticket with id '{id}' was not found.");

        EnsureCanModifySubmittedTicket(ticket, userId);

        if (ticket.Status != TicketStatus.Submitted)
            throw new ValidationException("Only submitted tickets can be updated.");

        ticket.Update(
            request.Title,
            request.Description,
            request.Category,
            request.Impact,
            request.Urgency,
            clock.UtcNow);

        await ticketStore.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket);
    }

    public async Task<TicketResponse> ClaimAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        if (!IsAgent() && !IsAdmin())
            throw new ForbiddenException("Only agents or administrators can claim tickets.");

        var ticket = await ticketStore.GetByIdAsync(id, cancellationToken);
        if (ticket is null)
            throw new NotFoundException($"Ticket with id '{id}' was not found.");

        if (ticket.Status != TicketStatus.Submitted || !string.IsNullOrWhiteSpace(ticket.AssigneeId))
            throw new ConflictException("Ticket is already assigned or no longer submitted.");

        ticket.Claim(userId, clock.UtcNow);
        await ticketStore.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket);
    }

    public async Task<TicketResponse> AssignAsync(Guid id, AssignTicketRequest request, CancellationToken cancellationToken)
    {
        RequireUserId();
        if (!IsAdmin())
            throw new ForbiddenException("Only administrators can assign tickets.");

        if (string.IsNullOrWhiteSpace(request.AssigneeId))
            throw new ValidationException("AssigneeId is required.");

        var isAgent = await identityService.IsInRoleAsync(request.AssigneeId, "Agent", cancellationToken);
        var isAdmin = await identityService.IsInRoleAsync(request.AssigneeId, "Admin", cancellationToken);
        if (!isAgent && !isAdmin)
            throw new ValidationException("Assignee must have role Agent or Admin.");

        var ticket = await ticketStore.GetByIdAsync(id, cancellationToken);
        if (ticket is null)
            throw new NotFoundException($"Ticket with id '{id}' was not found.");

        if (ticket.Status is TicketStatus.Cancelled or TicketStatus.Closed)
            throw new ValidationException("Terminal tickets cannot be assigned.");

        ticket.Assign(request.AssigneeId, clock.UtcNow);
        await ticketStore.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket);
    }

    public async Task<TicketResponse> ResolveAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var ticket = await ticketStore.GetByIdAsync(id, cancellationToken);
        if (ticket is null)
            throw new NotFoundException($"Ticket with id '{id}' was not found.");

        if (!IsAdmin() && ticket.AssigneeId != userId)
            throw new ForbiddenException("Only the assigned agent or an administrator can resolve this ticket.");

        if (ticket.Status != TicketStatus.InProgress)
            throw new ValidationException("Only in-progress tickets can be resolved.");

        ticket.Resolve(clock.UtcNow);
        await ticketStore.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket);
    }

    public async Task<TicketResponse> ReopenAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var ticket = await ticketStore.GetByIdAsync(id, cancellationToken);
        if (ticket is null)
            throw new NotFoundException($"Ticket with id '{id}' was not found.");

        EnsureCanReopenOrClose(ticket, userId);

        if (ticket.Status != TicketStatus.Resolved)
            throw new ValidationException("Only resolved tickets can be reopened.");

        ticket.Reopen(clock.UtcNow);
        await ticketStore.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket);
    }

    public async Task<TicketResponse> CloseAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var ticket = await ticketStore.GetByIdAsync(id, cancellationToken);
        if (ticket is null)
            throw new NotFoundException($"Ticket with id '{id}' was not found.");

        EnsureCanReopenOrClose(ticket, userId);

        if (ticket.Status != TicketStatus.Resolved)
            throw new ValidationException("Only resolved tickets can be closed.");

        ticket.Close(clock.UtcNow);
        await ticketStore.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket);
    }

    public async Task<TicketResponse> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var ticket = await ticketStore.GetByIdAsync(id, cancellationToken);
        if (ticket is null)
            throw new NotFoundException($"Ticket with id '{id}' was not found.");

        EnsureCanModifySubmittedTicket(ticket, userId);

        if (ticket.Status != TicketStatus.Submitted)
            throw new ValidationException("Only submitted tickets can be cancelled.");

        ticket.Cancel(clock.UtcNow);
        await ticketStore.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket);
    }

    public async Task<TicketCommentResponse> AddCommentAsync(Guid id, AddCommentRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ValidationException("Comment content is required.");

        var ticket = await ticketStore.GetByIdAsync(id, cancellationToken);
        if (ticket is null)
            throw new NotFoundException($"Ticket with id '{id}' was not found.");

        EnsureCanReadTicket(ticket, userId);

        if (ticket.Status is TicketStatus.Cancelled or TicketStatus.Closed)
            throw new ValidationException("Terminal tickets cannot receive comments.");

        ticket.AddComment(userId, request.Content, clock.UtcNow);
        await ticketStore.SaveChangesAsync(cancellationToken);

        var added = ticket.Comments.Last();
        return ToCommentResponse(added);
    }

    public async Task<IReadOnlyList<TicketCommentResponse>> ListCommentsAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var ticket = await ticketStore.GetByIdAsync(id, cancellationToken);
        if (ticket is null)
            throw new NotFoundException($"Ticket with id '{id}' was not found.");

        EnsureCanReadTicket(ticket, userId);

        var comments = await ticketStore.GetCommentsAsync(id, cancellationToken);
        return comments.Select(ToCommentResponse).ToList();
    }

    private string RequireUserId()
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
            throw new ForbiddenException("Authentication required.");
        return currentUser.UserId;
    }

    private bool IsAdmin() => currentUser.IsInRole("Admin");
    private bool IsAgent() => currentUser.IsInRole("Agent");

    private void EnsureCanReadTicket(Ticket ticket, string userId)
    {
        if (!IsAgent() && !IsAdmin() && ticket.RequesterId != userId)
            throw new NotFoundException($"Ticket with id '{ticket.Id}' was not found.");
    }

    private void EnsureCanModifySubmittedTicket(Ticket ticket, string userId)
    {
        if (!IsAdmin() && ticket.RequesterId != userId)
        {
            if (IsAgent())
                throw new ForbiddenException("Agents cannot modify tickets owned by other users.");
            throw new NotFoundException($"Ticket with id '{ticket.Id}' was not found.");
        }
    }

    private void EnsureCanReopenOrClose(Ticket ticket, string userId)
    {
        if (!IsAdmin() && ticket.RequesterId != userId)
        {
            if (IsAgent())
                throw new ForbiddenException("Only the requester or an administrator can reopen/close this ticket.");
            throw new NotFoundException($"Ticket with id '{ticket.Id}' was not found.");
        }
    }

    private static void ValidateCreateRequest(CreateTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException("Title is required.");
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ValidationException("Description is required.");
        if (string.IsNullOrWhiteSpace(request.Category))
            throw new ValidationException("Category is required.");
    }

    private static void ValidateUpdateRequest(UpdateTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException("Title is required.");
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ValidationException("Description is required.");
        if (string.IsNullOrWhiteSpace(request.Category))
            throw new ValidationException("Category is required.");
    }

    private static TicketResponse ToResponse(Ticket ticket) =>
        new(
            ticket.Id,
            ticket.RequesterId,
            ticket.AssigneeId,
            ticket.Title,
            ticket.Description,
            ticket.Category,
            ticket.Impact,
            ticket.Urgency,
            ticket.Priority,
            ticket.Status,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.ResolvedAt,
            ticket.ClosedAt,
            ticket.CancelledAt);

    private static TicketCommentResponse ToCommentResponse(TicketComment comment) =>
        new(
            comment.Id,
            comment.TicketId,
            comment.AuthorId,
            comment.Content,
            comment.CreatedAt);
}
