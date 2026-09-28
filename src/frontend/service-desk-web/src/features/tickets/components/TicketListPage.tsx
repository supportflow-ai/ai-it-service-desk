import { useEffect, useState } from 'react';
import { Table, Tag, Button, Typography, Space, Card, Empty, Alert } from 'antd';
import { PlusOutlined, EyeOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { ticketApi } from '../api/ticketApi';
import { TicketDto, TicketStatus } from '../types';

const { Title } = Typography;

export function TicketListPage() {
  const [tickets, setTickets] = useState<TicketDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    const fetchTickets = async () => {
      try {
        const data = await ticketApi.getMyTickets();
        setTickets(data);
      } catch (err) {
        console.error('Failed to fetch tickets:', err);
        setError('Không thể tải danh sách ticket. Vui lòng thử lại sau.');
      } finally {
        setLoading(false);
      }
    };
    fetchTickets();
  }, []);

  const getStatusColor = (status: TicketStatus) => {
    switch (status) {
      case TicketStatus.Submitted: return 'blue';
      case TicketStatus.InProgress: return 'processing';
      case TicketStatus.Resolved: return 'success';
      case TicketStatus.Closed: return 'default';
      case TicketStatus.PendingUser: return 'warning';
      default: return 'default';
    }
  };

  const getStatusText = (status: TicketStatus) => {
    switch (status) {
      case TicketStatus.Draft: return 'Bản nháp';
      case TicketStatus.Submitted: return 'Đã gửi';
      case TicketStatus.Triaged: return 'Đã phân loại';
      case TicketStatus.Assigned: return 'Đã phân công';
      case TicketStatus.InProgress: return 'Đang xử lý';
      case TicketStatus.PendingUser: return 'Chờ phản hồi';
      case TicketStatus.PendingExternal: return 'Chờ đối tác';
      case TicketStatus.Resolved: return 'Đã giải quyết';
      case TicketStatus.Closed: return 'Đã đóng';
      default: return 'Không xác định';
    }
  };

  const columns = [
    {
      title: 'Mã Ticket',
      dataIndex: 'ticketNumber',
      key: 'ticketNumber',
      render: (text: string, record: TicketDto) => (
        <a onClick={() => navigate(`/requester-dashboard/tickets/${record.id}`)}>{text}</a>
      ),
    },
    {
      title: 'Tiêu đề',
      dataIndex: 'title',
      key: 'title',
    },
    {
      title: 'Danh mục',
      dataIndex: 'categoryId',
      key: 'categoryId',
      render: (text: string) => <Tag>{text}</Tag>,
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      render: (status: TicketStatus) => <Tag color={getStatusColor(status)}>{getStatusText(status)}</Tag>,
    },
    {
      title: 'Ngày tạo',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (date: string) => new Date(date).toLocaleString('vi-VN'),
    },
    {
      title: 'Thao tác',
      key: 'action',
      render: (_: any, record: TicketDto) => (
        <Button
          type="text"
          icon={<EyeOutlined />}
          onClick={() => navigate(`/requester-dashboard/tickets/${record.id}`)}
        >
          Xem
        </Button>
      ),
    },
  ];

  return (
    <div style={{ maxWidth: 1000, margin: '0 auto' }}>
      <Card bordered={false} style={{ boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
          <Title level={3} style={{ margin: 0 }}>Yêu cầu hỗ trợ của tôi</Title>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/tickets/new')}>
            Tạo yêu cầu mới
          </Button>
        </div>
        {error && <Alert message={error} type="error" showIcon style={{ marginBottom: 16 }} />}
        {!loading && tickets.length === 0 && !error ? (
          <Empty description="Bạn chưa có ticket nào" />
        ) : (
          <Table
            columns={columns}
            dataSource={tickets}
            rowKey="id"
            loading={loading}
            pagination={{ pageSize: 10 }}
          />
        )}
      </Card>
    </div>
  );
}
