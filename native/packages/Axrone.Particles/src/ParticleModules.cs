namespace Axrone.Particles;

// 1. Texture Sheet Module (Priority 1100)
/// <summary>Sprite sheet animation module.</summary>
public struct TextureSheetModule : IParticleModulePolicy<TextureSheetModule>
{
    /// <inheritdoc/>
    public static int Priority => 1100;

    /// <summary>Tiles on X.</summary>
    public int TilesX = 1;

    /// <summary>Tiles on Y.</summary>
    public int TilesY = 1;

    /// <summary>Animation fps.</summary>
    public float AnimationFps = 30.0f;

    /// <summary>Cycle count.</summary>
    public int CycleCount = 1;

    /// <summary>Creates a module.</summary>
    public TextureSheetModule()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Process(NativeSoaParticleStorage storage, in ModuleExecutionContext context, ref TextureSheetModule state)
    {
        ArgumentNullException.ThrowIfNull(storage);
        int count = storage.ActiveCount;
        if (count == 0)
        {
            return;
        }

        int totalTiles = Math.Max(1, state.TilesX * state.TilesY);
        float invTilesX = 1.0f / state.TilesX;
        float invTilesY = 1.0f / state.TilesY;

        float* custom3 = storage.Custom3;
        float* age = storage.Age;
        float* lifetime = storage.Lifetime;

        for (int i = 0; i < count; i++)
        {
            float normAge = age[i] / Math.Max(lifetime[i], 1e-5f);
            float frameProgress = (normAge * state.CycleCount) % 1.0f;
            int frame = (int)(frameProgress * totalTiles) % totalTiles;

            int tileX = frame % state.TilesX;
            int tileY = frame / state.TilesX;

            custom3[i] = (tileX * invTilesX) + (tileY * invTilesY * 1000.0f);
        }
    }
}

// 2. Custom Data Module (Priority 1000)
/// <summary>Custom channel module.</summary>
public struct CustomDataModule : IParticleModulePolicy<CustomDataModule>
{
    /// <inheritdoc/>
    public static int Priority => 1000;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Process(NativeSoaParticleStorage storage, in ModuleExecutionContext context, ref CustomDataModule state)
    {
        ArgumentNullException.ThrowIfNull(storage);
    }

    /// <summary>Sets a custom channel value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void SetCustomChannel(NativeSoaParticleStorage storage, ParticleId id, int channel, float value)
    {
        ArgumentNullException.ThrowIfNull(storage);

        if (storage.TryGetDenseIndex(id, out int denseIdx))
        {
            switch (channel)
            {
                case 0: storage.Custom0[denseIdx] = value; break;
                case 1: storage.Custom1[denseIdx] = value; break;
                case 2: storage.Custom2[denseIdx] = value; break;
                case 3: storage.Custom3[denseIdx] = value; break;
                default: break;
            }
        }
    }
}

// 3. Lights Module (Priority 900)
/// <summary>Particle light packing module.</summary>
public struct LightsModule : IParticleModulePolicy<LightsModule>
{
    /// <inheritdoc/>
    public static int Priority => 900;

    /// <summary>Lighting mode.</summary>
    public LightingMode Mode = LightingMode.Simple;

    /// <summary>Attenuation mode.</summary>
    public LightAttenuationMode Attenuation = LightAttenuationMode.InverseSquare;

    /// <summary>Light intensity.</summary>
    public float Intensity = 1.0f;

    /// <summary>Light range.</summary>
    public float Range = 10.0f;

    /// <summary>Creates a module.</summary>
    public LightsModule()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Process(NativeSoaParticleStorage storage, in ModuleExecutionContext context, ref LightsModule state)
    {
        ArgumentNullException.ThrowIfNull(storage);
        if (state.Mode == LightingMode.None)
        {
            return;
        }

        int count = storage.ActiveCount;
        float* custom2 = storage.Custom2;
        float packed = state.Intensity * state.Range;

        for (int i = 0; i < count; i++)
        {
            custom2[i] = packed;
        }
    }
}
