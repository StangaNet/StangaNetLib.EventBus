using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StangaNetLib.Concurrency.Locking;
using StangaNetLib.EventBus.Abstractions;
using StangaNetLib.EventBus.Events;
using StangaNetLib.EventBus.Implementations;
using StangaNetLib.EventBus.Implementations.InMemory;
using StangaNetLib.EventBus.Implementations.RabbitMq;
using StangaNetLib.EventBus.JobIntegration;
using StangaNetLib.EventBus.Settings;
using StangaNetLib.Jobs.Abstractions;

namespace StangaNetLib.EventBus.Extensions;

/// <summary>
/// Extension methods for registering StangaNetLib.EventBus services.
/// </summary>
/// <example>
/// appsettings.json (in-memory):
/// <code>
/// {
///   "EventBusSettings": {
///     "PropagateHandlerExceptions": false,
///     "MaxConcurrentHandlers": 1
///   }
/// }
/// </code>
/// appsettings.json (RabbitMQ):
/// <code>
/// {
///   "RabbitMqSettings": {
///     "HostName": "localhost",
///     "Port": 5672,
///     "UserName": "guest",
///     "Password": "guest",
///     "ExchangeName": "my_app_events",
///     "QueueName": "my_app_queue"
///   }
/// }
/// </code>
/// Program.cs (in-memory):
/// <code>
/// builder.Services.AddStangaNetLibEventBus(builder.Configuration);
/// builder.Services.AddTransient&lt;OrderPlacedHandler&gt;();
///
/// var app = builder.Build();
/// app.Services.GetRequiredService&lt;IEventBus&gt;().Subscribe&lt;OrderPlacedEvent, OrderPlacedHandler&gt;();
/// </code>
/// Program.cs (RabbitMQ):
/// <code>
/// builder.Services.AddStangaNetLibEventBusWithRabbitMq(builder.Configuration);
/// builder.Services.AddTransient&lt;OrderPlacedHandler&gt;();
///
/// // Subscribe BEFORE the host starts so bindings are created in StartAsync:
/// builder.Services.AddSingleton&lt;IHostedService&gt;(sp =>
/// {
///     var bus = sp.GetRequiredService&lt;IEventBus&gt;();
///     bus.Subscribe&lt;OrderPlacedEvent, OrderPlacedHandler&gt;();
///     return new NoOpHostedService();
/// });
/// </code>
/// </example>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers StangaNetLib.EventBus with the built-in <b>in-memory</b> backend.
    /// Uses configuration from the <c>EventBusSettings</c> section of <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStangaNetLibEventBus(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EventBusSettings>(configuration.GetSection(EventBusSettings.SectionName));
        return RegisterInMemory(services);
    }

    /// <summary>
    /// Registers StangaNetLib.EventBus with the built-in <b>in-memory</b> backend using default settings.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStangaNetLibEventBus(this IServiceCollection services)
    {
        services.Configure<EventBusSettings>(_ => { });
        return RegisterInMemory(services);
    }

    /// <summary>
    /// Registers StangaNetLib.EventBus with the <b>RabbitMQ</b> transport backend.
    /// The <see cref="RabbitMqEventBus"/> is registered as both <see cref="IEventBus"/> (singleton)
    /// and <see cref="Microsoft.Extensions.Hosting.IHostedService"/>, so the broker connection
    /// is opened when the host starts and closed when it stops.
    /// Uses configuration from the <c>RabbitMqSettings</c> section of <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStangaNetLibEventBusWithRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqSettings>(configuration.GetSection(RabbitMqSettings.SectionName));
        return RegisterRabbitMq(services);
    }

    /// <summary>
    /// Registers StangaNetLib.EventBus with the <b>RabbitMQ</b> transport backend using default settings.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStangaNetLibEventBusWithRabbitMq(this IServiceCollection services)
    {
        services.Configure<RabbitMqSettings>(_ => { });
        return RegisterRabbitMq(services);
    }

    private static IServiceCollection RegisterInMemory(IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<EventBusSettings>, EventBusSettingsValidator>();
        services.TryAddSingleton<IEventBusSubscriptionManager, InMemoryEventBusSubscriptionManager>();
        services.TryAddSingleton<IEventBus>(sp => new InMemoryEventBus(
            sp.GetRequiredService<IEventBusSubscriptionManager>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IOptions<EventBusSettings>>(),
            sp.GetService<IKeyedLock>()));
        return services;
    }

    private static IServiceCollection RegisterRabbitMq(IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<RabbitMqSettings>, RabbitMqSettingsValidator>();
        services.TryAddSingleton<IEventBusSubscriptionManager, InMemoryEventBusSubscriptionManager>();
        services.TryAddSingleton<RabbitMqEventBus>(sp => new RabbitMqEventBus(
            sp.GetRequiredService<IEventBusSubscriptionManager>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IOptions<RabbitMqSettings>>(),
            sp.GetRequiredService<ILogger<RabbitMqEventBus>>()));
        services.TryAddSingleton<IEventBus>(sp => sp.GetRequiredService<RabbitMqEventBus>());
        services.AddHostedService(sp => sp.GetRequiredService<RabbitMqEventBus>());
        return services;
    }

    /// <summary>
    /// Registers an event-triggered job that fires immediately when <typeparamref name="TEvent"/> is published.
    /// At host startup the dispatcher is automatically subscribed to the <see cref="IEventBus"/>; no manual
    /// <c>Subscribe</c> call is required.
    /// </summary>
    /// <typeparam name="TEvent">The integration event type that triggers the job.</typeparam>
    /// <typeparam name="TJob">The background job type to enqueue.</typeparam>
    /// <typeparam name="TArgs">The argument type passed to <typeparamref name="TJob"/>.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="argsFactory">Maps the incoming event to the job arguments.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddEventTriggeredJob<TEvent, TJob, TArgs>(
        this IServiceCollection services,
        Func<TEvent, TArgs> argsFactory)
        where TEvent : IntegrationEvent
        where TJob : class, IBackgroundJob<TArgs>
        => RegisterEventTriggeredJob<TEvent, TJob, TArgs>(services, argsFactory, delay: null);

    /// <summary>
    /// Registers an event-triggered job that is scheduled to run after <paramref name="delay"/> when
    /// <typeparamref name="TEvent"/> is published.
    /// At host startup the dispatcher is automatically subscribed to the <see cref="IEventBus"/>; no manual
    /// <c>Subscribe</c> call is required.
    /// </summary>
    /// <typeparam name="TEvent">The integration event type that triggers the job.</typeparam>
    /// <typeparam name="TJob">The background job type to schedule.</typeparam>
    /// <typeparam name="TArgs">The argument type passed to <typeparamref name="TJob"/>.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="argsFactory">Maps the incoming event to the job arguments.</param>
    /// <param name="delay">How long to wait before executing the job after the event is published.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddEventTriggeredJob<TEvent, TJob, TArgs>(
        this IServiceCollection services,
        Func<TEvent, TArgs> argsFactory,
        TimeSpan delay)
        where TEvent : IntegrationEvent
        where TJob : class, IBackgroundJob<TArgs>
        => RegisterEventTriggeredJob<TEvent, TJob, TArgs>(services, argsFactory, delay);

    private static IServiceCollection RegisterEventTriggeredJob<TEvent, TJob, TArgs>(
        IServiceCollection services,
        Func<TEvent, TArgs> argsFactory,
        TimeSpan? delay)
        where TEvent : IntegrationEvent
        where TJob : class, IBackgroundJob<TArgs>
    {
        services.AddTransient(sp => new EventTriggeredJobDispatcher<TEvent, TJob, TArgs>(
            sp.GetRequiredService<IJobScheduler>(), argsFactory, delay));

        services.Configure<EventTriggeredJobRegistry>(reg =>
            reg.Subscriptions.Add(bus =>
                bus.Subscribe<TEvent, EventTriggeredJobDispatcher<TEvent, TJob, TArgs>>()));

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, EventTriggeredJobBootstrapper>());

        return services;
    }
}
