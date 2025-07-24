[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace SWEN3.Paperless.RabbitMq.Tests;

[ExcludeFromCodeCoverage]
internal static class Messages
{
    internal record SimpleMessage(int Id);

    internal record EnumMessage(MessageStatus Status);

    internal enum MessageStatus
    {
        Active
    }

    internal class SseTestEvent
    {
        public int Id { get; init; }
        public string? Message { get; init; }
    }
}