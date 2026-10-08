// Native AOT shims. BenchmarkDotNet generates and loads IL at run time and its dependency graph
// produces trim/AOT warnings, so the AOT publish excludes the package and compiles these no-op
// attributes instead. The benchmark class then compiles unchanged, and the manual harness in
// Program.cs drives it through CreateAotPlan. This file is only compiled when PublishAot=true.
namespace BenchmarkDotNet.Attributes;

/// <summary>No-op stand-in for BenchmarkDotNet's benchmark marker.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class BenchmarkAttribute : Attribute
{
    /// <summary>Gets or sets a value indicating whether the method is the group baseline.</summary>
    public bool Baseline { get; set; }
}

/// <summary>No-op stand-in for BenchmarkDotNet's category marker.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class BenchmarkCategoryAttribute : Attribute
{
    /// <summary>Initialises the marker with the category name.</summary>
    /// <param name="name">The category name; ignored by the AOT harness.</param>
    public BenchmarkCategoryAttribute(string name) => Name = name;

    /// <summary>Gets the category name.</summary>
    public string Name { get; }
}

/// <summary>No-op stand-in for BenchmarkDotNet's global setup marker.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class GlobalSetupAttribute : Attribute
{
}

/// <summary>No-op stand-in for BenchmarkDotNet's memory diagnoser marker.</summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class MemoryDiagnoserAttribute : Attribute
{
}
