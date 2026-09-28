import apiClient from '@/shared/api/apiClient';
import { AuthResponse, LoginCredentials, RegisterCredentials, User } from '../types';

export const authApi = {
  /**
   * Đăng nhập người dùng bằng email và mật khẩu
   */
  login: async (credentials: LoginCredentials): Promise<AuthResponse> => {
    const response = await apiClient.post<AuthResponse>('/auth/login', credentials);
    return response.data;
  },

  /**
   * Đăng ký người dùng mới (nếu có)
   */
  register: async (credentials: RegisterCredentials): Promise<AuthResponse> => {
    const response = await apiClient.post<AuthResponse>('/auth/register', credentials);
    return response.data;
  },

  /**
   * Lấy thông tin user hiện tại (Dùng để restore session sau khi f5)
   */
  getCurrentUser: async (): Promise<User> => {
    const response = await apiClient.get<User>('/auth/me');
    return response.data;
  },
};
