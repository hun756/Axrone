namespace Axrone.Animation.Tests;

using Xunit;
using FluentAssertions;

public class SinkTests
{
    private struct CountingSink : IClipEventSink
    {
        public int Count;
        public float TotalWeight;

        public void Emit(in ClipEvent clipEvent)
        {
            Count++;
            TotalWeight += clipEvent.Payload.Length;
        }
    }

    private static readonly float[] s_times = new float[] { 0.0f, 1.0f };
    private static readonly float[] s_values = new float[] { 0, 0, 0, 1, 0, 0 };

    private static StateMachineInstance CreateMachine(out ParameterStore parameters)
    {
        parameters = new ParameterStore(new Dictionary<string, ParameterType>());
        var clip = new AnimationClip(
            new ClipId("c"), 1.0f,
            new AnimationChannel[]
            {
                new(0, ChannelTarget.Translation, InterpolationMode.Linear, s_times, s_values),
            },
            new ClipEvent[] { new(0.5f, "hit", "x") });
        return new StateMachineInstance(
            new AnimationState[] { new(new StateId("s"), new ClipMotionNode(clip)) },
            Array.Empty<StateTransition>(), 0);
    }

    [Fact]
    public void UpdateSink_CollectsWithoutAllocating()
    {
        StateMachineInstance machine = CreateMachine(out ParameterStore parameters);

        var sink = new CountingSink();
        machine.Update(0.6f, parameters, ref sink);
        sink.Count.Should().Be(1);
        sink.TotalWeight.Should().Be(1.0f);

        var list = new List<ClipEvent>();
        StateMachineInstance machine2 = CreateMachine(out ParameterStore parameters2);
        machine2.Update(0.6f, parameters2, list, 1.0f);
        list.Should().HaveCount(1);
        list[0].Should().Be(new ClipEvent(0.5f, "hit", "x"));
    }
}
