import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import type { ReactNode } from 'react';
import { AuthProvider } from '@/features/auth/context/AuthContext';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 5 * 60 * 1000, // 5 minutes
      refetchOnWindowFocus: false,
    },
  },
});

interface AppProvidersProps {
  children: ReactNode;
}

/**
 * App-level providers wrapping the entire application.
 * ConfigProvider: Ant Design theme/locale configuration.
 * QueryClientProvider: TanStack Query server-state management.
 * AuthProvider: Global authentication state.
 */
export function AppProviders({ children }: AppProvidersProps) {
  return (
    <ConfigProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          {children}
        </AuthProvider>
      </QueryClientProvider>
    </ConfigProvider>
  );
}
