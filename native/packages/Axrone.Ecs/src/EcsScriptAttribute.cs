namespace Axrone.Ecs;

/// <summary>
/// Marks a component with script metadata: display name, execution priority,
/// dependencies, lifetime flags and authorship. The C# counterpart of the
/// web <c>@script</c> decorator. Applies to classes and structs alike since
/// native components are unmanaged value types.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class ScriptAttribute : Attribute
{
    /// <summary>Registry name; defaults to the type name.</summary>
    public string? ScriptName { get; set; }

    /// <summary>Execution priority.</summary>
    public int Priority { get; set; }

    /// <summary>Required component types.</summary>
    public Type[]? Dependencies { get; set; }

    /// <summary>Only one instance may exist.</summary>
    public bool Singleton { get; set; }

    /// <summary>Multiple instances per entity are allowed.</summary>
    public bool AllowMultiple { get; set; }

    /// <summary>Runs outside play mode.</summary>
    public bool ExecuteInEditMode { get; set; }

    /// <summary>Instances are tracked.</summary>
    public bool TrackInstances { get; set; } = true;

    /// <summary>Semantic version.</summary>
    public string? Version { get; set; }

    /// <summary>Author.</summary>
    public string? Author { get; set; }

    /// <summary>Description.</summary>
    public string? Description { get; set; }

    /// <summary>Tags.</summary>
    public string[]? Tags { get; set; }

    /// <summary>Experimental marker.</summary>
    public bool Experimental { get; set; }

    /// <summary>Deprecation marker.</summary>
    public bool Deprecated { get; set; }

    /// <summary>Deprecation message.</summary>
    public string? DeprecationMessage { get; set; }
}

/// <summary>
/// Resolved script metadata for one component type.
/// </summary>
public sealed record ScriptMetadata(
    string ScriptName,
    int Priority,
    Type[] Dependencies,
    bool Singleton,
    bool AllowMultiple,
    bool ExecuteInEditMode,
    bool TrackInstances,
    string? Version,
    string? Author,
    string? Description,
    string[] Tags,
    bool Experimental,
    bool Deprecated,
    string? DeprecationMessage);
