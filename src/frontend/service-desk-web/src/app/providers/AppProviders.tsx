import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import type { ReactNode } from 'react';

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
 */
export function AppProviders({ children }: AppProvidersProps) {
  return (
    <ConfigProvider>
      <QueryClientProvider client={queryClient}>
        {children}
      </QueryClientProvider>
    </ConfigProvider>
  );
}
