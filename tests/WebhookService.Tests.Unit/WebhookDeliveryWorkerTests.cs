using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebhookService.Core.Interfaces;
using WebhookService.Core.Models;
using WebhookService.Infrastructure.Workers;
using NSubstitute;

namespace WebhookService.Tests.Unit;

public class WebhookDeliveryWorkerTests
{
    [Fact]
    public async Task Worker_ConsumesQueuedWorkItemAndCallsDeliveryService()
    {
        // Arrange
        var queue = Substitute.For<IWebhookDeliveryQueue>();
        var logger = Substitute.For<ILogger<WebhookDeliveryWorker>>();
        var deliveryService = Substitute.For<IWebhookDeliveryService>();

        var services = new ServiceCollection();
        services.AddSingleton(deliveryService);
        var serviceProvider = services.BuildServiceProvider();
        var serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var worker = new WebhookDeliveryWorker(queue, serviceScopeFactory, logger);

        var workItem = new DeliveryWorkItem
        {
            EventId = Guid.NewGuid(),
            EventType = "test.event",
            TargetWebhookIds = new List<Guid> { Guid.NewGuid() },
            CorrelationId = Guid.NewGuid()
        };

        // Make the queue return the item on first call, and throw cancellation on second to exit the loop
        queue.DequeueAsync(Arg.Any<CancellationToken>())
            .Returns(
                new ValueTask<DeliveryWorkItem>(workItem),
                new ValueTask<DeliveryWorkItem>(Task.FromException<DeliveryWorkItem>(new OperationCanceledException()))
            );

        using var cts = new CancellationTokenSource();

        // Act
        var exception = await Record.ExceptionAsync(async () => 
        {
            await worker.StartAsync(cts.Token);
            // Give the background task time to run its loop and hit the mock exception
            await Task.Delay(100);
            await worker.StopAsync(cts.Token);
        });

        // Assert
        Assert.Null(exception);
        
        // Verify delivery service was called
        await deliveryService.Received(1).ProcessDeliveryAsync(workItem, Arg.Any<CancellationToken>());
    }
}
