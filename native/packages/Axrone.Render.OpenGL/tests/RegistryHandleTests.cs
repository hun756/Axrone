namespace Axrone.Render.OpenGL.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Render.Core;
using Axrone.Render.Core.Abstractions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;
using Axrone.Utility.Descriptors;

public class RegistryHandleTests
{
    private static GLContext CreateContext(out MockGLApi mock)
    {
        mock = new MockGLApi();
        return new GLContext(mock);
    }

    [Fact]
    public void Register_IssuesResolvableHandle()
    {
        using var context = CreateContext(out _);
        using var buffer = new GLBuffer(context, GLConst.ArrayBuffer, GLConst.StaticDraw, 64);

        DescriptorHandle<GLResourceNode> handle = buffer.RegistryHandle;
        handle.IsValid.Should().BeTrue();
        context.Registry.TryResolve(in handle, out IGLResource? resolved)
            .Should().BeTrue();
        resolved.Should().BeSameAs(buffer);
    }

    [Fact]
    public void StaleHandle_FailsClosedAfterSlotReuse()
    {
        using var context = CreateContext(out _);
        var first = new GLBuffer(context, GLConst.ArrayBuffer, GLConst.StaticDraw, 64);
        DescriptorHandle<GLResourceNode> stale = first.RegistryHandle;
        first.Dispose();

        context.Registry.TryResolve(in stale, out IGLResource? _).Should().BeFalse();

        using var second = new GLBuffer(context, GLConst.ArrayBuffer, GLConst.StaticDraw, 64);
        context.Registry.TryResolve(in stale, out IGLResource? _).Should().BeFalse(
            "recycled slot with bumped generation must not resurrect the old handle");
        DescriptorHandle<GLResourceNode> live = second.RegistryHandle;
        context.Registry.TryResolve(in live, out IGLResource? resolved)
            .Should().BeTrue();
        resolved.Should().BeSameAs(second);
    }

    [Fact]
    public void DoubleRegister_FailsFast()
    {
        using var context = CreateContext(out _);
        using var buffer = new GLBuffer(context, GLConst.ArrayBuffer, GLConst.StaticDraw, 64);

        Action register = () => context.Registry.Register(buffer);
        register.Should().Throw<Exception>();
        context.Registry.Count.Should().Be(1);
    }

    [Fact]
    public void Unregister_IsIdempotent()
    {
        using var context = CreateContext(out _);
        var buffer = new GLBuffer(context, GLConst.ArrayBuffer, GLConst.StaticDraw, 64);
        context.Registry.Count.Should().Be(1);

        context.Registry.Unregister(buffer);
        context.Registry.Unregister(buffer);
        context.Registry.Count.Should().Be(0);

        buffer.Dispose();
    }

    [Fact]
    public void Registry_ExhaustionThrows_BelowOrAtCapacity()
    {
        using var context = CreateContext(out _);
        var buffers = new List<GLBuffer>(1024);
        try
        {
            for (int i = 0; i < 1024; i++)
                buffers.Add(new GLBuffer(context, GLConst.ArrayBuffer, GLConst.StaticDraw, 8));

            context.Registry.Count.Should().Be(1024);

            // Registration happens inside the constructor, so the full table
            // surfaces as a construction failure (mock GL name leaks, harmless).
            var create = () => new GLBuffer(context, GLConst.ArrayBuffer, GLConst.StaticDraw, 8);
            create.Should().Throw<Exception>("table is full");
        }
        finally
        {
            foreach (var buffer in buffers)
                buffer.Dispose();
        }

        context.Registry.Count.Should().Be(0);
    }
}
