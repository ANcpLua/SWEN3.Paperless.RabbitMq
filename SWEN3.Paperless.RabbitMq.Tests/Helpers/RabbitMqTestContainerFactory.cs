using DotNet.Testcontainers.Builders;

namespace SWEN3.Paperless.RabbitMq.Tests.Helpers;

internal static class RabbitMqTestContainerFactory
{
    public static RabbitMqContainer Create() =>
        new RabbitMqBuilder()
            .WithImage("rabbitmq:3.12-management")
            .WithEnvironment("RABBITMQ_LOGS", "-")
            .WithEnvironment("RABBITMQ_SASL_LOGS", "-")
            .WithTmpfsMount("/var/log/rabbitmq")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Server startup complete"))
            .Build();
}
