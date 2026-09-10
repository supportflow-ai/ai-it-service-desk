import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Spin } from 'antd';
import { useAuth } from '@/features/auth/context/AuthContext';
import { Role } from '@/features/auth/types';

interface ProtectedRouteProps {
  allowedRoles?: Role[];
}

export function ProtectedRoute({ allowedRoles }: ProtectedRouteProps) {
  const { isAuthenticated, isLoading, user } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
        <Spin size="large" />
      </div>
    );
  }

  // Nếu người dùng chưa đăng nhập, chuyển về trang login kèm state để điều hướng ngược lại sau khi login thành công
  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  // Nếu route yêu cầu phân quyền và user không có quyền hợp lệ
  if (allowedRoles && user && !allowedRoles.includes(user.role)) {
    return <Navigate to="/403" replace />;
  }

  // Render các child routes
  return <Outlet />;
}
