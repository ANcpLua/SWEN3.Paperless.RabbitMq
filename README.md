[![codecov](https://codecov.io/gh/ANcpLua/SWEN3.Paperless.RabbitMq/branch/main/graph/badge.svg?token=lgxIXBnFrn)](https://codecov.io/gh/ANcpLua/SWEN3.Paperless.RabbitMq)
[![.NET 10](https://img.shields.io/badge/.NET-10.0_Preview-7C3AED)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![NuGet](https://img.shields.io/nuget/v/SWEN3.Paperless.RabbitMq?label=NuGet&color=0891B2)](https://www.nuget.org/packages/SWEN3.Paperless.RabbitMq/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/ANcpLua/SWEN3.Paperless.RabbitMq/blob/main/LICENSE)

# SWEN3.Paperless.RabbitMq

RabbitMQ messaging library for .NET with SSE support.

## Configuration

```csharp
<EnablePreviewFeatures>true</EnablePreviewFeatures>
```
       
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
builder.Services.AddPaperlessRabbitMq(configuration, includeOcrResultStream: true);
```

### Publishing

```csharp
var command = new OcrCommand(docId, fileName, storagePath);
await publisher.PublishOcrCommandAsync(command);

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

### SSE Endpoint

```csharp
// Map endpoint
app.MapOcrEventStream();

// Client-side
const eventSource = new EventSource('/api/v1/ocr-results');
eventSource.addEventListener('ocr-completed', (event) => {
    const data = JSON.parse(event.data);
    console.log(data);
});
```

## Message Types

```csharp
public record OcrCommand(Guid JobId, string FileName, string FilePath);
public record OcrEvent(Guid JobId, string Status, string? Text, DateTimeOffset ProcessedAt);
```

## Installation

```bash
dotnet add package SWEN3.Paperless.RabbitMq
```

## License

This project is licensed under the [MIT License](LICENSE).
