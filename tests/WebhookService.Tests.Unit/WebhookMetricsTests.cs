using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WebhookService.Core.Exceptions;
using WebhookService.Core.Instrumentation;
using WebhookService.Core.Interfaces;
using WebhookService.Core.Models;
using WebhookService.Core.Settings;
using WebhookService.Infrastructure.Queues;

namespace WebhookService.Tests.Unit;

public class WebhookMetricsTests
{
    private class DummyMeterFactory : IMeterFactory
    {
        public string ScopeName { get; } = Guid.NewGuid().ToString();
        public Meter Create(MeterOptions options) => new Meter(ScopeName);
        public void Dispose() { }
    }

    private (WebhookMetrics Metrics, MeterListener Listener, Dictionary<string, long> LongMeasurements, Dictionary<string, double> DoubleMeasurements, Action Dispose) CreateIsolatedMetrics()
    {
        var meterFactory = new DummyMeterFactory();
        
        var metrics = new WebhookMetrics(meterFactory);

        var listener = new MeterListener();
        var longMeasurements = new Dictionary<string, long>();
        var doubleMeasurements = new Dictionary<string, double>();

        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == meterFactory.ScopeName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (longMeasurements.ContainsKey(instrument.Name))
                longMeasurements[instrument.Name] += measurement;
            else
                longMeasurements[instrument.Name] = measurement;
        });

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (doubleMeasurements.ContainsKey(instrument.Name))
                doubleMeasurements[instrument.Name] += measurement;
            else
                doubleMeasurements[instrument.Name] = measurement;
        });

        listener.Start();

        return (metrics, listener, longMeasurements, doubleMeasurements, () => { listener.Dispose(); });
    }

    private long GetLongMeasurement(Dictionary<string, long> measurements, string instrumentName)
    {
        return measurements.TryGetValue(instrumentName, out var val) ? val : 0;
    }

    private double GetDoubleMeasurement(Dictionary<string, double> measurements, string instrumentName)
    {
        return measurements.TryGetValue(instrumentName, out var val) ? val : 0;
    }

    [Fact]
    public void RecordEventReceived_IncrementsCounter()
    {
        var (metrics, listener, longMeasurements, doubleMeasurements, dispose) = CreateIsolatedMetrics();
        try
        {
            metrics.RecordEventReceived();
            
            var count = GetLongMeasurement(longMeasurements, "webhook.events.received");
            Assert.Equal(1, count);
        }
        finally
        {
            dispose();
        }
    }

    [Fact]
    public void RecordEventDuplicate_IncrementsCounter()
    {
        var (metrics, listener, longMeasurements, doubleMeasurements, dispose) = CreateIsolatedMetrics();
        try
        {
            metrics.RecordEventDuplicate();
            
            var count = GetLongMeasurement(longMeasurements, "webhook.events.duplicates");
            Assert.Equal(1, count);
        }
        finally
        {
            dispose();
        }
    }

    [Fact]
    public void DeliveryOutcomes_MutuallyConsistent()
    {
        var (metrics, listener, longMeasurements, doubleMeasurements, dispose) = CreateIsolatedMetrics();
        try
        {
            // Simulate a successful delivery
            metrics.RecordDeliveryAttempt();
            metrics.RecordDeliverySuccess();
            metrics.RecordDeliveryDuration(150);

            // Simulate a failed delivery that is retried
            metrics.RecordDeliveryAttempt();
            metrics.RecordDeliveryFailure();
            metrics.RecordDeliveryRetry();

            // Simulate a failed delivery that is dead-lettered
            metrics.RecordDeliveryAttempt();
            metrics.RecordDeliveryFailure();
            metrics.RecordDeliveryDeadLetter();

            Assert.Equal(3, GetLongMeasurement(longMeasurements, "webhook.deliveries.total"));
            Assert.Equal(1, GetLongMeasurement(longMeasurements, "webhook.deliveries.success"));
            Assert.Equal(2, GetLongMeasurement(longMeasurements, "webhook.deliveries.failure"));
            Assert.Equal(1, GetLongMeasurement(longMeasurements, "webhook.deliveries.retry"));
            Assert.Equal(1, GetLongMeasurement(longMeasurements, "webhook.deliveries.dead_letter"));
            
            // For histograms, we just verify we can capture it
            Assert.Equal(150, GetDoubleMeasurement(doubleMeasurements, "webhook.delivery.duration"));
        }
        finally
        {
            dispose();
        }
    }

    [Fact]
    public async Task QueueDepthConsistency_NeverNegative()
    {
        var (metrics, listener, longMeasurements, doubleMeasurements, dispose) = CreateIsolatedMetrics();
        try
        {
            var options = Options.Create(new QueueSettings { Capacity = 10 });
            var logger = Substitute.For<ILogger<WebhookDeliveryQueue>>();
            var queue = new WebhookDeliveryQueue(options, logger, metrics);

            var workItem = new DeliveryWorkItem { EventId = Guid.NewGuid(), EventType = "test" };

            // Initially 0
            Assert.Equal(0, queue.CurrentDepth);

            // Enqueue increments
            await queue.EnqueueAsync(workItem);
            Assert.Equal(1, queue.CurrentDepth);

            Assert.Equal(1, GetLongMeasurement(longMeasurements, "webhook.queue.enqueued"));
            Assert.Equal(1, GetLongMeasurement(longMeasurements, "webhook.queue.depth"));

            // Dequeue decrements
            var dequeued = await queue.DequeueAsync();
            Assert.Equal(0, queue.CurrentDepth);

            Assert.Equal(1, GetLongMeasurement(longMeasurements, "webhook.queue.dequeued"));
            Assert.Equal(0, GetLongMeasurement(longMeasurements, "webhook.queue.depth")); // +1 then -1
        }
        finally
        {
            dispose();
        }
    }
}
