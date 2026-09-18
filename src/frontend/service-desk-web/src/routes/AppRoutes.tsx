import { BrowserRouter, Routes, Route, Outlet } from 'react-router-dom';
import { Button, Layout } from 'antd';
import { ProtectedRoute } from './components/ProtectedRoute';
import { Unauthorized } from '@/features/auth/components/Unauthorized';
import { Login } from '@/features/auth/components/Login';
import { Register } from '@/features/auth/components/Register';
import { useAuth } from '@/features/auth/context/AuthContext';
import { CreateTicketPage } from '@/features/tickets/components/CreateTicketPage';
import { TicketListPage } from '@/features/tickets/components/TicketListPage';
import { TicketDetailPage } from '@/features/tickets/components/TicketDetailPage';

const { Header, Content } = Layout;

function MainLayout() {
  const { user, logout } = useAuth();

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: '#fff', padding: '0 24px', borderBottom: '1px solid #f0f0f0' }}>
        <div style={{ fontWeight: 'bold', fontSize: '18px' }}>AI IT Service Desk</div>
        <div>
          <span style={{ marginRight: 16 }}>Xin chào, {user?.fullName} ({user?.roles?.[0]})</span>
          <Button type="primary" danger onClick={logout}>Đăng xuất</Button>
        </div>
      </Header>
      <Content style={{ padding: '24px', background: '#f5f5f5' }}>
        <Outlet />
      </Content>
    </Layout>
  );
}

function NotFound() {
  return (
    <div style={{ textAlign: 'center', padding: '4rem' }}>
      <h1>404</h1>
      <p>Page not found</p>
    </div>
  );
}

function Home() {
  return (
    <div style={{ textAlign: 'center', padding: '4rem' }}>
      <h1>Trang chủ</h1>
      <p>Internal IT Service Request Management System</p>
    </div>
  );
}

export function AppRoutes() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Public Routes */}
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route path="/403" element={<Unauthorized />} />

        {/* Protected Routes (Require Login) */}
        <Route element={<ProtectedRoute />}>
          <Route element={<MainLayout />}>
            <Route path="/" element={<Home />} />
            
            {/* Role-Based Routes */}
            <Route element={<ProtectedRoute allowedRoles={['Requester']} />}>
              <Route path="/requester-dashboard/tickets" element={<TicketListPage />} />
              <Route path="/requester-dashboard/tickets/:id" element={<TicketDetailPage />} />
              <Route path="/tickets/new" element={<CreateTicketPage />} />
            </Route>

            <Route element={<ProtectedRoute allowedRoles={['Agent']} />}>
              <Route path="/agent-workspace" element={<DemoPage title="Agent Workspace" />} />
            </Route>

            <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
              <Route path="/admin-panel" element={<DemoPage title="Admin Panel" />} />
            </Route>
          </Route>
        </Route>

        <Route path="*" element={<NotFound />} />
      </Routes>
    </BrowserRouter>
  );
}
