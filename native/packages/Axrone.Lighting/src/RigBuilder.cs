namespace Axrone.Lighting;

/// <summary>Rig configuration stage marker.</summary>
public interface IRigConfigurationStage
{
}

/// <summary>Initial configuration stage.</summary>
public readonly struct StageInitial : IRigConfigurationStage
{
}

/// <summary>Sealed configuration stage.</summary>
public readonly struct StageSealed : IRigConfigurationStage
{
}

/// <summary>Shared rig configuration parameters.</summary>
public sealed class RigConfigurationParameters
{
    /// <summary>Rig identity.</summary>
    public RigId RigId
    {
        get => field;
        set => field = value;
    } = new(1);

    /// <summary>Environment.</summary>
    public LightingEnvironment Environment
    {
        get => field;
        set
        {
            LightingGuards.ValidateEnvironment(value);
            field = value;
        }
    } = LightingEnvironment.Default;

    /// <summary>Capacity.</summary>
    public LightingCapacity Capacity
    {
        get => field;
        set => field = value;
    } = LightingCapacity.Default;
}

/// <summary>Typestate rig builder.</summary>
/// <typeparam name="TStage">Configuration stage.</typeparam>
public readonly struct RigBuilder<TStage>
    where TStage : struct, IRigConfigurationStage
{
    internal readonly RigConfigurationParameters Parameters;

    internal RigBuilder(RigConfigurationParameters parameters)
    {
        Parameters = parameters;
    }

    /// <summary>Creates an initial builder.</summary>
    public static RigBuilder<StageInitial> Create() => new(new RigConfigurationParameters());

    /// <summary>Sets the rig identity.</summary>
    public RigBuilder<TStage> WithRigId(RigId id)
    {
        Parameters.RigId = id;
        return this;
    }

    /// <summary>Sets the environment.</summary>
    public RigBuilder<TStage> WithEnvironment(in LightingEnvironment environment)
    {
        Parameters.Environment = environment;
        return this;
    }

    /// <summary>Sets the capacity.</summary>
    public RigBuilder<TStage> WithCapacity(in LightingCapacity capacity)
    {
        Parameters.Capacity = capacity;
        return this;
    }
}

/// <summary>Rig builder stage transitions.</summary>
public static partial class RigBuilderExtensions
{
    /// <summary>Seals an initial builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RigBuilder<StageSealed> Seal(this RigBuilder<StageInitial> builder)
    {
        return new RigBuilder<StageSealed>(builder.Parameters);
    }
}
