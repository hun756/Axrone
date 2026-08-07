using System.Buffers;
using Axrone.Utility.Base64;

namespace Axrone.Utility.Tests.Base64;

public class Base64HelperTests
{
    [Fact]
    public void Encode_Standard_RoundTrips()
    {
        var data = "Hello, Axrone!"u8.ToArray();
        var encoded = Base64Helper.Encode(data);
        var decoded = Base64Helper.Decode(encoded);
        decoded.Should().Equal(data);
    }

    [Fact]
    public void Encode_Empty_ReturnsEmpty()
    {
        Base64Helper.Encode(ReadOnlySpan<byte>.Empty).Should().BeEmpty();
    }

    [Fact]
    public void EncodeToString_MatchesEncode()
    {
        var data = "Axrone engine"u8.ToArray();
        Base64Helper.EncodeToString(data).Should().Be(Base64Helper.Encode(data));
    }

    [Fact]
    public void Encode_SpanToDestination_RoundTrips()
    {
        var data = "Span encode test"u8.ToArray();
        var dst = new byte[Base64Helper.GetEncodedLength(data.Length)];
        var status = Base64Helper.Encode(data, dst, out var written);
        status.Should().Be(OperationStatus.Done);
        written.Should().Be(dst.Length);

        var decoded = Base64Helper.Decode(System.Text.Encoding.ASCII.GetString(dst));
        decoded.Should().Equal(data);
    }

    [Fact]
    public void EncodeUrlSafe_NoPaddingOrSpecialChars()
    {
        var data = new byte[] { 0xFF, 0xFE, 0xFD };
        var encoded = Base64Helper.EncodeUrlSafe(data);
        encoded.Should().NotContain("+");
        encoded.Should().NotContain("/");
        encoded.Should().NotContain("=");
    }

    [Fact]
    public void EncodeUrlSafe_RoundTrips()
    {
        var data = "Test data with URL-safe encoding!"u8.ToArray();
        var encoded = Base64Helper.EncodeUrlSafe(data);
        var decoded = Base64Helper.DecodeUrlSafe(encoded);
        decoded.Should().Equal(data);
    }

    [Fact]
    public void EncodeUrlSafe_SpanToDestination_RoundTrips()
    {
        var data = "Span URL-safe encode"u8.ToArray();
        var dst = new byte[Base64Helper.GetEncodedLength(data.Length)];
        var written = Base64Helper.EncodeUrlSafe(data, dst);
        written.Should().BeGreaterThan(0);

        var encodedStr = System.Text.Encoding.ASCII.GetString(dst, 0, written);
        encodedStr.Should().NotContain("+");
        encodedStr.Should().NotContain("/");
        encodedStr.Should().NotContain("=");

        var decoded = Base64Helper.DecodeUrlSafe(encodedStr);
        decoded.Should().Equal(data);
    }

    [Fact]
    public void DecodeUrlSafe_SpanFromSource_RoundTrips()
    {
        var data = "Span URL-safe decode test!"u8.ToArray();
        var encoded = Base64Helper.EncodeUrlSafe(data);
        var encodedBytes = System.Text.Encoding.ASCII.GetBytes(encoded);
        var dst = new byte[data.Length + 4];

        var written = Base64Helper.DecodeUrlSafe(encodedBytes, dst);
        written.Should().Be(data.Length);
        dst.AsSpan(0, written).ToArray().Should().Equal(data);
    }

    [Fact]
    public void DecodeUrlSafe_Span_Empty_ReturnsZero()
    {
        var dst = new byte[16];
        Base64Helper.DecodeUrlSafe(ReadOnlySpan<byte>.Empty, dst).Should().Be(0);
    }

    [Fact]
    public void EncodeToWriter_RoundTrips()
    {
        var data = "IBufferWriter encode"u8.ToArray();
        var writer = new ArrayBufferWriter<byte>();
        Base64Helper.EncodeToWriter(data, writer);

        var encoded = System.Text.Encoding.ASCII.GetString(writer.WrittenSpan);
        var decoded = Base64Helper.Decode(encoded);
        decoded.Should().Equal(data);
    }

    [Fact]
    public void EncodeToWriter_Empty_WritesNothing()
    {
        var writer = new ArrayBufferWriter<byte>();
        Base64Helper.EncodeToWriter(ReadOnlySpan<byte>.Empty, writer);
        writer.WrittenCount.Should().Be(0);
    }

    [Fact]
    public void EncodeToWriter_NullWriter_Throws()
    {
        var act = () => Base64Helper.EncodeToWriter([1, 2, 3], null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EncodeUrlSafeToWriter_RoundTrips()
    {
        var data = "IBufferWriter URL-safe"u8.ToArray();
        var writer = new ArrayBufferWriter<byte>();
        Base64Helper.EncodeUrlSafeToWriter(data, writer);

        var encoded = System.Text.Encoding.ASCII.GetString(writer.WrittenSpan);
        encoded.Should().NotContain("+");
        encoded.Should().NotContain("/");
        encoded.Should().NotContain("=");

        var decoded = Base64Helper.DecodeUrlSafe(encoded);
        decoded.Should().Equal(data);
    }

    [Fact]
    public void EncodeUrlSafeToWriter_NullWriter_Throws()
    {
        var act = () => Base64Helper.EncodeUrlSafeToWriter([1, 2, 3], null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Decode_SpanFromSource_RoundTrips()
    {
        var data = "Span decode test"u8.ToArray();
        var encoded = System.Text.Encoding.ASCII.GetBytes(Base64Helper.Encode(data));
        var dst = new byte[data.Length];

        var status = Base64Helper.Decode(encoded, dst, out var written);
        status.Should().Be(OperationStatus.Done);
        written.Should().Be(data.Length);
        dst.AsSpan(0, written).ToArray().Should().Equal(data);
    }

    [Fact]
    public void GetMaxDecodedLength_Correct()
    {
        Base64Helper.GetMaxDecodedLength(0).Should().Be(0);
        Base64Helper.GetMaxDecodedLength(4).Should().Be(3);
        Base64Helper.GetMaxDecodedLength(8).Should().Be(6);
    }

    [Fact]
    public void GetEncodedLength_Correct()
    {
        Base64Helper.GetEncodedLength(0).Should().Be(0);
        Base64Helper.GetEncodedLength(1).Should().Be(4);
        Base64Helper.GetEncodedLength(2).Should().Be(4);
        Base64Helper.GetEncodedLength(3).Should().Be(4);
        Base64Helper.GetEncodedLength(4).Should().Be(8);
    }

    [Fact]
    public void IsValid_ValidBase64_ReturnsTrue()
    {
        Base64Helper.IsValid("SGVsbG8=").Should().BeTrue();
    }

    [Fact]
    public void IsValid_EmptyString_ReturnsTrue()
    {
        Base64Helper.IsValid("").Should().BeTrue();
    }

    [Fact]
    public void IsValid_InvalidBase64_ReturnsFalse()
    {
        Base64Helper.IsValid("not-valid!").Should().BeFalse();
    }
}
