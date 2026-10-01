namespace Axrone.Render.Core.Tests;

public sealed class AttachmentDescriptorTests
{
    [Fact]
    public void With_ReplacingOneMember_LeavesEveryOtherMemberEqual()
    {
        var color = AttachmentDescriptor.Color(2, AttachmentLoadAction.Clear, AttachmentStoreAction.Discard, new ClearColorValue(0.5f, 0.25f, 0.125f, 1f), resolveFramebuffer: 9);
        var retargeted = color with { ResolveFramebuffer = 10 };

        retargeted.Should().NotBe(color);
        (retargeted == color).Should().BeFalse();
        (retargeted != color).Should().BeTrue();

        retargeted.Slot.Should().Be(color.Slot);
        retargeted.LoadAction.Should().Be(color.LoadAction);
        retargeted.StoreAction.Should().Be(color.StoreAction);
        retargeted.ClearColor.Should().Be(color.ClearColor);
        retargeted.ClearDepth.Should().Be(color.ClearDepth);
        retargeted.ClearStencil.Should().Be(color.ClearStencil);
        retargeted.IsDepthStencil.Should().Be(color.IsDepthStencil);
        retargeted.ResolveFramebuffer.Should().Be(10);
    }

    [Fact]
    public void With_WithoutChanges_IsEqualToTheSource()
    {
        var depth = AttachmentDescriptor.DepthStencil(AttachmentLoadAction.Clear, AttachmentStoreAction.Store, 0.25f, 7);
        var copy = depth with { };

        copy.Should().Be(depth);
        (copy == depth).Should().BeTrue();
        copy.GetHashCode().Should().Be(depth.GetHashCode());
    }

    [Fact]
    public void Constructor_RejectsASlotBeyondTheMaximum()
    {
        var action = () => AttachmentDescriptor.Color((byte)(AttachmentDescriptor.MaxSlot + 1));
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_AcceptsBothEndsOfTheSlotRange()
    {
        var lowest = AttachmentDescriptor.Color(0);
        var highest = AttachmentDescriptor.Color(AttachmentDescriptor.MaxSlot);

        var first = () => AttachmentDescriptor.Validate(in lowest);
        var second = () => AttachmentDescriptor.Validate(in highest);

        first.Should().NotThrow();
        second.Should().NotThrow();
    }

    [Fact]
    public void Validate_ThrowsWhenTheSlotExceedsTheMaximum()
    {
        var forged = AttachmentDescriptor.Color(0) with { Slot = AttachmentDescriptor.MaxSlot + 1 };

        var action = () => AttachmentDescriptor.Validate(in forged);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Describe_IsNonEmptyAndNamesTheSlot()
    {
        var descriptor = AttachmentDescriptor.Color(3, AttachmentLoadAction.Clear, AttachmentStoreAction.Store);

        string text = AttachmentDescriptor.Describe(in descriptor);

        text.Should().NotBeNullOrEmpty();
        text.Should().Contain(nameof(AttachmentDescriptor.Slot));
        text.Should().Be(descriptor.ToString());
    }
}

public sealed class ViewportScissorDescriptorTests
{
    [Fact]
    public void ViewportRect_Validate_AcceptsTheDefaultAndPositiveExtents()
    {
        ViewportRect zero = default;
        ViewportRect positive = new(-4, -8, 1920, 1080);

        var first = () => ViewportRect.Validate(in zero);
        var second = () => ViewportRect.Validate(in positive);

        first.Should().NotThrow();
        second.Should().NotThrow();
    }

    [Fact]
    public void ViewportRect_Validate_ThrowsOnNegativeExtents()
    {
        var width = new ViewportRect(0, 0, -1, 1080);
        var height = new ViewportRect(0, 0, 1920, -1);

        var first = () => ViewportRect.Validate(in width);
        var second = () => ViewportRect.Validate(in height);

        first.Should().Throw<ArgumentOutOfRangeException>();
        second.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ScissorRect_Validate_AcceptsTheDefaultAndPositiveExtents()
    {
        ScissorRect zero = default;
        ScissorRect positive = new(10, 20, 500, 500);

        var first = () => ScissorRect.Validate(in zero);
        var second = () => ScissorRect.Validate(in positive);

        first.Should().NotThrow();
        second.Should().NotThrow();
    }

    [Fact]
    public void ScissorRect_Validate_ThrowsOnNegativeExtents()
    {
        var width = new ScissorRect(0, 0, -1, 500);
        var height = new ScissorRect(0, 0, 500, -1);

        var first = () => ScissorRect.Validate(in width);
        var second = () => ScissorRect.Validate(in height);

        first.Should().Throw<ArgumentOutOfRangeException>();
        second.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Describe_RendersTheSynthesizedRecordText()
    {
        ViewportRect viewport = new(0, 0, 1920, 1080);
        ScissorRect scissor = new(10, 10, 500, 500);

        ViewportRect.Describe(in viewport).Should().Be(viewport.ToString()).And.Contain("1920");
        ScissorRect.Describe(in scissor).Should().Be(scissor.ToString()).And.Contain("500");
    }
}

public sealed class FormatDescriptorContractTests
{
    [Fact]
    public void Validate_AcceptsEveryRegisteredFormat()
    {
        foreach (string name in GLFormatRegistry.SupportedFormats)
        {
            var descriptor = GLFormatRegistry.GetFormat(name);

            var action = () => FormatDescriptor.Validate(in descriptor);
            action.Should().NotThrow();
        }
    }

    [Fact]
    public void Validate_ThrowsWhenBytesPerPixelIsZero()
    {
        var template = GLFormatRegistry.GetFormat("rgba8");
        var forged = template with { BytesPerPixel = 0 };

        var action = () => FormatDescriptor.Validate(in forged);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Describe_IsNonEmptyAndNamesTheInternalFormat()
    {
        var descriptor = GLFormatRegistry.GetFormat("rgba8");

        string text = FormatDescriptor.Describe(in descriptor);

        text.Should().NotBeNullOrEmpty();
        text.Should().Contain(nameof(FormatDescriptor.InternalFormat));
        text.Should().Be(descriptor.ToString());
    }
}
