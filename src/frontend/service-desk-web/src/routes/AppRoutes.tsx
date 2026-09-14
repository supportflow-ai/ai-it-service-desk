import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { ProtectedRoute } from './components/ProtectedRoute';
import { Unauthorized } from '@/features/auth/components/Unauthorized';
import { Login } from '@/features/auth/components/Login';

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
      <h1>AI IT Service Desk</h1>
      <p>Internal IT Service Request Management System</p>
      <p style={{ color: '#888', fontSize: '0.875rem' }}>
        Foundation scaffold — business features will be added in future sprints.
      </p>
    </div>
  );
}

function DemoPage({ title }: { title: string }) {
  return (
    <div style={{ textAlign: 'center', padding: '4rem' }}>
      <h1>{title}</h1>
      <p>This is a protected page.</p>
    </div>
  );
}

export function AppRoutes() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Public Routes */}
        <Route path="/login" element={<Login />} />
        <Route path="/403" element={<Unauthorized />} />

        {/* Protected Routes (Require Login) */}
        <Route element={<ProtectedRoute />}>
          <Route path="/" element={<Home />} />
        </Route>

        {/* Role-Based Routes */}
        <Route element={<ProtectedRoute allowedRoles={['Requester']} />}>
          <Route path="/requester-dashboard" element={<DemoPage title="Requester Dashboard" />} />
        </Route>

        <Route element={<ProtectedRoute allowedRoles={['Agent']} />}>
          <Route path="/agent-workspace" element={<DemoPage title="Agent Workspace" />} />
        </Route>

        <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
          <Route path="/admin-panel" element={<DemoPage title="Admin Panel" />} />
        </Route>

        <Route path="*" element={<NotFound />} />
      </Routes>
    </BrowserRouter>
  );
}
