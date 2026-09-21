namespace Axrone.Event.Tests;

public class DeadLetterTests
{
    [Fact]
    public void Capture_FlattensExceptionAndStampsClock()
    {
        var envelope = new EventEnvelope<int>(7);

        var entry = DeadLetterEntry<int>.Capture(
            in envelope,
            DeadLetterReason.HandlerException,
            new InvalidOperationException("boom"));

        entry.Reason.Should().Be(DeadLetterReason.HandlerException);
        entry.Error.Should().Contain("InvalidOperationException");
        entry.Error.Should().Contain("boom");
        entry.DeadLetteredAt.Should().BeGreaterThan(0);
        entry.Envelope.Payload.Should().Be(7);
    }

    [Fact]
    public void Capture_NullException_CarriesNoError()
    {
        var envelope = new EventEnvelope<int>(7);

        var entry = DeadLetterEntry<int>.Capture(in envelope, DeadLetterReason.PoisonMessage, null);

        entry.Error.Should().BeNull();
        entry.Reason.Should().Be(DeadLetterReason.PoisonMessage);
    }
}
