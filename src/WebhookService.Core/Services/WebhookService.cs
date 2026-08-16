using System.Security.Cryptography;
using WebhookService.Core.DTOs;
using WebhookService.Core.Entities;
using WebhookService.Core.Interfaces;

namespace WebhookService.Core.Services;

public class WebhookService : IWebhookService
{
    private readonly IWebhookRepository _webhookRepository;

    public WebhookService(IWebhookRepository webhookRepository)
    {
        _webhookRepository = webhookRepository;
    }

    public async Task<WebhookResponse> CreateAsync(Guid userId, CreateWebhookRequest request, CancellationToken ct = default)
    {
        ValidateEventTypes(request.EventTypes);

        // Generate a secure secret
        var secretBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(secretBytes);
        }
        var secret = Convert.ToBase64String(secretBytes);

        var webhook = new Webhook
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Url = request.Url,
            Secret = secret,
            EventTypes = request.EventTypes.Distinct().ToList(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _webhookRepository.AddAsync(webhook, ct);

        return MapToResponse(webhook);
    }

    public async Task<List<WebhookResponse>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var webhooks = await _webhookRepository.ListByUserIdAsync(userId, ct);
        return webhooks.Select(MapToResponse).ToList();
    }

    public async Task<WebhookResponse> GetAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var webhook = await _webhookRepository.GetByIdAndUserAsync(id, userId, ct);
        if (webhook == null)
        {
            throw new KeyNotFoundException($"Webhook with ID {id} was not found.");
        }

        return MapToResponse(webhook);
    }

    public async Task<WebhookResponse> UpdateAsync(Guid id, Guid userId, UpdateWebhookRequest request, CancellationToken ct = default)
    {
        ValidateEventTypes(request.EventTypes);

        var webhook = await _webhookRepository.GetByIdAndUserAsync(id, userId, ct);
        if (webhook == null)
        {
            throw new KeyNotFoundException($"Webhook with ID {id} was not found.");
        }

        webhook.Name = request.Name;
        webhook.Url = request.Url;
        webhook.EventTypes = request.EventTypes.Distinct().ToList();
        webhook.IsActive = request.IsActive;
        webhook.UpdatedAt = DateTimeOffset.UtcNow;

        await _webhookRepository.UpdateAsync(webhook, ct);

        return MapToResponse(webhook);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var webhook = await _webhookRepository.GetByIdAndUserAsync(id, userId, ct);
        if (webhook == null)
        {
            throw new KeyNotFoundException($"Webhook with ID {id} was not found.");
        }

        await _webhookRepository.DeleteAsync(webhook, ct);
    }

    private static void ValidateEventTypes(List<string> eventTypes)
    {
        if (eventTypes == null || !eventTypes.Any())
        {
            throw new ArgumentException("At least one event type is required.");
        }

        if (eventTypes.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Event types cannot contain empty values.");
        }
    }

    private static WebhookResponse MapToResponse(Webhook webhook)
    {
        return new WebhookResponse
        {
            Id = webhook.Id,
            UserId = webhook.UserId,
            Name = webhook.Name,
            Url = webhook.Url,
            EventTypes = webhook.EventTypes,
            IsActive = webhook.IsActive,
            CreatedAt = webhook.CreatedAt,
            UpdatedAt = webhook.UpdatedAt
        };
    }
}
