namespace TodoBackend.Models;

public class Family
{
    public string Id { get; set; } = string.Empty;
    public required string Name { get; set; } = string.Empty;
    public string BoardId { get; set; } = "family-home";
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
