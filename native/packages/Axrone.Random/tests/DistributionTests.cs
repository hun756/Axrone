using System.Reflection;
using System.Runtime.CompilerServices;

namespace Axrone.Random.Tests;

/// <summary>
/// Statistical and analytic contracts of <see cref="BernoulliDistribution"/>: the empirical mean
/// of a fixed-seed stream, the analytic moments, the degenerate rates, and the shape of its
/// two-point mass function, cumulative function and quantile.
/// </summary>
public class BernoulliDistributionTests
{
    private const ulong Seed = 0x0123456789ABCDEFUL;

    [Fact]
    public void Sample_FixedSeedStream_MeanMatchesTheProbability()
    {
        var distribution = new BernoulliDistribution(0.3);
        Xoshiro256PlusPlus engine = Xoshiro256PlusPlus.Create(Seed);

        const int Samples = 100_000;
        double total = 0.0;
        for (int i = 0; i < Samples; i++)
        {
            total += distribution.Sample(ref engine);
        }

        double mean = total / Samples;

        // sqrt(p(1 - p) / n) is 0.00145, so the requested +/-0.01 band is about seven sigma.
        mean.Should().BeInRange(0.29, 0.31);
        distribution.Mean.Should().Be(0.3);
        distribution.Variance.Should().BeApproximately(0.21, 1e-15);
    }

    [Fact]
    public void Sample_ConsumesExactlyOneDrawPerSample()
    {
        var distribution = new BernoulliDistribution(0.5);

        // One Bernoulli draw is one 53-bit slice of one 64-bit engine step, so a stream of draws
        // must line up draw for draw with a stream of raw doubles off the same seed. If the
        // sampler ever took a second draw - or built its uniform differently - the two streams
        // would decorrelate immediately.
        Xoshiro256PlusPlus bernoulliEngine = Xoshiro256PlusPlus.Create(Seed);
        Xoshiro256PlusPlus rawEngine = Xoshiro256PlusPlus.Create(Seed);

        const int Draws = 10_000;
        int failures = 0;
        for (int i = 0; i < Draws; i++)
        {
            if (distribution.Sample(ref bernoulliEngine) == 0)
            {
                failures++;
            }

            double draw = (Xoshiro256PlusPlus.NextUInt64(ref rawEngine) >> 11) * (1.0 / 9007199254740992.0);
            if (draw >= 0.5)
            {
                failures--;
            }
        }

        failures.Should().Be(0, "the failure counts of the two streams must be identical");
    }

    [Fact]
    public void MassCumulativeAndQuantile_DescribeTheSameTwoPointDistribution()
    {
        var distribution = new BernoulliDistribution(0.3);

        distribution.Probability(0).Should().BeApproximately(0.7, 1e-15);
        distribution.Probability(1).Should().Be(0.3);
        distribution.Probability(-1).Should().Be(0.0);
        distribution.Probability(2).Should().Be(0.0);

        distribution.CumulativeProbability(0).Should().Be(0.0);
        distribution.CumulativeProbability(1).Should().Be(0.3);
        distribution.CumulativeProbability(5).Should().Be(0.3, "the mass is bounded by the two support points");

        int k = distribution.Quantile(0.3);
        k.Should().Be(1);
        distribution.CumulativeProbability(k).Should().BeGreaterThanOrEqualTo(0.3);
        distribution.CumulativeProbability(k - 1).Should().BeLessThan(0.3);
    }

    [Fact]
    public void Quantile_OfTheDegenerateRatesInvertsToTheirPointMass()
    {
        new BernoulliDistribution(0.0).Quantile(0.9).Should().Be(0, "a distribution of all failures reaches any probability at 0");
        new BernoulliDistribution(1.0).Quantile(0.1).Should().Be(1, "a distribution of all successes reaches any probability at 1");
        new BernoulliDistribution(0.0).Quantile(0.0).Should().Be(0);
        new BernoulliDistribution(1.0).Quantile(0.0).Should().Be(0, "probability zero inverts to the lower support point");
    }

    [Fact]
    public void Sample_DegenerateRates_AreConstant()
    {
        Xoshiro256PlusPlus engine = Xoshiro256PlusPlus.Create(Seed);
        var never = new BernoulliDistribution(0.0);
        var always = new BernoulliDistribution(1.0);

        for (int i = 0; i < 10_000; i++)
        {
            never.Sample(ref engine).Should().Be(0);
            always.Sample(ref engine).Should().Be(1);
        }
    }

    [Fact]
    public void Sample_OverAnyEngineCore_MatchesTheDeclaredProbability()
    {
        // The distributions take the engine core, not the wrapper, so any core in the package
        // drives them: three different algorithms, three different streams, one contract.
        WyRand wyrand = WyRand.Create(Seed);
        SplitMix64 splitMix = SplitMix64.Create(Seed);
        MersenneTwister mersenne = MersenneTwister.Create(Seed);

        var bernoulli = new BernoulliDistribution(0.3);
        var geometric = new GeometricDistribution(0.25);
        var poisson = new PoissonDistribution(25.0);

        double wyrandMean = MeanOf<WyRand>(bernoulli, ref wyrand, 50_000);
        double splitMixMean = MeanOf<SplitMix64>(geometric, ref splitMix, 50_000);
        double mersenneMean = MeanOf<MersenneTwister>(poisson, ref mersenne, 20_000);

        // sqrt(p(1 - p) / 50000) is 0.002, sqrt(12 / 50000) is 0.0155 and sqrt(25 / 20000) is
        // 0.035: every band below is at least seven sigma wide.
        wyrandMean.Should().BeInRange(0.28, 0.32);
        splitMixMean.Should().BeInRange(3.9, 4.1);
        mersenneMean.Should().BeInRange(24.0, 26.0);
    }

    /// <summary>Draws <paramref name="samples"/> counts from <paramref name="engine"/> and averages them.</summary>
    private static double MeanOf<TEngine>(BernoulliDistribution distribution, ref TEngine engine, int samples)
        where TEngine : struct, IRandomSource<TEngine>
    {
        double total = 0.0;
        for (int i = 0; i < samples; i++)
        {
            total += distribution.Sample(ref engine);
        }

        return total / samples;
    }

    /// <summary>Draws <paramref name="samples"/> counts from <paramref name="engine"/> and averages them.</summary>
    private static double MeanOf<TEngine>(GeometricDistribution distribution, ref TEngine engine, int samples)
        where TEngine : struct, IRandomSource<TEngine>
    {
        double total = 0.0;
        for (int i = 0; i < samples; i++)
        {
            total += distribution.Sample(ref engine);
        }

        return total / samples;
    }

    /// <summary>Draws <paramref name="samples"/> counts from <paramref name="engine"/> and averages them.</summary>
    private static double MeanOf<TEngine>(PoissonDistribution distribution, ref TEngine engine, int samples)
        where TEngine : struct, IRandomSource<TEngine>
    {
        double total = 0.0;
        for (int i = 0; i < samples; i++)
        {
            total += distribution.Sample(ref engine);
        }

        return total / samples;
    }
}

/// <summary>
/// Contracts of <see cref="GeometricDistribution"/>: the trials-until-first-success
/// parameterisation, its inverse-CDF sampler, the closed-form mass and cumulative functions, and
/// the closed-form quantile.
/// </summary>
public class GeometricDistributionTests
{
    private const ulong Seed = 0x0123456789ABCDEFUL;

    [Fact]
    public void Sample_FixedSeedStream_MeanMatchesOneOverP()
    {
        var distribution = new GeometricDistribution(0.25);
        Xoshiro256PlusPlus engine = Xoshiro256PlusPlus.Create(Seed);

        const int Samples = 50_000;
        double total = 0.0;
        for (int i = 0; i < Samples; i++)
        {
            int sample = distribution.Sample(ref engine);
            sample.Should().BeGreaterThanOrEqualTo(1, "the support starts at the first trial");
            total += sample;
        }

        double mean = total / Samples;

        // sqrt((1 - p) / (p^2 * n)) is 0.0155, so the requested +/-0.1 band is about six sigma.
        mean.Should().BeInRange(3.9, 4.1);
        distribution.Mean.Should().Be(4.0);
        distribution.Variance.Should().Be(12.0);
    }

    [Fact]
    public void Sample_UnitSuccessProbability_IsAlwaysOne()
    {
        var distribution = new GeometricDistribution(1.0);
        Xoshiro256PlusPlus engine = Xoshiro256PlusPlus.Create(Seed);

        for (int i = 0; i < 10_000; i++)
        {
            distribution.Sample(ref engine).Should().Be(1, "p = 1 succeeds on the first trial");
        }

        distribution.CumulativeProbability(1).Should().Be(1.0);
        distribution.Probability(2).Should().Be(0.0);
    }

    [Fact]
    public void MassAndCumulative_MatchTheClosedForms()
    {
        var distribution = new GeometricDistribution(0.5);

        distribution.Probability(0).Should().Be(0.0);
        distribution.Probability(1).Should().Be(0.5);
        distribution.Probability(2).Should().Be(0.25);
        distribution.Probability(3).Should().Be(0.125);

        distribution.CumulativeProbability(0).Should().Be(0.0);
        distribution.CumulativeProbability(1).Should().BeApproximately(0.5, 1e-15);
        distribution.CumulativeProbability(3).Should().BeApproximately(0.875, 1e-15);

        var quarter = new GeometricDistribution(0.25);
        quarter.CumulativeProbability(1).Should().BeApproximately(0.25, 1e-15);
        quarter.CumulativeProbability(10).Should().BeApproximately(1.0 - Math.Pow(0.75, 10), 1e-14);
    }

    [Fact]
    public void CumulativeProbability_TinyRate_KeepsItsSmallMass()
    {
        // The cancellation guard: a direct 1 - (1 - p)^k loses the whole mass once p drops under
        // the epsilon at 1, which is exactly where the closed form stops being representable.
        const double Rate = 1e-9;
        var distribution = new GeometricDistribution(Rate);

        distribution.CumulativeProbability(1).Should().BeApproximately(Rate, 1e-22);
        distribution.CumulativeProbability(2).Should().BeApproximately((2 * Rate) - (Rate * Rate), 1e-22, "F(2) = 2p - p^2");
        distribution.CumulativeProbability(3).Should().BeApproximately((3 * Rate) - (3 * Rate * Rate), 1e-21);
    }

    [Fact]
    public void Quantile_InvertsTheCumulativeFunction()
    {
        var distribution = new GeometricDistribution(0.5);

        const double Target = 0.6;
        int k = distribution.Quantile(Target);
        k.Should().Be(2);
        distribution.CumulativeProbability(k).Should().BeGreaterThanOrEqualTo(Target);
        distribution.CumulativeProbability(k - 1).Should().BeLessThan(Target);

        distribution.Quantile(0.0).Should().Be(1, "probability zero clamps to the lower support point");
        distribution.Quantile(1.0).Should().Be(int.MaxValue, "the unbounded support saturates the representable end");
    }

    [Fact]
    public void Quantile_IsMonotoneInTheTarget()
    {
        var distribution = new GeometricDistribution(0.25);
        int previous = 0;

        for (int step = 1; step <= 20; step++)
        {
            int k = distribution.Quantile(step / 20.0);
            k.Should().BeGreaterThanOrEqualTo(previous, "the inverse of a monotone CDF is monotone");
            previous = k;
        }
    }
}

/// <summary>
/// Contracts of <see cref="BinomialDistribution"/>: both sampling paths - the exact Bernoulli
/// summation and the normal approximation - plus the log-space mass function, the tail-symmetric
/// cumulative function and the binary-search quantile.
/// </summary>
public class BinomialDistributionTests
{
    private const ulong Seed = 0x0123456789ABCDEFUL;

    [Fact]
    public void Sample_ExactPath_FixedSeedStream_MeanMatchesTrialsTimesP()
    {
        // 20 trials is inside ExactSamplingTrialLimit, so this exercises the summation.
        var distribution = new BinomialDistribution(20, 0.35);
        Xoshiro256PlusPlus engine = Xoshiro256PlusPlus.Create(Seed);

        const int Samples = 50_000;
        double total = 0.0;
        for (int i = 0; i < Samples; i++)
        {
            int sample = distribution.Sample(ref engine);
            sample.Should().BeInRange(0, 20);
            total += sample;
        }

        // sqrt(20 * 0.35 * 0.65 / n) is 0.0095, so +/-0.03 is about three sigma.
        (total / Samples).Should().BeInRange(6.97, 7.03);
    }

    [Fact]
    public void Sample_NormalApproximationPath_FixedSeedStream_MeanMatchesTrialsTimesP()
    {
        // 100 trials is above ExactSamplingTrialLimit, so this exercises Box-Muller over the
        // by-ref engine: two unit draws and one clamp, no summation.
        var distribution = new BinomialDistribution(100, 0.5);
        100.Should().BeGreaterThan(BinomialDistribution.ExactSamplingTrialLimit);

        Xoshiro256PlusPlus engine = Xoshiro256PlusPlus.Create(Seed);

        const int Samples = 20_000;
        double total = 0.0;
        for (int i = 0; i < Samples; i++)
        {
            int sample = distribution.Sample(ref engine);
            sample.Should().BeInRange(0, 100);
            total += sample;
        }

        // sqrt(100 * 0.25 / n) is 0.035, so the requested +/-1 band is about 28 sigma.
        (total / Samples).Should().BeInRange(49.0, 51.0);
        distribution.Mean.Should().Be(50.0);
        distribution.Variance.Should().Be(25.0);
    }

    [Fact]
    public void Sample_LargeTrialCount_StaysInsideTheSupportAndOnTheMean()
    {
        var distribution = new BinomialDistribution(1_000, 0.5);
        Xoshiro256PlusPlus engine = Xoshiro256PlusPlus.Create(Seed);

        const int Samples = 20_000;
        double total = 0.0;
        double totalSquares = 0.0;
        for (int i = 0; i < Samples; i++)
        {
            int sample = distribution.Sample(ref engine);
            sample.Should().BeInRange(0, 1_000);
            total += sample;
            totalSquares += (double)sample * sample;
        }

        double mean = total / Samples;
        double deviation = Math.Sqrt((totalSquares / Samples) - (mean * mean));

        mean.Should().BeInRange(495.0, 505.0, "the normal approximation is unbiased at the mean");
        deviation.Should().BeInRange(15.0, 16.5, "sqrt(1000 * 0.25) is 15.81; the approximation rounds a little wide");
    }

    [Fact]
    public void Mass_MatchesTheClosedForms()
    {
        var single = new BinomialDistribution(1, 0.5);
        single.Probability(1).Should().Be(0.5);
        single.Probability(0).Should().Be(0.5);
        single.Probability(2).Should().Be(0.0);
        single.Probability(-1).Should().Be(0.0);

        // The interior masses run through four log-gammas in log space.
        var ten = new BinomialDistribution(10, 0.3);
        ten.Probability(3).Should().BeApproximately(120 * Math.Pow(0.3, 3) * Math.Pow(0.7, 7), 1e-14);
        ten.Probability(0).Should().BeApproximately(Math.Pow(0.7, 10), 1e-16);
        ten.Probability(10).Should().BeApproximately(Math.Pow(0.3, 10), 1e-18);

        // Degenerate rates collapse to their point masses.
        var never = new BinomialDistribution(10, 0.0);
        never.Probability(0).Should().Be(1.0);
        never.Probability(5).Should().Be(0.0);

        var always = new BinomialDistribution(10, 1.0);
        always.Probability(10).Should().Be(1.0);
        always.Probability(9).Should().Be(0.0);

        var noTrials = new BinomialDistribution(0, 0.5);
        noTrials.Probability(0).Should().Be(1.0);
        noTrials.Mean.Should().Be(0.0);
    }

    [Fact]
    public void Mass_SumsToOneAcrossTheSupport()
    {
        var distribution = new BinomialDistribution(40, 0.25);
        double total = 0.0;
        for (int k = 0; k <= 40; k++)
        {
            total += distribution.Probability(k);
        }

        total.Should().BeApproximately(1.0, 1e-12);
    }

    [Fact]
    public void CumulativeProbability_IsMonotoneAndClampedAtTheSupportEnds()
    {
        var distribution = new BinomialDistribution(20, 0.3);

        distribution.CumulativeProbability(-1).Should().Be(0.0);
        distribution.CumulativeProbability(20).Should().Be(1.0);
        distribution.CumulativeProbability(25).Should().Be(1.0);

        double previous = -1.0;
        for (int k = 0; k <= 20; k++)
        {
            double value = distribution.CumulativeProbability(k);
            value.Should().BeGreaterThanOrEqualTo(previous, $"F is non-decreasing at k = {k}");
            value.Should().BeInRange(0.0, 1.0);
            previous = value;
        }

        previous.Should().BeApproximately(1.0, 1e-12);
    }

    [Fact]
    public void CumulativeProbability_UpperHalfUsesTheComplementaryTail()
    {
        // Above the midpoint the CDF is evaluated as 1 - F(n - k - 1; n, 1 - p); the result must
        // still be the sum of the masses below k, not a different distribution's tail.
        var distribution = new BinomialDistribution(10, 0.3);

        double head = 0.0;
        for (int k = 0; k <= 7; k++)
        {
            head += distribution.Probability(k);
        }

        distribution.CumulativeProbability(7).Should().BeApproximately(head, 1e-14);
    }

    [Fact]
    public void CumulativeProbability_SymmetricRate_PairsToOne()
    {
        var distribution = new BinomialDistribution(100, 0.5);

        // p = 0.5 makes the distribution symmetric about n/2, so F(49) and F(50) straddle one.
        distribution.CumulativeProbability(49).Should().BeApproximately(0.460205, 1e-5);
        distribution.CumulativeProbability(50).Should().BeApproximately(0.539795, 1e-5);
        (distribution.CumulativeProbability(49) + distribution.CumulativeProbability(50)).Should().BeApproximately(1.0, 1e-12);
    }

    [Fact]
    public void Quantile_InvertsTheCumulativeFunction()
    {
        var distribution = new BinomialDistribution(100, 0.5);

        const double Target = 0.5;
        int k = distribution.Quantile(Target);
        k.Should().Be(50);
        distribution.CumulativeProbability(k).Should().BeGreaterThanOrEqualTo(Target);
        distribution.CumulativeProbability(k - 1).Should().BeLessThan(Target);

        distribution.Quantile(0.0).Should().Be(0, "the lower support point holds all zero-success mass");
        distribution.Quantile(1.0).Should().Be(100, "a bounded support makes F(n) = 1 exact");
    }
}

/// <summary>
/// Contracts of <see cref="PoissonDistribution"/>: Knuth's loop below the PTRS threshold,
/// Hörmann's rejection above it, the log-space mass function, the shorter-tail cumulative function
/// and the binary-search quantile.
/// </summary>
public class PoissonDistributionTests
{
    private const ulong Seed = 0x0123456789ABCDEFUL;

    [Fact]
    public void Sample_KnuthPath_FixedSeedStream_MeanMatchesTheRate()
    {
        var distribution = new PoissonDistribution(4.0);
        PoissonDistribution.PtrsThreshold.Should().Be(10.0, "4 is below the PTRS crossover");

        Xoshiro256PlusPlus engine = Xoshiro256PlusPlus.Create(Seed);

        const int Samples = 20_000;
        double total = 0.0;
        for (int i = 0; i < Samples; i++)
        {
            int sample = distribution.Sample(ref engine);
            sample.Should().BeGreaterThanOrEqualTo(0);
            total += sample;
        }

        // sqrt(lambda / n) is 0.014, so the requested +/-0.1 band is about seven sigma.
        (total / Samples).Should().BeInRange(3.9, 4.1);
        distribution.Mean.Should().Be(4.0);
        distribution.Variance.Should().Be(4.0);
    }

    [Fact]
    public void Sample_PtrsPath_FixedSeedStream_MeanAndVarianceMatchTheRate()
    {
        var distribution = new PoissonDistribution(25.0);
        Xoshiro256PlusPlus engine = Xoshiro256PlusPlus.Create(Seed);

        const int Samples = 20_000;
        double total = 0.0;
        double totalSquares = 0.0;
        for (int i = 0; i < Samples; i++)
        {
            int sample = distribution.Sample(ref engine);
            sample.Should().BeGreaterThanOrEqualTo(0);
            total += sample;
            totalSquares += (double)sample * sample;
        }

        double mean = total / Samples;
        double variance = (totalSquares / Samples) - (mean * mean);

        // sqrt(lambda / n) is 0.035, so the requested +/-1 band on the mean is about 28 sigma.
        mean.Should().BeInRange(24.0, 26.0);
        variance.Should().BeInRange(23.0, 27.0, "a Poisson has variance equal to its mean");
    }

    [Fact]
    public void Sample_PtrsPath_StaysCorrectAtAnExtremeRate()
    {
        // PTRS is a rejection method: a rate three orders of magnitude above the crossover still
        // has to terminate, land inside the support and keep its mean.
        var distribution = new PoissonDistribution(1_000_000.0);
        Xoshiro256PlusPlus engine = Xoshiro256PlusPlus.Create(Seed ^ 99UL);

        double total = 0.0;
        for (int i = 0; i < 2_000; i++)
        {
            int sample = distribution.Sample(ref engine);
            sample.Should().BeGreaterThan(0);
            total += sample;
        }

        (total / 2_000).Should().BeInRange(995_000.0, 1_005_000.0);
    }

    [Fact]
    public void Sample_ZeroRate_IsAlwaysZero()
    {
        var distribution = new PoissonDistribution(0.0);
        Xoshiro256PlusPlus engine = Xoshiro256PlusPlus.Create(Seed);

        for (int i = 0; i < 10_000; i++)
        {
            distribution.Sample(ref engine).Should().Be(0);
        }

        distribution.Mean.Should().Be(0.0);
        distribution.Probability(0).Should().Be(1.0);
        distribution.Probability(1).Should().Be(0.0);
        distribution.CumulativeProbability(0).Should().Be(1.0);
    }

    [Fact]
    public void Mass_MatchesTheClosedForms()
    {
        var one = new PoissonDistribution(1.0);

        // P(0) = exp(-lambda) exactly: the empty-count mass skips the log space entirely.
        one.Probability(0).Should().Be(Math.Exp(-1.0));
        one.Probability(-1).Should().Be(0.0);

        var four = new PoissonDistribution(4.0);
        four.Probability(2).Should().BeApproximately((Math.Exp(-4.0) * 16.0) / 2.0, 1e-15);
        four.Probability(7).Should().BeApproximately((Math.Exp(-4.0) * Math.Pow(4.0, 7)) / 5040.0, 1e-15);
        four.Probability(400).Should().Be(0.0, "a mass under the double epsilon underflows to zero, never to infinity");
    }

    [Fact]
    public void Mass_SumsToOneAcrossTheBulk()
    {
        var distribution = new PoissonDistribution(3.0);
        double total = 0.0;
        for (int k = 0; k <= 30; k++)
        {
            total += distribution.Probability(k);
        }

        total.Should().BeApproximately(1.0, 1e-12);
    }

    [Fact]
    public void CumulativeProbability_AboveTheMean_MatchesTheSumOfTheMasses()
    {
        // Above the mean the CDF sums the geometric tail instead; the result must still equal the
        // head sum, so the reference is recomputed here mass by mass.
        var distribution = new PoissonDistribution(4.0);

        int[] probes = [0, 2, 3, 4, 9, 25];
        foreach (int k in probes)
        {
            double reference = 0.0;
            for (int i = 0; i <= k; i++)
            {
                reference += Math.Exp((-4.0) + (i * Math.Log(4.0)) - DistributionMath.LogGamma(i + 1.0));
            }

            distribution.CumulativeProbability(k).Should().BeApproximately(reference, 1e-13, $"F({k}) must equal the head sum");
        }
    }

    [Fact]
    public void CumulativeProbability_IsMonotoneAndClampedAtZero()
    {
        var distribution = new PoissonDistribution(6.0);

        distribution.CumulativeProbability(-1).Should().Be(0.0);

        double previous = 0.0;
        for (int k = 0; k <= 40; k++)
        {
            double value = distribution.CumulativeProbability(k);
            value.Should().BeGreaterThanOrEqualTo(previous, $"F is non-decreasing at k = {k}");
            value.Should().BeInRange(0.0, 1.0);
            previous = value;
        }

        distribution.CumulativeProbability(int.MaxValue).Should().Be(1.0);
    }

    [Fact]
    public void Quantile_InvertsTheCumulativeFunction()
    {
        var distribution = new PoissonDistribution(4.0);

        const double Target = 0.5;
        int k = distribution.Quantile(Target);
        k.Should().Be(4);
        distribution.CumulativeProbability(k).Should().BeGreaterThanOrEqualTo(Target);
        distribution.CumulativeProbability(k - 1).Should().BeLessThan(Target);

        distribution.Quantile(0.0).Should().Be(0, "zero is the lower support point");
        new PoissonDistribution(25.0).Quantile(0.5).Should().Be(25);
    }
}

/// <summary>
/// Contracts shared by all four distributions: the Lanczos log-gamma the Poisson and binomial
/// query paths rest on, constructor validation, seeded determinism, the equivalence of the batch
/// and single-sample APIs, and the allocation and inlining promises of the hot paths.
/// </summary>
public class DistributionContractTests
{
    private const ulong Seed = 0x0123456789ABCDEFUL;

    [Fact]
    public void LogGamma_MatchesTheIntegerFacts()
    {
        DistributionMath.LogGamma(1.0).Should().BeApproximately(0.0, 1e-12, "Gamma(1) = 1");
        DistributionMath.LogGamma(2.0).Should().BeApproximately(0.0, 1e-12, "Gamma(2) = 1");
        DistributionMath.LogGamma(5.0).Should().BeApproximately(Math.Log(24.0), 1e-12, "Gamma(5) = 4!");
        DistributionMath.LogGamma(11.0).Should().BeApproximately(Math.Log(3628800.0), 1e-11, "Gamma(11) = 10!");
        DistributionMath.LogGamma(0.5).Should().BeApproximately(0.5 * Math.Log(Math.PI), 1e-12, "Gamma(0.5) = sqrt(pi)");

        // Every integer factorial up to 12!, checked against an independent running product.
        double factorial = 0.0;
        for (int n = 1; n <= 12; n++)
        {
            factorial = n == 1 ? 1.0 : factorial * n;
            DistributionMath.LogGamma(n + 1.0).Should().BeApproximately(Math.Log(factorial), 1e-11, $"Gamma({n + 1})");
        }
    }

    [Fact]
    public void LogGamma_BelowAHalf_ObeysTheReflectionIdentity()
    {
        // The reflection branch only runs below 0.5; Gamma(x) * Gamma(1 - x) = pi / sin(pi x)
        // pins it against the direct branch at 1 - x.
        double expected = Math.Log(Math.PI) + (0.5 * Math.Log(2.0)); // Gamma(0.25) * Gamma(0.75) = pi * sqrt(2)
        (DistributionMath.LogGamma(0.25) + DistributionMath.LogGamma(0.75)).Should().BeApproximately(expected, 1e-12);

        DistributionMath.LogGamma(0.0).Should().Be(double.PositiveInfinity, "the pole at zero is a limit, not a number");
        double.IsNaN(DistributionMath.LogGamma(double.NaN)).Should().BeTrue();
    }

    [Fact]
    public void Constructors_RejectOutOfRangeParameters()
    {
        Action negativeLambda = () => _ = new PoissonDistribution(-1.0);
        negativeLambda.Should().Throw<ArgumentOutOfRangeException>();

        Action nanLambda = () => _ = new PoissonDistribution(double.NaN);
        nanLambda.Should().Throw<ArgumentOutOfRangeException>();

        Action infiniteLambda = () => _ = new PoissonDistribution(double.PositiveInfinity);
        infiniteLambda.Should().Throw<ArgumentOutOfRangeException>();

        Action negativeTrials = () => _ = new BinomialDistribution(-1, 0.5);
        negativeTrials.Should().Throw<ArgumentOutOfRangeException>();

        Action tooLargeP = () => _ = new BinomialDistribution(10, 1.5);
        tooLargeP.Should().Throw<ArgumentOutOfRangeException>();

        Action negativeP = () => _ = new BinomialDistribution(10, -0.1);
        negativeP.Should().Throw<ArgumentOutOfRangeException>();

        Action nanP = () => _ = new BernoulliDistribution(double.NaN);
        nanP.Should().Throw<ArgumentOutOfRangeException>();

        Action bernoulliAboveOne = () => _ = new BernoulliDistribution(1.0001);
        bernoulliAboveOne.Should().Throw<ArgumentOutOfRangeException>();

        Action geometricZero = () => _ = new GeometricDistribution(0.0);
        geometricZero.Should().Throw<ArgumentOutOfRangeException>("p = 0 has no support and would loop forever");

        Action geometricAboveOne = () => _ = new GeometricDistribution(1.5);
        geometricAboveOne.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Quantile_RejectsProbabilitiesOutsideTheUnitInterval()
    {
        var bernoulli = new BernoulliDistribution(0.5);
        var binomial = new BinomialDistribution(10, 0.5);
        var geometric = new GeometricDistribution(0.5);
        var poisson = new PoissonDistribution(4.0);

        double[] invalidProbabilities = [-0.001, 1.001, double.NaN];
        foreach (double invalid in invalidProbabilities)
        {
            Action bernoulliAct = () => bernoulli.Quantile(invalid);
            bernoulliAct.Should().Throw<ArgumentOutOfRangeException>();

            Action binomialAct = () => binomial.Quantile(invalid);
            binomialAct.Should().Throw<ArgumentOutOfRangeException>();

            Action geometricAct = () => geometric.Quantile(invalid);
            geometricAct.Should().Throw<ArgumentOutOfRangeException>();

            Action poissonAct = () => poisson.Quantile(invalid);
            poissonAct.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    [Fact]
    public void Sample_SameSeed_ReplaysIdenticalSequences()
    {
        const int Draws = 1_000;

        Xoshiro256PlusPlus bernoulliFirst = Xoshiro256PlusPlus.Create(Seed);
        Xoshiro256PlusPlus bernoulliSecond = Xoshiro256PlusPlus.Create(Seed);
        Xoshiro256PlusPlus geometricFirst = Xoshiro256PlusPlus.Create(Seed);
        Xoshiro256PlusPlus geometricSecond = Xoshiro256PlusPlus.Create(Seed);
        Xoshiro256PlusPlus binomialFirst = Xoshiro256PlusPlus.Create(Seed);
        Xoshiro256PlusPlus binomialSecond = Xoshiro256PlusPlus.Create(Seed);
        Xoshiro256PlusPlus poissonFirst = Xoshiro256PlusPlus.Create(Seed);
        Xoshiro256PlusPlus poissonSecond = Xoshiro256PlusPlus.Create(Seed);

        var bernoulli = new BernoulliDistribution(0.3);
        var geometric = new GeometricDistribution(0.25);
        var binomial = new BinomialDistribution(100, 0.5);
        var poisson = new PoissonDistribution(4.0);

        for (int i = 0; i < Draws; i++)
        {
            bernoulli.Sample(ref bernoulliFirst).Should().Be(bernoulli.Sample(ref bernoulliSecond));
            geometric.Sample(ref geometricFirst).Should().Be(geometric.Sample(ref geometricSecond));
            binomial.Sample(ref binomialFirst).Should().Be(binomial.Sample(ref binomialSecond));
            poisson.Sample(ref poissonFirst).Should().Be(poisson.Sample(ref poissonSecond));
        }
    }

    [Fact]
    public void Sample_DifferentSeeds_Diverge()
    {
        var poisson = new PoissonDistribution(4.0);

        Xoshiro256PlusPlus first = Xoshiro256PlusPlus.Create(Seed);
        Xoshiro256PlusPlus second = Xoshiro256PlusPlus.Create(Seed + 1);

        int identical = 0;
        for (int i = 0; i < 1_000; i++)
        {
            if (poisson.Sample(ref first) == poisson.Sample(ref second))
            {
                identical++;
            }
        }

        identical.Should().BeLessThan(600, "two streams cannot agree on two thirds of a thousand counts");
    }

    [Fact]
    public void SampleMany_EqualsTheSingleSampleLoop()
    {
        const int Count = 4_096;

        var bernoulli = new BernoulliDistribution(0.3);
        var geometric = new GeometricDistribution(0.25);
        var binomial = new BinomialDistribution(100, 0.5);
        var poisson = new PoissonDistribution(4.0);
        var ptrs = new PoissonDistribution(25.0);

        int[] buffer = new int[Count];

        Xoshiro256PlusPlus batchEngine = Xoshiro256PlusPlus.Create(Seed);
        Xoshiro256PlusPlus loopEngine = Xoshiro256PlusPlus.Create(Seed);

        AssertBatchMatchesLoop(bernoulli, ref batchEngine, ref loopEngine, buffer);
        AssertBatchMatchesLoop(geometric, ref batchEngine, ref loopEngine, buffer);
        AssertBatchMatchesLoop(binomial, ref batchEngine, ref loopEngine, buffer);
        AssertBatchMatchesLoop(poisson, ref batchEngine, ref loopEngine, buffer);
        AssertBatchMatchesLoop(ptrs, ref batchEngine, ref loopEngine, buffer);
    }

    /// <summary>Fills the buffer by batch, then replays the same engine by hand, element by element.</summary>
    private static void AssertBatchMatchesLoop(BernoulliDistribution distribution, ref Xoshiro256PlusPlus batchEngine, ref Xoshiro256PlusPlus loopEngine, int[] buffer)
    {
        distribution.SampleMany(ref batchEngine, buffer);
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i].Should().Be(distribution.Sample(ref loopEngine), $"element {i}");
        }
    }

    /// <summary>Fills the buffer by batch, then replays the same engine by hand, element by element.</summary>
    private static void AssertBatchMatchesLoop(GeometricDistribution distribution, ref Xoshiro256PlusPlus batchEngine, ref Xoshiro256PlusPlus loopEngine, int[] buffer)
    {
        distribution.SampleMany(ref batchEngine, buffer);
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i].Should().Be(distribution.Sample(ref loopEngine), $"element {i}");
        }
    }

    /// <summary>Fills the buffer by batch, then replays the same engine by hand, element by element.</summary>
    private static void AssertBatchMatchesLoop(BinomialDistribution distribution, ref Xoshiro256PlusPlus batchEngine, ref Xoshiro256PlusPlus loopEngine, int[] buffer)
    {
        distribution.SampleMany(ref batchEngine, buffer);
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i].Should().Be(distribution.Sample(ref loopEngine), $"element {i}");
        }
    }

    /// <summary>Fills the buffer by batch, then replays the same engine by hand, element by element.</summary>
    private static void AssertBatchMatchesLoop(PoissonDistribution distribution, ref Xoshiro256PlusPlus batchEngine, ref Xoshiro256PlusPlus loopEngine, int[] buffer)
    {
        distribution.SampleMany(ref batchEngine, buffer);
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i].Should().Be(distribution.Sample(ref loopEngine), $"element {i}");
        }
    }

    [Fact]
    public void SampleMany_EmptySpan_IsANoOpAndConsumesNoDraw()
    {
        var poisson = new PoissonDistribution(25.0);

        Xoshiro256PlusPlus untouched = Xoshiro256PlusPlus.Create(Seed);
        Xoshiro256PlusPlus reference = Xoshiro256PlusPlus.Create(Seed);

        poisson.SampleMany(ref untouched, Span<int>.Empty);

        // An empty batch writes nothing and advances nothing, so the engine still replays the
        // very first draw of its stream. Had it consumed a draw, the two would disagree.
        poisson.Sample(ref untouched).Should().Be(poisson.Sample(ref reference));
    }

    [Fact]
    public void SampleMany_AllocatesNothingOnTheHotPath()
    {
        var bernoulli = new BernoulliDistribution(0.3);
        var geometric = new GeometricDistribution(0.25);
        var binomial = new BinomialDistribution(100, 0.5);
        var poisson = new PoissonDistribution(4.0);
        var ptrs = new PoissonDistribution(25.0);

        int[] buffer = new int[4_096];
        Xoshiro256PlusPlus engine = Xoshiro256PlusPlus.Create(Seed);

        // Warm every generic instantiation and every tiered-JIT first call before the counter
        // starts, so the measurement sees steady-state code only.
        for (int round = 0; round < 8; round++)
        {
            bernoulli.SampleMany(ref engine, buffer);
            geometric.SampleMany(ref engine, buffer);
            binomial.SampleMany(ref engine, buffer);
            poisson.SampleMany(ref engine, buffer);
            ptrs.SampleMany(ref engine, buffer);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int round = 0; round < 8; round++)
        {
            bernoulli.SampleMany(ref engine, buffer);
            geometric.SampleMany(ref engine, buffer);
            binomial.SampleMany(ref engine, buffer);
            poisson.SampleMany(ref engine, buffer);
            ptrs.SampleMany(ref engine, buffer);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0, "163,840 samples across five distributions allocate nothing");
    }

    [Fact]
    public void HotPaths_AreReadonlyStructsWithInlinedEntryPoints()
    {
        Type[] distributions =
        [
            typeof(BernoulliDistribution),
            typeof(BinomialDistribution),
            typeof(GeometricDistribution),
            typeof(PoissonDistribution),
        ];

        foreach (Type type in distributions)
        {
            type.IsValueType.Should().BeTrue();
            type.GetCustomAttributes(typeof(IsReadOnlyAttribute), false).Should().HaveCount(1, $"{type.Name} is a readonly struct");

            MethodInfo sample = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(method => method.Name == "Sample" && method.IsGenericMethodDefinition);
            MethodImplAttributes attributes = sample.GetMethodImplementationFlags();

            attributes.HasFlag(MethodImplAttributes.AggressiveInlining).Should().BeTrue($"{type.Name}.Sample is aggressively inlined");
            attributes.HasFlag(MethodImplAttributes.AggressiveOptimization).Should().BeTrue($"{type.Name}.Sample is optimized for the hot path");

            MethodInfo sampleMany = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(method => method.Name == "SampleMany");
            sampleMany.GetParameters()[1].ParameterType.Should().Be(typeof(Span<int>), $"{type.Name} fills a concrete count span");
        }
    }
}