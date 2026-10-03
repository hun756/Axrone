namespace Axrone.Numeric.Tests;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Xunit;
using FluentAssertions;
using Axrone.Numeric;

public class Vec3CoreTests
{
    [Fact]
    public void Layout_IsTwelveBytesWithPackedFields()
    {
        Marshal.SizeOf<Vec3>().Should().Be(12);
        Marshal.OffsetOf<Vec3>(nameof(Vec3.X)).Should().Be(0);
        Marshal.OffsetOf<Vec3>(nameof(Vec3.Y)).Should().Be(4);
        Marshal.OffsetOf<Vec3>(nameof(Vec3.Z)).Should().Be(8);
    }

    [Fact]
    public void MachineEpsilonAndDefaultTolerance_AreStable()
    {
        Vec3.MachineEpsilon.Should().Be(MathF.Pow(2F, -23F));
        Vec3.DefaultTolerance.Should().Be(Vec3.MachineEpsilon * 8F);
        Vec3.DefaultTolerance.Should().BeApproximately(9.5367432E-07F, 1E-12F);
    }

    [Fact]
    public void Constants_HaveExpectedComponents()
    {
        AssertComponents(Vec3.Zero, 0F, 0F, 0F);
        AssertComponents(Vec3.One, 1F, 1F, 1F);
        AssertComponents(Vec3.UnitX, 1F, 0F, 0F);
        AssertComponents(Vec3.UnitY, 0F, 1F, 0F);
        AssertComponents(Vec3.UnitZ, 0F, 0F, 1F);
        AssertComponents(Vec3.NegativeOne, -1F, -1F, -1F);
        AssertComponents(Vec3.NegativeUnitX, -1F, 0F, 0F);
        AssertComponents(Vec3.NegativeUnitY, 0F, -1F, 0F);
        AssertComponents(Vec3.NegativeUnitZ, 0F, 0F, -1F);
        AssertComponents(Vec3.Epsilon, float.Epsilon, float.Epsilon, float.Epsilon);
        AssertComponents(Vec3.Pi, MathF.PI, MathF.PI, MathF.PI);
        AssertComponents(Vec3.Tau, MathF.Tau, MathF.Tau, MathF.Tau);
        AssertComponents(Vec3.E, MathF.E, MathF.E, MathF.E);
    }

    [Fact]
    public void NegativeZero_HasTheSignBitSet()
    {
        foreach (float component in new[] { Vec3.NegativeZero.X, Vec3.NegativeZero.Y, Vec3.NegativeZero.Z })
        {
            BitConverter.SingleToUInt32Bits(component).Should().Be(0x80000000U);
        }
    }

    [Fact]
    public void InfinityAndNaN_Constants_AreClassifiedCorrectly()
    {
        Vec3.PositiveInfinity.IsAllFinite.Should().BeFalse();
        Vec3.PositiveInfinity.IsAnyInfinity.Should().BeTrue();
        Vec3.PositiveInfinity.IsAnyNaN.Should().BeFalse();
        Vec3.NegativeInfinity.IsAnyInfinity.Should().BeTrue();
        Vec3.NaN.IsAnyNaN.Should().BeTrue();
        Vec3.NaN.IsAllFinite.Should().BeFalse();
    }

    [Fact]
    public void AllBitsSet_IsNegativeQuietNaN()
    {
        BitConverter.SingleToUInt32Bits(Vec3.AllBitsSet.X).Should().Be(uint.MaxValue);
        Vec3.AllBitsSet.IsAnyNaN.Should().BeTrue();
    }

    [Fact]
    public void Constructors_AssignComponents()
    {
        AssertComponents(new Vec3(2F), 2F, 2F, 2F);
        AssertComponents(new Vec3(1F, 2F, 3F), 1F, 2F, 3F);
        AssertComponents(new Vec3((ReadOnlySpan<float>)[4F, 5F, 6F, 7F]), 4F, 5F, 6F);
    }

    [Fact]
    public void SpanConstructor_ThrowsWhenTooShort()
    {
        var tooShort = () => new Vec3((ReadOnlySpan<float>)[1F, 2F]);
        tooShort.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_RoundTripsComponents()
    {
        var value = new Vec3(1F, 2F, 3F);

        value[0].Should().Be(1F);
        value[1].Should().Be(2F);
        value[2].Should().Be(3F);

        value[1] = -2F;
        AssertComponents(value, 1F, -2F, 3F);
    }

    [Fact]
    public void Indexer_ThrowsOutsideComponentRange()
    {
        var value = new Vec3(1F, 2F, 3F);

        var negativeRead = () => _ = value[-1];
        var highRead = () => _ = value[3];
        var negativeWrite = () => value[-1] = 0F;
        var highWrite = () => value[3] = 0F;

        negativeRead.Should().Throw<ArgumentOutOfRangeException>();
        highRead.Should().Throw<ArgumentOutOfRangeException>();
        negativeWrite.Should().Throw<ArgumentOutOfRangeException>();
        highWrite.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Classification_ReportsZeroFiniteNaNAndInfinity()
    {
        Vec3.Zero.IsAllZero.Should().BeTrue();
        new Vec3(0F, 1F, 0F).IsAllZero.Should().BeFalse();
        Vec3.NegativeZero.IsAllZero.Should().BeTrue();

        new Vec3(1F, 2F, 3F).IsAllFinite.Should().BeTrue();
        new Vec3(1F, float.NaN, 3F).IsAllFinite.Should().BeFalse();
        new Vec3(1F, 2F, float.PositiveInfinity).IsAnyInfinity.Should().BeTrue();
        new Vec3(float.NaN, 0F, 0F).IsAnyNaN.Should().BeTrue();
        new Vec3(1F, 2F, 3F).IsAnyNaN.Should().BeFalse();
        new Vec3(1F, 2F, 3F).IsAnyInfinity.Should().BeFalse();
    }

    [Fact]
    public void Deconstruct_ProjectsComponents()
    {
        var (x, y, z) = new Vec3(1F, 2F, 3F);

        x.Should().Be(1F);
        y.Should().Be(2F);
        z.Should().Be(3F);
    }

    [Fact]
    public void SpanBridge_RoundTripsComponents()
    {
        var value = new Vec3(1F, 2F, 3F);

        Span<float> writable = value.AsSpan();
        writable.Length.Should().Be(3);
        writable[2] = 30F;
        AssertComponents(value, 1F, 2F, 30F);

        ReadOnlySpan<float> readable = value.AsReadOnlySpan();
        readable.ToArray().Should().Equal(1F, 2F, 30F);
    }

    [Fact]
    public void CopyTo_FloatSpan_RoundTrips()
    {
        var value = new Vec3(1F, 2F, 3F);
        Span<float> destination = stackalloc float[4];

        value.CopyTo(destination);
        destination.ToArray().Should().Equal(1F, 2F, 3F, 0F);
    }

    [Fact]
    public void CopyTo_ByteSpan_WritesPackedLayout()
    {
        var value = new Vec3(1F, 2F, 3F);
        Span<byte> destination = stackalloc byte[12];

        value.CopyTo(destination);
        BitConverter.ToSingle(destination[..4]).Should().Be(1F);
        BitConverter.ToSingle(destination[4..8]).Should().Be(2F);
        BitConverter.ToSingle(destination[8..]).Should().Be(3F);
    }

    [Fact]
    public void CopyTo_VectorSpanAndArray_RoundTrip()
    {
        var value = new Vec3(1F, 2F, 3F);
        Span<Vec3> spanDestination = stackalloc Vec3[1];
        Vec3[] arrayDestination = new Vec3[2];

        value.CopyTo(spanDestination);
        value.CopyTo(arrayDestination, 1);

        AssertComponents(spanDestination[0], 1F, 2F, 3F);
        AssertComponents(arrayDestination[1], 1F, 2F, 3F);
        AssertComponents(arrayDestination[0], 0F, 0F, 0F);
    }

    [Fact]
    public void CopyTo_ThrowsWhenDestinationIsTooSmall()
    {
        var value = new Vec3(1F, 2F, 3F);

        var floatSpan = () => value.CopyTo(new float[2].AsSpan());
        var byteSpan = () => value.CopyTo(new byte[11].AsSpan());
        var vectorSpan = () => value.CopyTo(Span<Vec3>.Empty);
        var arrayIndex = () => value.CopyTo(new Vec3[1], 1);
        var nullArray = () => value.CopyTo(null!, 0);

        floatSpan.Should().Throw<ArgumentException>();
        byteSpan.Should().Throw<ArgumentException>();
        vectorSpan.Should().Throw<ArgumentException>();
        arrayIndex.Should().Throw<ArgumentOutOfRangeException>();
        nullArray.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryCopyTo_ReportsSuccessAndFailure()
    {
        var value = new Vec3(1F, 2F, 3F);
        Span<float> destination = stackalloc float[3];

        value.TryCopyTo(destination).Should().BeTrue();
        destination.ToArray().Should().Equal(1F, 2F, 3F);

        Span<float> tooSmall = stackalloc float[2];
        tooSmall[0] = 9F;
        value.TryCopyTo(tooSmall).Should().BeFalse();
        tooSmall[0].Should().Be(9F);
    }

    [Fact]
    public void Vector128Bridge_RoundTrips()
    {
        var value = new Vec3(1F, 2F, 3F);
        Vector128<float> wide = value.AsVector128();

        wide.GetElement(0).Should().Be(1F);
        wide.GetElement(1).Should().Be(2F);
        wide.GetElement(2).Should().Be(3F);
        wide.GetElement(3).Should().Be(0F);

        var roundTripped = (Vec3)wide;
        AssertComponents(roundTripped, 1F, 2F, 3F);

        var explicitCast = (Vector128<float>)value;
        AssertComponents((Vec3)explicitCast, 1F, 2F, 3F);
    }

    [Fact]
    public void TupleBridge_RoundTrips()
    {
        Vec3 fromTuple = (1F, 2F, 3F);
        AssertComponents(fromTuple, 1F, 2F, 3F);

        var value = new Vec3(4F, 5F, 6F);
        (float x, float y, float z) = value;

        x.Should().Be(4F);
        y.Should().Be(5F);
        z.Should().Be(6F);
    }

    [Fact]
    public void SystemNumericsBridge_BitCastsInBothDirections()
    {
        var value = new Vec3(1F, 2F, 3F);

        System.Numerics.Vector3 asSystemNumerics = (System.Numerics.Vector3)value;
        System.Numerics.Vector3 viaHelper = value.ToSystemNumerics();

        asSystemNumerics.X.Should().Be(1F);
        asSystemNumerics.Y.Should().Be(2F);
        asSystemNumerics.Z.Should().Be(3F);
        viaHelper.Should().Be(asSystemNumerics);

        var backAgain = (Vec3)asSystemNumerics;
        AssertComponents(backAgain, 1F, 2F, 3F);
        AssertComponents(Vec3.FromSystemNumerics(asSystemNumerics), 1F, 2F, 3F);
    }

    [Fact]
    public void Create_BuildsComponentsAndScalarBroadcast()
    {
        AssertComponents(Vec3.Create(1F, 2F, 3F), 1F, 2F, 3F);
        AssertComponents(Vec3.CreateScalar(5F), 5F, 5F, 5F);
    }

    [Fact]
    public void CreateScalarUnsafe_MatchesScalarBroadcast()
    {
        AssertComponents(Vec3.CreateScalarUnsafe(5F), 5F, 5F, 5F);
        AssertComponents(Vec3.CreateScalarUnsafe(float.NaN), float.NaN, float.NaN, float.NaN);
    }

    [Fact]
    public void Load_ReadsComponentsAndThrowsWhenTooShort()
    {
        AssertComponents(Vec3.Load((ReadOnlySpan<float>)[1F, 2F, 3F, 4F]), 1F, 2F, 3F);

        var tooShort = () => Vec3.Load((ReadOnlySpan<float>)[1F, 2F]);
        tooShort.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public unsafe void LoadUnsafe_ReadsComponentsWithAndWithoutOffset()
    {
        float* source = stackalloc float[5];
        source[0] = 1F;
        source[1] = 2F;
        source[2] = 3F;
        source[3] = 4F;
        source[4] = 5F;

        AssertComponents(Vec3.LoadUnsafe(source), 1F, 2F, 3F);
        AssertComponents(Vec3.LoadUnsafe(source, 2), 3F, 4F, 5F);

        var negativeOffset = () => Vec3.LoadUnsafe(source, -1);
        negativeOffset.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public unsafe void LoadAligned_AndNonTemporal_ReadComponents()
    {
        void* block = NativeMemory.AlignedAlloc(64, 16);
        try
        {
            float* source = (float*)block;
            source[0] = 1F;
            source[1] = 2F;
            source[2] = 3F;

            AssertComponents(Vec3.LoadAligned(ref source[0]), 1F, 2F, 3F);
            AssertComponents(Vec3.LoadAlignedNonTemporal(ref source[0]), 1F, 2F, 3F);
        }
        finally
        {
            NativeMemory.AlignedFree(block);
        }
    }

    [Fact]
    public void ArithmeticOperators_OperateComponentWise()
    {
        var left = new Vec3(1F, 2F, 3F);
        var right = new Vec3(4F, 5F, 6F);

        AssertComponents(left + right, 5F, 7F, 9F);
        AssertComponents(right - left, 3F, 3F, 3F);
        AssertComponents(left * right, 4F, 10F, 18F);
        AssertComponents(right / left, 4F, 2.5F, 2F);
        AssertComponents(left * 2F, 2F, 4F, 6F);
        AssertComponents(2F * left, 2F, 4F, 6F);
        AssertComponents(left / 2F, 0.5F, 1F, 1.5F);
        AssertComponents(-left, -1F, -2F, -3F);
        AssertComponents(+left, 1F, 2F, 3F);
        AssertComponents(Vec3.AdditiveIdentity, 0F, 0F, 0F);
        AssertComponents(Vec3.MultiplicativeIdentity, 1F, 1F, 1F);
    }

    [Fact]
    public void BitwiseOperators_ActOnIeeePayloads()
    {
        var mask = new Vec3(BitConverter.Int32BitsToSingle(0x7FFFFFFF));
        var negative = new Vec3(-1F, -2F, -3F);

        AssertComponents(negative & mask, MathF.Abs(-1F), MathF.Abs(-2F), MathF.Abs(-3F));
        (negative | Vec3.Zero).Should().Be(negative);
        (negative ^ negative).Should().Be(Vec3.Zero);

        AssertBits(~Vec3.Zero, uint.MaxValue, uint.MaxValue, uint.MaxValue);
        AssertBits(Vec3.AllBitsSet, uint.MaxValue, uint.MaxValue, uint.MaxValue);
    }

    private static void AssertBits(Vec3 value, uint x, uint y, uint z)
    {
        BitConverter.SingleToUInt32Bits(value.X).Should().Be(x);
        BitConverter.SingleToUInt32Bits(value.Y).Should().Be(y);
        BitConverter.SingleToUInt32Bits(value.Z).Should().Be(z);
    }

    [Fact]
    public void Equality_ComparesComponentsExactly()
    {
        var value = new Vec3(1F, 2F, 3F);
        Vec3 notANumber = Vec3.NaN;
        Vec3 otherNotANumber = Vec3.CreateScalar(float.NaN);

        value.Equals(new Vec3(1F, 2F, 3F)).Should().BeTrue();
        (value == new Vec3(1F, 2F, 3F)).Should().BeTrue();
        (value != new Vec3(1F, 2F, 4F)).Should().BeTrue();
        value.Equals((object)new Vec3(1F, 2F, 3F)).Should().BeTrue();
        value.Equals((object)"not a vector").Should().BeFalse();
        value.GetHashCode().Should().Be(new Vec3(1F, 2F, 3F).GetHashCode());
        (notANumber == otherNotANumber).Should().BeFalse();
        notANumber.Equals(otherNotANumber).Should().BeFalse();
    }

    [Fact]
    public void NormalizationStrategies_AgreeWithinDefaultTolerance()
    {
        var value = new Vec3(3F, -4F, 12F);
        Vec3 strict = Vec3.Normalize<StrictIeeeStrategy>(value);
        Vec3 fast = Vec3.Normalize<FastApproximationStrategy>(value);

        strict.X.Should().BeApproximately(3F / 13F, 1E-6F);
        strict.Y.Should().BeApproximately(-4F / 13F, 1E-6F);
        strict.Z.Should().BeApproximately(12F / 13F, 1E-6F);

        fast.X.Should().BeApproximately(strict.X, Vec3.DefaultTolerance);
        fast.Y.Should().BeApproximately(strict.Y, Vec3.DefaultTolerance);
        fast.Z.Should().BeApproximately(strict.Z, Vec3.DefaultTolerance);

        Length(fast).Should().BeApproximately(1F, 1E-6F);
    }

    [Fact]
    public void StrictNormalization_SurvivesSubnormalSquaredLength()
    {
        Vec3 normalized = Vec3.Normalize<StrictIeeeStrategy>(new Vec3(1e-20F, 0F, 0F));

        normalized.X.Should().BeApproximately(1F, 1E-5F);
        normalized.Y.Should().Be(0F);
        normalized.Z.Should().Be(0F);
    }

    [Fact]
    public void FormatAndParse_RoundTripThroughTextAndUtf8()
    {
        var value = new Vec3(1.5F, -2.25F, 3F);
        CultureInfo culture = CultureInfo.InvariantCulture;

        string text = value.ToString("R", culture);
        text.Should().Be("1.5,-2.25,3");

        Vec3 parsed = Vec3.Parse(text, culture);
        AssertComponents(parsed, 1.5F, -2.25F, 3F);

        var utf8 = new byte[64];
        value.TryFormat(utf8, out int bytesWritten, "R", culture).Should().BeTrue();
        AssertComponents(Vec3.Parse(utf8.AsSpan(0, bytesWritten), culture), 1.5F, -2.25F, 3F);
    }

    [Fact]
    public void TryParse_AcceptsBracketedTextAndRejectsMalformedText()
    {
        CultureInfo culture = CultureInfo.InvariantCulture;

        Vec3.TryParse("[1, 2, 3]", culture, out Vec3 bracketed).Should().BeTrue();
        AssertComponents(bracketed, 1F, 2F, 3F);

        Vec3.TryParse("(4, 5, 6)", culture, out Vec3 parenthesized).Should().BeTrue();
        AssertComponents(parenthesized, 4F, 5F, 6F);

        Vec3.TryParse("1, 2", culture, out _).Should().BeFalse();
        Vec3.TryParse("1, 2, 3, 4", culture, out _).Should().BeFalse();
        Vec3.TryParse("1, 2, 3,", culture, out _).Should().BeFalse();
        Vec3.TryParse("1, 2, three", culture, out _).Should().BeFalse();
        Vec3.TryParse(string.Empty, culture, out Vec3 empty).Should().BeFalse();
        AssertComponents(empty, 0F, 0F, 0F);

        var malformed = () => Vec3.Parse("nope", culture);
        malformed.Should().Throw<FormatException>();
    }

    [Fact]
    public void TryFormat_ReportsFailureOnShortDestination()
    {
        var value = new Vec3(1F, 2F, 3F);
        Span<char> tooShort = stackalloc char[4];
        Span<byte> tooSmallUtf8 = stackalloc byte[3];

        value.TryFormat(tooShort, out int charsWritten).Should().BeFalse();
        charsWritten.Should().Be(0);
        value.TryFormat(tooSmallUtf8, out int bytesWritten).Should().BeFalse();
        bytesWritten.Should().Be(0);
    }

    private static void AssertComponents(Vec3 value, float x, float y, float z)
    {
        value.X.Should().Be(x);
        value.Y.Should().Be(y);
        value.Z.Should().Be(z);
    }

    private static float Length(Vec3 value) =>
        MathF.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));
}
