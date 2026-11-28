using ServiceDesk.Modules.Ticketing.Application.EvaluateSla;

namespace ServiceDesk.Worker;

/// <summary>Trạng thái lần quét gần nhất trong process; không lưu lịch sử qua restart.</summary>
public sealed record SlaWorkerSnapshot(
    string State,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? LastSuccessfulAt,
    SlaEvaluationResult? Result,
    string? FailureType);
