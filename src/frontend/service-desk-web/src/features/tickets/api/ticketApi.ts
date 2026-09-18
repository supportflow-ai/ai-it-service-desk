import apiClient from '@/shared/api/apiClient';
import { CreateTicketPayload, TicketDto } from '../types';

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
   * Tạo một yêu cầu (Ticket) mới.
   * Quá trình này sẽ tạo Draft và Submit luôn trong 1 bước cho MVP, 
   * hoặc làm theo 2 bước (Tạo Draft -> Submit) tuỳ backend.
   */
  createTicket: async (payload: CreateTicketPayload): Promise<{ id: string, ticketNumber: string }> => {
    // Gọi API POST tạo ticket
    const response = await apiClient.post<{ id: string, ticketNumber: string }>('/tickets', payload);
    return response.data;
  },
};
