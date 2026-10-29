namespace ReadyM.SDK.Events;

internal static class NetworkedEventThread
{
    /// <param name="onWorldThread">Whether the caller is on the world's thread.</param>
    /// <exception cref="InvalidOperationException">The caller is not on the world's thread.</exception>
    public static void ThrowIfNotOnWorldThread<T>(bool onWorldThread) where T : struct, INetworkedEvent
    {
        if (onWorldThread)
            return;

        var thread = Thread.CurrentThread;
        var named = thread.Name is { Length: > 0 } name ? $"'{name}'" : $"#{thread.ManagedThreadId}";

        throw new InvalidOperationException(
            $"{typeof(T).Name} was raised on thread {named}, which is not the ECS thread. Events are raised on the "
            + "ECS thread, where the entities they name and any guess made at them live, and from anywhere else "
            + "the raise races them. Schedule the raise onto the ECS thread first: a callback the game makes on "
            + "a thread of its own has to be handed over before it touches entities or raises anything.");
    }
}
