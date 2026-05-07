namespace TodoBackend.Models;

public class AppUser
{
    public string Id { get; set; } = string.Empty;
    public required string Email { get; set; } = string.Empty;
    public required string DisplayName { get; set; } = string.Empty;
    [GraphQLIgnore]
    public required string PasswordHash { get; set; } = string.Empty;
    [GraphQLIgnore]
    public string SessionToken { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
