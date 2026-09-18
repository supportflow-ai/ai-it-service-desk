export type TicketCategory = 'ACC' | 'NET' | 'VPN' | 'MAIL' | 'SW' | 'HW' | 'PRINT' | 'ACCESS' | 'EQUIP' | 'SEC' | 'OTHER';

export type TicketStatus = 'Draft' | 'Submitted' | 'Triaged' | 'Assigned' | 'InProgress' | 'PendingUser' | 'PendingExternal' | 'Resolved' | 'Closed';
export type TicketPriority = 'P1' | 'P2' | 'P3' | 'P4';

export interface CreateTicketPayload {
  title: string;
  categoryId: TicketCategory;
  description: string;
}

export interface TicketDto {
  id: string;
  ticketNumber: string;
  title: string;
  description?: string;
  categoryId: TicketCategory;
  status: TicketStatus;
  priority?: TicketPriority;
  resolution?: string;
  createdAt: string;
}
