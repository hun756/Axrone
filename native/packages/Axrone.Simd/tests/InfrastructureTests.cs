using System.Buffers;
using Axrone.Utility.Alignment;

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

    // ── Alignment ─────────────────────────────────────────────────────────

    [Fact]
    public void Alignment_ValidPowerOfTwo_StoresCorrectly()
    {
        var align = new Alignment(64);
        align.Value.Should().Be(64u);
    }

    [Fact]
    public void Alignment_CacheLine64_HasCorrectValue()
    {
        Alignment.CacheLine64.Value.Should().Be(64u);
    }

    [Fact]
    public void Alignment_CacheLine128_HasCorrectValue()
    {
        Alignment.CacheLine128.Value.Should().Be(128u);
    }

    [Fact]
    public void Alignment_Mask_ReturnsValueMinusOne()
    {
        var align = new Alignment(64);
        align.Mask.Should().Be((nuint)63);
    }

    [Fact]
    public void Alignment_Shift_ReturnsLog2()
    {
        var align = new Alignment(64);
        align.Shift.Should().Be(6);
    }

    [Fact]
    public void Alignment_NonPowerOfTwo_Throws()
    {
        var act = () => new Alignment(3);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Alignment_Zero_Throws()
    {
        var act = () => new Alignment(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Alignment_TryCreate_ValidValue_ReturnsTrue()
    {
        Alignment.TryCreate(128, out var align).Should().BeTrue();
        align.Value.Should().Be(128u);
    }

    [Fact]
    public void Alignment_TryCreate_InvalidValue_ReturnsFalse()
    {
        Alignment.TryCreate(7, out _).Should().BeFalse();
    }

    [Fact]
    public void Alignment_AlignUp_RoundsCorrectly()
    {
        var align = new Alignment(64);
        align.AlignUp((nuint)65).Should().Be((nuint)128);
        align.AlignUp((nuint)64).Should().Be((nuint)64);
    }

    [Fact]
    public void Alignment_AlignDown_RoundsCorrectly()
    {
        var align = new Alignment(64);
        align.AlignDown((nuint)65).Should().Be((nuint)64);
        align.AlignDown((nuint)127).Should().Be((nuint)64);
    }

    [Fact]
    public void Alignment_IsAligned_DetectsCorrectly()
    {
        var align = new Alignment(64);
        align.IsAligned((nuint)64).Should().BeTrue();
        align.IsAligned((nuint)65).Should().BeFalse();
    }

    [Fact]
    public void Alignment_Equality_SameValues_AreEqual()
    {
        var a = new Alignment(64);
        var b = new Alignment(64);
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
