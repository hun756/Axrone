using Axrone.Utility.MessageCatalog;

namespace Axrone.Utility.Tests.MessageCatalog;

public class MessageCatalogTests
{
    [Fact]
    public void Resolve_KnownCode_ReturnsMessage()
    {
        var catalog = MessageCatalogFactory.Create(
            ("err.not-found", "Item not found"),
            ("err.timeout", "Operation timed out"));

        catalog.Resolve("err.not-found").Should().Be("Item not found");
    }

    [Fact]
    public void Resolve_UnknownCode_ReturnsFallback()
    {
        var catalog = MessageCatalogFactory.Create(
            ("err.not-found", "Item not found"));

        catalog.Resolve("unknown").Should().Contain("Unknown message code");
    }

    [Fact]
    public void TryResolve_KnownCode_ReturnsTrue()
    {
        var catalog = MessageCatalogFactory.Create(
            ("err.not-found", "Item not found"));

        catalog.TryResolve("err.not-found", out var msg).Should().BeTrue();
        msg.Should().Be("Item not found");
    }

    [Fact]
    public void TryResolve_UnknownCode_ReturnsFalse()
    {
        var catalog = MessageCatalogFactory.Create(
            ("err.not-found", "Item not found"));

        catalog.TryResolve("unknown", out _).Should().BeFalse();
    }

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        var catalog = MessageCatalogFactory.Create(
            ("a", "msg a"),
            ("b", "msg b"),
            ("c", "msg c"));

        catalog.Count.Should().Be(3);
    }

    [Fact]
    public void Contains_ExistingCode_ReturnsTrue()
    {
        var catalog = MessageCatalogFactory.Create(
            ("err.not-found", "Item not found"));

        catalog.Contains("err.not-found").Should().BeTrue();
    }
}
