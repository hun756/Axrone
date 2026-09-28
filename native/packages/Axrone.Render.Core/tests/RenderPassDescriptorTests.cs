namespace Axrone.Render.Core.Tests;

public sealed class RenderPassDescriptorTests
{
    [Fact]
    public void CreateDefault_SetsBackbufferTargetAndDefaults()
    {
        var vp = new ViewportRect(0, 0, 1920, 1080);
        var clearColor = new ClearColorValue(0.1f, 0.2f, 0.3f, 1f);

        var desc = RenderPassDescriptor.CreateDefault(vp, clearColor, clearDepth: 0.5f, clearStencil: 3);

        desc.FramebufferId.Should().Be(0);
        desc.Viewport.Should().Be(vp);
        desc.Scissor.Should().Be(new ScissorRect(0, 0, 1920, 1080));
        desc.ScissorTest.Should().BeFalse();
        desc.ColorAttachmentCount.Should().Be(1);
        desc.HasDepthStencil.Should().BeTrue();

        var colorAtt = desc.GetColorAttachment(0);
        colorAtt.Slot.Should().Be(0);
        colorAtt.LoadAction.Should().Be(AttachmentLoadAction.Clear);
        colorAtt.StoreAction.Should().Be(AttachmentStoreAction.Store);
        colorAtt.ClearColor.Should().Be(clearColor);

        var depthAtt = desc.DepthStencilAttachment;
        depthAtt.IsDepthStencil.Should().BeTrue();
        depthAtt.LoadAction.Should().Be(AttachmentLoadAction.Clear);
        depthAtt.ClearDepth.Should().Be(0.5f);
        depthAtt.ClearStencil.Should().Be(3);
    }

    [Fact]
    public void Create_WithMultipleColorAttachments_PreservesOrder()
    {
        var vp = new ViewportRect(0, 0, 800, 600);
        Span<AttachmentDescriptor> attachments = stackalloc AttachmentDescriptor[3]
        {
            AttachmentDescriptor.Color(0, AttachmentLoadAction.Clear, AttachmentStoreAction.Store),
            AttachmentDescriptor.Color(1, AttachmentLoadAction.Load, AttachmentStoreAction.Store),
            AttachmentDescriptor.Color(2, AttachmentLoadAction.DontCare, AttachmentStoreAction.Discard)
        };

        var desc = RenderPassDescriptor.Create(42, vp, attachments);

        desc.FramebufferId.Should().Be(42);
        desc.ColorAttachmentCount.Should().Be(3);
        desc.HasDepthStencil.Should().BeFalse();

        desc.GetColorAttachment(0).Slot.Should().Be(0);
        desc.GetColorAttachment(0).LoadAction.Should().Be(AttachmentLoadAction.Clear);

        desc.GetColorAttachment(1).Slot.Should().Be(1);
        desc.GetColorAttachment(1).LoadAction.Should().Be(AttachmentLoadAction.Load);

        desc.GetColorAttachment(2).Slot.Should().Be(2);
        desc.GetColorAttachment(2).LoadAction.Should().Be(AttachmentLoadAction.DontCare);
        desc.GetColorAttachment(2).StoreAction.Should().Be(AttachmentStoreAction.Discard);

        var span = desc.GetColorAttachments();
        span.Length.Should().Be(3);
        span[0].Slot.Should().Be(0);
        span[1].Slot.Should().Be(1);
        span[2].Slot.Should().Be(2);
    }

    [Fact]
    public void Create_ExceedingMaxColorAttachments_ThrowsArgumentOutOfRange()
    {
        var vp = new ViewportRect(0, 0, 800, 600);
        var atts = new AttachmentDescriptor[9]
        {
            AttachmentDescriptor.Color(0),
            AttachmentDescriptor.Color(1),
            AttachmentDescriptor.Color(2),
            AttachmentDescriptor.Color(3),
            AttachmentDescriptor.Color(4),
            AttachmentDescriptor.Color(5),
            AttachmentDescriptor.Color(6),
            AttachmentDescriptor.Color(7),
            AttachmentDescriptor.Color(0) // 9th
        };

        var action = () => RenderPassDescriptor.Create(1, vp, atts);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Equality_ComparesAllFieldsBitIdentically()
    {
        var vp1 = new ViewportRect(0, 0, 1920, 1080);
        var sc1 = new ScissorRect(10, 10, 500, 500);
        var att1 = AttachmentDescriptor.Color(0, AttachmentLoadAction.Clear, AttachmentStoreAction.Store, new ClearColorValue(1f, 0f, 0f, 1f));
        var depth = AttachmentDescriptor.DepthStencil(AttachmentLoadAction.Clear, AttachmentStoreAction.Store, 1f, 0);

        Span<AttachmentDescriptor> list1 = stackalloc AttachmentDescriptor[] { att1, depth };
        var desc1 = RenderPassDescriptor.Create(10, vp1, sc1, true, list1);

        Span<AttachmentDescriptor> list2 = stackalloc AttachmentDescriptor[] { att1, depth };
        var desc2 = RenderPassDescriptor.Create(10, vp1, sc1, true, list2);

        desc1.Should().Be(desc2);
        (desc1 == desc2).Should().BeTrue();
        (desc1 != desc2).Should().BeFalse();
        desc1.GetHashCode().Should().Be(desc2.GetHashCode());

        // Differing FBO
        var descDifferFbo = RenderPassDescriptor.Create(11, vp1, sc1, true, list1);
        (desc1 == descDifferFbo).Should().BeFalse();

        // Differing scissor test
        var descDifferScissorTest = RenderPassDescriptor.Create(10, vp1, sc1, false, list1);
        (desc1 == descDifferScissorTest).Should().BeFalse();
    }
}
