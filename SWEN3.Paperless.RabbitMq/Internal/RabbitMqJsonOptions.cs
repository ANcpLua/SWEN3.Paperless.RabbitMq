using System.Text.Json;
using System.Text.Json.Serialization;

namespace SWEN3.Paperless.RabbitMq.Internal;

internal static class RabbitMqJsonOptions
{
    internal static readonly JsonSerializerOptions Options = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}