namespace StangaNetLib.EventBus.Settings;

/// <summary>Configuration for the RabbitMQ transport backend of StangaNetLib.EventBus.</summary>
public sealed class RabbitMqSettings : EventBusSettings
{
    /// <summary>The <c>appsettings.json</c> section key. Value: <c>"RabbitMqSettings"</c>.</summary>
    public new const string SectionName = "RabbitMqSettings";

    /// <summary>RabbitMQ broker hostname or IP address. Defaults to <c>"localhost"</c>.</summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>RabbitMQ AMQP port. Defaults to <c>5672</c>.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>Username for RabbitMQ broker authentication. Defaults to <c>"guest"</c>.</summary>
    public string UserName { get; set; } = "guest";

    /// <summary>Password for RabbitMQ broker authentication. Defaults to <c>"guest"</c>.</summary>
    public string Password { get; set; } = "guest";

    /// <summary>RabbitMQ virtual host. Defaults to <c>"/"</c>.</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Name of the direct exchange used to route published events.
    /// Defaults to <c>"stanganetlib_eventbus"</c>.
    /// </summary>
    public string ExchangeName { get; set; } = "stanganetlib_eventbus";

    /// <summary>
    /// Name of the queue used to consume events.
    /// Defaults to <c>"stanganetlib_eventbus_queue"</c>.
    /// </summary>
    public string QueueName { get; set; } = "stanganetlib_eventbus_queue";

    /// <summary>
    /// When <c>true</c>, the exchange and queue survive broker restarts.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, the exchange and queue are deleted when they have no more consumers or bindings.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool AutoDelete { get; set; } = false;
}
