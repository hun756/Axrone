namespace Axrone.Render.OpenGL.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Render.Core;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Pipeline;
using Axrone.Render.OpenGL.Texture;

public class FormatParityTests
{
    [Fact]
    public void AllocatorKeys_ResolveOnBothTables_WithMatchingInternalFormat()
    {
        // Only InternalFormat is compared: the immutable-storage path
        // (TexStorage2D/3D) consumes InternalFormat alone; Format/Type feed
        // pixel-transfer ops whose descriptors legitimately differ per layer.
        foreach (string key in GLFormatRegistry.SupportedFormats)
        {
            TextureFormats.TryParseName(key, out TextureFormat format)
                .Should().BeTrue($"key '{key}' must resolve on the creation-path table");
            var desc = GLFormatRegistry.GetFormat(key);
            TextureFormats.Get(format).InternalFormat
                .Should().Be(desc.InternalFormat, $"key '{key}' wire format must agree");
        }
    }

    [Fact]
    public void Allocator_CreateTexture_UsesEnumTable()
    {
        using var context = new GLContext(new MockGLApi());
        using var allocator = new RenderResourceAllocator(context);

        NativeHandle handle = allocator.CreateTexture("color", "rgba8", 64, 64);
        handle.IsDefaultFramebuffer.Should().BeFalse();
        handle.TextureHandle.IsValid.Should().BeTrue();
        handle.TextureHandle.Format.Should().Be(0x8058u);
    }

    [Fact]
    public void Allocator_CreateTexture_UnknownKeyThrows()
    {
        using var context = new GLContext(new MockGLApi());
        using var allocator = new RenderResourceAllocator(context);

        Action create = () => allocator.CreateTexture("color", "nope", 64, 64);
        create.Should().Throw<Exception>();
    }

    [Fact]
    public void Allocator_PresentName_RoutesToDefaultFramebuffer()
    {
        using var context = new GLContext(new MockGLApi());
        using var allocator = new RenderResourceAllocator(context);

        NativeHandle handle = allocator.CreateTexture("present-color", "rgba8", 64, 64);
        handle.IsDefaultFramebuffer.Should().BeTrue();
    }
}
