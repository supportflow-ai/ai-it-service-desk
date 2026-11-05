namespace ServiceDesk.Infrastructure.Knowledge.Indexing;

internal sealed class KnowledgeIndexJob
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public Guid ArticleVersionId { get; set; }
    public int VersionNumber { get; set; }
    public string SourceHash { get; set; } = "";
    public string ProfileId { get; set; } = "";
    public string ProfileHash { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorCode { get; set; }
}

internal sealed class KnowledgeIndexState
{
    public Guid ArticleId { get; set; }
    public Guid? RequestedJobId { get; set; }
    public Guid? ActiveIndexSetId { get; set; }
}

internal sealed class KnowledgeIndexSet
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public Guid ArticleVersionId { get; set; }
    public int VersionNumber { get; set; }
    public string SourceHash { get; set; } = "";
    public string NormalizedContentHash { get; set; } = "";
    public string ProfileId { get; set; } = "";
    public string ProfileHash { get; set; } = "";
    public string ProfileJson { get; set; } = "";
    public int Dimension { get; set; }
    public int ChunkCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class KnowledgeChunk
{
    public Guid IndexSetId { get; set; }
    public int Index { get; set; }
    public string ChunkId { get; set; } = "";
    public string Text { get; set; } = "";
    public string TextHash { get; set; } = "";
    public int StartChar { get; set; }
    public int EndChar { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string[] HeadingPath { get; set; } = [];
    public int TokenCount { get; set; }
}
