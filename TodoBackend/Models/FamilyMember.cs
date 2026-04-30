namespace TodoBackend.Models;

public class FamilyMember
{
    public string Id { get; set; } = string.Empty;
    public required string FamilyId { get; set; } = string.Empty;
    public required string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6dbec2";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
