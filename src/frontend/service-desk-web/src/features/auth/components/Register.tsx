import { useState } from 'react';
import { Form, Input, Button, Card, Typography, message } from 'antd';
import { UserOutlined, LockOutlined, IdcardOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../api/authApi';
import { RegisterCredentials } from '../types';

const { Title } = Typography;

export function Register() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);

  const onFinish = async (values: RegisterCredentials) => {
    setLoading(true);
    try {
      await authApi.register(values);
      message.success('Đăng ký thành công! Vui lòng đăng nhập.');
      navigate('/login');
    } catch (error) {
      console.error('Register failed:', error);
      message.error('Đăng ký thất bại. Vui lòng thử lại!');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh', backgroundColor: '#f0f2f5' }}>
      <Card style={{ width: 400, boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
        <div style={{ textAlign: 'center', marginBottom: 24 }}>
          <Title level={3} style={{ margin: 0 }}>Đăng ký tài khoản</Title>
          <div style={{ color: '#8c8c8c', marginTop: 8 }}>Hệ thống AI IT Service Desk</div>
        </div>

        <Form
          name="register_form"
          layout="vertical"
          onFinish={onFinish}
          size="large"
        >
          <Form.Item
            name="fullName"
            rules={[
              { required: true, message: 'Vui lòng nhập Họ và Tên!' }
            ]}
          >
            <Input prefix={<IdcardOutlined />} placeholder="Họ và Tên (VD: Nguyễn Văn A)" />
          </Form.Item>

          <Form.Item
            name="email"
            rules={[
              { required: true, message: 'Vui lòng nhập Email!' },
              { type: 'email', message: 'Email không đúng định dạng!' }
            ]}
          >
            <Input prefix={<UserOutlined />} placeholder="Email" />
          </Form.Item>

          <Form.Item
            name="password"
            rules={[
              { required: true, message: 'Vui lòng nhập Mật khẩu!' },
              { min: 6, message: 'Mật khẩu phải có ít nhất 6 ký tự!' }
            ]}
          >
            <Input.Password prefix={<LockOutlined />} placeholder="Mật khẩu" />
          </Form.Item>

          <Form.Item style={{ marginBottom: 16 }}>
            <Button type="primary" htmlType="submit" block loading={loading}>
              Đăng ký
            </Button>
          </Form.Item>
          
          <div style={{ textAlign: 'center' }}>
            <span style={{ color: '#8c8c8c' }}>Đã có tài khoản? </span>
            <Button type="link" onClick={() => navigate('/login')} style={{ padding: 0 }}>
              Đăng nhập ngay
            </Button>
          </div>
        </Form>
      </Card>
    </div>
  );
}
