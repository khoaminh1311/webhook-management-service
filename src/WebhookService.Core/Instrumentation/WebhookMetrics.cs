using System.Diagnostics.Metrics;

namespace WebhookService.Core.Instrumentation;

/// <summary>
/// Provides lightweight application metrics for the webhook service using
/// System.Diagnostics.Metrics. Registered as a singleton.
/// Contains no business logic — all methods are simple counter/histogram wrappers.
/// </summary>
public class WebhookMetrics
{
    public const string MeterName = "WebhookService";

    private readonly Counter<long> _eventsReceived;
    private readonly Counter<long> _eventsDuplicate;
    private readonly Counter<long> _deliveriesTotal;
    private readonly Counter<long> _deliveriesSuccess;
    private readonly Counter<long> _deliveriesFailure;
    private readonly Counter<long> _deliveriesRetry;
    private readonly Counter<long> _deliveriesDeadLetter;
    private readonly Counter<long> _queueEnqueued;
    private readonly Counter<long> _queueDequeued;
    private readonly UpDownCounter<long> _queueDepth;
    private readonly Histogram<double> _deliveryDuration;

    public WebhookMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _eventsReceived = meter.CreateCounter<long>("webhook.events.received", description: "Total events received");
        _eventsDuplicate = meter.CreateCounter<long>("webhook.events.duplicates", description: "Total duplicate events rejected");
        _deliveriesTotal = meter.CreateCounter<long>("webhook.deliveries.total", description: "Total delivery attempts");
        _deliveriesSuccess = meter.CreateCounter<long>("webhook.deliveries.success", description: "Successful delivery attempts");
        _deliveriesFailure = meter.CreateCounter<long>("webhook.deliveries.failure", description: "Failed delivery attempts");
        _deliveriesRetry = meter.CreateCounter<long>("webhook.deliveries.retry", description: "Delivery attempts that triggered a retry");
        _deliveriesDeadLetter = meter.CreateCounter<long>("webhook.deliveries.dead_letter", description: "Delivery attempts that were dead-lettered");
        _queueEnqueued = meter.CreateCounter<long>("webhook.queue.enqueued", description: "Total items enqueued");
        _queueDequeued = meter.CreateCounter<long>("webhook.queue.dequeued", description: "Total items dequeued");
        _queueDepth = meter.CreateUpDownCounter<long>("webhook.queue.depth", description: "Current queue depth");
        _deliveryDuration = meter.CreateHistogram<double>("webhook.delivery.duration", unit: "ms", description: "Delivery attempt duration in milliseconds");
    }

    public void RecordEventReceived() => _eventsReceived.Add(1);
    public void RecordEventDuplicate() => _eventsDuplicate.Add(1);
    public void RecordDeliveryAttempt() => _deliveriesTotal.Add(1);
    public void RecordDeliverySuccess() => _deliveriesSuccess.Add(1);
    public void RecordDeliveryFailure() => _deliveriesFailure.Add(1);
    public void RecordDeliveryRetry() => _deliveriesRetry.Add(1);
    public void RecordDeliveryDeadLetter() => _deliveriesDeadLetter.Add(1);
    public void RecordDeliveryDuration(double durationMs) => _deliveryDuration.Record(durationMs);
    public void RecordQueueEnqueued() => _queueEnqueued.Add(1);
    public void RecordQueueDequeued() => _queueDequeued.Add(1);
    public void RecordQueueDepthIncrement() => _queueDepth.Add(1);
    public void RecordQueueDepthDecrement() => _queueDepth.Add(-1);
}
