import { Button, Result } from 'antd';
import { useNavigate } from 'react-router-dom';

export function Unauthorized() {
  const navigate = useNavigate();

  return (
    <Result
      status="403"
      title="403"
      subTitle="Xin lỗi, bạn không có quyền truy cập vào trang này."
      extra={
        <Button type="primary" onClick={() => navigate('/')}>
          Về trang chủ
        </Button>
      }
    />
  );
}
