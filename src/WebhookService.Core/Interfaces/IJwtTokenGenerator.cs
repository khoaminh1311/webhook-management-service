using WebhookService.Core.Entities;

namespace WebhookService.Core.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
