import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Card, Descriptions, Tag, Button, Typography, Skeleton, Alert, Space, Divider } from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import { ticketApi } from '../api/ticketApi';
import { TicketDto } from '../types';

const { Title, Text } = Typography;

export function TicketDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [ticket, setTicket] = useState<TicketDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchTicket = async () => {
      if (!id) return;
      try {
        setLoading(true);
        setError(null);
        const data = await ticketApi.getTicketById(id);
        setTicket(data);
      } catch (err) {
        console.error('Failed to fetch ticket details:', err);
        setError('Không thể tải thông tin ticket hoặc ticket không tồn tại.');
      } finally {
        setLoading(false);
      }
    };
    fetchTicket();
  }, [id]);

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

  return (
    <div style={{ maxWidth: 1000, margin: '0 auto' }}>
      <Button 
        type="link" 
        icon={<ArrowLeftOutlined />} 
        onClick={() => navigate('/requester-dashboard/tickets')}
        style={{ padding: 0, marginBottom: 16 }}
      >
        Quay lại danh sách
      </Button>

      <Card bordered={false} style={{ boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
        {loading ? (
          <Skeleton active paragraph={{ rows: 6 }} />
        ) : error ? (
          <Alert message={error} type="error" showIcon />
        ) : !ticket ? (
          <Alert message="Không tìm thấy dữ liệu." type="warning" showIcon />
        ) : (
          <>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
              <Space direction="vertical" size={0}>
                <Text type="secondary">{ticket.ticketNumber}</Text>
                <Title level={3} style={{ margin: 0 }}>{ticket.title}</Title>
              </Space>
              <Tag color={getStatusColor(ticket.status)} style={{ padding: '4px 12px', fontSize: 14 }}>
                {ticket.status}
              </Tag>
            </div>

            <Descriptions bordered column={{ xxl: 2, xl: 2, lg: 2, md: 1, sm: 1, xs: 1 }}>
              <Descriptions.Item label="Danh mục">
                <Tag>{ticket.categoryId}</Tag>
              </Descriptions.Item>
              <Descriptions.Item label="Độ ưu tiên">
                {ticket.priority || 'Chưa xếp hạng'}
              </Descriptions.Item>
              <Descriptions.Item label="Ngày tạo">
                {new Date(ticket.createdAt).toLocaleString('vi-VN')}
              </Descriptions.Item>
            </Descriptions>

            <Divider orientation="left">Mô tả chi tiết</Divider>
            <div style={{ whiteSpace: 'pre-wrap', backgroundColor: '#f9f9f9', padding: 16, borderRadius: 8 }}>
              {ticket.description || 'Không có mô tả chi tiết.'}
            </div>

            {ticket.resolution && (
              <>
                <Divider orientation="left">Cách giải quyết (Resolution)</Divider>
                <Alert 
                  message={ticket.resolution} 
                  type="success" 
                  style={{ whiteSpace: 'pre-wrap' }}
                />
              </>
            )}
          </>
        )}
      </Card>
    </div>
  );
}
