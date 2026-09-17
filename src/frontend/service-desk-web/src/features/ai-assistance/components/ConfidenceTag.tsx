import { Tag, Tooltip } from 'antd';
import type { ConfidenceBand } from '../types';

/**
 * Displays a confidence band tag with tooltip explaining the score semantics.
 * IMPORTANT: Never displays "probability" or "accuracy" — this is a model score
 * used to help reviewers prioritize which suggestions to examine.
 */
export function ConfidenceTag({ band, score }: { band: ConfidenceBand | null; score?: number | null }) {
  if (!band) return null;

  const colorMap: Record<string, string> = {
    Low: 'orange',
    Medium: 'blue',
    High: 'green',
  };

  const tooltipText = score != null
    ? `Model score: ${score.toFixed(2)} — This score helps prioritize review. It is not a calibrated probability.`
    : 'Confidence level for reviewer prioritization. Not a calibrated probability.';

  return (
    <Tooltip title={tooltipText}>
      <Tag color={colorMap[band] ?? 'default'}>
        Confidence: {band}
      </Tag>
    </Tooltip>
  );
}
