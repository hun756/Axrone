using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BclRandom = System.Random;

namespace Axrone.Random.Benchmarks;

/// <summary>
/// Head-to-head throughput of the <see cref="Random"/> static facade and the
/// <see cref="RandomEngine{TEngine}"/> wrappers against the BCL <c>System.Random</c> surface,
/// one group per draw shape: scalar 32-bit, scalar double, byte fills, a 1024-element shuffle
/// and a 128-element 64-bit fill.
/// </summary>
/// <remarks>
/// <para><b>Running (JIT).</b>
/// <c>dotnet run -c Release --project packages/Axrone.Random/benchmarks/Axrone.Random.Benchmarks.csproj -- --job short --filter *</c>.
/// The <c>--job short</c> switch selects BenchmarkDotNet's ShortRun job; drop it for the full
/// default job when a machine-stable number is needed.</para>
/// <para><b>Running (Native AOT).</b> BenchmarkDotNet generates and loads IL at run time and
/// cannot execute under Native AOT, so the AOT numbers come from the manual stopwatch harness in
/// <c>Program.cs</c>. Publish with the AOT switch on the command line, never in the project file:
/// <c>dotnet publish -c Release -p:PublishAot=true packages/Axrone.Random/benchmarks/Axrone.Random.Benchmarks.csproj</c>,
/// then run the produced executable. The harness prints the same benchmark names with ns/op and
/// B/op.</para>
/// <para><b>Runtime probe.</b> The entry point chooses the harness from two independent signals -
/// a <c>coreclr.dll</c> host probe against the runtime directory and the
/// <c>RuntimeFeature.IsDynamicCodeSupported</c> guard - and never from <c>runtimeconfig.json</c>,
/// which records the host the build intended rather than the process actually running. The probe
/// line is printed before any numbers.</para>
/// <para><b>Baseline.</b> The BCL <c>Random.Shared</c> method is the baseline of each group, so
/// every ratio is relative to the shared BCL stream. The groups are separated with
/// <see cref="GroupBenchmarksByAttribute"/> so each case carries its own baseline.</para>
/// <para><b>Fill case.</b> The package exposes <c>Fill(Span&lt;int&gt;)</c>,
/// <c>Fill(Span&lt;double&gt;, ...)</c> and <c>Fill(Span&lt;float&gt;, ...)</c> but no
/// <c>Fill(Span&lt;ulong&gt;)</c>; the requested <c>Fill&lt;ulong&gt;(128)</c> case is therefore
/// the manual fill loop over <c>NextUInt64</c> (ours) and <c>NextInt64</c> (BCL).</para>
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RandomVsBclBenchmarks
{
    private const ulong Seed = 0x0123456789ABCDEFUL;
    private const int ShuffleLength = 1024;
    private const int FillLength = 128;

    private RandomEngine<Xoroshiro128PlusPlus> _xoroshiro;
    private RandomEngine<Xoshiro256PlusPlus> _xoshiro;
    private BclRandom _bclInstance = null!;
    private byte[] _bytes64 = null!;
    private byte[] _bytes1K = null!;
    private int[] _shuffle = null!;
    private ulong[] _fill = null!;
    private uint _uintSink;
    private double _doubleSink;

    /// <summary>Seeds every stream and allocates every buffer once, outside the measured region.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _xoroshiro = new RandomEngine<Xoroshiro128PlusPlus>(Seed);
        _xoshiro = new RandomEngine<Xoshiro256PlusPlus>(Seed);
        _bclInstance = new BclRandom(42);
        _bytes64 = new byte[64];
        _bytes1K = new byte[1024];
        _shuffle = new int[ShuffleLength];
        for (int i = 0; i < _shuffle.Length; i++)
        {
            _shuffle[i] = i;
        }

        _fill = new ulong[FillLength];
    }

    // ---- NextUInt32 ----

    /// <summary>Draws a 32-bit word from the ambient thread-local facade.</summary>
    [BenchmarkCategory("NextUInt32")]
    [Benchmark]
    public void NextUInt32_OursStatic() => _uintSink = Random.NextUInt32();

    /// <summary>Draws a 32-bit word from a seeded xoroshiro128++ wrapper.</summary>
    [BenchmarkCategory("NextUInt32")]
    [Benchmark]
    public void NextUInt32_OursXoroshiro128() => _uintSink = _xoroshiro.NextUInt32();

    /// <summary>Draws a 32-bit word from a seeded xoshiro256++ wrapper.</summary>
    [BenchmarkCategory("NextUInt32")]
    [Benchmark]
    public void NextUInt32_OursXoshiro256() => _uintSink = _xoshiro.NextUInt32();

    /// <summary>Draws a 32-bit word from the shared BCL stream; the group baseline.</summary>
    [BenchmarkCategory("NextUInt32")]
    [Benchmark(Baseline = true)]
    public void NextUInt32_BclShared() => _uintSink = (uint)BclRandom.Shared.Next();

    /// <summary>Draws a 32-bit word from a seeded BCL instance.</summary>
    [BenchmarkCategory("NextUInt32")]
    [Benchmark]
    public void NextUInt32_BclInstance() => _uintSink = (uint)_bclInstance.Next();

    // ---- NextDouble ----

    /// <summary>Draws a double in [0, 1) from the ambient thread-local facade.</summary>
    [BenchmarkCategory("NextDouble")]
    [Benchmark]
    public void NextDouble_OursStatic() => _doubleSink = Random.NextDouble();

    /// <summary>Draws a double in [0, 1) from a seeded xoroshiro128++ wrapper.</summary>
    [BenchmarkCategory("NextDouble")]
    [Benchmark]
    public void NextDouble_OursXoroshiro128() => _doubleSink = _xoroshiro.NextDouble();

    /// <summary>Draws a double in [0, 1) from a seeded xoshiro256++ wrapper.</summary>
    [BenchmarkCategory("NextDouble")]
    [Benchmark]
    public void NextDouble_OursXoshiro256() => _doubleSink = _xoshiro.NextDouble();

    /// <summary>Draws a double in [0, 1) from the shared BCL stream; the group baseline.</summary>
    [BenchmarkCategory("NextDouble")]
    [Benchmark(Baseline = true)]
    public void NextDouble_BclShared() => _doubleSink = BclRandom.Shared.NextDouble();

    /// <summary>Draws a double in [0, 1) from a seeded BCL instance.</summary>
    [BenchmarkCategory("NextDouble")]
    [Benchmark]
    public void NextDouble_BclInstance() => _doubleSink = _bclInstance.NextDouble();

    // ---- NextBytes (64 B) ----

    /// <summary>Fills 64 bytes from the ambient thread-local facade.</summary>
    [BenchmarkCategory("NextBytes64")]
    [Benchmark]
    public void NextBytes64_OursStatic() => Random.NextBytes(_bytes64);

    /// <summary>Fills 64 bytes from a seeded xoroshiro128++ wrapper.</summary>
    [BenchmarkCategory("NextBytes64")]
    [Benchmark]
    public void NextBytes64_OursXoroshiro128() => _xoroshiro.NextBytes(_bytes64);

    /// <summary>Fills 64 bytes from a seeded xoshiro256++ wrapper.</summary>
    [BenchmarkCategory("NextBytes64")]
    [Benchmark]
    public void NextBytes64_OursXoshiro256() => _xoshiro.NextBytes(_bytes64);

    /// <summary>Fills 64 bytes from the shared BCL stream; the group baseline.</summary>
    [BenchmarkCategory("NextBytes64")]
    [Benchmark(Baseline = true)]
    public void NextBytes64_BclShared() => BclRandom.Shared.NextBytes(_bytes64);

    /// <summary>Fills 64 bytes from a seeded BCL instance.</summary>
    [BenchmarkCategory("NextBytes64")]
    [Benchmark]
    public void NextBytes64_BclInstance() => _bclInstance.NextBytes(_bytes64);

    // ---- NextBytes (1 KiB) ----

    /// <summary>Fills 1 KiB from the ambient thread-local facade.</summary>
    [BenchmarkCategory("NextBytes1K")]
    [Benchmark]
    public void NextBytes1K_OursStatic() => Random.NextBytes(_bytes1K);

    /// <summary>Fills 1 KiB from a seeded xoroshiro128++ wrapper.</summary>
    [BenchmarkCategory("NextBytes1K")]
    [Benchmark]
    public void NextBytes1K_OursXoroshiro128() => _xoroshiro.NextBytes(_bytes1K);

    /// <summary>Fills 1 KiB from a seeded xoshiro256++ wrapper.</summary>
    [BenchmarkCategory("NextBytes1K")]
    [Benchmark]
    public void NextBytes1K_OursXoshiro256() => _xoshiro.NextBytes(_bytes1K);

    /// <summary>Fills 1 KiB from the shared BCL stream; the group baseline.</summary>
    [BenchmarkCategory("NextBytes1K")]
    [Benchmark(Baseline = true)]
    public void NextBytes1K_BclShared() => BclRandom.Shared.NextBytes(_bytes1K);

    /// <summary>Fills 1 KiB from a seeded BCL instance.</summary>
    [BenchmarkCategory("NextBytes1K")]
    [Benchmark]
    public void NextBytes1K_BclInstance() => _bclInstance.NextBytes(_bytes1K);

    // ---- Shuffle (1024) ----

    /// <summary>Shuffles 1024 elements with the ambient thread-local facade.</summary>
    [BenchmarkCategory("Shuffle1024")]
    [Benchmark]
    public void Shuffle1024_OursStatic() => Random.Shuffle<int>(_shuffle);

    /// <summary>Shuffles 1024 elements with a seeded xoroshiro128++ wrapper.</summary>
    [BenchmarkCategory("Shuffle1024")]
    [Benchmark]
    public void Shuffle1024_OursXoroshiro128() => _xoroshiro.Shuffle<int>(_shuffle);

    /// <summary>Shuffles 1024 elements with a seeded xoshiro256++ wrapper.</summary>
    [BenchmarkCategory("Shuffle1024")]
    [Benchmark]
    public void Shuffle1024_OursXoshiro256() => _xoshiro.Shuffle<int>(_shuffle);

    /// <summary>Shuffles 1024 elements with the shared BCL stream; the group baseline.</summary>
    [BenchmarkCategory("Shuffle1024")]
    [Benchmark(Baseline = true)]
    public void Shuffle1024_BclShared() => BclRandom.Shared.Shuffle<int>(_shuffle);

    /// <summary>Shuffles 1024 elements with a seeded BCL instance.</summary>
    [BenchmarkCategory("Shuffle1024")]
    [Benchmark]
    public void Shuffle1024_BclInstance() => _bclInstance.Shuffle<int>(_shuffle);

    // ---- Fill (128 x 64-bit) ----

    /// <summary>Fills 128 64-bit words from the ambient thread-local facade.</summary>
    [BenchmarkCategory("FillUInt64_128")]
    [Benchmark]
    public void FillUInt64_128_OursStatic()
    {
        for (int i = 0; i < _fill.Length; i++)
        {
            _fill[i] = Random.NextUInt64();
        }
    }

    /// <summary>Fills 128 64-bit words from a seeded xoroshiro128++ wrapper.</summary>
    [BenchmarkCategory("FillUInt64_128")]
    [Benchmark]
    public void FillUInt64_128_OursXoroshiro128()
    {
        for (int i = 0; i < _fill.Length; i++)
        {
            _fill[i] = _xoroshiro.NextUInt64();
        }
    }

    /// <summary>Fills 128 64-bit words from a seeded xoshiro256++ wrapper.</summary>
    [BenchmarkCategory("FillUInt64_128")]
    [Benchmark]
    public void FillUInt64_128_OursXoshiro256()
    {
        for (int i = 0; i < _fill.Length; i++)
        {
            _fill[i] = _xoshiro.NextUInt64();
        }
    }

    /// <summary>Fills 128 64-bit words from the shared BCL stream; the group baseline.</summary>
    [BenchmarkCategory("FillUInt64_128")]
    [Benchmark(Baseline = true)]
    public void FillUInt64_128_BclShared()
    {
        for (int i = 0; i < _fill.Length; i++)
        {
            _fill[i] = unchecked((ulong)BclRandom.Shared.NextInt64());
        }
    }

    /// <summary>Fills 128 64-bit words from a seeded BCL instance.</summary>
    [BenchmarkCategory("FillUInt64_128")]
    [Benchmark]
    public void FillUInt64_128_BclInstance()
    {
        for (int i = 0; i < _fill.Length; i++)
        {
            _fill[i] = unchecked((ulong)_bclInstance.NextInt64());
        }
    }

    /// <summary>
    /// Enumerates the benchmark methods as name/action pairs for the Native AOT manual harness.
    /// Method groups are direct references, so the plan survives trimming without reflection.
    /// </summary>
    /// <returns>The benchmark plan, in declaration order.</returns>
    public IEnumerable<(string Name, Action Run)> CreateAotPlan()
    {
        yield return (nameof(NextUInt32_OursStatic), NextUInt32_OursStatic);
        yield return (nameof(NextUInt32_OursXoroshiro128), NextUInt32_OursXoroshiro128);
        yield return (nameof(NextUInt32_OursXoshiro256), NextUInt32_OursXoshiro256);
        yield return (nameof(NextUInt32_BclShared), NextUInt32_BclShared);
        yield return (nameof(NextUInt32_BclInstance), NextUInt32_BclInstance);

        yield return (nameof(NextDouble_OursStatic), NextDouble_OursStatic);
        yield return (nameof(NextDouble_OursXoroshiro128), NextDouble_OursXoroshiro128);
        yield return (nameof(NextDouble_OursXoshiro256), NextDouble_OursXoshiro256);
        yield return (nameof(NextDouble_BclShared), NextDouble_BclShared);
        yield return (nameof(NextDouble_BclInstance), NextDouble_BclInstance);

        yield return (nameof(NextBytes64_OursStatic), NextBytes64_OursStatic);
        yield return (nameof(NextBytes64_OursXoroshiro128), NextBytes64_OursXoroshiro128);
        yield return (nameof(NextBytes64_OursXoshiro256), NextBytes64_OursXoshiro256);
        yield return (nameof(NextBytes64_BclShared), NextBytes64_BclShared);
        yield return (nameof(NextBytes64_BclInstance), NextBytes64_BclInstance);

        yield return (nameof(NextBytes1K_OursStatic), NextBytes1K_OursStatic);
        yield return (nameof(NextBytes1K_OursXoroshiro128), NextBytes1K_OursXoroshiro128);
        yield return (nameof(NextBytes1K_OursXoshiro256), NextBytes1K_OursXoshiro256);
        yield return (nameof(NextBytes1K_BclShared), NextBytes1K_BclShared);
        yield return (nameof(NextBytes1K_BclInstance), NextBytes1K_BclInstance);

        yield return (nameof(Shuffle1024_OursStatic), Shuffle1024_OursStatic);
        yield return (nameof(Shuffle1024_OursXoroshiro128), Shuffle1024_OursXoroshiro128);
        yield return (nameof(Shuffle1024_OursXoshiro256), Shuffle1024_OursXoshiro256);
        yield return (nameof(Shuffle1024_BclShared), Shuffle1024_BclShared);
        yield return (nameof(Shuffle1024_BclInstance), Shuffle1024_BclInstance);

        yield return (nameof(FillUInt64_128_OursStatic), FillUInt64_128_OursStatic);
        yield return (nameof(FillUInt64_128_OursXoroshiro128), FillUInt64_128_OursXoroshiro128);
        yield return (nameof(FillUInt64_128_OursXoshiro256), FillUInt64_128_OursXoshiro256);
        yield return (nameof(FillUInt64_128_BclShared), FillUInt64_128_BclShared);
        yield return (nameof(FillUInt64_128_BclInstance), FillUInt64_128_BclInstance);
    }
}
