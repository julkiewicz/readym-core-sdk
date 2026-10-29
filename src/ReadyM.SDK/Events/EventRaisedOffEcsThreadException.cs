namespace ReadyM.SDK.Events;

/// <summary>A networked event was raised on a thread other than the ECS thread.</summary>
public sealed class EventRaisedOffEcsThreadException : InvalidOperationException
{
    internal EventRaisedOffEcsThreadException(Type networkEvent)
        : base($"{networkEvent.Name} was raised on thread {CurrentThread()}, which is not the ECS thread. Schedule "
               + "the raise onto the ECS thread first.")
    {
    }

    private static string CurrentThread()
    {
        var thread = Thread.CurrentThread;

        return thread.Name is { Length: > 0 } name ? $"'{name}'" : $"#{thread.ManagedThreadId}";
    }
}
