import { Alert, Button, Card, Descriptions, Select, Space, Tag, Typography, Input } from 'antd';
import { WarningOutlined, RobotOutlined, CheckOutlined, CloseOutlined, EditOutlined, ReloadOutlined } from '@ant-design/icons';
import { useState } from 'react';
import type { AgentTriageView, SuggestionDecisionRequest, SuggestionDto } from '../types';
import { TriageOutcome, SuggestionDecision, TicketCategory } from '../types';
import { ConfidenceTag } from './ConfidenceTag';

const { Text } = Typography;

interface Props {
  result: AgentTriageView | null;
  suggestions?: SuggestionDto[];
  loading?: boolean;
  stale?: boolean;
  onDecision?: (suggestionId: string, request: SuggestionDecisionRequest) => void;
  onReanalyze?: () => void;
}

const categoryOptions = Object.values(TicketCategory).map((c) => ({
  label: c,
  value: c,
}));

/**
 * Agent/Lead/Admin AI suggestion review panel.
 * Full visibility: Category, Impact, Urgency, scores, confidence.
 * Clearly separates "Suggested" vs "Final" values.
 * Stale suggestions disable Accept/Override actions.
 */
export function AgentReviewPanel({
  result,
  suggestions,
  loading,
  stale,
  onDecision,
  onReanalyze,
}: Props) {
  const [overrideValue, setOverrideValue] = useState<string>('');
  const [overrideTarget, setOverrideTarget] = useState<string | null>(null);

  if (loading) {
    return (
      <Card size="small" title={<><RobotOutlined /> AI Triage Review</>} loading />
    );
  }

  if (!result) {
    return (
      <Card size="small" title={<><RobotOutlined /> AI Triage Review</>}>
        <Text type="secondary">No AI analysis available. Click &quot;Analyze&quot; to request AI assistance.</Text>
      </Card>
    );
  }

  if (result.outcome !== TriageOutcome.Success) {
    return (
      <Card size="small" title={<><RobotOutlined /> AI Triage Review</>}>
        <Alert
          type="info"
          showIcon
          message="AI assistance is currently unavailable."
          description={result.failureReason ?? undefined}
        />
        {result.validationErrors.length > 0 && (
          <Alert
            type="warning"
            showIcon
            message="Validation Errors"
            description={
              <ul>
                {result.validationErrors.map((e, i) => (
                  <li key={i}>{e}</li>
                ))}
              </ul>
            }
            style={{ marginTop: 8 }}
          />
        )}
      </Card>
    );
  }

  const handleDecision = (id: string, decision: 'Accepted' | 'Rejected', ticketRevision: number) => {
    onDecision?.(id, { decision, ticketRevision });
  };

  const handleOverride = (id: string, ticketRevision: number) => {
    if (!overrideValue) return;
    onDecision?.(id, { decision: 'Overridden', finalValue: overrideValue, ticketRevision });
    setOverrideTarget(null);
    setOverrideValue('');
  };

  return (
    <Card
      size="small"
      title={<><RobotOutlined /> AI Triage Suggestions</>}
      extra={
        stale ? (
          <Space>
            <Tag color="red">Stale</Tag>
            {onReanalyze && (
              <Button size="small" icon={<ReloadOutlined />} onClick={onReanalyze}>
                Re-analyze
              </Button>
            )}
          </Space>
        ) : undefined
      }
    >
      {stale && (
        <Alert
          type="warning"
          showIcon
          icon={<WarningOutlined />}
          message="Ticket has changed since this analysis. Suggestions are stale and cannot be applied. Please re-analyze."
          style={{ marginBottom: 12 }}
        />
      )}

      {result.requiresManualTriage && (
        <Alert
          type="error"
          showIcon
          icon={<WarningOutlined />}
          message="Security Incident — Manual triage required. AI suggestions are informational only."
          style={{ marginBottom: 12 }}
        />
      )}

      <Descriptions column={1} size="small" bordered>
        <Descriptions.Item label="Category">
          <Space>
            <Tag color="processing">{result.suggestedCategory ?? '—'}</Tag>
            <ConfidenceTag band={result.categoryConfidence} score={result.categoryScore} />
          </Space>
        </Descriptions.Item>
        <Descriptions.Item label="Impact">
          <Space>
            <Tag>{result.suggestedImpact ?? '—'}</Tag>
            <ConfidenceTag band={result.impactConfidence} score={result.impactScore} />
          </Space>
        </Descriptions.Item>
        <Descriptions.Item label="Urgency">
          <Space>
            <Tag>{result.suggestedUrgency ?? '—'}</Tag>
            <ConfidenceTag band={result.urgencyConfidence} score={result.urgencyScore} />
          </Space>
        </Descriptions.Item>
      </Descriptions>

      {/* Per-suggestion decisions */}
      {suggestions && suggestions.length > 0 && (
        <div style={{ marginTop: 12 }}>
          <Text strong>Review Suggestions:</Text>
          {suggestions.map((s) => {
            const isTerminal = s.decision !== SuggestionDecision.Pending;
            const isExpired = s.decision === SuggestionDecision.Expired;
            const disabled = stale || isTerminal;

            return (
              <Card key={s.id} size="small" style={{ marginTop: 8 }}>
                <Space direction="vertical" style={{ width: '100%' }}>
                  <Space>
                    <Text strong>{s.type}:</Text>
                    <Text>Suggested: <Tag>{s.suggestedValue}</Tag></Text>
                    {s.finalValue && (
                      <Text>Final: <Tag color="green">{s.finalValue}</Tag></Text>
                    )}
                    <Tag color={
                      s.decision === SuggestionDecision.Accepted ? 'green' :
                      s.decision === SuggestionDecision.Overridden ? 'blue' :
                      s.decision === SuggestionDecision.Rejected ? 'red' :
                      isExpired ? 'default' : 'processing'
                    }>
                      {s.decision}
                    </Tag>
                  </Space>

                  {!disabled && (
                    <Space>
                      <Button
                        size="small"
                        type="primary"
                        icon={<CheckOutlined />}
                        onClick={() => handleDecision(s.id, 'Accepted', 0)}
                      >
                        Accept
                      </Button>
                      <Button
                        size="small"
                        danger
                        icon={<CloseOutlined />}
                        onClick={() => handleDecision(s.id, 'Rejected', 0)}
                      >
                        Reject
                      </Button>
                      {overrideTarget === s.id ? (
                        <Space.Compact>
                          {s.type === 'Category' ? (
                            <Select
                              size="small"
                              options={categoryOptions}
                              placeholder="Select value"
                              style={{ width: 120 }}
                              onChange={(v) => setOverrideValue(v)}
                            />
                          ) : (
                            <Input
                              size="small"
                              placeholder="Final value"
                              style={{ width: 120 }}
                              onChange={(e) => setOverrideValue(e.target.value)}
                            />
                          )}
                          <Button
                            size="small"
                            type="primary"
                            onClick={() => handleOverride(s.id, 0)}
                          >
                            Apply
                          </Button>
                        </Space.Compact>
                      ) : (
                        <Button
                          size="small"
                          icon={<EditOutlined />}
                          onClick={() => setOverrideTarget(s.id)}
                        >
                          Override
                        </Button>
                      )}
                    </Space>
                  )}

                  {isExpired && (
                    <Text type="warning">
                      Ticket changed. This suggestion is expired and cannot be applied.
                    </Text>
                  )}
                </Space>
              </Card>
            );
          })}
        </div>
      )}
    </Card>
  );
}
