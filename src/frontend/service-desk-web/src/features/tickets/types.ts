export type TicketCategory = 'ACC' | 'NET' | 'VPN' | 'MAIL' | 'SW' | 'HW' | 'PRINT' | 'ACCESS' | 'EQUIP' | 'SEC' | 'OTHER';

export interface CreateTicketPayload {
  title: string;
  categoryId: TicketCategory;
  description: string;
}
