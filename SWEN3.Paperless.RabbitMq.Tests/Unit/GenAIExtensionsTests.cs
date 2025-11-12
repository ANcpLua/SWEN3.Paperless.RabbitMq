using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                ["Gemini:ApiKey"] = "test-key"
            })
            .Build();

        services.AddPaperlessGenAI(configuration);

        services.Should().Contain(sd => sd.ServiceType == typeof(IConfigureOptions<GeminiOptions>));
        services.Should().Contain(sd => sd.ServiceType == typeof(ITextSummarizer));
        services.Should().Contain(sd => sd.ServiceType == typeof(IHostedService) && sd.ImplementationType == typeof(GenAIWorker));
    }
}
