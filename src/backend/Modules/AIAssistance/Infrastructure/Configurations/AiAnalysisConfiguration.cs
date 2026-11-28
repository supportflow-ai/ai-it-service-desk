using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Modules.AIAssistance.Domain;

namespace ServiceDesk.Modules.AIAssistance.Infrastructure.Configurations;

/// <summary>
/// Bảng ai_analyses: mỗi lần phân tích một dòng, quyết định HITL nằm trên chính dòng đó (tối đa một quyết định).
/// Enum lưu dạng chuỗi để đổi thứ tự khai báo không đổi nghĩa dữ liệu đã lưu.
/// Không có khóa ngoại tới tickets: module không ánh xạ thực thể của Ticketing.
/// </summary>
internal sealed class AiAnalysisConfiguration : IEntityTypeConfiguration<AIAnalysis>
{
    public void Configure(EntityTypeBuilder<AIAnalysis> entity)
    {
        entity.ToTable("ai_analyses", table => table.HasCheckConstraint(
            "ck_ai_analyses_decision",
            "(decision = 'Pending') = (decided_by IS NULL) AND (decided_by IS NULL) = (decided_at IS NULL) " +
            "AND (decision = 'Pending' OR succeeded)"));
        entity.HasKey(a => a.Id);
        entity.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(a => a.TicketId).HasColumnName("ticket_id");
        entity.Property(a => a.TicketRevision).HasColumnName("ticket_revision");
        entity.Property(a => a.InputHash).HasColumnName("input_hash").HasMaxLength(64).IsRequired();
        entity.Property(a => a.CreatedAt).HasColumnName("created_at");
        entity.Property(a => a.Succeeded).HasColumnName("succeeded");
        entity.Property(a => a.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(32);
        entity.Property(a => a.ValidationErrors).HasColumnName("validation_errors");
        entity.Property(a => a.FailureReason).HasColumnName("failure_reason");
        entity.Property(a => a.Decision).HasColumnName("decision").HasConversion<string>().HasMaxLength(16);
        entity.Property(a => a.DecidedBy).HasColumnName("decided_by");
        entity.Property(a => a.DecidedAt).HasColumnName("decided_at");
        entity.Property(a => a.DecisionReason).HasColumnName("decision_reason");
        entity.Property(a => a.DecisionRevision).HasColumnName("decision_revision");

        entity.OwnsOne(a => a.Previous, values => Map(values, "previous"));
        entity.OwnsOne(a => a.Applied, values => Map(values, "applied"));

        entity.HasIndex(a => new { a.TicketId, a.CreatedAt, a.Id }).HasDatabaseName("ix_ai_analyses_ticket_id_created_at_id");

        entity.HasMany(a => a.Suggestions).WithOne()
            .HasForeignKey(s => s.AnalysisId).OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(a => a.Suggestions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void Map(OwnedNavigationBuilder<AIAnalysis, TriageValues> values, string prefix)
    {
        values.Property(v => v.CategoryId).HasColumnName($"{prefix}_category_id").HasMaxLength(50);
        values.Property(v => v.Impact).HasColumnName($"{prefix}_impact").HasMaxLength(16);
        values.Property(v => v.Urgency).HasColumnName($"{prefix}_urgency").HasMaxLength(16);
        values.Property(v => v.Priority).HasColumnName($"{prefix}_priority").HasMaxLength(16);
    }
}

internal sealed class AiSuggestionConfiguration : IEntityTypeConfiguration<AISuggestion>
{
    public void Configure(EntityTypeBuilder<AISuggestion> entity)
    {
        entity.ToTable("ai_suggestions", table => table.HasCheckConstraint(
            "ck_ai_suggestions_model_score", "model_score IS NULL OR (model_score >= 0 AND model_score <= 1)"));
        entity.HasKey(s => s.Id);
        entity.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(s => s.AnalysisId).HasColumnName("analysis_id");
        entity.Property(s => s.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(16);
        entity.Property(s => s.SuggestedValue).HasColumnName("suggested_value").HasMaxLength(50).IsRequired();
        entity.Property(s => s.ModelScore).HasColumnName("model_score");
        entity.Property(s => s.Decision).HasColumnName("decision").HasConversion<string>().HasMaxLength(16);
        entity.Property(s => s.FinalValue).HasColumnName("final_value").HasMaxLength(50);
        entity.Property(s => s.ReviewedBy).HasColumnName("reviewed_by").HasMaxLength(36);
        entity.Property(s => s.ReviewedAt).HasColumnName("reviewed_at");
        entity.HasIndex(s => new { s.AnalysisId, s.Type }).IsUnique().HasDatabaseName("ux_ai_suggestions_analysis_id_type");
    }
}
