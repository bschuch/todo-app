using TodoBackend.Services;
using Xunit;

namespace TodoBackend.Tests;

public class AuthServiceTests
{
    [Fact]
    public void PasswordHashesVerifyOnlyTheOriginalPassword()
    {
        var hash = AuthService.HashPassword("correct horse battery staple");

        Assert.True(AuthService.VerifyPassword("correct horse battery staple", hash));
        Assert.False(AuthService.VerifyPassword("incorrect password", hash));
        Assert.DoesNotContain("correct horse battery staple", hash);
    }

    [Fact]
    public void SessionTokensAreRandomAndStoredAsHashes()
    {
        var firstToken = AuthService.CreateSessionToken();
        var secondToken = AuthService.CreateSessionToken();
        var firstHash = AuthService.HashSessionToken(firstToken);

        Assert.NotEqual(firstToken, secondToken);
        Assert.NotEqual(firstToken, firstHash);
        Assert.Equal(firstHash, AuthService.HashSessionToken(firstToken));
        Assert.NotEqual(firstHash, AuthService.HashSessionToken(secondToken));
    }
}
