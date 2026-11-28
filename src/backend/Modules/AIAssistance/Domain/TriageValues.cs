namespace ServiceDesk.Modules.AIAssistance.Domain;

/// <summary>Giá trị phân loại của phiếu tại một thời điểm, lưu làm bằng chứng trước và sau quyết định HITL.</summary>
public sealed record TriageValues(string CategoryId, string? Impact, string? Urgency, string? Priority);
