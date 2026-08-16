using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WebhookService.Core.Models;
using WebhookService.Core.Settings;
using WebhookService.Infrastructure.Queues;
using Microsoft.Extensions.DependencyInjection;

namespace WebhookService.Tests.Unit;

public class WebhookDeliveryQueueTests
{
    private class DummyMeterFactory : System.Diagnostics.Metrics.IMeterFactory
    {
        public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options) => new System.Diagnostics.Metrics.Meter(options);
        public void Dispose() { }
    }

    private WebhookService.Core.Instrumentation.WebhookMetrics CreateMetrics()
    {
        var meterFactory = new DummyMeterFactory();
        return new WebhookService.Core.Instrumentation.WebhookMetrics(meterFactory);
    }

    [Fact]
    public async Task Queue_AcceptsAndReturnsWorkItem()
    {
        // Arrange
        var options = Options.Create(new QueueSettings { Capacity = 10 });
        var logger = Substitute.For<ILogger<WebhookDeliveryQueue>>();
        var metrics = CreateMetrics();
        var queue = new WebhookDeliveryQueue(options, logger, metrics);
        var workItem = new DeliveryWorkItem { EventId = Guid.NewGuid(), EventType = "test" };

        // Act
        await queue.EnqueueAsync(workItem);
        var dequeued = await queue.DequeueAsync();

        // Assert
        Assert.NotNull(dequeued);
        Assert.Equal(workItem.EventId, dequeued.EventId);
    }

    [Fact]
    public async Task Queue_SupportsCancellation_WhenReadingEmptyQueue()
    {
        // Arrange
        var options = Options.Create(new QueueSettings { Capacity = 10 });
        var logger = Substitute.For<ILogger<WebhookDeliveryQueue>>();
        var metrics = CreateMetrics();
        var queue = new WebhookDeliveryQueue(options, logger, metrics);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await queue.DequeueAsync(cts.Token);
        });
    }
}
