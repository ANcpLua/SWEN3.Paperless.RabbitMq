# Migration Guide: v1.0.5 → v2.0.0

## For Customers Using SWEN3.Paperless.RabbitMq

### Prerequisites
- Upgrade to .NET 10
- Update package reference to `SWEN3.Paperless.RabbitMq` version `2.0.0`

---

## Step 1: Update Package Reference

```xml
<!-- Before -->
<PackageReference Include="SWEN3.Paperless.RabbitMq" Version="1.0.5" />

<!-- After -->
<PackageReference Include="SWEN3.Paperless.RabbitMq" Version="2.0.0" />
```

---

## Step 2: Simplify Service Registration

### Example: `ServiceCollectionExtensions.cs`

**BEFORE (v1.0.5):**
```csharp
using SWEN3.Paperless.RabbitMq.GenAI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGenAiServices(this IServiceCollection services)
    {
        services
            .AddOptionsWithValidateOnStart<GeminiOptions>()
                .BindConfiguration("GenAI:Gemini")
                .ValidateDataAnnotations();

        services.AddHttpClient<ITextSummarizer, GeminiService>();
        services.AddHostedService<GenAIWorker>();

        return services;
    }
}
```

**AFTER (v2.0.0):**
```csharp
using SWEN3.Paperless.RabbitMq.GenAI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGenAiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // One line replaces all the manual setup!
        return services.AddPaperlessGenAI(configuration);
    }
}
```

**Changes:**
- ✅ 8 lines → 1 line (87% reduction)
- ✅ No manual HttpClient registration
- ✅ No manual worker registration
- ✅ No manual options configuration
- ✅ Automatic resilience (retry, circuit breaker, timeout)

---

## Step 3: Update Configuration

### appsettings.json

**BEFORE (v1.0.5):**
```json
{
  "GenAI": {
    "Gemini": {
      "ApiKey": "your-api-key-here",
      "Model": "gemini-2.0-flash",
      "MaxRetries": 3,
      "TimeoutSeconds": 30
    }
  }
}
```

**AFTER (v2.0.0):**
```json
{
  "Gemini": {
    "ApiKey": "your-api-key-here",
    "Model": "gemini-2.0-flash",
    "TimeoutSeconds": 30
  }
}
```

**Changes:**
- ✅ Configuration section renamed: `GenAI:Gemini` → `Gemini` (cleaner)
- ✅ `MaxRetries` removed (handled by Microsoft's standard handler)

---

## Step 4: Update Program.cs / Startup

**BEFORE (v1.0.5):**
```csharp
// Program.cs
services.AddGenAiServices();
```

**AFTER (v2.0.0):**
```csharp
// Program.cs
services.AddGenAiServices(configuration);
```

**Change:** Pass `configuration` parameter.

---

## What You Get Automatically in v2.0.0

### Resilience Features (Zero Configuration)

| Feature | Description | Configuration Required |
|---------|-------------|------------------------|
| **Retry** | 3 attempts with exponential backoff + jitter | ✅ None - automatic |
| **Circuit Breaker** | Stops calling failing API to prevent cascade failures | ✅ None - automatic |
| **Timeout** | Per-request timeout handling | ✅ None - automatic |
| **Transient Error Detection** | Automatically retries 5xx, timeouts, rate limits (429) | ✅ None - automatic |

### Powered By
- **Microsoft.Extensions.Http.Resilience 10.0.0** (official Microsoft package)
- Replaces 3rd party Polly dependency
- Battle-tested across thousands of production systems

---

## Breaking Changes

### 1. Configuration Section Renamed
```json
// Old
"GenAI": { "Gemini": { ... } }

// New
"Gemini": { ... }
```

### 2. MaxRetries Option Removed
The `MaxRetries` property no longer exists in `GeminiOptions`. Retry behavior is now handled by Microsoft's standard resilience handler (3 retries by default).

If you need custom retry behavior, you can configure it:

```csharp
services.AddHttpClient<ITextSummarizer, GeminiService>()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 5;  // Override default of 3
    });
```

### 3. Service Registration Signature Changed
```csharp
// Old
AddGenAiServices(this IServiceCollection services)

// New
AddGenAiServices(this IServiceCollection services, IConfiguration configuration)
```

### 4. New Extension Method Available
You can now use `AddPaperlessGenAI()` directly:

```csharp
// Minimal setup - just use the extension directly
services.AddPaperlessGenAI(configuration);

// Or wrap it in your own extension (recommended)
public static IServiceCollection AddGenAiServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    return services.AddPaperlessGenAI(configuration);
}
```

---

## Complete Example

### Full `ServiceCollectionExtensions.cs` After Migration

```csharp
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using PaperlessServices.BL.Ocr;
using PaperlessServices.BL.Search;
using PaperlessServices.BL.Storage;
using PaperlessServices.Configuration;
using PaperlessServices.Workers;
using SWEN3.Paperless.RabbitMq.GenAI;  // ← Same namespace

namespace PaperlessServices.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all OCR-related services: storage, search indexing, and OCR processing.
    /// </summary>
    public static IServiceCollection AddOcrServices(this IServiceCollection services) =>
        services
            .AddMinioStorage()
            .AddElasticsearchSearch()
            .AddOcrProcessing();

    /// <summary>
    /// Registers GenAI summarization services with Gemini integration.
    /// Includes automatic resilience (retry, circuit breaker, timeout) via Microsoft.Extensions.Http.Resilience.
    /// </summary>
    public static IServiceCollection AddGenAiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // That's it! One line replaces everything.
        return services.AddPaperlessGenAI(configuration);
    }

    // ... rest of your methods stay the same ...
}
```

### Workers Stay Unchanged

**OcrWorker.cs** - No changes needed! ✅
```csharp
// Lines 75-76 - Works exactly the same
var genAiCommand = new GenAICommand(request.JobId, ocrResult.Text!);
await publisher.PublishGenAICommandAsync(genAiCommand);
```

**GenAIResultListener.cs** - No changes needed! ✅
```csharp
// Your listener continues to consume GenAIEvent messages
// All the resilience happens transparently in the background
```

---

## Testing the Migration

### 1. Verify Configuration
```bash
# Check your appsettings.json has the new structure
cat appsettings.json | jq '.Gemini'
```

### 2. Run Your Application
```bash
dotnet run
```

### 3. Verify Resilience is Working

Check logs for retry behavior:
```
[INF] Processing OCR job 12345...
[WRN] Gemini API call failed, retrying (attempt 1/3)...
[WRN] Gemini API call failed, retrying (attempt 2/3)...
[INF] Gemini API call succeeded
[INF] Successfully updated document with GenAI summary
```

---

## Questions?

### Q: Do I need to change my workers?
**A:** No! `OcrWorker` and `GenAIResultListener` work exactly the same.

### Q: Will my existing configuration work?
**A:** Almost! Just rename the section from `GenAI:Gemini` to `Gemini` and remove `MaxRetries`.

### Q: What if I need custom retry behavior?
**A:** Use the configuration overload:
```csharp
services.AddHttpClient<ITextSummarizer, GeminiService>()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 5;
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
    });
```

### Q: Is this production-ready?
**A:** Yes! It uses Microsoft's official resilience package with battle-tested defaults used by thousands of production systems.

---

## Summary

**v2.0.0 Benefits:**
- ✅ 87% less code (8 lines → 1 line)
- ✅ Built-in resilience (retry, circuit breaker, timeout)
- ✅ Official Microsoft package (not 3rd party)
- ✅ Zero configuration needed
- ✅ Workers unchanged
- ✅ Production-ready defaults

**Migration Time:** ~5 minutes ⏱️

Happy coding! 🚀
