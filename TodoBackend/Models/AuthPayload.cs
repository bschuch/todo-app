namespace TodoBackend.Models;

public class AuthPayload
{
    public required AppUser User { get; set; }
    public required string Token { get; set; }
}
