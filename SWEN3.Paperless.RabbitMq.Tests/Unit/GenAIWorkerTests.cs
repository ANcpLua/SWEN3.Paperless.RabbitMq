using Microsoft.Extensions.Logging;
using SWEN3.Paperless.RabbitMq.GenAI;

namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class GenAIWorkerTests
{
    private readonly Mock<IRabbitMqConsumerFactory> _consumerFactoryMock = new();
    private readonly Mock<ILogger<GenAIWorker>> _loggerMock = new();
    private readonly Mock<IRabbitMqPublisher> _publisherMock = new();
    private readonly Mock<ITextSummarizer> _summarizerMock = new();

    [Fact]
    public async Task ExecuteAsync_ProcessesCommandSuccessfully()
    {
        var documentId = Guid.NewGuid();
        var command = new GenAICommand(documentId, "Document text");
        const string summary = "Generated summary";

        var processed = new TaskCompletionSource();

        var consumerMock = new Mock<IRabbitMqConsumer<GenAICommand>>();
        consumerMock.Setup(c => c.ConsumeAsync(It.IsAny<CancellationToken>())).Returns(SingleMessageAsync(command));
        consumerMock.Setup(c => c.AckAsync()).Callback(() => processed.SetResult()).Returns(Task.CompletedTask);

        _consumerFactoryMock.Setup(f => f.CreateConsumerAsync<GenAICommand>()).ReturnsAsync(consumerMock.Object);

        _summarizerMock.Setup(s => s.SummarizeAsync(command.Text, It.IsAny<CancellationToken>())).ReturnsAsync(summary);

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _ = worker.StartAsync(cts.Token);
        await processed.Task.WaitAsync(cts.Token);
        await worker.StopAsync(cts.Token);

        _summarizerMock.Verify(s => s.SummarizeAsync(command.Text, It.IsAny<CancellationToken>()), Times.Once);
        _publisherMock.Verify(
            p => p.PublishAsync(It.IsAny<string>(),
                It.Is<GenAIEvent>(e => e.DocumentId == documentId && e.Summary == summary)), Times.Once);
        consumerMock.Verify(c => c.AckAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyText_SkipsProcessing()
    {
        var command = new GenAICommand(Guid.NewGuid(), "");
        var processed = new TaskCompletionSource();

        var consumerMock = new Mock<IRabbitMqConsumer<GenAICommand>>();
        consumerMock.Setup(c => c.ConsumeAsync(It.IsAny<CancellationToken>())).Returns(SingleMessageAsync(command));
        consumerMock.Setup(c => c.AckAsync()).Callback(() => processed.SetResult()).Returns(Task.CompletedTask);

        _consumerFactoryMock.Setup(f => f.CreateConsumerAsync<GenAICommand>()).ReturnsAsync(consumerMock.Object);

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _ = worker.StartAsync(cts.Token);
        await processed.Task.WaitAsync(cts.Token);
        await worker.StopAsync(cts.Token);

        _summarizerMock.Verify(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<GenAIEvent>()), Times.Never);
        consumerMock.Verify(c => c.AckAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithTransientError_RequeuesMessage()
    {
        var command = new GenAICommand(Guid.NewGuid(), "Text");
        var processed = new TaskCompletionSource();

        var consumerMock = new Mock<IRabbitMqConsumer<GenAICommand>>();
        consumerMock.Setup(c => c.ConsumeAsync(It.IsAny<CancellationToken>())).Returns(SingleMessageAsync(command));
        consumerMock.Setup(c => c.NackAsync(requeue: true)).Callback(() => processed.SetResult())
            .Returns(Task.CompletedTask);

        _consumerFactoryMock.Setup(f => f.CreateConsumerAsync<GenAICommand>()).ReturnsAsync(consumerMock.Object);

        _summarizerMock.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _ = worker.StartAsync(cts.Token);
        await processed.Task.WaitAsync(cts.Token);
        await worker.StopAsync(cts.Token);

        consumerMock.Verify(c => c.NackAsync(requeue: true), Times.Once);
        consumerMock.Verify(c => c.AckAsync(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithFatalError_PublishesFailureEvent()
    {
        var documentId = Guid.NewGuid();
        var command = new GenAICommand(documentId, "Text");
        var processed = new TaskCompletionSource();

        var consumerMock = new Mock<IRabbitMqConsumer<GenAICommand>>();
        consumerMock.Setup(c => c.ConsumeAsync(It.IsAny<CancellationToken>())).Returns(SingleMessageAsync(command));
        consumerMock.Setup(c => c.NackAsync(requeue: false)).Callback(() => processed.SetResult())
            .Returns(Task.CompletedTask);

        _consumerFactoryMock.Setup(f => f.CreateConsumerAsync<GenAICommand>()).ReturnsAsync(consumerMock.Object);

        _summarizerMock.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fatal error"));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _ = worker.StartAsync(cts.Token);
        await processed.Task.WaitAsync(cts.Token);
        await worker.StopAsync(cts.Token);

        _publisherMock.Verify(
            p => p.PublishAsync(It.IsAny<string>(),
                It.Is<GenAIEvent>(e => e.DocumentId == documentId && e.ErrorMessage == "Fatal error")), Times.Once);
        consumerMock.Verify(c => c.NackAsync(requeue: false), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullSummary_PublishesFailureEvent()
    {
        var documentId = Guid.NewGuid();
        var command = new GenAICommand(documentId, "Text");
        var processed = new TaskCompletionSource();

        var consumerMock = new Mock<IRabbitMqConsumer<GenAICommand>>();
        consumerMock.Setup(c => c.ConsumeAsync(It.IsAny<CancellationToken>())).Returns(SingleMessageAsync(command));
        consumerMock.Setup(c => c.AckAsync()).Callback(() => processed.SetResult()).Returns(Task.CompletedTask);

        _consumerFactoryMock.Setup(f => f.CreateConsumerAsync<GenAICommand>()).ReturnsAsync(consumerMock.Object);

        _summarizerMock.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _ = worker.StartAsync(cts.Token);
        await processed.Task.WaitAsync(cts.Token);
        await worker.StopAsync(cts.Token);

        _publisherMock.Verify(
            p => p.PublishAsync(It.IsAny<string>(),
                It.Is<GenAIEvent>(e =>
                    e.DocumentId == documentId && e.Summary == null && e.ErrorMessage == "Failed to generate summary")),
            Times.Once);
        consumerMock.Verify(c => c.AckAsync(), Times.Once);
    }

    [Fact]
    public async Task HandleCommandAsync_WhenCanceledDuringProcessing_Rethrows()
    {
        var command = new GenAICommand(Guid.NewGuid(), "Text");
        var consumerMock = new Mock<IRabbitMqConsumer<GenAICommand>>();

        using var cts = new CancellationTokenSource(TimeSpan.Zero);

        _summarizerMock.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Canceled"));

        var worker = CreateWorker();

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await worker.HandleCommandAsync(command, consumerMock.Object, cts.Token));

        consumerMock.Verify(c => c.AckAsync(), Times.Never);
        consumerMock.Verify(c => c.NackAsync(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task HandleCommandAsync_WhenPublishingFailureEventFails_StillNacksMessage()
    {
        var documentId = Guid.NewGuid();
        var command = new GenAICommand(documentId, "Text");
        var processed = new TaskCompletionSource();

        var consumerMock = new Mock<IRabbitMqConsumer<GenAICommand>>();
        consumerMock.Setup(c => c.ConsumeAsync(It.IsAny<CancellationToken>())).Returns(SingleMessageAsync(command));
        consumerMock.Setup(c => c.NackAsync(requeue: false)).Callback(() => processed.SetResult())
            .Returns(Task.CompletedTask);

        _consumerFactoryMock.Setup(f => f.CreateConsumerAsync<GenAICommand>()).ReturnsAsync(consumerMock.Object);

        _summarizerMock.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fatal error"));

        _publisherMock.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<GenAIEvent>()))
            .ThrowsAsync(new InvalidOperationException("Publish failed"));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _ = worker.StartAsync(cts.Token);
        await processed.Task.WaitAsync(cts.Token);
        await worker.StopAsync(cts.Token);

        _loggerMock.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to publish failure event")),
                It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);

        consumerMock.Verify(c => c.NackAsync(requeue: false), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ChecksCancellationToken()
    {
        var consumerMock = new Mock<IRabbitMqConsumer<GenAICommand>>();

        consumerMock.Setup(c => c.ConsumeAsync(It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable<GenAICommand>());

        _consumerFactoryMock.Setup(f => f.CreateConsumerAsync<GenAICommand>()).ReturnsAsync(consumerMock.Object);

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();

        await worker.StartAsync(cts.Token);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        _loggerMock.Verify(
            l => l.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("GenAIWorker stopped")), exception: null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    private static async IAsyncEnumerable<T> EmptyAsyncEnumerable<T>()
    {
        await Task.Yield();
        yield break;
    }

    private GenAIWorker CreateWorker()
    {
        return new GenAIWorker(_consumerFactoryMock.Object, _publisherMock.Object, _summarizerMock.Object,
            _loggerMock.Object);
    }

    private static async IAsyncEnumerable<T> SingleMessageAsync<T>(T message)
    {
        yield return message;
        await Task.Delay(Timeout.Infinite, TestContext.Current.CancellationToken);
    }
}
