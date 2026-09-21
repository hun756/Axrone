namespace Axrone.Event;

/// <summary>Handle to an active subscription; disposing unsubscribes.</summary>
public interface IEventSubscription : IDisposable, IAsyncDisposable
{
    /// <summary>Stable identity of this subscription.</summary>
    Guid SubscriptionId { get; }

    /// <summary>Whether dispatch currently delivers to this subscription.</summary>
    bool IsActive { get; }

    /// <summary>Stops delivery without unsubscribing.</summary>
    void Pause();

    /// <summary>Resumes delivery after <see cref="Pause"/>.</summary>
    void Resume();
}
