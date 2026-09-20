namespace Axrone.Event;

/// <summary>Schema migration for one payload version step.</summary>
/// <typeparam name="TEvent">Payload type.</typeparam>
public interface IEventUpgrader<TEvent>
{
    /// <summary>Whether this upgrader migrates from the given version.</summary>
    bool CanUpgrade(int version);

    /// <summary>Migrates the payload one version forward.</summary>
    TEvent Upgrade(in TEvent oldEvent, int oldVersion);
}
