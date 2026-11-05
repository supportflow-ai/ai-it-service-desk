using System.Text;

namespace ServiceDesk.Application.Knowledge.Indexing;

public sealed record KnowledgeIndexSnapshot(Guid RequestId, Guid ArticleId, Guid ArticleVersionId,
    int VersionNumber, string Title, string Content)
{
    public string SourceHash => KnowledgeIndexProfile.SnapshotHash(Title, Content);
}

public sealed record KnowledgeIndexLease(Guid JobId, Guid LeaseToken, KnowledgeIndexSnapshot Snapshot);

public sealed record PreparedKnowledgeChunk(string ChunkId, int Index, string Text, string TextHash,
    int StartChar, int EndChar, int StartLine, int EndLine, string[] HeadingPath, int TokenCount, float[] Embedding);

public sealed record PreparedKnowledgeIndex(Guid RequestId, Guid ArticleId, Guid ArticleVersionId,
    int VersionNumber, string SourceHash, string ProfileId, string ProfileHash, string NormalizedContentHash,
    IReadOnlyList<PreparedKnowledgeChunk> Chunks)
{
    public void Validate(KnowledgeIndexSnapshot snapshot)
    {
        if (RequestId != snapshot.RequestId || ArticleId != snapshot.ArticleId ||
            ArticleVersionId != snapshot.ArticleVersionId || VersionNumber != snapshot.VersionNumber ||
            SourceHash != snapshot.SourceHash || ProfileId != KnowledgeIndexProfile.Id ||
            ProfileHash != KnowledgeIndexProfile.Fingerprint || Chunks is null ||
            Chunks.Count is < 1 or > KnowledgeIndexProfile.MaxChunks)
            throw new KnowledgeIndexException("invalid_index_response", false);

        var source = KnowledgeIndexProfile.Normalize(snapshot.Content);
        if (NormalizedContentHash != KnowledgeIndexProfile.Hash(source))
            throw new KnowledgeIndexException("invalid_source_hash", false);
        var positions = new List<int> { 0 };
        foreach (var rune in source.EnumerateRunes()) positions.Add(positions[^1] + rune.Utf16SequenceLength);
        var previousEnd = 0;
        for (var i = 0; i < Chunks.Count; i++)
        {
            var c = Chunks[i];
            if (c is null || c.Index != i || c.StartChar < 0 || c.EndChar <= c.StartChar ||
                c.EndChar >= positions.Count || c.EndChar <= previousEnd ||
                c.HeadingPath is null || c.HeadingPath.Length > 6 ||
                c.HeadingPath.Any(x => x is null) || c.TokenCount is < 1 or > KnowledgeIndexProfile.MaxInputTokens ||
                c.Embedding is null || c.Embedding.Length != KnowledgeIndexProfile.Dimension ||
                c.Embedding.Any(x => !float.IsFinite(x) || Math.Abs(x) > 1.00001f))
                throw new KnowledgeIndexException("invalid_chunk", false);
            var norm = Math.Sqrt(c.Embedding.Sum(x => (double)x * x));
            if (norm is < 0.999 or > 1.001 || string.IsNullOrWhiteSpace(c.Text) ||
                c.Text != source[positions[c.StartChar]..positions[c.EndChar]] ||
                c.TextHash != KnowledgeIndexProfile.Hash(c.Text) ||
                c.ChunkId != KnowledgeIndexProfile.ChunkId(ArticleVersionId, SourceHash, i, c.TextHash) ||
                c.StartLine != 1 + source[..positions[c.StartChar]].Count(x => x == '\n') ||
                c.EndLine != 1 + source[..positions[c.EndChar - 1]].Count(x => x == '\n'))
                throw new KnowledgeIndexException("invalid_chunk_metadata", false);
            if (c.StartChar > previousEnd && !string.IsNullOrWhiteSpace(
                    source[positions[previousEnd]..positions[c.StartChar]]))
                throw new KnowledgeIndexException("incomplete_source_coverage", false);
            previousEnd = c.EndChar;
        }
        if (!string.IsNullOrWhiteSpace(source[positions[previousEnd]..]))
            throw new KnowledgeIndexException("incomplete_source_coverage", false);
    }
}

public sealed class KnowledgeIndexException(string code, bool retryable) : Exception(code)
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
}

public sealed record KnowledgeIndexStatusDto(Guid ArticleId, Guid? JobId, Guid? ActiveIndexSetId,
    string Status, int? VersionNumber, int Attempts, string? ErrorCode, int ChunkCount);

public interface IKnowledgeIndexPreparer
{
    Task<PreparedKnowledgeIndex> PrepareAsync(KnowledgeIndexSnapshot snapshot, CancellationToken cancellationToken);
}

public interface IKnowledgeIndexStore
{
    Task<KnowledgeIndexLease?> ClaimAsync(DateTime now, TimeSpan leaseDuration, int maxAttempts, CancellationToken ct);
    Task<bool> CompleteAsync(KnowledgeIndexLease lease, PreparedKnowledgeIndex result, DateTime now, CancellationToken ct);
    Task FailAsync(KnowledgeIndexLease lease, string errorCode, bool retryable, DateTime now, int maxAttempts, CancellationToken ct);
    Task<Guid> RequestRebuildAsync(Guid articleId, Guid requestId, uint expectedRevision, DateTime now, CancellationToken ct);
    Task<KnowledgeIndexStatusDto> GetStatusAsync(Guid articleId, CancellationToken ct);
}
