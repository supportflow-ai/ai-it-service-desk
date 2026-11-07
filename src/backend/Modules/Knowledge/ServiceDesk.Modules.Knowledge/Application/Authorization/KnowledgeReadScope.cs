namespace ServiceDesk.Modules.Knowledge.Application.Authorization;

public abstract record KnowledgeReadScope
{
    public sealed record Public : KnowledgeReadScope;
    public sealed record Staff : KnowledgeReadScope;
    public sealed record Author(Guid AuthorId) : KnowledgeReadScope;
}
