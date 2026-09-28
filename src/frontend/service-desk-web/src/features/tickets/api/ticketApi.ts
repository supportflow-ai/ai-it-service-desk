import apiClient from '@/shared/api/apiClient';
import { CreateTicketPayload, TicketDto, TicketTimelineEventDto } from '../types';

export const ticketApi = {
  /**
   * Lấy danh sách ticket của Requester hiện tại (TC-271)
   */
  getMyTickets: async (): Promise<TicketDto[]> => {
    const response = await apiClient.get<TicketDto[]>('/tickets/me');
    return response.data;
  },

  /**
   * Lấy chi tiết ticket theo id của Requester hiện tại.
   */
  getTicketById: async (id: string): Promise<TicketDto> => {
    const response = await apiClient.get<TicketDto>(`/tickets/${id}`);
    return response.data;
  },

  /**
   * Lấy lịch sử/timeline của ticket (dành cho Agent Workspace)
   */
  getTicketTimeline: async (id: string): Promise<TicketTimelineEventDto[]> => {
    // Tạm mock data để dựng layout TC-312
    return [
      {
        id: 'event-1',
        ticketId: id,
        eventType: 'status_change',
        actorName: 'Hệ thống',
        content: 'Ticket được tạo mới',
        createdAt: new Date(Date.now() - 3600000).toISOString()
      }
    ];
  },

  /**
   * Tạo một yêu cầu (Ticket) mới.
   * Quá trình này sẽ tạo Draft và Submit luôn trong 1 bước cho MVP, 
   * hoặc làm theo 2 bước (Tạo Draft -> Submit) tuỳ backend.
   */
  createTicket: async (payload: CreateTicketPayload): Promise<{ id: string, ticketNumber: string }> => {
    const response = await apiClient.post<{ id: string, ticketNumber: string }>('/tickets', payload);
    return response.data;
  },

  // Command-oriented API endpoints (Theo PRD Section 10)
  triageTicket: async (id: string, payload: { categoryId: string, impact: string, urgency: string }) => {
    const response = await apiClient.post(`/tickets/${id}/triage`, payload);
    return response.data;
  },

  assignTicket: async (id: string, payload: { assignedAgentId: string }) => {
    const response = await apiClient.post(`/tickets/${id}/assign`, payload);
    return response.data;
  },

  startTicket: async (id: string) => {
    const response = await apiClient.post(`/tickets/${id}/start`);
    return response.data;
  },

  requestInfo: async (id: string, payload: { message: string }) => {
    const response = await apiClient.post(`/tickets/${id}/request-information`, payload);
    return response.data;
  },

  resolveTicket: async (id: string, payload: { resolution: string }) => {
    const response = await apiClient.post(`/tickets/${id}/resolve`, payload);
    return response.data;
  },

  addComment: async (id: string, payload: { content: string, isInternal: boolean }) => {
    const response = await apiClient.post(`/tickets/${id}/comments`, payload);
    return response.data;
  }
};
