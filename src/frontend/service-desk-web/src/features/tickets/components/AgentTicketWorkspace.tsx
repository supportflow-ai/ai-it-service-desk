import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Row, Col, Card, Typography, Tag, Select, Button, Timeline, Input, Space, Divider, Skeleton, Alert, message, Modal, Form } from 'antd';
import { ArrowLeftOutlined, UserOutlined, MessageOutlined, FileTextOutlined, PaperClipOutlined, CheckCircleOutlined, PlayCircleOutlined, UserSwitchOutlined } from '@ant-design/icons';
import { ticketApi } from '../api/ticketApi';
import { TicketDto, TicketTimelineEventDto, TicketStatus } from '../types';

const { Title, Text } = Typography;
const { TextArea } = Input;
const { Option } = Select;

export function AgentTicketWorkspace() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [ticket, setTicket] = useState<TicketDto | null>(null);
  const [events, setEvents] = useState<TicketTimelineEventDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [commentText, setCommentText] = useState('');

  // Resolution Modal State
  const [isResolveModalVisible, setIsResolveModalVisible] = useState(false);
  const [resolveForm] = Form.useForm();

  const fetchWorkspaceData = async () => {
    if (!id) return;
    try {
      setLoading(true);
      setError(null);
      const [ticketData, timelineData] = await Promise.all([
        ticketApi.getTicketById(id),
        ticketApi.getTicketTimeline(id).catch(() => []) // Fallback if API missing
      ]);
      setTicket(ticketData);
      setEvents(timelineData);
    } catch (err: any) {
      console.error('Failed to fetch workspace data:', err);
      // Fallback for demo when backend is not fully ready
      if (err.response?.status === 404) {
         setError('Không tìm thấy Ticket (404).');
      } else {
         setError('Lỗi kết nối tới máy chủ.');
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchWorkspaceData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  // Handle generic actions with query invalidation (refresh)
  const handleAction = async (action: () => Promise<any>, successMsg: string) => {
    try {
      setActionLoading(true);
      await action();
      message.success(successMsg);
      await fetchWorkspaceData(); // Query invalidation
    } catch (err: any) {
      console.error(err);
      if (err.response?.status === 409) {
        message.error('Dữ liệu đã bị thay đổi bởi người khác (Xmin Conflict). Vui lòng tải lại trang.');
      } else {
        message.error('Thao tác thất bại. Vui lòng thử lại.');
      }
    } finally {
      setActionLoading(false);
    }
  };

  const handleStart = () => handleAction(() => ticketApi.startTicket(id!), 'Bắt đầu xử lý Ticket');
  const handleAssignToMe = () => handleAction(() => ticketApi.assignTicket(id!, { assignedAgentId: 'me' }), 'Đã nhận xử lý Ticket');
  
  const handleSendComment = (isInternal: boolean) => {
    if (!commentText.trim()) {
      message.warning('Vui lòng nhập nội dung!');
      return;
    }
    handleAction(async () => {
      await ticketApi.addComment(id!, { content: commentText, isInternal });
      setCommentText(''); // Clear on success
    }, isInternal ? 'Đã thêm ghi chú nội bộ' : 'Đã phản hồi cho Requester');
  };

  const handleResolve = async () => {
    try {
      const values = await resolveForm.validateFields();
      setIsResolveModalVisible(false);
      handleAction(() => ticketApi.resolveTicket(id!, { resolution: values.resolution }), 'Đã giải quyết Ticket');
    } catch (info) {
      console.log('Validate Failed:', info);
    }
  };

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

  if (loading) return <div style={{ padding: 24 }}><Skeleton active paragraph={{ rows: 10 }} /></div>;
  if (error || !ticket) return <div style={{ padding: 24 }}><Alert message={error || 'Ticket không tồn tại'} type="error" showIcon /></div>;

  return (
    <div style={{ maxWidth: 1400, margin: '0 auto' }}>
      <Button type="link" icon={<ArrowLeftOutlined />} onClick={() => navigate('/agent-workspace')} style={{ padding: 0, marginBottom: 16 }}>
        Quay lại hàng đợi (Queue)
      </Button>

      <Row gutter={24}>
        <Col xs={24} lg={16}>
          {/* Ticket Metadata */}
          <Card bordered={false} style={{ marginBottom: 24, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <div>
                <Text type="secondary">{ticket.ticketNumber}</Text>
                <Title level={3} style={{ marginTop: 4, marginBottom: 16 }}>{ticket.title}</Title>
              </div>
              <Tag color={getStatusColor(ticket.status)} style={{ fontSize: 14, padding: '4px 12px' }}>
                {getStatusText(ticket.status)}
              </Tag>
            </div>
            <Row gutter={16} style={{ marginBottom: 16 }}>
              <Col span={8}>
                <Text type="secondary">Người yêu cầu (Requester):</Text>
                <div><UserOutlined /> {ticket.requesterName || 'N/A'}</div>
              </Col>
              <Col span={8}>
                <Text type="secondary">Danh mục:</Text>
                <div><Tag>{ticket.categoryId}</Tag></div>
              </Col>
              <Col span={8}>
                <Text type="secondary">Độ ưu tiên:</Text>
                <div><Tag color={ticket.priority ? 'volcano' : 'default'}>{ticket.priority || 'Chưa xếp hạng'}</Tag></div>
              </Col>
            </Row>
            <Divider orientation="left" plain>Mô tả chi tiết</Divider>
            <div style={{ whiteSpace: 'pre-wrap', backgroundColor: '#f9f9f9', padding: 16, borderRadius: 8 }}>
              {ticket.description || 'Không có mô tả chi tiết.'}
            </div>
            {ticket.resolution && (
              <Alert message="Cách giải quyết" description={ticket.resolution} type="success" showIcon style={{ marginTop: 16 }} />
            )}
          </Card>

          {/* Timeline & Interaction */}
          <Card bordered={false} title="Trao đổi & Lịch sử xử lý" style={{ boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
            <Timeline>
              {events.map(event => (
                <Timeline.Item key={event.id} color={event.isInternal ? 'red' : event.eventType === 'status_change' ? 'gray' : 'blue'} dot={event.eventType === 'note' ? <FileTextOutlined style={{ fontSize: '16px' }} /> : undefined}>
                  <div style={{ marginBottom: 4 }}>
                    <Text strong>{event.actorName}</Text>{' '}
                    <Text type="secondary" style={{ fontSize: 12 }}>{new Date(event.createdAt).toLocaleString('vi-VN')}</Text>
                    {event.isInternal && <Tag color="error" style={{ marginLeft: 8 }}>Ghi chú nội bộ</Tag>}
                  </div>
                  <div style={{ whiteSpace: 'pre-wrap' }}>{event.content}</div>
                </Timeline.Item>
              ))}
            </Timeline>
            
            <Divider />
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              <TextArea rows={4} placeholder="Nhập nội dung trao đổi với Requester hoặc Ghi chú nội bộ..." value={commentText} onChange={(e) => setCommentText(e.target.value)} disabled={ticket.status === TicketStatus.Closed} />
              <Space>
                <Button type="primary" icon={<MessageOutlined />} onClick={() => handleSendComment(false)} loading={actionLoading} disabled={ticket.status === TicketStatus.Closed}>
                  Gửi bình luận (Public)
                </Button>
                <Button type="default" danger icon={<FileTextOutlined />} onClick={() => handleSendComment(true)} loading={actionLoading} disabled={ticket.status === TicketStatus.Closed}>
                  Lưu Note nội bộ (Internal)
                </Button>
                <Button type="dashed" icon={<PaperClipOutlined />} disabled={ticket.status === TicketStatus.Closed}>
                  Đính kèm file
                </Button>
              </Space>
            </div>
          </Card>
        </Col>

        {/* Action Panel (Role & State based - TC-314) */}
        <Col xs={24} lg={8}>
          <Card bordered={false} title="Hành động (Command Actions)" style={{ marginBottom: 24, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
            
            {ticket.status === TicketStatus.Submitted && (
              <Alert type="info" showIcon message="Ticket đang chờ Triage. Vui lòng xác định Impact và Urgency." style={{ marginBottom: 12 }} />
            )}

            {ticket.status === TicketStatus.Triaged && (
              <Button type="primary" block style={{ marginBottom: 12 }} onClick={handleAssignToMe} loading={actionLoading} icon={<UserSwitchOutlined />}>
                Nhận xử lý (Self-Assign)
              </Button>
            )}

            {ticket.status === TicketStatus.Assigned && (
              <Button type="primary" block style={{ marginBottom: 12 }} onClick={handleStart} loading={actionLoading} icon={<PlayCircleOutlined />}>
                Bắt đầu xử lý (Start)
              </Button>
            )}

            {ticket.status === TicketStatus.InProgress && (
              <>
                <Button type="primary" success block style={{ marginBottom: 12, backgroundColor: '#52c41a', borderColor: '#52c41a', color: 'white' }} onClick={() => setIsResolveModalVisible(true)} icon={<CheckCircleOutlined />}>
                  Giải quyết (Resolve)
                </Button>
                <Button block style={{ marginBottom: 12 }} onClick={() => message.info('Tính năng đang phát triển')}>
                  Yêu cầu thêm thông tin (Pending User)
                </Button>
              </>
            )}

            {ticket.status === TicketStatus.Resolved && (
              <Alert type="success" showIcon message="Ticket đã được giải quyết. Chờ Requester xác nhận đóng." />
            )}

            {ticket.status === TicketStatus.Closed && (
              <Alert type="warning" showIcon message="Ticket đã đóng. Không thể thao tác thêm." />
            )}
          </Card>
        </Col>
      </Row>

      {/* Resolution Modal */}
      <Modal title="Xác nhận Giải quyết (Resolve)" visible={isResolveModalVisible} onOk={handleResolve} onCancel={() => setIsResolveModalVisible(false)} confirmLoading={actionLoading}>
        <Form form={resolveForm} layout="vertical">
          <Form.Item name="resolution" label="Cách giải quyết (Resolution)" rules={[{ required: true, message: 'Bắt buộc nhập cách giải quyết!' }]}>
            <TextArea rows={4} placeholder="Mô tả chi tiết nguyên nhân và cách bạn đã xử lý sự cố này..." />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
