namespace TodoBackend.Models;

public class FamilyMembership
{
    public string Id { get; set; } = string.Empty;
    public required string FamilyId { get; set; } = string.Empty;
    public required string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = "Member";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
