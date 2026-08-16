using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebhookService.Core.Instrumentation;
using WebhookService.Core.Interfaces;
using WebhookService.Core.Models;
using WebhookService.Core.Settings;

namespace WebhookService.Infrastructure.Queues;

public class WebhookDeliveryQueue : IWebhookDeliveryQueue
{
    private readonly Channel<DeliveryWorkItem> _channel;
    private readonly ILogger<WebhookDeliveryQueue> _logger;
    private readonly WebhookMetrics _metrics;
    private int _currentDepth;

    public WebhookDeliveryQueue(
        IOptions<QueueSettings> options,
        ILogger<WebhookDeliveryQueue> logger,
        WebhookMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;

        var capacity = options.Value.Capacity;
        var channelOptions = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };

        _channel = Channel.CreateBounded<DeliveryWorkItem>(channelOptions);
    }

    /// <summary>
    /// Current queue depth, tracked via Interlocked counters.
    /// Updated only after successful enqueue/dequeue operations.
    /// </summary>
    public int CurrentDepth => Volatile.Read(ref _currentDepth);

    public async ValueTask EnqueueAsync(DeliveryWorkItem workItem, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        // Await the channel write first — only update metrics after success
        await _channel.Writer.WriteAsync(workItem, ct);

        Interlocked.Increment(ref _currentDepth);
        _metrics.RecordQueueDepthIncrement();
        _metrics.RecordQueueEnqueued();

        _logger.LogInformation(
            "Work item enqueued. EventId: {EventId}, CorrelationId: {CorrelationId}, QueueDepth: {QueueDepth}",
            workItem.EventId, workItem.CorrelationId, CurrentDepth);
    }

    public async ValueTask<DeliveryWorkItem> DequeueAsync(CancellationToken ct = default)
    {
        // Await the channel read first — only update metrics after success
        var workItem = await _channel.Reader.ReadAsync(ct);

        Interlocked.Decrement(ref _currentDepth);
        _metrics.RecordQueueDepthDecrement();
        _metrics.RecordQueueDequeued();

        _logger.LogInformation(
            "Work item dequeued. EventId: {EventId}, CorrelationId: {CorrelationId}, QueueDepth: {QueueDepth}",
            workItem.EventId, workItem.CorrelationId, CurrentDepth);

        return workItem;
    }
}
