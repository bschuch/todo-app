namespace TodoBackend.Models;

public class AppSession
{
    public string Id { get; set; } = string.Empty;
    public required string UserId { get; set; } = string.Empty;
    [GraphQLIgnore]
    public required string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
