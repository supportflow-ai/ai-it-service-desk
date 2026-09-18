export type Role = 'Requester' | 'Agent' | 'Admin';

export interface User {
  userId: string;
  email: string;
  fullName: string;
  roles: Role[];
  avatarUrl?: string;
}

export interface LoginCredentials {
  email: string;
  password?: string;
}

export interface RegisterCredentials {
  email: string;
  fullName: string;
  password?: string;
}

export interface AuthResponse {
  token: string;
  user: User;
}
