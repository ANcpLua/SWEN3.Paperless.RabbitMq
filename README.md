[![codecov](https://codecov.io/gh/ANcpLua/SWEN3.Paperless.RabbitMq/branch/main/graph/badge.svg?token=lgxIXBnFrn)](https://codecov.io/gh/ANcpLua/SWEN3.Paperless.RabbitMq)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-7C3AED)](https://dotnet.microsoft.com/download/dotnet/10.0)[![.NET 9](https://img.shields.io/badge/-9.0-6366F1)](https://dotnet.microsoft.com/download/dotnet/9.0)[![.NET 8](https://img.shields.io/badge/-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![NuGet](https://img.shields.io/nuget/v/SWEN3.Paperless.RabbitMq?label=NuGet&color=0891B2)](https://www.nuget.org/packages/SWEN3.Paperless.RabbitMq/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/ANcpLua/SWEN3.Paperless.RabbitMq/blob/main/LICENSE)

[![Star this repo](https://img.shields.io/github/stars/ANcpLua/SWEN3.Paperless.RabbitMq?style=social)](https://github.com/ANcpLua/SWEN3.Paperless.RabbitMq/stargazers)

**Star if this works for you** — helps others find it.

# SWEN3.Paperless.RabbitMq

RabbitMQ messaging library for .NET with SSE support and AI-powered document summarization.

## Migration to v2.0.0

```csharp
// Before (v1.0.4)
services.AddOptionsWithValidateOnStart<GeminiOptions>()
    .BindConfiguration("GenAI:Gemini")
    .ValidateDataAnnotations();
services.AddHttpClient<ITextSummarizer, GeminiService>();
services.AddHostedService<GenAIWorker>();

// After (v2.0.0) - Minimal
services.AddPaperlessGenAI(configuration);

// After (v2.0.0) - With optional validation
services.AddOptionsWithValidateOnStart<GeminiOptions>()
    .BindConfiguration(GeminiOptions.SectionName)
    .ValidateDataAnnotations();
services.AddPaperlessGenAI(configuration);
```

**Breaking Changes:**

- Configuration section renamed: `GenAI:Gemini` → `Gemini`
- Removed `MaxRetries` property from `GeminiOptions` (retry handling now managed by `AddStandardResilienceHandler()`)

## Configuration

```json
{
  "RabbitMQ": {
    "Uri": "amqp://guest:guest@localhost:5672"
  }
}
```

## Usage

### Basic Setup

```csharp
// Add RabbitMQ
builder.Services.AddPaperlessRabbitMq(builder.Configuration);

// With SSE support
services.AddPaperlessRabbitMq(config, includeOcrResultStream: true, includeGenAiResultStream: true);
```

### Publishing

```csharp
// Basic command
var command = new OcrCommand(docId, fileName, storagePath);
await publisher.PublishOcrCommandAsync(command);

// With timestamp (v2.2.0+)
var command = new OcrCommand(docId, fileName, storagePath, createdAt: DateTimeOffset.UtcNow);
await publisher.PublishOcrCommandAsync(command);

// Event
var result = new OcrEvent(jobId, "Completed", text, DateTimeOffset.UtcNow);
await publisher.PublishOcrEventAsync(result);
```

### Consuming

```csharp
await using var consumer = await factory.CreateConsumerAsync<OcrCommand>();

await foreach (var command in consumer.ConsumeAsync(cancellationToken))
{
    try
    {
        // Process message
        await consumer.AckAsync();
    }
    catch
    {
        await consumer.NackAsync(requeue: true);
    }
}
```

### SSE Endpoints

```csharp
// Built-in endpoints
app.MapOcrEventStream();      // /api/v1/ocr-results
app.MapGenAIEventStream();    // /api/v1/events/genai

// Custom endpoint with dynamic event types (v2.3.0+)
app.MapSse<NotificationEvent>("/api/notifications", evt => evt.Type);

// Custom endpoint with fixed event type (v2.4.0+)
app.MapSse<HeartbeatEvent>("/api/heartbeat", "heartbeat");

// Full control with payload projection
app.MapSse<OcrEvent>("/api/ocr",
    evt => new { evt.JobId, evt.Status },  // payload
    evt => evt.Status == "Completed" ? "done" : "error");  // event type
```

**Client-side:**

```javascript
const eventSource = new EventSource('/api/v1/ocr-results');
eventSource.addEventListener('ocr-completed', (event) => {
    const data = JSON.parse(event.data);
    console.log(data);
});
```

### GenAI Support (v2.0.0+)

```csharp
// Enable GenAI features with RabbitMQ streaming
builder.Services.AddPaperlessRabbitMq(configuration,
    includeOcrResultStream: true,
    includeGenAiResultStream: true);

// Add Gemini document summarization (includes automatic resilience)
builder.Services.AddPaperlessGenAI(configuration);

// Publish GenAI command
var genAiCommand = new GenAICommand(request.JobId, result.Text!);
await _publisher.PublishGenAICommandAsync(genAiCommand);
```

**Configuration:**

```json
{
  "Gemini": {
    "ApiKey": "your-api-key",
    "Model": "gemini-2.5-flash",
    "TimeoutSeconds": 30
  }
}
```

**What you get automatically:**

- HTTP retry with exponential backoff + jitter (3 attempts)
- Circuit breaker for preventing cascade failures
- Timeout handling per request
- All powered by `Microsoft.Extensions.Http.Resilience`

## Message Types

```csharp
// OCR
public record OcrCommand(Guid JobId, string FileName, string FilePath, DateTimeOffset? CreatedAt = null);
public record OcrEvent(Guid JobId, string Status, string? Text, DateTimeOffset ProcessedAt);

// GenAI
public record GenAICommand(Guid DocumentId, string Text);
public record GenAIEvent(Guid DocumentId, string? Summary, DateTimeOffset GeneratedAt, string? ErrorMessage = null);
```

## SSE MapSse Overloads

| Overload | Use Case |
|----------|----------|
| `MapSse<T>(pattern, payloadSelector, eventTypeSelector)` | Full control over payload and per-event type |
| `MapSse<T>(pattern, eventTypeSelector)` | Serialize whole object, dynamic event type |
| `MapSse<T>(pattern, eventType)` | Serialize whole object, fixed event type |

## Installation

```bash
dotnet add package SWEN3.Paperless.RabbitMq
```

## Changelog

### v2.4.0

- Added `MapSse<T>(pattern, eventType)` overload for fixed event type streams
- .NET 10: Uses optimized `TypedResults.ServerSentEvents` overload (no per-event `SseItem<T>` allocation)

### v2.3.0

- `ISseStream<T>` now extends `IAsyncDisposable` for graceful shutdown
- Added `MapSse<T>(pattern, eventTypeSelector)` overload for simplified endpoint registration
- `SseStream<T>` completes all client channels on dispose

### v2.2.0

- Added optional `CreatedAt` parameter to `OcrCommand` for document timestamp tracking
- Updated default Gemini model to `gemini-2.5-flash`

### v2.1.0

- GenAI SSE streaming support

### v2.0.0

- Simplified `AddPaperlessGenAI()` registration
- Added `Microsoft.Extensions.Http.Resilience` for automatic retry/circuit breaker
- Configuration section renamed: `GenAI:Gemini` → `Gemini`

## License

This project is licensed under the [MIT License](LICENSE).
