using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TodoBackend.Data;
using TodoBackend.Models;

namespace TodoBackend.Services;

public record CurrentUser(string Id, string Email, string DisplayName, bool IsDemo);

public class AuthService
{
    private const string DemoUserId = "development-demo-user";
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IWebHostEnvironment environment;

    public AuthService(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment environment)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.environment = environment;
    }

    public async Task<CurrentUser?> GetCurrentUserAsync(TodoDbContext dbContext)
    {
        var token = GetBearerToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            var user = await dbContext.AppUsers.FirstOrDefaultAsync(currentUser => currentUser.SessionToken == token);
            if (user != null)
            {
                return new CurrentUser(user.Id, user.Email, user.DisplayName, false);
            }
        }

        if (environment.IsDevelopment())
        {
            var demoUser = await dbContext.AppUsers.FirstOrDefaultAsync(user => user.Email == "demo@family.local");
            return demoUser == null
                ? new CurrentUser(DemoUserId, "demo@family.local", "Demo Family", true)
                : new CurrentUser(demoUser.Id, demoUser.Email, demoUser.DisplayName, true);
        }

        return null;
    }

    public async Task<CurrentUser> RequireCurrentUserAsync(TodoDbContext dbContext)
    {
        return await GetCurrentUserAsync(dbContext) ?? throw new GraphQLException("Sign in is required.");
    }

    public async Task<bool> CanAccessFamilyAsync(TodoDbContext dbContext, string familyId)
    {
        var user = await GetCurrentUserAsync(dbContext);
        if (user == null)
        {
            return false;
        }

        if (user.IsDemo)
        {
            return true;
        }

        return await dbContext.FamilyMemberships.AnyAsync(membership => membership.FamilyId == familyId && membership.UserId == user.Id);
    }

    public async Task RequireFamilyAccessAsync(TodoDbContext dbContext, string familyId)
    {
        if (!await CanAccessFamilyAsync(dbContext, familyId))
        {
            throw new GraphQLException("You do not have access to this family.");
        }
    }

    public async Task<string?> GetFamilyRoleAsync(TodoDbContext dbContext, string familyId)
    {
        var user = await GetCurrentUserAsync(dbContext);
        if (user == null)
        {
            return null;
        }

        if (user.IsDemo)
        {
            return "Owner";
        }

        var membership = await dbContext.FamilyMemberships.FirstOrDefaultAsync(currentMembership =>
            currentMembership.FamilyId == familyId && currentMembership.UserId == user.Id);
        return membership?.Role;
    }

    public async Task RequireFamilyOwnerAsync(TodoDbContext dbContext, string familyId)
    {
        var role = await GetFamilyRoleAsync(dbContext, familyId);
        if (!string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            throw new GraphQLException("Family owner permission is required.");
        }
    }

    public static string CreateSessionToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private string? GetBearerToken()
    {
        var header = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }
}
