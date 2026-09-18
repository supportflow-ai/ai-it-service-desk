import { useState } from 'react';
import { Form, Input, Select, Button, Card, Typography, message } from 'antd';
import { CreateTicketPayload } from '../types';

import { useNavigate } from 'react-router-dom';
import { ticketApi } from '../api/ticketApi';

const { Title, Paragraph } = Typography;
const { TextArea } = Input;
const { Option } = Select;

export function CreateTicketPage() {
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();

  const onFinish = async (values: CreateTicketPayload) => {
    setLoading(true);
    try {
      // TC-268: Gọi API tạo ticket
      const result = await ticketApi.createTicket(values);
      message.success(`Tạo ticket ${result.ticketNumber} thành công!`);
      
      // Chuyển hướng tới trang chi tiết ticket
      navigate(`/requester-dashboard/tickets/${result.id}`);
    } catch (error) {
      console.error('Failed to create ticket:', error);
      message.error('Có lỗi xảy ra khi tạo ticket. Vui lòng thử lại!');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ maxWidth: 800, margin: '0 auto' }}>
      <Card bordered={false} style={{ boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
        <Title level={3}>Gửi yêu cầu IT mới</Title>
        <Paragraph type="secondary">
          Vui lòng cung cấp chi tiết về sự cố hoặc yêu cầu của bạn để chúng tôi có thể hỗ trợ tốt nhất.
        </Paragraph>

        <Form
          name="create_ticket"
          layout="vertical"
          onFinish={onFinish}
          style={{ marginTop: 24 }}
        >
          <Form.Item
            name="title"
            label="Tiêu đề yêu cầu"
            rules={[
              { required: true, message: 'Vui lòng nhập tiêu đề!' },
              { max: 100, message: 'Tiêu đề không được vượt quá 100 ký tự!' }
            ]}
          >
            <Input placeholder="Ví dụ: Không thể truy cập vào mạng nội bộ" />
          </Form.Item>

          <Form.Item
            name="categoryId"
            label="Danh mục sự cố"
            rules={[{ required: true, message: 'Vui lòng chọn danh mục!' }]}
          >
            <Select placeholder="Chọn danh mục phù hợp">
              <Option value="ACC">Tài khoản & Mật khẩu (ACC)</Option>
              <Option value="NET">Mạng & Wi-Fi (NET)</Option>
              <Option value="VPN">VPN</Option>
              <Option value="MAIL">Email (MAIL)</Option>
              <Option value="SW">Cài đặt phần mềm (SW)</Option>
              <Option value="HW">Phần cứng (HW)</Option>
              <Option value="PRINT">Máy in (PRINT)</Option>
              <Option value="ACCESS">Cấp quyền truy cập (ACCESS)</Option>
              <Option value="EQUIP">Thiết bị phòng họp/văn phòng (EQUIP)</Option>
              <Option value="SEC">Sự cố bảo mật (SEC)</Option>
              <Option value="OTHER">Khác (OTHER)</Option>
            </Select>
          </Form.Item>

          <Form.Item
            name="description"
            label="Mô tả chi tiết"
            rules={[
              { required: true, message: 'Vui lòng nhập mô tả chi tiết!' },
              { min: 10, message: 'Mô tả cần ít nhất 10 ký tự để kỹ thuật viên hiểu rõ vấn đề.' }
            ]}
          >
            <TextArea 
              rows={6} 
              placeholder="Mô tả rõ các bước bạn đã làm, thông báo lỗi (nếu có), và thời điểm xảy ra sự cố..." 
            />
          </Form.Item>

          <Form.Item style={{ marginTop: 32, marginBottom: 0, textAlign: 'right' }}>
            <Button type="default" style={{ marginRight: 12 }}>
              Hủy
            </Button>
            <Button type="primary" htmlType="submit" loading={loading}>
              Gửi yêu cầu
            </Button>
          </Form.Item>
        </Form>
      </Card>
    </div>
  );
}
