using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ServiceDesk.Application.Knowledge.Indexing;

public static class KnowledgeIndexProfile
{
    public const string Id = "kb-e5-small-384-v1";
    public const string Fingerprint = "0060fca67682d9d04a273d74df430840b6b367bcc9505bc5cade76268df77bcb";
    public const int Dimension = 384;
    public const int MaxChunks = 256;
    public const int MaxInputTokens = 480;
    public const string Json = """{"chunk_tokens":384,"chunker_version":"markdown-blocks-v1","dimension":384,"id":"kb-e5-small-384-v1","input_template":"passage: {title}\n\n{chunk}","max_input_tokens":480,"model":"intfloat/multilingual-e5-small","model_revision":"614241f622f53c4eeff9890bdc4f31cfecc418b3","normalization":"lf-v1","overlap_tokens":32,"provider":"sentence-transformers-cpu","title_tokens":48,"tokenizer_revision":"614241f622f53c4eeff9890bdc4f31cfecc418b3"}""";

    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static string Normalize(string text) => text.Replace("\r\n", "\n").Replace("\r", "\n");
    public static string SnapshotHash(string title, string content) => Hash(
        Encoding.UTF8.GetByteCount(title).ToString(CultureInfo.InvariantCulture) + ":" + title +
        Encoding.UTF8.GetByteCount(content).ToString(CultureInfo.InvariantCulture) + ":" + content);
    public static string ChunkId(Guid versionId, string sourceHash, int index, string textHash) => Hash(
        $"{versionId:D}\n{sourceHash}\n{Fingerprint}\n{index.ToString(CultureInfo.InvariantCulture)}\n{textHash}");
}
