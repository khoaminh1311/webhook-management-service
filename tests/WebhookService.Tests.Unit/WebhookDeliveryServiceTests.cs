using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WebhookService.Core.Entities;
using WebhookService.Core.Interfaces;
using WebhookService.Core.Models;
using WebhookService.Core.Services;
using WebhookService.Core.Settings;

namespace WebhookService.Tests.Unit;

public class WebhookDeliveryServiceTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebhookRepository _webhookRepository;
    private readonly IDeliveryAttemptRepository _deliveryAttemptRepository;
    private readonly IRetryPolicyService _retryPolicyService;
    private readonly IDelayService _delayService;
    private readonly IWebhookSignatureService _signatureService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookDeliveryService> _logger;
    private readonly IOptions<RetrySettings> _retrySettings;
    private readonly WebhookService.Core.Instrumentation.WebhookMetrics _metrics;

    private class DummyMeterFactory : System.Diagnostics.Metrics.IMeterFactory
    {
        public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options) => new System.Diagnostics.Metrics.Meter(options);
        public void Dispose() { }
    }

    public WebhookDeliveryServiceTests()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _webhookRepository = Substitute.For<IWebhookRepository>();
        _deliveryAttemptRepository = Substitute.For<IDeliveryAttemptRepository>();
        _retryPolicyService = Substitute.For<IRetryPolicyService>();
        _delayService = Substitute.For<IDelayService>();
        _signatureService = Substitute.For<IWebhookSignatureService>();
        _timeProvider = Substitute.For<TimeProvider>();
        _logger = Substitute.For<ILogger<WebhookDeliveryService>>();
        
        var options = Options.Create(new RetrySettings { MaxAttempts = 3, InitialDelaySeconds = 2 });
        _retrySettings = options;

        var utcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _timeProvider.GetUtcNow().Returns(utcNow);
        _signatureService.GenerateSignature(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns("mocked-signature");

        var meterFactory = new DummyMeterFactory();
        _metrics = new WebhookService.Core.Instrumentation.WebhookMetrics(meterFactory);
    }

    private WebhookDeliveryService CreateService()
    {
        return new WebhookDeliveryService(
            _httpClientFactory,
            _webhookRepository,
            _deliveryAttemptRepository,
            _retryPolicyService,
            _delayService,
            _signatureService,
            _timeProvider,
            _retrySettings,
            _logger,
            _metrics);
    }

    private DeliveryWorkItem CreateWorkItem(Guid webhookId)
    {
        return new DeliveryWorkItem
        {
            EventId = Guid.NewGuid(),
            ClientEventId = "test-event-123",
            EventType = "test.event",
            Payload = "{\"key\":\"value\"}",
            TargetWebhookIds = new List<Guid> { webhookId },
            CorrelationId = Guid.NewGuid()
        };
    }

    [Fact]
    public async Task ProcessDeliveryAsync_2xxResponse_RecordsSuccessAndDoesNotRetry()
    {
        // Arrange
        var webhookId = Guid.NewGuid();
        var workItem = CreateWorkItem(webhookId);

        _webhookRepository.GetByIdAsync(webhookId, Arg.Any<CancellationToken>())
            .Returns(new Webhook { Id = webhookId, Url = "https://example.com/webhook", Secret = "secret" });

        var handler = new MockHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("OK")
        });
        var httpClient = new HttpClient(handler);
        _httpClientFactory.CreateClient("WebhookClient").Returns(httpClient);

        var service = CreateService();

        // Act
        await service.ProcessDeliveryAsync(workItem);

        // Assert
        await _deliveryAttemptRepository.Received(1).AddAsync(
            Arg.Is<DeliveryAttempt>(a => 
                a.WebhookId == webhookId && 
                a.EventId == workItem.EventId &&
                a.IsSuccessful == true &&
                a.HttpStatusCode == 200 &&
                a.AttemptNumber == 1 &&
                a.IsDeadLettered == false),
            Arg.Any<CancellationToken>());
        
        await _delayService.DidNotReceive().DelayAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());

        Assert.NotNull(handler.Request);
        Assert.True(handler.Request!.Headers.Contains("X-Webhook-Timestamp"));
        Assert.Equal("test.event", handler.Request.Headers.GetValues("X-Webhook-Event").First());
        Assert.Equal(workItem.ClientEventId, handler.Request.Headers.GetValues("X-Event-Id").First());
        Assert.Equal(workItem.CorrelationId.ToString(), handler.Request.Headers.GetValues("X-Correlation-Id").First());
    }

    [Fact]
    public async Task ProcessDeliveryAsync_4xxResponse_RecordsFailureAndDeadLettersWithoutRetry()
    {
        // Arrange
        var webhookId = Guid.NewGuid();
        var workItem = CreateWorkItem(webhookId);

        _webhookRepository.GetByIdAsync(webhookId, Arg.Any<CancellationToken>())
            .Returns(new Webhook { Id = webhookId, Url = "https://example.com/webhook", Secret = "secret" });

        var httpClient = new HttpClient(new MockHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Bad Request")
        }));
        _httpClientFactory.CreateClient("WebhookClient").Returns(httpClient);
        _retryPolicyService.ShouldRetry(400, null).Returns(false);

        var service = CreateService();

        // Act
        await service.ProcessDeliveryAsync(workItem);

        // Assert
        await _deliveryAttemptRepository.Received(1).AddAsync(
            Arg.Is<DeliveryAttempt>(a => 
                a.WebhookId == webhookId && 
                a.EventId == workItem.EventId &&
                a.IsSuccessful == false &&
                a.HttpStatusCode == 400 &&
                a.AttemptNumber == 1 &&
                a.IsDeadLettered == true),
            Arg.Any<CancellationToken>());

        await _delayService.DidNotReceive().DelayAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDeliveryAsync_5xxResponse_RetriesToMaxAttemptsAndDeadLetters()
    {
        // Arrange
        var webhookId = Guid.NewGuid();
        var workItem = CreateWorkItem(webhookId);

        _webhookRepository.GetByIdAsync(webhookId, Arg.Any<CancellationToken>())
            .Returns(new Webhook { Id = webhookId, Url = "https://example.com/webhook", Secret = "secret" });

        var httpClient = new HttpClient(new MockHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Error")
        }));
        _httpClientFactory.CreateClient("WebhookClient").Returns(httpClient);
        _retryPolicyService.ShouldRetry(500, null).Returns(true);

        var service = CreateService();

        // Act
        await service.ProcessDeliveryAsync(workItem);

        // Assert
        await _deliveryAttemptRepository.Received(3).AddAsync(Arg.Any<DeliveryAttempt>(), Arg.Any<CancellationToken>());
        
        await _deliveryAttemptRepository.Received(1).AddAsync(
            Arg.Is<DeliveryAttempt>(a => a.AttemptNumber == 1 && a.IsDeadLettered == false), Arg.Any<CancellationToken>());
        await _deliveryAttemptRepository.Received(1).AddAsync(
            Arg.Is<DeliveryAttempt>(a => a.AttemptNumber == 2 && a.IsDeadLettered == false), Arg.Any<CancellationToken>());
        await _deliveryAttemptRepository.Received(1).AddAsync(
            Arg.Is<DeliveryAttempt>(a => a.AttemptNumber == 3 && a.IsDeadLettered == true), Arg.Any<CancellationToken>());

        await _delayService.Received(2).DelayAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDeliveryAsync_TransportException_RetriesToMaxAttemptsAndDeadLetters()
    {
        // Arrange
        var webhookId = Guid.NewGuid();
        var workItem = CreateWorkItem(webhookId);

        _webhookRepository.GetByIdAsync(webhookId, Arg.Any<CancellationToken>())
            .Returns(new Webhook { Id = webhookId, Url = "https://example.com/webhook", Secret = "secret" });

        var exception = new HttpRequestException("Connection refused");
        var httpClient = new HttpClient(new MockHttpMessageHandler(exception));
        _httpClientFactory.CreateClient("WebhookClient").Returns(httpClient);
        _retryPolicyService.ShouldRetry(null, exception).Returns(true);

        var service = CreateService();

        // Act
        await service.ProcessDeliveryAsync(workItem);

        // Assert
        await _deliveryAttemptRepository.Received(3).AddAsync(Arg.Any<DeliveryAttempt>(), Arg.Any<CancellationToken>());
        
        await _deliveryAttemptRepository.Received(1).AddAsync(
            Arg.Is<DeliveryAttempt>(a => a.AttemptNumber == 3 && a.IsDeadLettered == true), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDeliveryAsync_WorkerCancellation_StopsImmediatelyWithoutRetry()
    {
        // Arrange
        var webhookId = Guid.NewGuid();
        var workItem = CreateWorkItem(webhookId);

        _webhookRepository.GetByIdAsync(webhookId, Arg.Any<CancellationToken>())
            .Returns(new Webhook { Id = webhookId, Url = "https://example.com/webhook", Secret = "secret" });

        var httpClient = new HttpClient(new MockHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        _httpClientFactory.CreateClient("WebhookClient").Returns(httpClient);
        _retryPolicyService.ShouldRetry(500, null).Returns(true);

        // Simulate cancellation during the first delay
        _delayService.When(x => x.DelayAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()))
            .Do(x => throw new OperationCanceledException());

        var service = CreateService();

        // Act
        await service.ProcessDeliveryAsync(workItem);

        // Assert
        await _deliveryAttemptRepository.Received(1).AddAsync(Arg.Any<DeliveryAttempt>(), Arg.Any<CancellationToken>());
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpResponseMessage>? _responseMessageFactory;
    private readonly Exception? _exception;

    public HttpRequestMessage? Request { get; private set; }

    public MockHttpMessageHandler(Func<HttpResponseMessage> responseMessageFactory)
    {
        _responseMessageFactory = responseMessageFactory;
    }

    public MockHttpMessageHandler(HttpResponseMessage responseMessage)
    {
        _responseMessageFactory = () => responseMessage;
    }

    public MockHttpMessageHandler(Exception exception)
    {
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request; // Capture the request

        if (_exception != null)
        {
            throw _exception;
        }

        var response = _responseMessageFactory!();
        
        return Task.FromResult(response);
    }
}
