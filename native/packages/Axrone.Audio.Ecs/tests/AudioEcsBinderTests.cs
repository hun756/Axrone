namespace Axrone.Audio.Ecs.Tests;

public sealed class AudioEcsBinderTests : IDisposable
{
    private readonly AudioSystem _system = new();
    private readonly World _world = new();
    private readonly AudioEcsBinder _binder;
    private readonly List<AudioClipRecord> _records = new();

    public AudioEcsBinderTests()
    {
        ComponentRegistry.Register<AudioSourceRef>();
        ComponentRegistry.Register<AudioListenerRef>();
        _binder = new AudioEcsBinder(_system, _world);
    }

    public void Dispose()
    {
        _binder.Dispose();
        _world.Dispose();
        _system.Dispose();
        foreach (AudioClipRecord record in _records)
        {
            record.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private AudioClipId RegisterClip()
    {
        var memory = new NativeAudioMemory(48000, 1, 480);
        memory.AsSpan().Fill(0.25f);
        var record = new AudioClipRecord(new AudioClipId((uint)(_records.Count + 1)), memory);
        _records.Add(record);
        return _system.Clips.Register(record);
    }

    [Fact]
    public void PlayCommand_Renders_ThroughSystem()
    {
        AudioClipId clip = RegisterClip();
        var sourceId = new AudioSourceId(1U);
        _system.Sources.Upsert(sourceId, new AudioSourceDefinition(ClipId: clip, Loop: true));

        Entity entity = _world.BeginEntity().Commit();
        _world.AddComponent(entity, new AudioSourceRef(sourceId));

        _binder.Play(entity);
        _binder.Update();

        Span<float> block = stackalloc float[256];
        _system.ProcessAudioRenderBlock(block);
        block.ToArray().Should().Contain(v => v != 0.0f);
    }

    [Fact]
    public void StopCommand_Silences_Voice()
    {
        AudioClipId clip = RegisterClip();
        var sourceId = new AudioSourceId(1U);
        _system.Sources.Upsert(sourceId, new AudioSourceDefinition(ClipId: clip, Loop: true));

        Entity entity = _world.BeginEntity().Commit();
        _world.AddComponent(entity, new AudioSourceRef(sourceId));

        _binder.Play(entity);
        _binder.Update();
        _binder.Stop(entity);
        _binder.Update();

        Span<float> block = stackalloc float[256];
        _system.ProcessAudioRenderBlock(block);
        block.ToArray().Should().OnlyContain(v => v == 0.0f);
    }

    [Fact]
    public void Destroyed_Entity_Drops_Queue()
    {
        AudioClipId clip = RegisterClip();
        var sourceId = new AudioSourceId(1U);
        _system.Sources.Upsert(sourceId, new AudioSourceDefinition(ClipId: clip, Loop: true));

        Entity entity = _world.BeginEntity().Commit();
        _world.AddComponent(entity, new AudioSourceRef(sourceId));

        _binder.Play(entity);
        _world.DestroyEntity(entity);
        _binder.Update();

        Span<float> block = stackalloc float[256];
        _system.ProcessAudioRenderBlock(block);
        block.ToArray().Should().OnlyContain(v => v == 0.0f);
    }

    [Fact]
    public void AttachListener_Registers_Entry()
    {
        Entity entity = _world.BeginEntity().Commit();
        _world.AddComponent(entity, new AudioListenerRef(new AudioListenerId(2U)));

        _binder.AttachListener(entity);
        _binder.Update();

        _system.Listeners.ActiveListenerId.Should().Be(AudioListenerId.Primary);
        _binder.DetachListener(entity);
        _binder.Update();
    }
}
