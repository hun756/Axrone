namespace Axrone.Reactive.Scene;

using Axrone.Scene;

/// <summary>
/// Lifetime-bound subscriptions for <see cref="SceneBehaviour"/>. Every helper registers the
/// returned subscription with <see cref="SceneBehaviour.TrackSubscription"/>, so the owning table
/// disposes it when the behaviour is removed or its actor is destroyed.
/// </summary>
public static class SceneBehaviourReactiveExtensions
{
    /// <summary>Observes component additions for one unmanaged component type.</summary>
    public static IDisposable ObserveComponentAdded<T>(
        this SceneBehaviour behaviour,
        SceneReactiveBridge bridge,
        Action<ComponentAdded<T>> handler)
        where T : unmanaged, ISceneDataComponent
    {
        ArgumentNullException.ThrowIfNull(behaviour);
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(handler);

        IDisposable subscription = bridge.ComponentAdded<T>().Subscribe(new ActionObserver<ComponentAdded<T>>(handler));
        behaviour.TrackSubscription(subscription);
        return subscription;
    }

    /// <summary>Observes component removals for one unmanaged component type.</summary>
    public static IDisposable ObserveComponentRemoved<T>(
        this SceneBehaviour behaviour,
        SceneReactiveBridge bridge,
        Action<ComponentRemoved<T>> handler)
        where T : unmanaged, ISceneDataComponent
    {
        ArgumentNullException.ThrowIfNull(behaviour);
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(handler);

        IDisposable subscription = bridge.ComponentRemoved<T>().Subscribe(new ActionObserver<ComponentRemoved<T>>(handler));
        behaviour.TrackSubscription(subscription);
        return subscription;
    }

    /// <summary>Observes component changes for one unmanaged component type.</summary>
    public static IDisposable ObserveComponentChanged<T>(
        this SceneBehaviour behaviour,
        SceneReactiveBridge bridge,
        Action<ComponentChanged<T>> handler)
        where T : unmanaged, ISceneDataComponent
    {
        ArgumentNullException.ThrowIfNull(behaviour);
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(handler);

        IDisposable subscription = bridge.ComponentChanged<T>().Subscribe(new ActionObserver<ComponentChanged<T>>(handler));
        behaviour.TrackSubscription(subscription);
        return subscription;
    }

    /// <summary>Observes collisions that involve this behaviour's actor.</summary>
    public static IDisposable OnCollision(
        this SceneBehaviour behaviour,
        SceneReactiveBridge bridge,
        Action<SceneCollisionEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(behaviour);
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(handler);

        SceneActor actor = behaviour.Actor;
        IDisposable subscription = bridge.Collisions.Subscribe(new ActionObserver<SceneCollisionEvent>(
            collision =>
            {
                if (collision.ActorA == actor || collision.ActorB == actor)
                {
                    handler(collision);
                }
            }));

        behaviour.TrackSubscription(subscription);
        return subscription;
    }

    /// <summary>Observes sensor overlaps that involve this behaviour's actor.</summary>
    public static IDisposable OnSensor(
        this SceneBehaviour behaviour,
        SceneReactiveBridge bridge,
        Action<SceneSensorEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(behaviour);
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(handler);

        SceneActor actor = behaviour.Actor;
        IDisposable subscription = bridge.Sensors.Subscribe(new ActionObserver<SceneSensorEvent>(
            sensor =>
            {
                if (sensor.SensorActor == actor || sensor.VisitorActor == actor)
                {
                    handler(sensor);
                }
            }));

        behaviour.TrackSubscription(subscription);
        return subscription;
    }

    /// <summary>Runs the handler once when this behaviour's actor is destroyed.</summary>
    public static IDisposable OnActorDestroyed(
        this SceneBehaviour behaviour,
        SceneReactiveBridge bridge,
        Action<SceneActor> handler)
    {
        ArgumentNullException.ThrowIfNull(behaviour);
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(handler);

        SceneActor actor = behaviour.Actor;
        IDisposable subscription = bridge.ActorDestroyed.Subscribe(new ActionObserver<SceneActor>(
            destroyed =>
            {
                if (destroyed == actor)
                {
                    handler(destroyed);
                }
            }));

        behaviour.TrackSubscription(subscription);
        return subscription;
    }

    private sealed class ActionObserver<T>(Action<T> handler) : IObserver<T>
    {
        public void OnNext(T value) => handler(value);

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }
}
