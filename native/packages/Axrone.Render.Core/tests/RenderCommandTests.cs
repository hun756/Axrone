namespace Axrone.Render.Core.Tests;

using System.Runtime.InteropServices;
using Xunit;
using FluentAssertions;

public class RenderCommandTests
{
    [Fact]
    public void Packet_Is64Bytes()
    {
        Marshal.SizeOf<RenderCommand>().Should().Be(64);
    }

    [Fact]
    public void CreateBlit_RoundTripsFields()
    {
        var source = new DescriptorHandle<GLResourceNode>(3, 7);
        var destination = new DescriptorHandle<GLResourceNode>(5, 11);

        RenderCommand command = RenderCommand.CreateBlit(
            in source, in destination,
            0, 0, 64, 64, 0, 0, 128, 128,
            0x00004000u, 0x2600u);

        command.Type.Should().Be(RenderCommandType.Blit);
        command.Source.Should().Be(source);
        command.Destination.Should().Be(destination);
        command.SourceX1.Should().Be(64);
        command.DestinationX1.Should().Be(128);
        command.Mask.Should().Be(0x00004000u);
        command.Filter.Should().Be(0x2600u);
    }

    [Fact]
    public void Equality_ComparesAllFields()
    {
        var source = new DescriptorHandle<GLResourceNode>(3, 7);
        var destination = new DescriptorHandle<GLResourceNode>(5, 11);

        RenderCommand a = RenderCommand.CreateBlit(in source, in destination, 0, 0, 1, 1, 0, 0, 1, 1, 1u, 2u);
        RenderCommand b = RenderCommand.CreateBlit(in source, in destination, 0, 0, 1, 1, 0, 0, 1, 1, 1u, 2u);
        RenderCommand c = RenderCommand.CreateBlit(in source, in destination, 0, 0, 2, 1, 0, 0, 1, 1, 1u, 2u);

        (a == b).Should().BeTrue();
        (a != c).Should().BeTrue();
    }
}
