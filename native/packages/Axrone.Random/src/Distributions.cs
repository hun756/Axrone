namespace Axrone.Random;

/// <summary>
/// Package-private numeric core shared by the discrete distributions: the 53-bit unit draw
/// off any <see cref="IRandomSource{TSelf}"/> engine, the open-interval variant the inverse-CDF
/// and Box-Muller paths need, the unit-interval guard and the Lanczos log-gamma.
/// </summary>
/// <remarks>
/// Everything here is either a few ALU operations or a handful of transcendentals over a
/// value-type engine passed by <see langword="ref"/>: no allocation, no heap round trip and
/// nothing that reflects over the type parameter, so the distributions stay AOT/trim clean.
/// </remarks>
internal static class DistributionMath
{
    /// <summary>1 / 2^53: the scaling that turns the top 53 bits of a 64-bit draw into [0, 1).</summary>
    private const double InverseTwoTo53 = 1.0 / 9007199254740992.0;

    /// <summary>
    /// Advances <paramref name="engine"/> by one 64-bit step and scales the top 53 bits into
    /// <c>[0, 1)</c> - the same 53-bit construction <see cref="RandomEngine{TEngine}.NextDouble()"/>
    /// uses, reached through the static-abstract shape of <see cref="IRandomSource{TSelf}"/> so
    /// the distributions never depend on the wrapper type.
    /// </summary>
    /// <typeparam name="TEngine">The engine core; the state is advanced in place.</typeparam>
    /// <param name="engine">The engine state to advance. It is a by-ref local, never a field copy.</param>
    /// <returns>A value in <c>[0, 1)</c>; the value 1 is never produced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static double NextUnitDouble<TEngine>(ref TEngine engine)
        where TEngine : struct, IRandomSource<TEngine> =>
        (TEngine.NextUInt64(ref engine) >> 11) * InverseTwoTo53;

    /// <summary>
    /// Advances <paramref name="engine"/> and returns a unit draw in the open interval
    /// <c>(0, 1)</c>, which is what a logarithm of a uniform draw requires.
    /// </summary>
    /// <typeparam name="TEngine">The engine core; the state is advanced in place.</typeparam>
    /// <param name="engine">The engine state to advance. It is a by-ref local, never a field copy.</param>
    /// <returns>A value in <c>(0, 1)</c>.</returns>
    /// <remarks>
    /// The all-zero 64-bit word maps to exactly <c>0.0</c>, and a strict inverse-CDF or the
    /// Box-Muller radius would then take <c>log(0)</c>. The closed endpoint is nudged to the
    /// smallest positive double instead: it perturbs one draw in 2^53 and saves a rejection loop
    /// on every call site that would otherwise have to guard for it.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static double NextOpenUnitDouble<TEngine>(ref TEngine engine)
        where TEngine : struct, IRandomSource<TEngine>
    {
        double unit = NextUnitDouble(ref engine);
        return unit > 0.0 ? unit : double.Epsilon;
    }

    /// <summary>Rejects a probability that is NaN or outside the closed unit interval.</summary>
    /// <param name="value">The candidate probability.</param>
    /// <param name="paramName">The name of the rejected parameter.</param>
    internal static void ValidateUnitInterval(double value, string paramName)
    {
        if (double.IsNaN(value) || value < 0.0 || value > 1.0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(paramName, "The probability must be within [0, 1].");
        }
    }

    /// <summary>
    /// Evaluates <c>log(1 - value)</c> for <paramref name="value"/> in <c>[0, 1]</c> without
    /// losing the argument to cancellation.
    /// </summary>
    /// <param name="value">The complement to log; <c>1</c> yields <c>-inf</c>.</param>
    /// <returns><c>log(1 - value)</c>, exact to full double precision over the whole range.</returns>
    /// <remarks>
    /// Once <paramref name="value"/> drops under the epsilon at 1, the subtraction <c>1 - value</c>
    /// rounds to exactly 1 and a direct logarithm returns <c>0</c> instead of <c>-value</c> - a
    /// total loss of the argument, not a rounding error. Below the cutoff the truncated series
    /// <c>-x - x^2/2 - x^3/3 - x^4/4</c> carries the value instead; its truncation error at the
    /// cutoff is below <c>1e-20</c> relative. Above the cutoff the subtraction is exact and the
    /// logarithm is the cheaper, better-conditioned operation.
    /// </remarks>
    internal static double LogOneMinus(double value) =>
        value < 1e-4
            ? -value * (1.0 + (value * (0.5 + (value * (0.33333333333333331 + (value * 0.25))))))
            : Math.Log(1.0 - value);

    /// <summary>
    /// Evaluates <c>1 - e^exponent</c> for a non-positive <paramref name="exponent"/> without
    /// losing the exponent to cancellation.
    /// </summary>
    /// <param name="exponent">The exponent; must be non-positive.</param>
    /// <returns>The complement of the exponential, accurate as the exponent goes to zero.</returns>
    /// <remarks>
    /// Near zero the subtraction <c>1 - e^t</c> cancels away most of the significant digits, and
    /// the truncated series <c>t + t^2/2 + t^3/6</c> restores them; its truncation error at the
    /// cutoff is below <c>1e-24</c> relative.
    /// </remarks>
    internal static double OneMinusExp(double exponent) =>
        exponent > -1e-8
            ? -exponent * (1.0 + (exponent * (0.5 + (exponent / 6.0))))
            : 1.0 - Math.Exp(exponent);

    /// <summary>
    /// Evaluates <c>log Gamma(x)</c> with the Lanczos approximation at <c>g = 7</c>
    /// (Numerical Recipes, 14 coefficients reduced to the nine published ones).
    /// </summary>
    /// <param name="value">The argument. It must be finite and non-negative - the whole positive axis,
    /// which is every argument the distributions ever pass (a count plus one, or nothing at all
    /// for the degenerate rates).</param>
    /// <returns>The natural logarithm of the gamma function.</returns>
    /// <remarks>
    /// <para>The coefficients are a local read-only span, so the table lands in the assembly's
    /// data section instead of a static array that a trimming pass would have to reason about.</para>
    /// <para>Accuracy is about 1e-15 relative over the whole positive axis, which is well inside
    /// the resolution a PMF evaluated in log space can report.</para>
    /// </remarks>
    internal static double LogGamma(double value)
    {
        ReadOnlySpan<double> coefficients =
        [
            0.99999999999980993,
            676.5203681218851,
            -1259.1392167224028,
            771.32342877765313,
            -176.61502916214059,
            12.507343278686905,
            -0.13857109526572012,
            9.9843695780195716e-6,
            1.5056327351493116e-7,
        ];

        // Reflection: Gamma(x) * Gamma(1 - x) = pi / sin(pi x). It is only taken below 0.5, so
        // the recursive call always lands in the direct branch and cannot recurse again.
        if (value < 0.5)
        {
            return Math.Log(Math.PI / Math.Sin(Math.PI * value)) - LogGamma(1.0 - value);
        }

        double shifted = value - 1.0;
        double sum = coefficients[0];
        for (int i = 1; i < coefficients.Length; i++)
        {
            sum += coefficients[i] / (shifted + i);
        }

        double t = shifted + 7.5; // g + 0.5
        return (0.5 * Math.Log(2.0 * Math.PI)) + ((shifted + 0.5) * Math.Log(t)) - t + Math.Log(sum);
    }
}

/// <summary>
/// Bernoulli(p): the two-point distribution whose support is <c>{0, 1}</c> and whose probability
/// mass function is <c>p^k * (1 - p)^(1 - k)</c>. It is the one-draw leaf every count
/// distribution is built from - <see cref="BinomialDistribution"/> is a summation of it and
/// Poisson's Knuth branch is a multiplication loop over it.
/// </summary>
/// <remarks>
/// <para><b>By-ref sampling is the hoisted form.</b> <see cref="Sample{TEngine}"/> and
/// <see cref="SampleMany{TEngine}"/> take the engine core by <see langword="ref"/>, so the state
/// the loop advances is the caller's own variable: it already lives in a register for the whole
/// batch and there is nothing left to hoist. The hoisting gate
/// (<see cref="RandomConfig.MaxHoistableStateBytes"/>) exists for <see cref="RandomEngine{TEngine}"/>,
/// where the state sits behind a field of a heap-held wrapper and would otherwise be reloaded per
/// draw; by-ref passing removes that indirection instead of paying for it.</para>
/// <para>Cost is one engine step and one comparison per sample: the cheapest surface in the
/// package, and the one to reach for when a spawn, a hit or a loot roll outnumbers the frame cost
/// of everything else.</para>
/// </remarks>
public readonly struct BernoulliDistribution
{
    private readonly double _successProbability;

    /// <summary>Initialises a Bernoulli distribution with the given success probability.</summary>
    /// <param name="successProbability">The probability of the <c>1</c> outcome, in <c>[0, 1]</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="successProbability"/> is NaN or outside <c>[0, 1]</c>.
    /// </exception>
    public BernoulliDistribution(double successProbability)
    {
        DistributionMath.ValidateUnitInterval(successProbability, nameof(successProbability));
        _successProbability = successProbability;
    }

    /// <summary>Gets the expected value, <c>p</c>.</summary>
    public double Mean => _successProbability;

    /// <summary>Gets the variance, <c>p (1 - p)</c>.</summary>
    public double Variance => _successProbability * (1.0 - _successProbability);

    /// <summary>Evaluates the probability mass function.</summary>
    /// <param name="k">The outcome: <c>0</c> for failure, <c>1</c> for success.</param>
    /// <returns>The probability of <paramref name="k"/>, and zero outside <c>{0, 1}</c>.</returns>
    /// <remarks>A cold query path: no engine state is read and no draw is consumed.</remarks>
    public double Probability(int k)
    {
        if (k is < 0 or > 1)
        {
            return 0.0;
        }

        return k == 0 ? 1.0 - _successProbability : _successProbability;
    }

    /// <summary>Evaluates the cumulative distribution function.</summary>
    /// <param name="k">The outcome to evaluate at.</param>
    /// <returns>The probability of drawing <c>0</c> or less: zero below <c>1</c>, else <c>p</c>.</returns>
    public double CumulativeProbability(int k) => k < 1 ? 0.0 : _successProbability;

    /// <summary>Inverts the cumulative distribution function.</summary>
    /// <param name="probability">The target cumulative probability, in <c>[0, 1]</c>.</param>
    /// <returns><c>1</c> for any positive probability, <c>0</c> at exactly zero.</returns>
    /// <remarks>
    /// A two-point support has a single step, so the inverse is degenerate by construction:
    /// <c>F(0) = 0</c> and <c>F(1) = p</c>. The target is therefore first saturated at the largest
    /// cumulative probability the distribution can reach - which is what makes the degenerate
    /// <c>p = 0</c> invert to its own point mass at 0 - and then crosses the step. No search, and
    /// never a value outside the support.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> is NaN or outside <c>[0, 1]</c>.</exception>
    public int Quantile(double probability)
    {
        DistributionMath.ValidateUnitInterval(probability, nameof(probability));
        double target = Math.Min(probability, _successProbability);
        return target <= 0.0 ? 0 : 1;
    }

    /// <summary>Draws one outcome: a single unit draw compared against <c>p</c>.</summary>
    /// <typeparam name="TEngine">The engine core to draw from.</typeparam>
    /// <param name="engine">The engine state to advance. It is mutated in place.</param>
    /// <returns><c>1</c> with probability <c>p</c>, otherwise <c>0</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int Sample<TEngine>(ref TEngine engine)
        where TEngine : struct, IRandomSource<TEngine> =>
        DistributionMath.NextUnitDouble(ref engine) < _successProbability ? 1 : 0;

    /// <summary>Fills the destination with independent outcomes.</summary>
    /// <typeparam name="TEngine">The engine core to draw from.</typeparam>
    /// <param name="engine">The engine state to advance. It is mutated in place.</param>
    /// <param name="destination">The span to overwrite; an empty span is a no-op.</param>
    /// <remarks>
    /// The loop hoists nothing because there is nothing left to hoist: <paramref name="engine"/> is
    /// a by-ref local of this frame, so the state is advanced in the caller's own storage for the
    /// whole batch instead of behind a field of a heap-held wrapper. One engine step and one
    /// comparison per element, zero allocations.
    /// </remarks>
    public void SampleMany<TEngine>(ref TEngine engine, Span<int> destination)
        where TEngine : struct, IRandomSource<TEngine>
    {
        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = Sample(ref engine);
        }
    }
}

/// <summary>
/// Binomial(n, p): the count of successes in <c>n</c> independent Bernoulli trials. Up to
/// <see cref="ExactSamplingTrialLimit"/> trials the sampler is the exact summation of
/// <see cref="BernoulliDistribution"/> outcomes; above it, the normal approximation with a
/// Box-Muller deviate, clamped to the support.
/// </summary>
/// <remarks>
/// <para><b>Exactness has a cost.</b> The summation spends <c>n</c> engine steps and is exact for
/// any <c>n</c>, but <c>n</c> engine steps is a linear bill, and the normal approximation becomes
/// the cheaper approximation long before it stops being accurate. The threshold is where the two
/// costs cross: at <c>n = 64</c> the exact path is 64 steps, and the approximation's error at that
/// size is below the resolution of the count itself.</para>
/// <para><b>By-ref sampling is the hoisted form.</b> Both branches advance the engine through a
/// by-ref parameter, so the batch loop in <see cref="SampleMany{TEngine}"/> keeps the state in the
/// caller's storage rather than reloading it per draw from a field of a heap-held wrapper.</para>
/// <para>The query paths (<see cref="Probability"/>, <see cref="CumulativeProbability"/>,
/// <see cref="Quantile"/>) are pure math evaluated in log space and deliberately make no
/// hot-path claim; the CDF sums the shorter tail, so it costs
/// <c>O(min(k, n - k))</c> terms.</para>
/// </remarks>
public readonly struct BinomialDistribution
{
    /// <summary>
    /// The largest trial count sampled by exact Bernoulli summation; above it the normal
    /// approximation takes over.
    /// </summary>
    public const int ExactSamplingTrialLimit = 64;

    private readonly int _trials;
    private readonly double _successProbability;
    private readonly double _mean;
    private readonly double _standardDeviation;
    private readonly bool _useNormalApproximation;

    /// <summary>Initialises a binomial distribution.</summary>
    /// <param name="trials">The number of independent trials; must not be negative.</param>
    /// <param name="successProbability">The per-trial success probability, in <c>[0, 1]</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="trials"/> is negative, or <paramref name="successProbability"/> is NaN or
    /// outside <c>[0, 1]</c>.
    /// </exception>
    public BinomialDistribution(int trials, double successProbability)
    {
        if (trials < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(trials), "The trial count must not be negative.");
        }

        DistributionMath.ValidateUnitInterval(successProbability, nameof(successProbability));

        _trials = trials;
        _successProbability = successProbability;
        _mean = trials * successProbability;
        _standardDeviation = Math.Sqrt(_mean * (1.0 - successProbability));
        _useNormalApproximation = trials > ExactSamplingTrialLimit;
    }

    /// <summary>Gets the expected value, <c>n p</c>.</summary>
    public double Mean => _mean;

    /// <summary>Gets the variance, <c>n p (1 - p)</c>.</summary>
    public double Variance => _mean * (1.0 - _successProbability);

    /// <summary>Evaluates the probability mass function.</summary>
    /// <param name="k">The number of successes to evaluate at.</param>
    /// <returns>The probability of exactly <paramref name="k"/> successes, zero outside <c>[0, n]</c>.</returns>
    /// <remarks>
    /// Evaluated as <c>exp(lgamma(n + 1) - lgamma(k + 1) - lgamma(n - k + 1) + k log p +
    /// (n - k) log(1 - p))</c>, so neither the binomial coefficient nor the powers overflow. The
    /// two boundary masses take their exact closed form instead, which keeps <c>p^k</c> and
    /// <c>(1 - p)^n</c> free of the log-space rounding the interior masses pay.
    /// </remarks>
    public double Probability(int k) => Mass(_trials, _successProbability, k);

    /// <summary>Evaluates the cumulative distribution function.</summary>
    /// <param name="k">The number of successes to evaluate at.</param>
    /// <returns>The probability of drawing at most <paramref name="k"/> successes.</returns>
    /// <remarks>
    /// A cold summation over <c>0..k</c>, or over the complementary tail through
    /// <c>F(k; n, p) = 1 - F(n - k - 1; n, 1 - p)</c>, whichever covers fewer terms.
    /// </remarks>
    public double CumulativeProbability(int k)
    {
        if (k < 0)
        {
            return 0.0;
        }

        if (k >= _trials)
        {
            return 1.0;
        }

        if ((long)k * 2L <= _trials)
        {
            return Math.Clamp(SumLowerTail(_trials, _successProbability, k), 0.0, 1.0);
        }

        return Math.Clamp(1.0 - SumLowerTail(_trials, 1.0 - _successProbability, _trials - k - 1), 0.0, 1.0);
    }

    /// <summary>Inverts the cumulative distribution function.</summary>
    /// <param name="probability">The target cumulative probability, in <c>[0, 1]</c>.</param>
    /// <returns>The smallest success count whose cumulative probability reaches the target.</returns>
    /// <remarks>
    /// A lower-bound binary search over the support <c>[0, n]</c>. Each probe is one CDF
    /// evaluation, so a quantile costs <c>O(log n)</c> sums - acceptable on a cold query path,
    /// not on a per-frame one. The endpoints are answered directly rather than searched: a
    /// bounded support makes <c>F(0) = 0</c> and <c>F(n) = 1</c> exact, whereas the floating-point
    /// CDF saturates to one several counts early and a search would report that artefact.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> is NaN or outside <c>[0, 1]</c>.</exception>
    public int Quantile(double probability)
    {
        DistributionMath.ValidateUnitInterval(probability, nameof(probability));

        if (probability >= 1.0)
        {
            return _trials;
        }

        int low = 0;
        int high = _trials;
        while (low < high)
        {
            int middle = low + ((high - low) >> 1);
            if (CumulativeProbability(middle) >= probability)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
    }

    /// <summary>Draws one sample.</summary>
    /// <typeparam name="TEngine">The engine core to draw from.</typeparam>
    /// <param name="engine">The engine state to advance. It is mutated in place.</param>
    /// <returns>A number of successes in <c>[0, n]</c>.</returns>
    /// <remarks>
    /// The exact summation and the normal approximation both sit behind this one inlinable
    /// dispatch, so the branch costs a single compare at the call site while the two leaves stay
    /// out of line.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int Sample<TEngine>(ref TEngine engine)
        where TEngine : struct, IRandomSource<TEngine> =>
        _useNormalApproximation ? SampleNormalApproximation(ref engine) : SampleExact(ref engine);

    /// <summary>Fills the destination with independent samples.</summary>
    /// <typeparam name="TEngine">The engine core to draw from.</typeparam>
    /// <param name="engine">The engine state to advance. It is mutated in place.</param>
    /// <param name="destination">The span to overwrite; an empty span is a no-op.</param>
    /// <remarks>
    /// The loop hoists nothing because the engine already arrives by reference: the state is
    /// advanced in the caller's storage for the whole batch, with no field reload per draw and no
    /// allocation. The hoisting gate belongs to <see cref="RandomEngine{TEngine}"/>, whose state
    /// hides behind a field; by-ref passing is that hoist, performed by the parameter itself.
    /// </remarks>
    public void SampleMany<TEngine>(ref TEngine engine, Span<int> destination)
        where TEngine : struct, IRandomSource<TEngine>
    {
        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = Sample(ref engine);
        }
    }

    /// <summary>Sums the masses of <c>0..k</c> for the given parameters.</summary>
    private static double SumLowerTail(int trials, double successProbability, int k)
    {
        double total = 0.0;
        for (int i = 0; i <= k; i++)
        {
            total += Mass(trials, successProbability, i);
        }

        return total;
    }

    /// <summary>Evaluates the mass of <paramref name="k"/> successes in log space.</summary>
    private static double Mass(int trials, double successProbability, int k)
    {
        if (k < 0 || k > trials)
        {
            return 0.0;
        }

        // The degenerate rates short-circuit: log(0) would otherwise turn k * log(p) into NaN.
        if (successProbability <= 0.0)
        {
            return k == 0 ? 1.0 : 0.0;
        }

        if (successProbability >= 1.0)
        {
            return k == trials ? 1.0 : 0.0;
        }

        // The two boundary masses are plain powers; taking the closed form keeps P(0) = (1 - p)^n
        // and P(n) = p^n exact instead of paying the log-space round trip of four log-gammas.
        if (k == 0)
        {
            return Math.Pow(1.0 - successProbability, trials);
        }

        if (k == trials)
        {
            return Math.Pow(successProbability, trials);
        }

        double failures = trials - k;
        double logMass = DistributionMath.LogGamma(trials + 1.0)
            - DistributionMath.LogGamma(k + 1.0)
            - DistributionMath.LogGamma(failures + 1.0)
            + (k * Math.Log(successProbability))
            + (failures * DistributionMath.LogOneMinus(successProbability));

        return Math.Exp(logMass);
    }

    /// <summary>Sums the configured trial count of Bernoulli outcomes through the by-ref engine.</summary>
    private int SampleExact<TEngine>(ref TEngine engine)
        where TEngine : struct, IRandomSource<TEngine>
    {
        double threshold = _successProbability;
        int successes = 0;
        for (int trial = 0; trial < _trials; trial++)
        {
            if (DistributionMath.NextUnitDouble(ref engine) < threshold)
            {
                successes++;
            }
        }

        return successes;
    }

    /// <summary>
    /// Draws <c>round(n p + sqrt(n p (1 - p)) * Z)</c> with a standard normal deviate <c>Z</c>,
    /// clamped to <c>[0, n]</c>. <c>Z</c> comes from Box-Muller over two unit draws of the
    /// by-ref engine: the facade's pair cache cannot be reused here because a distribution is a
    /// readonly struct with nowhere to keep the spare deviate.
    /// </summary>
    private int SampleNormalApproximation<TEngine>(ref TEngine engine)
        where TEngine : struct, IRandomSource<TEngine>
    {
        double u1 = DistributionMath.NextOpenUnitDouble(ref engine);
        double u2 = DistributionMath.NextUnitDouble(ref engine);
        double deviate = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

        double value = Math.Round(_mean + (_standardDeviation * deviate));
        return value < 0.0 ? 0 : (value > _trials ? _trials : (int)value);
    }
}

/// <summary>
/// Geometric(p), counted as the number of trials up to and including the first success: the
/// support is <c>1, 2, 3, ...</c>, the mean is <c>1 / p</c> and the variance is
/// <c>(1 - p) / p^2</c>. Sampling is the inverse CDF in one logarithm per draw.
/// </summary>
/// <remarks>
/// <para><b>Counting convention.</b> This is the "trials until first success" parameterisation,
/// not the "failures before first success" one: the support starts at 1, and a certain outcome
/// <c>p = 1</c> is the point mass at 1.</para>
/// <para><b>By-ref sampling is the hoisted form.</b> <see cref="Sample{TEngine}"/> and
/// <see cref="SampleMany{TEngine}"/> advance the engine through a by-ref parameter, so a batch
/// loop keeps the state in the caller's own storage - the register-resident form the hoisting gate
/// buys for a heap-held <see cref="RandomEngine{TEngine}"/>, obtained without the copy.</para>
/// </remarks>
public readonly struct GeometricDistribution
{
    private readonly double _successProbability;
    private readonly double _logComplement;

    /// <summary>Initialises a geometric distribution.</summary>
    /// <param name="successProbability">The per-trial success probability, in <c>(0, 1]</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="successProbability"/> is NaN, negative or above 1. A zero probability has
    /// no support - the trial count would be unbounded - so it is rejected here rather than
    /// producing an infinite loop at sampling time.
    /// </exception>
    public GeometricDistribution(double successProbability)
    {
        if (double.IsNaN(successProbability) || successProbability <= 0.0 || successProbability > 1.0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(successProbability), "The success probability must be within (0, 1].");
        }

        _successProbability = successProbability;

        // log(1 - p) depends on the rate alone, so it is paid once here rather than per draw. At
        // p = 1 it is -inf, which is what collapses the sampler to the point mass at 1.
        _logComplement = DistributionMath.LogOneMinus(successProbability);
    }

    /// <summary>Gets the expected number of trials, <c>1 / p</c>.</summary>
    public double Mean => 1.0 / _successProbability;

    /// <summary>Gets the variance, <c>(1 - p) / p^2</c>.</summary>
    public double Variance => (1.0 - _successProbability) / (_successProbability * _successProbability);

    /// <summary>Evaluates the probability mass function.</summary>
    /// <param name="k">The trial count to evaluate at.</param>
    /// <returns>The probability of <paramref name="k"/> trials, zero below 1.</returns>
    public double Probability(int k) =>
        k < 1 ? 0.0 : _successProbability * Math.Pow(1.0 - _successProbability, k - 1);

    /// <summary>Evaluates the cumulative distribution function.</summary>
    /// <param name="k">The trial count to evaluate at.</param>
    /// <returns>The probability of finishing within <paramref name="k"/> trials.</returns>
    /// <remarks>
    /// <c>1 - (1 - p)^k</c> evaluated as <c>OneMinusExp(k * log(1 - p))</c> in log space, so the
    /// small tail a tiny <c>p</c> produces is not cancelled against 1.
    /// </remarks>
    public double CumulativeProbability(int k) =>
        k < 1 ? 0.0 : DistributionMath.OneMinusExp(k * _logComplement);

    /// <summary>Inverts the cumulative distribution function in closed form.</summary>
    /// <param name="probability">The target cumulative probability, in <c>[0, 1]</c>.</param>
    /// <returns>
    /// The smallest trial count whose cumulative probability reaches the target, clamped to the
    /// support: 1 at probability zero, <see cref="int.MaxValue"/> at probability one.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> is NaN or outside <c>[0, 1]</c>.</exception>
    public int Quantile(double probability)
    {
        DistributionMath.ValidateUnitInterval(probability, nameof(probability));

        if (probability <= 0.0)
        {
            return 1;
        }

        if (probability >= 1.0)
        {
            // The support is unbounded, so no finite trial count reaches probability one; the
            // representable end is the documented clamp rather than an overflowed cast.
            return int.MaxValue;
        }

        // F(k) >= t  <=>  (1 - p)^k <= 1 - t  <=>  k >= log(1 - t) / log(1 - p). Both logarithms are
        // negative, so the division is the increasing branch and the answer is its ceiling.
        double trials = Math.Ceiling(DistributionMath.LogOneMinus(probability) / _logComplement);
        if (trials >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return trials < 1.0 ? 1 : (int)trials;
    }

    /// <summary>
    /// Draws <c>floor(log(1 - u) / log(1 - p)) + 1</c> from one unit draw <c>u</c>.
    /// </summary>
    /// <typeparam name="TEngine">The engine core to draw from.</typeparam>
    /// <param name="engine">The engine state to advance. It is mutated in place.</param>
    /// <returns>A trial count in <c>[1, int.MaxValue]</c>.</returns>
    /// <remarks>
    /// <c>1 - u</c> lies in <c>(0, 1]</c> for a unit draw, so the numerator is never
    /// <c>log(0)</c>, and the result is at least 1 because both logarithms are non-positive. The
    /// denominator is the constructor's <c>log(1 - p)</c>, computed in log space so that a <c>p</c>
    /// near the double epsilon does not collapse to zero. At <c>p = 1</c> it is <c>-inf</c>, the
    /// quotient collapses to zero and the draw is the point mass at 1 - the degenerate
    /// distribution, handled by the algebra rather than by a special case.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int Sample<TEngine>(ref TEngine engine)
        where TEngine : struct, IRandomSource<TEngine>
    {
        double unit = DistributionMath.NextUnitDouble(ref engine);
        double trials = Math.Floor(Math.Log(1.0 - unit) / _logComplement) + 1.0;
        return trials >= int.MaxValue ? int.MaxValue : (int)trials;
    }

    /// <summary>Fills the destination with independent samples.</summary>
    /// <typeparam name="TEngine">The engine core to draw from.</typeparam>
    /// <param name="engine">The engine state to advance. It is mutated in place.</param>
    /// <param name="destination">The span to overwrite; an empty span is a no-op.</param>
    /// <remarks>
    /// Nothing is hoisted because the engine arrives by reference: the loop advances the caller's
    /// own state in place, which is exactly the hoist the <see cref="RandomConfig.MaxHoistableStateBytes"/>
    /// gate performs for a heap-held engine, minus the copy.
    /// </remarks>
    public void SampleMany<TEngine>(ref TEngine engine, Span<int> destination)
        where TEngine : struct, IRandomSource<TEngine>
    {
        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = Sample(ref engine);
        }
    }
}

/// <summary>
/// Poisson(lambda): the count of events in a fixed interval of mean <c>lambda</c>. Sampling
/// switches algorithm at <see cref="PtrsThreshold"/>: Knuth's multiply loop below it, Hörmann's
/// PTRS transformed rejection above it.
/// </summary>
/// <remarks>
/// <para><b>Why the switch.</b> Knuth's method costs one exponential, one multiplication and one
/// logarithm per <em>trial of the acceptance loop</em>, and that loop runs about <c>lambda + 1</c>
/// times - so its cost grows with the rate, and at <c>lambda = 100</c> it would average a hundred
/// engine steps per sample. PTRS replaces the loop with one proposal, two logarithms and an
/// acceptance test, independent of <c>lambda</c>; it needs its constants, and the log-gamma, which
/// is why the crossover sits at 10 rather than at 1.</para>
/// <para><b>By-ref sampling is the hoisted form.</b> Both branches advance the engine through a
/// by-ref parameter, so <see cref="SampleMany{TEngine}"/> keeps the state in the caller's storage
/// for the whole batch instead of reloading it from a field per draw.</para>
/// <para>The PTRS constants depend only on the rate, so they are computed once in the constructor
/// and the rejection loop pays two logarithms per candidate.</para>
/// </remarks>
public readonly struct PoissonDistribution
{
    /// <summary>
    /// The rate at and above which Hörmann's PTRS transformed rejection replaces Knuth's loop.
    /// </summary>
    public const double PtrsThreshold = 10.0;

    private readonly double _lambda;
    private readonly double _logLambda;
    private readonly double _beta;
    private readonly double _alpha;
    private readonly double _acceptanceThreshold;

    /// <summary>Initialises a Poisson distribution.</summary>
    /// <param name="lambda">The mean count; must be finite and non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="lambda"/> is NaN, negative or infinite.
    /// </exception>
    public PoissonDistribution(double lambda)
    {
        if (double.IsNaN(lambda) || lambda < 0.0 || double.IsInfinity(lambda))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(lambda), "The Poisson rate must be finite and non-negative.");
        }

        _lambda = lambda;
        _logLambda = lambda > 0.0 ? Math.Log(lambda) : 0.0;

        if (lambda >= PtrsThreshold)
        {
            // Hörmann's PTRS constants, hoisted out of the rejection loop: they depend on the
            // rate alone, so a candidate pays two logarithms instead of five.
            _beta = Math.PI / Math.Sqrt(3.0 * lambda);
            _alpha = _beta * lambda;
            double squeeze = 0.767 - (3.36 / lambda);
            _acceptanceThreshold = Math.Log(squeeze) - lambda - Math.Log(_beta);
        }
        else
        {
            _beta = 0.0;
            _alpha = 0.0;
            _acceptanceThreshold = 0.0;
        }
    }

    /// <summary>Gets the expected count, <c>lambda</c>.</summary>
    public double Mean => _lambda;

    /// <summary>Gets the variance, <c>lambda</c>.</summary>
    public double Variance => _lambda;

    /// <summary>Evaluates the probability mass function.</summary>
    /// <param name="k">The count to evaluate at.</param>
    /// <returns>The probability of exactly <paramref name="k"/> events, zero below 0.</returns>
    /// <remarks>
    /// <c>exp(-lambda + k log lambda - lgamma(k + 1))</c> in log space, so a rate or a count
    /// that would overflow the factorial never overflows the mass. The empty-count mass takes its
    /// exact closed form <c>exp(-lambda)</c> instead of riding the same round trip.
    /// </remarks>
    public double Probability(int k)
    {
        if (k < 0)
        {
            return 0.0;
        }

        if (_lambda == 0.0)
        {
            return k == 0 ? 1.0 : 0.0;
        }

        return Mass(_lambda, _logLambda, k);
    }

    /// <summary>Evaluates the cumulative distribution function.</summary>
    /// <param name="k">The count to evaluate at.</param>
    /// <returns>The probability of drawing at most <paramref name="k"/> events.</returns>
    /// <remarks>
    /// A cold query path that sums the shorter side: the head <c>0..k</c> when <c>k</c> sits below
    /// the mean, otherwise the geometric tail above <c>k</c>, whose terms decay by
    /// <c>lambda / k</c> per step.
    /// </remarks>
    public double CumulativeProbability(int k)
    {
        if (k < 0)
        {
            return 0.0;
        }

        if (_lambda == 0.0)
        {
            return 1.0;
        }

        if ((double)k + 1.0 <= _lambda)
        {
            double total = 0.0;
            for (int i = 0; i <= k; i++)
            {
                total += Mass(_lambda, _logLambda, i);
            }

            return Math.Clamp(total, 0.0, 1.0);
        }

        // P(X > k) = P(X >= k + 1) * (1 + lambda/(k + 2) + lambda^2/((k + 2)(k + 3)) + ...).
        double index = (double)k + 1.0;
        double term = Mass(_lambda, _logLambda, index);
        double tail = term;
        while (term > 0.0)
        {
            index += 1.0;
            term *= _lambda / index;
            tail += term;
        }

        return Math.Clamp(1.0 - tail, 0.0, 1.0);
    }

    /// <summary>Inverts the cumulative distribution function.</summary>
    /// <param name="probability">The target cumulative probability, in <c>[0, 1]</c>.</param>
    /// <returns>The smallest count whose cumulative probability reaches the target.</returns>
    /// <remarks>
    /// A lower-bound binary search over <c>[0, int.MaxValue]</c>: the tail branch of
    /// <see cref="CumulativeProbability"/> underflows to zero immediately above the mean, so a
    /// probe far outside the bulk costs one logarithm. An unbounded support has no finite
    /// quantile of exactly one, so a target of 1 converges to the smallest count whose
    /// cumulative probability rounds to one - the saturation point of the double, not a search
    /// failure.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> is NaN or outside <c>[0, 1]</c>.</exception>
    public int Quantile(double probability)
    {
        DistributionMath.ValidateUnitInterval(probability, nameof(probability));

        int low = 0;
        int high = int.MaxValue;
        while (low < high)
        {
            int middle = low + ((high - low) >> 1);
            if (CumulativeProbability(middle) >= probability)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
    }

    /// <summary>Draws one sample, dispatching to Knuth or PTRS by rate.</summary>
    /// <typeparam name="TEngine">The engine core to draw from.</typeparam>
    /// <param name="engine">The engine state to advance. It is mutated in place.</param>
    /// <returns>A non-negative event count.</returns>
    /// <remarks>
    /// The dispatch is the inlinable part; the two samplers stay out of line, so the call site
    /// pays one compare for the algorithm choice and no register pressure for the leaf it picks.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int Sample<TEngine>(ref TEngine engine)
        where TEngine : struct, IRandomSource<TEngine> =>
        _lambda < PtrsThreshold ? SampleKnuth(ref engine) : SamplePtrs(ref engine);

    /// <summary>Fills the destination with independent samples.</summary>
    /// <typeparam name="TEngine">The engine core to draw from.</typeparam>
    /// <param name="engine">The engine state to advance. It is mutated in place.</param>
    /// <param name="destination">The span to overwrite; an empty span is a no-op.</param>
    /// <remarks>
    /// The loop hoists nothing by design: <paramref name="engine"/> is already a by-ref local, so
    /// the state lives in the caller's storage for the whole batch. The hoisting lesson from
    /// <see cref="RandomEngine{TEngine}.NextBytes(Span{byte})"/> - where a field reload per word
    /// had to be traded off against a per-word state copy - applies only to heap-held engines,
    /// and by-ref passing is that hoist done once by the parameter instead of per word.
    /// </remarks>
    public void SampleMany<TEngine>(ref TEngine engine, Span<int> destination)
        where TEngine : struct, IRandomSource<TEngine>
    {
        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = Sample(ref engine);
        }
    }

    /// <summary>Evaluates the mass of a fractional-safe count in log space.</summary>
    private static double Mass(double lambda, double logLambda, double k)
    {
        if (k == 0.0)
        {
            return Math.Exp(-lambda);
        }

        return Math.Exp((-lambda) + (k * logLambda) - DistributionMath.LogGamma(k + 1.0));
    }

    /// <summary>
    /// Knuth's method: multiply unit draws until the running product drops to
    /// <c>exp(-lambda)</c>. The accepted count is one less than the number of draws, because the
    /// product is compared against the limit <em>after</em> the draw that crossed it.
    /// </summary>
    private int SampleKnuth<TEngine>(ref TEngine engine)
        where TEngine : struct, IRandomSource<TEngine>
    {
        double limit = Math.Exp(-_lambda);
        double product = 1.0;
        int draws = 0;
        do
        {
            draws++;
            product *= DistributionMath.NextUnitDouble(ref engine);
        }
        while (product > limit);

        return draws - 1;
    }

    /// <summary>
    /// Hörmann's PTRS transformed rejection: one proposal per candidate, accepted when the
    /// acceptance function falls below the target mass.
    /// </summary>
    private int SamplePtrs<TEngine>(ref TEngine engine)
        where TEngine : struct, IRandomSource<TEngine>
    {
        while (true)
        {
            double u = DistributionMath.NextOpenUnitDouble(ref engine);
            double x = (_alpha - Math.Log((1.0 - u) / u)) / _beta;
            double candidate = Math.Floor(x + 0.5);
            if (candidate < 0.0)
            {
                // The proposal reached below zero; the next candidate costs two draws, not a
                // logarithm of the acceptance function.
                continue;
            }

            // Saturate rather than overflow the cast: an out-of-representation candidate is
            // still accepted or rejected by the same test, so the loop always terminates.
            double count = candidate > int.MaxValue ? int.MaxValue : candidate;

            double y = _alpha - (_beta * x);
            double v = DistributionMath.NextOpenUnitDouble(ref engine);

            // log(v / (1 + e^y)^2), written as -2 * log(1 + e^y) + y and then split on the sign of
            // y: the positive branch carries the leading y down itself, so no intermediate
            // exponential can overflow into an unconditional accept, and the leading y terms
            // cannot cancel either.
            double acceptance = y > 0.0
                ? Math.Log(v) - y - (2.0 * Math.Log(1.0 + Math.Exp(-y)))
                : y + Math.Log(v) - (2.0 * Math.Log(1.0 + Math.Exp(y)));
            double target = _acceptanceThreshold + (count * _logLambda) - DistributionMath.LogGamma(count + 1.0);

            if (acceptance <= target)
            {
                return (int)count;
            }
        }
    }
}