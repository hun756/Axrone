namespace Axrone.Particles;

/// <summary>Unconfigured builder phase.</summary>
public readonly struct UnconfiguredPhase
{
}

/// <summary>Configured builder phase.</summary>
public readonly struct ConfiguredPhase
{
}

/// <summary>Typestate particle system builder.</summary>
/// <typeparam name="TPhase">Configuration phase.</typeparam>
public sealed class ParticleSystemBuilder<TPhase>
{
    /// <summary>Particle capacity.</summary>
    public ParticleCapacity Capacity { get; private set; } = (ParticleCapacity)1024;

    /// <summary>Spatial cell size.</summary>
    public float SpatialCellSize { get; private set; } = 10.0f;

    /// <summary>Simulation space.</summary>
    public SimulationSpace SimulationSpace { get; private set; } = SimulationSpace.World;

    /// <summary>Simulation bounds.</summary>
    public ParticleBounds Bounds { get; private set; } = new(new Vec3(-100f, -100f, -100f), new Vec3(100f, 100f, 100f));

    /// <summary>Default start lifetime.</summary>
    public float StartLifetime { get; private set; } = 5.0f;

    /// <summary>Default start speed.</summary>
    public float StartSpeed { get; private set; } = 5.0f;

    /// <summary>Default start size.</summary>
    public Vec3 StartSize { get; private set; } = Vec3.One;

    /// <summary>Default start color.</summary>
    public Vec4 StartColor { get; private set; } = Vec4.One;

    internal ParticleSystemBuilder()
    {
    }

    /// <summary>Creates an unconfigured builder.</summary>
    public static ParticleSystemBuilder<UnconfiguredPhase> Create() => new();

    /// <summary>Sets the capacity (moves to the configured phase).</summary>
    public ParticleSystemBuilder<ConfiguredPhase> WithCapacity(ParticleCapacity capacity)
    {
        var configured = new ParticleSystemBuilder<ConfiguredPhase>();
        configured.AssignFrom(this);
        configured.Capacity = capacity;
        return configured;
    }

    /// <summary>Sets the bounds.</summary>
    public ParticleSystemBuilder<TPhase> WithBounds(in ParticleBounds bounds)
    {
        Bounds = bounds;
        return this;
    }

    /// <summary>Sets the spatial cell size.</summary>
    public ParticleSystemBuilder<TPhase> WithSpatialCellSize(float cellSize)
    {
        SpatialCellSize = cellSize;
        return this;
    }

    /// <summary>Sets the simulation space.</summary>
    public ParticleSystemBuilder<TPhase> WithSimulationSpace(SimulationSpace space)
    {
        SimulationSpace = space;
        return this;
    }

    /// <summary>Sets default kinematics.</summary>
    public ParticleSystemBuilder<TPhase> WithDefaultKinematics(float lifetime, float speed, in Vec3 size, in Vec4 color)
    {
        StartLifetime = lifetime;
        StartSpeed = speed;
        StartSize = size;
        StartColor = color;
        return this;
    }

    private void AssignFrom<T>(ParticleSystemBuilder<T> other)
    {
        Capacity = other.Capacity;
        SpatialCellSize = other.SpatialCellSize;
        SimulationSpace = other.SimulationSpace;
        Bounds = other.Bounds;
        StartLifetime = other.StartLifetime;
        StartSpeed = other.StartSpeed;
        StartSize = other.StartSize;
        StartColor = other.StartColor;
    }
}

/// <summary>Builder extensions.</summary>
public static class ParticleSystemBuilderExtensions
{
    /// <summary>Builds an engine from a configured builder.</summary>
    public static ParticleSystemEngine<TPipeline, TSink> Build<TPipeline, TSink>(
        this ParticleSystemBuilder<ConfiguredPhase> builder,
        TPipeline pipeline,
        TSink eventSink)
        where TPipeline : struct, IModulePipelineNode
        where TSink : IParticleEventSink
    {
        ArgumentNullException.ThrowIfNull(builder);
        return new ParticleSystemEngine<TPipeline, TSink>(builder, pipeline, eventSink);
    }
}
