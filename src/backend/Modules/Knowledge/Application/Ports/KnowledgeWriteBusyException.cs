using ServiceDesk.SharedKernel.Application;

namespace ServiceDesk.Modules.Knowledge.Application.Ports;

/// <summary>Hết thời gian chờ khóa bài viết; thử lại sau khi tải revision hiện hành.</summary>
public sealed class KnowledgeWriteBusyException(Exception inner) : RetryLaterException("Bài viết đang được cập nhật. Vui lòng thử lại.", inner);
