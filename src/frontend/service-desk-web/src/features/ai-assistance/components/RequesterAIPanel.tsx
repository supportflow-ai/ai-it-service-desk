import { Alert, Card, Tag, Typography, Space, Empty } from 'antd';
import { WarningOutlined, RobotOutlined } from '@ant-design/icons';
import type { RequesterTriageView } from '../types';
import { TriageOutcome } from '../types';
import { ConfidenceTag } from './ConfidenceTag';

const { Text } = Typography;

interface Props {
  result: RequesterTriageView | null;
  loading?: boolean;
}

/**
 * Requester-safe AI suggestion panel.
 * Shows ONLY category suggestion + confidence band.
 * Does NOT show: Impact, Urgency, raw scores, historical tickets, internal notes.
 *
 * Submit is NEVER disabled by AI unavailability.
 */
export function RequesterAIPanel({ result, loading }: Props) {
  if (loading) {
    return (
      <Card size="small" title={<><RobotOutlined /> AI Assistance</>} loading />
    );
  }

  if (!result) {
    return (
      <Card size="small" title={<><RobotOutlined /> AI Assistance</>}>
        <Empty
          description="Click 'Analyze' to get AI category suggestions. This is optional — you can submit without AI assistance."
          image={Empty.PRESENTED_IMAGE_SIMPLE}
        />
      </Card>
    );
  }

  // Fallback states — AI unavailable/timeout/error
  if (result.outcome !== TriageOutcome.Success) {
    return (
      <Card size="small" title={<><RobotOutlined /> AI Assistance</>}>
        <Alert
          type="info"
          showIcon
          message="AI assistance is currently unavailable. You can continue submitting the request."
          description={result.failureReason ?? undefined}
        />
      </Card>
    );
  }

  return (
    <Card size="small" title={<><RobotOutlined /> AI Suggestion</>}>
      <Space direction="vertical" style={{ width: '100%' }}>
        {result.requiresManualTriage && (
          <Alert
            type="warning"
            showIcon
            icon={<WarningOutlined />}
            message="Security incident detected — manual triage required."
          />
        )}

        {result.suggestedCategory && (
          <div>
            <Text type="secondary">Suggested Category: </Text>
            <Tag color="processing">{result.suggestedCategory}</Tag>
            <ConfidenceTag band={result.categoryConfidence} />
          </div>
        )}

        <Text type="secondary" style={{ fontSize: 12 }}>
          This is an AI suggestion to assist with your request. A support agent will review and finalize the categorization.
        </Text>
      </Space>
    </Card>
  );
}
