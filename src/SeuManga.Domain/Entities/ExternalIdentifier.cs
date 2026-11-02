using SeuManga.Domain.Enums;

namespace SeuManga.Domain.Entities;

public sealed class ExternalIdentifier
{
    public int Id { get; set; }
    public LibraryContentKind ContentKind { get; set; }
    public required string LocalId { get; set; }
    public required string Provider { get; set; }
    public required string ExternalId { get; set; }
    public required string MatchedBy { get; set; }
    public double Confidence { get; set; }
    public DateTimeOffset MatchedAt { get; set; } = DateTimeOffset.UtcNow;
}
