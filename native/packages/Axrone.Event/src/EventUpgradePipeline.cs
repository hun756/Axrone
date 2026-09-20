namespace Axrone.Event;

/// <summary>
/// Ordered schema-migration chain: each registered upgrader steps the payload one version forward.
/// </summary>
/// <typeparam name="TEvent">Payload type.</typeparam>
public sealed class EventUpgradePipeline<TEvent>
{
    private readonly List<IEventUpgrader<TEvent>> _upgraders = new();

    /// <summary>Registers an upgrader; order defines the migration path.</summary>
    public void Register(IEventUpgrader<TEvent> upgrader)
    {
        ArgumentNullException.ThrowIfNull(upgrader);
        _upgraders.Add(upgrader);
    }

    /// <summary>Migrates the payload from the given version through every applicable upgrader.</summary>
    public TEvent Transform(in TEvent source, int version)
    {
        TEvent current = source;
        int currentVersion = version;

        for (int i = 0; i < _upgraders.Count; i++)
        {
            if (_upgraders[i].CanUpgrade(currentVersion))
            {
                current = _upgraders[i].Upgrade(in current, currentVersion);
                currentVersion++;
            }
        }

        return current;
    }
}
