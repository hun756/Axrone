namespace Axrone.Ecs;

/// <summary>
/// Well-known property type ids for inspectors and serializers.
/// </summary>
public static class PropertyTypes
{
    /// <summary>Boolean flag.</summary>
    public const string Boolean = "boolean";

    /// <summary>Numeric scalar.</summary>
    public const string Number = "number";

    /// <summary>Text.</summary>
    public const string Text = "string";

    /// <summary>Two-component vector.</summary>
    public const string Vec2 = "vec2";

    /// <summary>Three-component vector.</summary>
    public const string Vec3 = "vec3";

    /// <summary>Rectangle.</summary>
    public const string Rect = "rect";

    /// <summary>Entity reference.</summary>
    public const string Entity = "entity";

    /// <summary>Transform reference.</summary>
    public const string Transform = "transform";

    /// <summary>Component reference.</summary>
    public const string Component = "component";
}

/// <summary>
/// Marks a component field with inspector and serialization metadata.
/// The C# counterpart of the web <c>@property</c> decorator.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class PropertyAttribute : Attribute
{
    /// <summary>Display label.</summary>
    public string? Label { get; set; }

    /// <summary>Description.</summary>
    public string? Description { get; set; }

    /// <summary>Type id (see <see cref="PropertyTypes"/>) or custom name.</summary>
    public string? Type { get; set; }

    /// <summary>CLR type for custom references.</summary>
    public Type? ClrType { get; set; }

    /// <summary>Default value; must be a compile-time constant.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Whether the field serializes.</summary>
    public bool Serializable { get; set; } = true;

    /// <summary>Whether the field shows in inspectors.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Minimum; NaN when unset.</summary>
    public double Min { get; set; } = double.NaN;

    /// <summary>Maximum; NaN when unset.</summary>
    public double Max { get; set; } = double.NaN;

    /// <summary>Step; NaN when unset.</summary>
    public double Step { get; set; } = double.NaN;
}

/// <summary>
/// Resolved property metadata for one component field.
/// </summary>
public sealed record PropertyMetadata(
    string PropertyKey,
    string? Label,
    string? Description,
    string? Type,
    Type? ClrType,
    object? DefaultValue,
    bool Serializable,
    bool Visible,
    double Min,
    double Max,
    double Step)
{
    /// <summary>Whether a minimum is set.</summary>
    public bool HasMin => !double.IsNaN(Min);

    /// <summary>Whether a maximum is set.</summary>
    public bool HasMax => !double.IsNaN(Max);

    /// <summary>Whether a step is set.</summary>
    public bool HasStep => !double.IsNaN(Step);
}
