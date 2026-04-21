using InstaVende.Core.Enums;

namespace InstaVende.Core.Entities;

public class KnowledgeEntry
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public KnowledgeCategory Category { get; set; } = KnowledgeCategory.Otros;
    public bool IsFavorite { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Business Business { get; set; } = null!;
}
