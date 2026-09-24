export type TicketCategory = 'ACC' | 'NET' | 'VPN' | 'MAIL' | 'SW' | 'HW' | 'PRINT' | 'ACCESS' | 'EQUIP' | 'SEC' | 'OTHER';

export enum TicketStatus {
  Draft = 0,
  Submitted = 1,
  Triaged = 2,
  Assigned = 3,
  InProgress = 4,
  PendingUser = 5,
  PendingExternal = 6,
  Resolved = 7,
  Closed = 8
}
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
