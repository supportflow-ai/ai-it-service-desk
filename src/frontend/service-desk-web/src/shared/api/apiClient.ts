import axios from 'axios';
import config from '@/app/config';

/**
 * Centralized Axios instance.
 * Base URL is read from environment config — never hardcoded.
 */
const apiClient = axios.create({
  baseURL: config.apiBaseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor — attach auth token when available
apiClient.interceptors.request.use((requestConfig) => {
  const token = localStorage.getItem('token');
  if (token) {
    requestConfig.headers.Authorization = `Bearer ${token}`;
  }
  return requestConfig;
});

// Response interceptor — handle common errors
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    // Xử lý tập trung lỗi 401 (Unauthorized / Expired Token)
    if (error.response && error.response.status === 401) {
      localStorage.removeItem('token');
      // Phát sự kiện để AuthContext lắng nghe và cập nhật state React
      window.dispatchEvent(new Event('auth:unauthorized'));
    }
    return Promise.reject(error);
  },
);

export default apiClient;
