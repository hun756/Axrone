namespace Axrone.Event.Tests;

public class EnvelopeTests
{
    [Fact]
    public void DefaultMetadata_IsUnstamped()
    {
        var metadata = default(EventMetadata);

        metadata.IsStamped.Should().BeFalse();
        metadata.Sequence.Should().Be(0);
        metadata.EventId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void PayloadOnlyEnvelope_IsUnstamped()
    {
        var envelope = new EventEnvelope<int>(42);

        envelope.IsStamped.Should().BeFalse();
        envelope.Payload.Should().Be(42);
    }

    [Fact]
    public void Withers_PreserveOtherFields()
    {
        var id = Guid.NewGuid();
        var correlation = Guid.NewGuid();
        var metadata = new EventMetadata(id, correlation, Guid.Empty, 0, 1, 0, 99L);

        var sequenced = metadata.WithSequence(7L);
        sequenced.Sequence.Should().Be(7L);
        sequenced.EventId.Should().Be(id);
        sequenced.Timestamp.Should().Be(99L);

        var reidentified = metadata.WithIdentity(Guid.NewGuid(), correlation);
        reidentified.CorrelationId.Should().Be(correlation);
        reidentified.Sequence.Should().Be(0);
    }

    [Fact]
    public void Envelope_EqualityIsStructural()
    {
        var id = Guid.NewGuid();
        var a = new EventEnvelope<string>(new EventMetadata(id, id, Guid.Empty, 1, 1, 0, 0), "hit");
        var b = new EventEnvelope<string>(new EventMetadata(id, id, Guid.Empty, 1, 1, 0, 0), "hit");
        var c = new EventEnvelope<string>(new EventMetadata(id, id, Guid.Empty, 1, 1, 0, 0), "miss");

        a.Should().Be(b);
        a.Should().NotBe(c);
    }
}
