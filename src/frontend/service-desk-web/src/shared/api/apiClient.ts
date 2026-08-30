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
    // Future: handle 401 → redirect to login, etc.
    return Promise.reject(error);
  },
);

export default apiClient;
