using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using WebhookService.Core.Entities;
using WebhookService.Core.Settings;
using WebhookService.Infrastructure.Services;

namespace WebhookService.Tests.Unit;

public class JwtTokenGeneratorTests
{
    [Fact]
    public void GenerateToken_ReturnsValidJwt()
    {
        // Arrange
        var settings = new JwtSettings
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SecretKey = "SuperSecretKeyForTestingPurposesOnly123!",
            ExpirationMinutes = 60
        };
        var options = Options.Create(settings);
        var sut = new JwtTokenGenerator(options);
        
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com"
        };

        // Act
        var tokenString = sut.GenerateToken(user);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(tokenString));
        
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);
        
        Assert.Equal("TestIssuer", token.Issuer);
        Assert.Equal("TestAudience", token.Audiences.First());
        Assert.Contains(token.Claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        Assert.Contains(token.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
    }
}
