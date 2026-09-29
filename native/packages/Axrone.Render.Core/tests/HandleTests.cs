namespace Axrone.Render.Core.Tests;

using Xunit;
using FluentAssertions;

public class HandleTests
{
    [Fact]
    public void NullHandles_AreInvalid()
    {
        GpuTextureHandle.Null.IsValid.Should().BeFalse();
        GpuBufferHandle.Null.IsValid.Should().BeFalse();
        GpuVertexArrayHandle.Null.IsValid.Should().BeFalse();
        GpuFramebufferHandle.Null.IsValid.Should().BeFalse();
        GpuProgramHandle.Null.IsValid.Should().BeFalse();
        GpuSamplerHandle.Null.IsValid.Should().BeFalse();
        GpuRenderbufferHandle.Null.IsValid.Should().BeFalse();
        GpuQueryHandle.Null.IsValid.Should().BeFalse();
        GpuSyncHandle.Null.IsValid.Should().BeFalse();
    }

    [Fact]
    public void NonZeroHandles_AreValid()
    {
        new GpuTextureHandle(7, 64, 64, 0).IsValid.Should().BeTrue();
        new GpuBufferHandle(3, 1024).IsValid.Should().BeTrue();
        new GpuSyncHandle(123).IsValid.Should().BeTrue();
    }

    [Fact]
    public void NativeHandle_DiscriminatesTextureFromFramebuffer()
    {
        NativeHandle texture = NativeHandle.FromTexture(new GpuTextureHandle(5, 16, 16, 0));
        texture.IsDefaultFramebuffer.Should().BeFalse();
        texture.TextureHandle.Id.Should().Be(5u);

        NativeHandle framebuffer = NativeHandle.FromDefaultFramebuffer();
        framebuffer.IsDefaultFramebuffer.Should().BeTrue();

        texture.Should().NotBe(framebuffer);
        texture.Should().Be(NativeHandle.FromTexture(new GpuTextureHandle(5, 16, 16, 0)));
    }
}
