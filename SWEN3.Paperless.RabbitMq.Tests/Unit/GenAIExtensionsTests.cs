using Microsoft.Extensions.Options;
using SWEN3.Paperless.RabbitMq.GenAI;

namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class GenAIExtensionsTests
{
    [Fact]
    public void AddPaperlessGenAI_RegistersServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-key",
                ["Gemini:Model"] = "gemini-2.0-flash",
                ["Gemini:TimeoutSeconds"] = "15"
            })
            .Build();

        services.AddPaperlessGenAI(configuration);

        services.Should().Contain(sd => sd.ServiceType == typeof(IConfigureOptions<GeminiOptions>));
        services.Should().Contain(sd => sd.ServiceType == typeof(ITextSummarizer));
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IHostedService) && sd.ImplementationType == typeof(GenAIWorker));

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<GeminiOptions>>().Value;
        options.ApiKey.Should().Be("test-key");
        options.Model.Should().Be("gemini-2.0-flash");
        options.TimeoutSeconds.Should().Be(15);

        var summarizer = provider.GetRequiredService<ITextSummarizer>();
        summarizer.Should().NotBeNull();
    }
}
