namespace TodoBackend.Models;

public class FamilyAccountMember
{
    public required string MembershipId { get; set; }
    public required string UserId { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public required string Role { get; set; }
}
