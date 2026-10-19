using Axrone.Collections;
using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputQueueTests
{
    [Fact]
    public void Capacity_Rejects_NonPowerOfTwo()
    {
        var actZero = () => new BufferCapacity(0);
        var actThree = () => new BufferCapacity(3);

        actZero.Should().Throw<ArgumentOutOfRangeException>();
        actThree.Should().Throw<ArgumentOutOfRangeException>();
        new BufferCapacity(2).Value.Should().Be(2u);
        ((uint)new BufferCapacity(4096)).Should().Be(4096u);
    }

    [Fact]
    public void EnqueueDequeue_Preserves_FifoOrder()
    {
        using var queue = new LockFreeInputRingBuffer<SpinWaitBackoff, NullMetricsPolicy>(new BufferCapacity(8));
        var stamp = InputTimestamp.Now();

        for (int i = 0; i < 5; i++)
        {
            queue.TryEnqueue(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, Key.A, (ushort)i, stamp)).Should().BeTrue();
        }

        for (int i = 0; i < 5; i++)
        {
            queue.TryDequeue(out RawInputEvent evt).Should().BeTrue();
            evt.EventType.Should().Be(RawInputType.KeyDown);
            evt.IntValue0.Should().Be(i);
        }

        queue.TryDequeue(out _).Should().BeFalse();
    }

    [Fact]
    public void FullQueue_Drops_WithFalse()
    {
        using var queue = new LockFreeInputRingBuffer<SpinWaitBackoff, NullMetricsPolicy>(new BufferCapacity(2));
        var stamp = InputTimestamp.Now();

        queue.TryEnqueue(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, Key.A, 0, stamp)).Should().BeTrue();
        queue.TryEnqueue(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, Key.B, 0, stamp)).Should().BeTrue();
        queue.TryEnqueue(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, Key.C, 0, stamp)).Should().BeFalse();
    }

    [Fact]
    public void Drain_Empties_IntoSpan()
    {
        using var queue = new LockFreeInputRingBuffer<SpinWaitBackoff, NullMetricsPolicy>(new BufferCapacity(8));
        var stamp = InputTimestamp.Now();

        queue.TryEnqueue(RawInputEvent.CreateMouseMove(DeviceIndex.Primary, 10.0f, 20.0f, stamp));
        queue.TryEnqueue(RawInputEvent.CreateMouseMove(DeviceIndex.Primary, 30.0f, 40.0f, stamp));

        Span<RawInputEvent> buffer = stackalloc RawInputEvent[8];
        int count = queue.Drain(buffer);

        count.Should().Be(2);
        buffer[0].ValueX.Should().Be(10.0f);
        buffer[1].ValueX.Should().Be(30.0f);
        queue.TryDequeue(out _).Should().BeFalse();
    }

    [Fact]
    public void Complete_Rejects_FurtherWrites()
    {
        using var queue = new LockFreeInputRingBuffer<SpinWaitBackoff, NullMetricsPolicy>(new BufferCapacity(8));
        var stamp = InputTimestamp.Now();

        queue.TryEnqueue(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, Key.A, 0, stamp)).Should().BeTrue();
        queue.Complete();

        queue.TryEnqueue(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, Key.B, 0, stamp)).Should().BeFalse();
        queue.TryDequeue(out RawInputEvent evt).Should().BeTrue();
        evt.KeyCode.Should().Be((ushort)Key.A);
    }

    [Fact]
    public void EventFactories_RoundTrip_Payloads()
    {
        var stamp = InputTimestamp.Now();

        RawInputEvent key = RawInputEvent.CreateKeyDown(DeviceIndex.Primary, Key.Space, 57, stamp);
        key.EventType.Should().Be(RawInputType.KeyDown);
        key.KeyCode.Should().Be((ushort)Key.Space);
        key.IntValue0.Should().Be(57);

        RawInputEvent axis = RawInputEvent.CreateGamepadAxis(DeviceIndex.Primary, 2, 0.75f, stamp);
        axis.EventType.Should().Be(RawInputType.GamepadAxis);
        axis.ValueX.Should().Be(0.75f);

        RawInputEvent scroll = RawInputEvent.CreateMouseScroll(DeviceIndex.Primary, 0.0f, -3.0f, stamp);
        scroll.ValueY.Should().Be(-3.0f);

        (key == RawInputEvent.CreateKeyDown(DeviceIndex.Primary, Key.Space, 57, stamp)).Should().BeTrue();
    }
}
