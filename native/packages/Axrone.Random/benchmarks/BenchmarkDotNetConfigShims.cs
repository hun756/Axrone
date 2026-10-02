// Native AOT shims for the BenchmarkDotNet.Configs surface used by the benchmark class. See
// BenchmarkDotNetShims.cs for why these exist. This file is only compiled when PublishAot=true.
namespace BenchmarkDotNet.Configs;

/// <summary>No-op stand-in for BenchmarkDotNet's logical grouping rule.</summary>
internal enum BenchmarkLogicalGroupRule
{
    /// <summary>Groups benchmarks by category.</summary>
    ByCategory,
}

/// <summary>No-op stand-in for BenchmarkDotNet's grouping attribute.</summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class GroupBenchmarksByAttribute : Attribute
{
    /// <summary>Initialises the marker with the grouping rule.</summary>
    /// <param name="rule">The grouping rule; ignored by the AOT harness.</param>
    public GroupBenchmarksByAttribute(BenchmarkLogicalGroupRule rule) => Rule = rule;

    /// <summary>Gets the grouping rule.</summary>
    public BenchmarkLogicalGroupRule Rule { get; }
}
