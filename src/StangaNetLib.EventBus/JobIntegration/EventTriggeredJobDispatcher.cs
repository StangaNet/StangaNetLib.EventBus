using StangaNetLib.Core.Common;
using StangaNetLib.EventBus.Abstractions;
using StangaNetLib.EventBus.Events;
using StangaNetLib.Jobs.Abstractions;

namespace StangaNetLib.EventBus.JobIntegration;

/// <summary>
/// Implements <see cref="IEventHandler{TEvent}"/> by enqueuing or scheduling a background job
/// whenever the event is published. Registered via <c>AddEventTriggeredJob</c>.
/// </summary>
internal sealed class EventTriggeredJobDispatcher<TEvent, TJob, TArgs>
    : IEventHandler<TEvent>, IEventTriggeredJobHandler<TEvent>
    where TEvent : IntegrationEvent
    where TJob : class, IBackgroundJob<TArgs>
{
    private readonly IJobScheduler _jobScheduler;
    private readonly Func<TEvent, TArgs> _argsFactory;
    private readonly TimeSpan? _delay;

    internal EventTriggeredJobDispatcher(
        IJobScheduler jobScheduler,
        Func<TEvent, TArgs> argsFactory,
        TimeSpan? delay = null)
    {
        _jobScheduler = jobScheduler;
        _argsFactory = argsFactory;
        _delay = delay;
    }

    /// <inheritdoc/>
    public async Task<Result> HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        var args = _argsFactory(@event);

        var result = _delay.HasValue
            ? await _jobScheduler.ScheduleAsync<TJob, TArgs>(args, _delay.Value, cancellationToken)
            : await _jobScheduler.EnqueueAsync<TJob, TArgs>(args, cancellationToken);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }
}
