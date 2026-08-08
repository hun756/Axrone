using Axrone.Utility.Singleton;

namespace Axrone.Utility.Tests.Singleton;

public class SingletonBaseTests
{
    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        var a = CrtpService.Instance;
        var b = CrtpService.Instance;
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void Instance_CreatedOnlyOnce()
    {
        CrtpService.ResetCounter();
        _ = CrtpService.Instance;
        _ = CrtpService.Instance;
        _ = CrtpService.Instance;
        CrtpService.Instance.CreateCount.Should().Be(1);
    }

    [Fact]
    public void OnCreated_CalledOnInit()
    {
        var instance = CrtpWithOnCreated.Instance;
        instance.OnCreatedCalled.Should().BeTrue();
    }

    [Fact]
    public void WarmUp_ForcesInit()
    {
        CrtpService.WarmUp();
        CrtpService.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void DirectConstruction_Throws()
    {
        var act = () => new CrtpService();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be accessed through the Instance property*");
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var instance = CrtpWithOnCreated.Instance;
        instance.Dispose();
        instance.Dispose();
    }

    [Fact]
    public void IsInitialized_TrueAfterAccess()
    {
        _ = CrtpWithOnCreated.Instance;
        CrtpWithOnCreated.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void Pinned_Instance_CreatedSuccessfully()
    {
        var instance = PinnedCrtpService.Instance;
        instance.Should().NotBeNull();
        PinnedCrtpService.IsInitialized.Should().BeTrue();
    }
}
