namespace TodoBackend.Models;

public class FamilyInvite
{
    public string Id { get; set; } = string.Empty;
    public required string FamilyId { get; set; } = string.Empty;
    public required string Code { get; set; } = string.Empty;
    public required string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(14);
    public bool Revoked { get; set; }
}
