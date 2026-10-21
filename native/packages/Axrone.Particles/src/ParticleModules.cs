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

// 4. Trail Module (Priority 800)
/// <summary>Particle trail history module.</summary>
public unsafe struct TrailModule : IParticleModulePolicy<TrailModule>, IDisposable
{
    /// <inheritdoc/>
    public static int Priority => 800;

    /// <summary>Trail mode.</summary>
    public TrailMode Mode = TrailMode.Ribbon;

    /// <summary>Ribbon width.</summary>
    public float RibbonWidth = 0.5f;

    /// <summary>Trail vertex.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct TrailPoint
    {
        /// <summary>Position.</summary>
        public Vec3 Position;

        /// <summary>Color.</summary>
        public Vec4 Color;

        /// <summary>Age.</summary>
        public float Age;
    }

    private TrailPoint* _history;
    private int _historyCapacity;
    private int _pointsPerTrail;

    /// <summary>Creates a module.</summary>
    public TrailModule(int maxTrackedParticles = 512, int pointsPerTrail = 16)
    {
        _pointsPerTrail = pointsPerTrail;
        _historyCapacity = maxTrackedParticles * pointsPerTrail;
        _history = (TrailPoint*)NativeMemory.AlignedAlloc((nuint)(_historyCapacity * sizeof(TrailPoint)), 64);
        NativeMemory.Clear(_history, (nuint)(_historyCapacity * sizeof(TrailPoint)));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Process(NativeSoaParticleStorage storage, in ModuleExecutionContext context, ref TrailModule state)
    {
        ArgumentNullException.ThrowIfNull(storage);

        if (state._history == null)
        {
            return;
        }

        int count = Math.Min(storage.ActiveCount, state._historyCapacity / state._pointsPerTrail);
        float dt = context.DeltaTime.Seconds;

        for (int i = 0; i < count; i++)
        {
            int baseIdx = i * state._pointsPerTrail;

            for (int p = state._pointsPerTrail - 1; p > 0; p--)
            {
                state._history[baseIdx + p] = state._history[baseIdx + p - 1];
                state._history[baseIdx + p].Age += dt;
            }

            state._history[baseIdx] = new TrailPoint
            {
                Position = new Vec3(storage.PosX[i], storage.PosY[i], storage.PosZ[i]),
                Color = new Vec4(storage.ColR[i], storage.ColG[i], storage.ColB[i], storage.ColA[i]),
                Age = 0f,
            };
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_history != null)
        {
            NativeMemory.Free(_history);
            _history = null;
        }
    }
}

// 5. Collision Module (Priority 700)
/// <summary>Plane collision module.</summary>
public struct CollisionModule : IParticleModulePolicy<CollisionModule>
{
    /// <inheritdoc/>
    public static int Priority => 700;

    /// <summary>Collision primitive.</summary>
    public CollisionPrimitive Primitive = CollisionPrimitive.Plane;

    /// <summary>Collision mode.</summary>
    public CollisionMode Mode = CollisionMode.Kill;

    /// <summary>Plane point.</summary>
    public Vec3 PlanePoint = Vec3.Zero;

    /// <summary>Plane normal.</summary>
    public Vec3 PlaneNormal = Vec3.UnitY;

    /// <summary>Bounce factor.</summary>
    public float Bounce = 0.5f;

    /// <summary>Dampen factor.</summary>
    public float Dampen = 0.1f;

    /// <summary>Creates a module.</summary>
    public CollisionModule()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Process(NativeSoaParticleStorage storage, in ModuleExecutionContext context, ref CollisionModule state)
    {
        ArgumentNullException.ThrowIfNull(storage);

        if (state.Mode == CollisionMode.Ignore)
        {
            return;
        }

        int i = 0;
        Vec3 n = state.PlaneNormal;
        Vec3 p0 = state.PlanePoint;

        while (i < storage.ActiveCount)
        {
            Vec3 pos = new(storage.PosX[i], storage.PosY[i], storage.PosZ[i]);
            float dist = Vec3.Dot(pos - p0, n);

            if (dist < 0.0f)
            {
                if (state.Mode == CollisionMode.Kill)
                {
                    storage.RemoveAtDense(i);
                    continue;
                }
                else
                {
                    Vec3 vel = new(storage.VelX[i], storage.VelY[i], storage.VelZ[i]);
                    Vec3 normVel = n * Vec3.Dot(vel, n);
                    Vec3 tangVel = vel - normVel;
                    Vec3 reflected = (tangVel * (1.0f - state.Dampen)) - (normVel * state.Bounce);

                    pos -= n * dist;
                    storage.PosX[i] = pos.X;
                    storage.PosY[i] = pos.Y;
                    storage.PosZ[i] = pos.Z;

                    storage.VelX[i] = reflected.X;
                    storage.VelY[i] = reflected.Y;
                    storage.VelZ[i] = reflected.Z;
                }
            }

            i++;
        }
    }
}

// 6. Noise Module (Priority 600)
/// <summary>Hash noise turbulence module.</summary>
public struct NoiseModule : IParticleModulePolicy<NoiseModule>
{
    /// <inheritdoc/>
    public static int Priority => 600;

    /// <summary>Noise type.</summary>
    public NoiseType Type = NoiseType.Perlin;

    /// <summary>Frequency.</summary>
    public float Frequency = 0.1f;

    /// <summary>Amplitude.</summary>
    public float Amplitude = 1.0f;

    /// <summary>Animation speed.</summary>
    public float AnimationSpeed = 0.5f;

    private float _timeAccum;

    /// <summary>Creates a module.</summary>
    public NoiseModule()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe void Process(NativeSoaParticleStorage storage, in ModuleExecutionContext context, ref NoiseModule state)
    {
        ArgumentNullException.ThrowIfNull(storage);

        state._timeAccum += context.DeltaTime.Seconds * state.AnimationSpeed;
        float dt = context.DeltaTime.Seconds;
        float amp = state.Amplitude;
        float freq = state.Frequency;
        float time = state._timeAccum;
        int count = storage.ActiveCount;

        float* px = storage.PosX;
        float* py = storage.PosY;
        float* pz = storage.PosZ;
        float* vx = storage.VelX;
        float* vy = storage.VelY;
        float* vz = storage.VelZ;

        for (int i = 0; i < count; i++)
        {
            float x = px[i] * freq;
            float y = py[i] * freq;
            float z = pz[i] * freq;

            float nx = FastHarmonicNoise(x, y, time);
            float ny = FastHarmonicNoise(y, z, time + 13.37f);
            float nz = FastHarmonicNoise(z, x, time + 27.89f);

            vx[i] += nx * amp * dt;
            vy[i] += ny * amp * dt;
            vz[i] += nz * amp * dt;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float FastHarmonicNoise(float x, float y, float z)
    {
        int ix = (int)MathF.Floor(x);
        int iy = (int)MathF.Floor(y);
        int iz = (int)MathF.Floor(z);
        float fx = x - ix;
        float fy = y - iy;
        float fz = z - iz;

        uint h0 = ((uint)ix * 73856093u) ^ ((uint)iy * 19349663u) ^ ((uint)iz * 83492791u);
        uint h1 = ((uint)(ix + 1) * 73856093u) ^ ((uint)iy * 19349663u) ^ ((uint)iz * 83492791u);

        float s0 = ((h0 & 0xFFFF) / 32768.0f) - 1.0f;
        float s1 = ((h1 & 0xFFFF) / 32768.0f) - 1.0f;

        return MathF.Sin(MathF.FusedMultiplyAdd(fx, s1 - s0, s0) + fy + fz);
    }
}
