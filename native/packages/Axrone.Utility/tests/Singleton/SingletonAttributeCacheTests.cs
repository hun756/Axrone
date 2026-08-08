using Axrone.Utility.Singleton;
using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Tests.Singleton;

public class SingletonAttributeCacheTests
{
    [Fact]
    public void Attribute_ReturnsNull_WhenNoAttributeApplied()
    {
        SingletonAttributeCache<SimpleService>.Attribute.Should().BeNull();
    }

    [Fact]
    public void Attribute_ReturnsAttribute_WhenApplied()
    {
        SingletonAttributeCache<MonitoredService>.Attribute.Should().NotBeNull();
    }

    [Fact]
    public void Lifetime_DefaultsToSingleton()
    {
        SingletonAttributeCache<SimpleService>.Lifetime.Should().Be(SingletonLifetime.Singleton);
    }

    [Fact]
    public void Lifetime_ReadsFromAttribute()
    {
        SingletonAttributeCache<MonitoredService>.Lifetime.Should().Be(SingletonLifetime.Singleton);
    }

    [Fact]
    public void ThreadSafety_DefaultsToExecutionAndPublication()
    {
        SingletonAttributeCache<SimpleService>.ThreadSafety.Should().Be(SingletonThreadSafety.ExecutionAndPublication);
    }

    [Fact]
    public void ThreadSafety_ReadsThreadStaticFromAttribute()
    {
        SingletonAttributeCache<ThreadStaticService>.ThreadSafety.Should().Be(SingletonThreadSafety.ThreadStatic);
    }

    [Fact]
    public void ThreadSafety_ReadsWeakFromAttribute()
    {
        SingletonAttributeCache<WeakStrategyService>.ThreadSafety.Should().Be(SingletonThreadSafety.Weak);
    }

    [Fact]
    public void Pinned_DefaultsFalse()
    {
        SingletonAttributeCache<SimpleService>.Pinned.Should().BeFalse();
    }

    [Fact]
    public void Monitored_ReadsFromAttribute()
    {
        SingletonAttributeCache<MonitoredService>.Monitored.Should().BeTrue();
    }

    [Fact]
    public void Monitored_DefaultsFalse()
    {
        SingletonAttributeCache<SimpleService>.Monitored.Should().BeFalse();
    }

    [Fact]
    public void CachedValues_AreConsistentAcrossMultipleReads()
    {
        var lifetime1 = SingletonAttributeCache<MonitoredService>.Lifetime;
        var lifetime2 = SingletonAttributeCache<MonitoredService>.Lifetime;
        var monitored1 = SingletonAttributeCache<MonitoredService>.Monitored;
        var monitored2 = SingletonAttributeCache<MonitoredService>.Monitored;

        lifetime1.Should().Be(lifetime2);
        monitored1.Should().Be(monitored2);
    }

    [Fact]
    public void IsEager_TrueForSingletonWithLifecycle()
    {
        SingletonAttributeCache<EagerLifecycleService>.IsEager.Should().BeTrue();
    }

    [Fact]
    public void IsEager_FalseForTypeWithoutAttribute()
    {
        SingletonAttributeCache<SimpleService>.IsEager.Should().BeFalse();
    }

    [Fact]
    public void IsEager_FalseForTypeWithoutLifecycle()
    {
        SingletonAttributeCache<PreRegisterService>.IsEager.Should().BeFalse();
    }
}
