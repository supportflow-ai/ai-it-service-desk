import apiClient from '@/shared/api/apiClient';
import { AuthResponse, LoginCredentials, User } from '../types';

export const authApi = {
  /**
   * Đăng nhập người dùng bằng email và mật khẩu
   */
  login: async (credentials: LoginCredentials): Promise<AuthResponse> => {
    const response = await apiClient.post<AuthResponse>('/api/v1/auth/login', credentials);
    return response.data;
  },

  /**
   * Lấy thông tin user hiện tại (Dùng để restore session sau khi f5)
   */
  getCurrentUser: async (): Promise<User> => {
    const response = await apiClient.get<User>('/api/v1/auth/me');
    return response.data;
  },
};
