using System.Buffers;

namespace Axrone.Simd.Tests;

public class InfrastructureTests
{
    // ── BatchCapacity ────────────────────────────────────────────────────

    [Fact]
    public void BatchCapacity_ValidValue_StoresCorrectly()
    {
        var cap = new BatchCapacity(1024);
        cap.Value.Should().Be(1024);
        cap.ToInt32().Should().Be(1024);
    }

    [Fact]
    public void BatchCapacity_ImplicitConversionToInt_ReturnsValue()
    {
        var cap = new BatchCapacity(512);
        int value = cap;
        value.Should().Be(512);
    }

    [Fact]
    public void BatchCapacity_ImplicitConversionToNuint_ReturnsValue()
    {
        var cap = new BatchCapacity(256);
        nuint value = cap;
        value.Should().Be((nuint)256);
    }

    [Fact]
    public void BatchCapacity_Zero_Throws()
    {
        var act = () => new BatchCapacity(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BatchCapacity_Negative_Throws()
    {
        var act = () => new BatchCapacity(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BatchCapacity_ExceedsMaximum_Throws()
    {
        var act = () => new BatchCapacity(BatchCapacity.MaximumCapacity + 1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BatchCapacity_AtMaximum_Succeeds()
    {
        var cap = new BatchCapacity(BatchCapacity.MaximumCapacity);
        cap.Value.Should().Be(BatchCapacity.MaximumCapacity);
    }

    [Fact]
    public void BatchCapacity_FromBatchCapacity_Factory_Works()
    {
        var cap = BatchCapacity.FromBatchCapacity(64);
        cap.Value.Should().Be(64);
    }

    [Fact]
    public void BatchCapacity_Equality_SameValues_AreEqual()
    {
        var a = new BatchCapacity(100);
        var b = new BatchCapacity(100);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void BatchCapacity_Equality_DifferentValues_AreNotEqual()
    {
        var a = new BatchCapacity(100);
        var b = new BatchCapacity(200);
        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    // ── MemoryAlignment ──────────────────────────────────────────────────

    [Fact]
    public void MemoryAlignment_ValidPowerOfTwo_StoresCorrectly()
    {
        var align = new MemoryAlignment(64);
        align.Value.Should().Be((nuint)64);
        align.ToUIntPtr().Should().Be((nuint)64);
    }

    [Fact]
    public void MemoryAlignment_CacheLine64_HasCorrectValue()
    {
        MemoryAlignment.CacheLine64.Value.Should().Be((nuint)64);
    }

    [Fact]
    public void MemoryAlignment_CacheLine128_HasCorrectValue()
    {
        MemoryAlignment.CacheLine128.Value.Should().Be((nuint)128);
    }

    [Fact]
    public void MemoryAlignment_ImplicitConversion_ReturnsValue()
    {
        var align = new MemoryAlignment(32);
        nuint value = align;
        value.Should().Be((nuint)32);
    }

    [Fact]
    public void MemoryAlignment_NonPowerOfTwo_Throws()
    {
        var act = () => new MemoryAlignment(3);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MemoryAlignment_Zero_Throws()
    {
        var act = () => new MemoryAlignment(0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MemoryAlignment_BelowIntPtrSize_Throws()
    {
        // On 64-bit, IntPtr.Size is 8, so alignment of 4 should fail
        if (IntPtr.Size > 4)
        {
            var act = () => new MemoryAlignment(4);
            act.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void MemoryAlignment_AtIntPtrSize_Succeeds()
    {
        var align = new MemoryAlignment((nuint)IntPtr.Size);
        align.Value.Should().Be((nuint)IntPtr.Size);
    }

    [Fact]
    public void MemoryAlignment_FromMemoryAlignment_Factory_Works()
    {
        var align = MemoryAlignment.FromMemoryAlignment(256);
        align.Value.Should().Be((nuint)256);
    }

    [Fact]
    public void MemoryAlignment_Equality_SameValues_AreEqual()
    {
        var a = new MemoryAlignment(64);
        var b = new MemoryAlignment(64);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    // ── ExtremaPair ──────────────────────────────────────────────────────

    [Fact]
    public void ExtremaPair_Constructor_SetsMinAndMax()
    {
        var pair = new ExtremaPair<int>(3, 10);
        pair.Min.Should().Be(3);
        pair.Max.Should().Be(10);
    }

    [Fact]
    public void ExtremaPair_Equality_SameValues_AreEqual()
    {
        var a = new ExtremaPair<int>(1, 100);
        var b = new ExtremaPair<int>(1, 100);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void ExtremaPair_Equality_DifferentValues_AreNotEqual()
    {
        var a = new ExtremaPair<int>(1, 100);
        var b = new ExtremaPair<int>(2, 100);
        a.Should().NotBe(b);
    }

    [Fact]
    public void ExtremaPair_Double_WorksCorrectly()
    {
        var pair = new ExtremaPair<double>(-1.5, 99.9);
        pair.Min.Should().BeApproximately(-1.5, 1e-9);
        pair.Max.Should().BeApproximately(99.9, 1e-9);
    }

    [Fact]
    public void ExtremaPair_Float_WorksCorrectly()
    {
        var pair = new ExtremaPair<float>(-2.0f, 50.0f);
        pair.Min.Should().Be(-2.0f);
        pair.Max.Should().Be(50.0f);
    }

    // ── ScanResult ───────────────────────────────────────────────────────

    [Fact]
    public void ScanResult_Success_HasDoneStatus()
    {
        var result = ScanResult.Success(42);
        result.Status.Should().Be(OperationStatus.Done);
        result.WrittenCount.Should().Be((nuint)42);
    }

    [Fact]
    public void ScanResult_Overflow_HasDestinationTooSmallStatus()
    {
        var result = ScanResult.Overflow(10);
        result.Status.Should().Be(OperationStatus.DestinationTooSmall);
        result.WrittenCount.Should().Be((nuint)10);
    }

    [Fact]
    public void ScanResult_Success_ZeroCount_IsValid()
    {
        var result = ScanResult.Success(0);
        result.Status.Should().Be(OperationStatus.Done);
        result.WrittenCount.Should().Be((nuint)0);
    }

    [Fact]
    public void ScanResult_Equality_SameValues_AreEqual()
    {
        var a = ScanResult.Success(5);
        var b = ScanResult.Success(5);
        a.Should().Be(b);
    }

    [Fact]
    public void ScanResult_Equality_DifferentStatus_AreNotEqual()
    {
        var success = ScanResult.Success(5);
        var overflow = ScanResult.Overflow(5);
        success.Should().NotBe(overflow);
    }

    [Fact]
    public void ScanResult_Equality_DifferentCount_AreNotEqual()
    {
        var a = ScanResult.Success(5);
        var b = ScanResult.Success(10);
        a.Should().NotBe(b);
    }
}
