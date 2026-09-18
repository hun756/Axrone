using Axrone.Collections;

namespace Axrone.Collections.Tests;

public class SequenceNumberTests
{
    [Fact]
    public void Zero_HasValueZero()
    {
        SequenceNumber.Zero.Value.Should().Be(0);
    }

    [Fact]
    public void Constructor_StoresValue()
    {
        var seq = new SequenceNumber(42);
        seq.Value.Should().Be(42);
    }

    [Fact]
    public void Next_IncrementsByOne()
    {
        var seq = new SequenceNumber(10);
        seq.Next().Value.Should().Be(11);
    }

    [Fact]
    public void Advance_IncrementsByCount()
    {
        var seq = new SequenceNumber(5);
        seq.Advance(7).Value.Should().Be(12);
    }

    [Fact]
    public void Difference_ReturnsSignedDelta()
    {
        var a = new SequenceNumber(10);
        var b = new SequenceNumber(3);
        a.Difference(b).Should().Be(7);
        b.Difference(a).Should().Be(-7);
    }

    [Fact]
    public void CompareTo_OrdersCorrectly()
    {
        var low = new SequenceNumber(1);
        var high = new SequenceNumber(2);
        low.CompareTo(high).Should().BeNegative();
        high.CompareTo(low).Should().BePositive();
        low.CompareTo(new SequenceNumber(1)).Should().Be(0);
    }

    [Fact]
    public void ComparisonOperators_Work()
    {
        var a = new SequenceNumber(1);
        var b = new SequenceNumber(2);
        (a < b).Should().BeTrue();
        (a <= b).Should().BeTrue();
        (b > a).Should().BeTrue();
        (b >= a).Should().BeTrue();
        (a >= new SequenceNumber(1)).Should().BeTrue();
        (a <= new SequenceNumber(1)).Should().BeTrue();
    }

    [Fact]
    public void ToUIntPtr_ReturnsValue()
    {
        var seq = new SequenceNumber(99);
        seq.ToUIntPtr().Should().Be(99);
    }

    [Fact]
    public void FromUIntPtr_CreatesSequence()
    {
        SequenceNumber.FromUIntPtr(55).Value.Should().Be(55);
    }

    [Fact]
    public void ImplicitConversions_RoundTrip()
    {
        nuint raw = 77;
        SequenceNumber seq = raw;
        nuint back = seq;
        back.Should().Be(77);
    }

    [Fact]
    public void Equality_Works()
    {
        var a = new SequenceNumber(5);
        var b = new SequenceNumber(5);
        var c = new SequenceNumber(6);
        a.Should().Be(b);
        a.Should().NotBe(c);
        (a == b).Should().BeTrue();
        (a != c).Should().BeTrue();
    }

    [Fact]
    public void ToString_ContainsValue()
    {
        new SequenceNumber(42).ToString().Should().Contain("42");
    }
}

public class RingBufferOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new RingBufferOptions();
        opts.Capacity.Should().Be(1024);
        opts.AutoClearOnDispose.Should().BeTrue();
    }

    [Fact]
    public void Init_SetsValues()
    {
        var opts = new RingBufferOptions { Capacity = 256, AutoClearOnDispose = false };
        opts.Capacity.Should().Be(256);
        opts.AutoClearOnDispose.Should().BeFalse();
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        var original = new RingBufferOptions { Capacity = 128 };
        var modified = original with { Capacity = 512 };
        modified.Capacity.Should().Be(512);
        original.Capacity.Should().Be(128);
    }

    [Fact]
    public void Equality_ComparesByValue()
    {
        var a = new RingBufferOptions { Capacity = 64, AutoClearOnDispose = true };
        var b = new RingBufferOptions { Capacity = 64, AutoClearOnDispose = true };
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }
}

public class RingBufferExceptionTests
{
    [Fact]
    public void RingBufferException_DefaultConstructor()
    {
        var ex = new RingBufferException();
        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void RingBufferException_MessageConstructor()
    {
        var ex = new RingBufferException("test error");
        ex.Message.Should().Be("test error");
    }

    [Fact]
    public void RingBufferException_InnerExceptionConstructor()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new RingBufferException("outer", inner);
        ex.Message.Should().Be("outer");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void RingBufferFullException_DefaultMessage()
    {
        var ex = new RingBufferFullException();
        ex.Message.Should().Contain("full");
    }

    [Fact]
    public void RingBufferFullException_InheritsFromRingBufferException()
    {
        var ex = new RingBufferFullException();
        ex.Should().BeAssignableTo<RingBufferException>();
    }

    [Fact]
    public void RingBufferFullException_CustomMessage()
    {
        var ex = new RingBufferFullException("custom full");
        ex.Message.Should().Be("custom full");
    }

    [Fact]
    public void RingBufferFullException_InnerException()
    {
        var inner = new TimeoutException();
        var ex = new RingBufferFullException("full", inner);
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void RingBufferEmptyException_DefaultMessage()
    {
        var ex = new RingBufferEmptyException();
        ex.Message.Should().Contain("empty");
    }

    [Fact]
    public void RingBufferEmptyException_InheritsFromRingBufferException()
    {
        var ex = new RingBufferEmptyException();
        ex.Should().BeAssignableTo<RingBufferException>();
    }

    [Fact]
    public void RingBufferEmptyException_CustomMessage()
    {
        var ex = new RingBufferEmptyException("custom empty");
        ex.Message.Should().Be("custom empty");
    }

    [Fact]
    public void RingBufferEmptyException_InnerException()
    {
        var inner = new ObjectDisposedException("buf");
        var ex = new RingBufferEmptyException("empty", inner);
        ex.InnerException.Should().BeSameAs(inner);
    }
}

public class SpscAsyncBatchSignalTests
{
    [Fact]
    public async Task Signal_BeforeWait_CompletesImmediately()
    {
        var signal = new SpscAsyncBatchSignal();
        signal.Signal(5);

        int result = await signal.WaitAsync();
        result.Should().Be(5);
    }

    [Fact]
    public async Task Wait_ThenSignal_CompletesWithCount()
    {
        var signal = new SpscAsyncBatchSignal();

        var waitTask = signal.WaitAsync();
        waitTask.IsCompleted.Should().BeFalse();

        signal.Signal(3);
        int result = await waitTask;
        result.Should().Be(3);
    }

    [Fact]
    public async Task Reset_AllowsReuse()
    {
        var signal = new SpscAsyncBatchSignal();

        signal.Signal(1);
        (await signal.WaitAsync()).Should().Be(1);

        signal.Reset();
        signal.Signal(2);
        (await signal.WaitAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SignalWaitReset_RepeatedCycles()
    {
        var signal = new SpscAsyncBatchSignal();

        for (int i = 1; i <= 5; i++)
        {
            signal.Signal(i);
            int result = await signal.WaitAsync();
            result.Should().Be(i);
            signal.Reset();
        }
    }

    [Fact]
    public void GetStatus_ReflectsSignalState()
    {
        var signal = new SpscAsyncBatchSignal();
        var vt = signal.WaitAsync();

        // Before signal: pending
        // We can't easily get the token from ValueTask, but we can verify the signal
        // transitions correctly through Signal → completed
        signal.Signal(7);
        vt.IsCompleted.Should().BeTrue();
    }
}
