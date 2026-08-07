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
}
