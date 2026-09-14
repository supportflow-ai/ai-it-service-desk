import apiClient from '@/shared/api/apiClient';
import type {
  TriageRequestDto,
  RequesterTriageView,
  AgentTriageView,
  SuggestionDecisionRequest,
  SuggestionDto,
} from './types';

/**
 * AI Assistance API client.
 * Endpoints are blocked until Identity/Authorization is implemented.
 * Types and client are ready for integration.
 */
export const aiAssistanceApi = {
  /**
   * Request AI triage analysis for a ticket.
   * POST /api/ai/ticket-analysis
   */
  analyzeTicket: async (request: TriageRequestDto): Promise<AgentTriageView> => {
    const response = await apiClient.post<AgentTriageView>(
      '/api/ai/ticket-analysis',
      request,
    );
    return response.data;
  },

  /**
   * Request AI triage analysis — requester-safe projection.
   * POST /api/ai/ticket-analysis/requester
   */
  analyzeTicketRequester: async (request: TriageRequestDto): Promise<RequesterTriageView> => {
    const response = await apiClient.post<RequesterTriageView>(
      '/api/ai/ticket-analysis/requester',
      request,
    );
    return response.data;
  },

  /**
   * Submit a decision on an AI suggestion.
   * POST /api/ai/suggestions/{id}/decision
   */
  submitDecision: async (
    suggestionId: string,
    request: SuggestionDecisionRequest,
  ): Promise<SuggestionDto> => {
    const response = await apiClient.post<SuggestionDto>(
      `/api/ai/suggestions/${suggestionId}/decision`,
      request,
    );
    return response.data;
  },
};
