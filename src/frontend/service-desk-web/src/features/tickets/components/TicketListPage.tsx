import { useEffect, useState } from 'react';
import { Table, Tag, Button, Typography, Space, Card } from 'antd';
import { PlusOutlined, EyeOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { ticketApi } from '../api/ticketApi';
import { TicketDto } from '../types';

const { Title } = Typography;

export function TicketListPage() {
  const [tickets, setTickets] = useState<TicketDto[]>([]);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    const fetchTickets = async () => {
      try {
        const data = await ticketApi.getMyTickets();
        setTickets(data);
      } catch (error) {
        console.error('Failed to fetch tickets:', error);
      } finally {
        setLoading(false);
      }
    };
    fetchTickets();
  }, []);

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Submitted': return 'blue';
      case 'InProgress': return 'processing';
      case 'Resolved': return 'success';
      case 'Closed': return 'default';
      case 'PendingUser': return 'warning';
      default: return 'default';
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
      render: (status: string) => <Tag color={getStatusColor(status)}>{status}</Tag>,
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

        <Table 
          columns={columns} 
          dataSource={tickets} 
          rowKey="id" 
          loading={loading}
          pagination={{ pageSize: 10 }}
        />
      </Card>
    </div>
  );
}
