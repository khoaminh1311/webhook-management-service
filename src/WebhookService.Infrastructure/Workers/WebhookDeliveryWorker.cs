using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebhookService.Core.Interfaces;

namespace WebhookService.Infrastructure.Workers;

public class WebhookDeliveryWorker : BackgroundService
{
    private readonly IWebhookDeliveryQueue _queue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<WebhookDeliveryWorker> _logger;

    public WebhookDeliveryWorker(
        IWebhookDeliveryQueue queue, 
        IServiceScopeFactory serviceScopeFactory,
        ILogger<WebhookDeliveryWorker> logger)
    {
        _queue = queue;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebhookDeliveryWorker is starting.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem = await _queue.DequeueAsync(stoppingToken);

                _logger.LogInformation(
                    "Processing work item. EventId: {EventId}, EventType: {EventType}, " +
                    "WebhookCount: {WebhookCount}, CorrelationId: {CorrelationId}",
                    workItem.EventId,
                    workItem.EventType,
                    workItem.TargetWebhookIds.Count,
                    workItem.CorrelationId);

                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var deliveryService = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();
                    
                    await deliveryService.ProcessDeliveryAsync(workItem, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing delivery. EventId: {EventId}, CorrelationId: {CorrelationId}",
                        workItem.EventId, workItem.CorrelationId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("WebhookDeliveryWorker is stopping due to cancellation.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "A critical error occurred in WebhookDeliveryWorker.");
        }
    }
}
