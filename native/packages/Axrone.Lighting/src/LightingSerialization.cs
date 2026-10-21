namespace Axrone.Lighting;

/// <summary>Serialized environment block.</summary>
public sealed class SerializedEnvironmentDto
{
    /// <summary>Ambient tuple.</summary>
    public float[] Ambient { get; set; } = [0.2f, 0.2f, 0.25f];

    /// <summary>Sky tuple.</summary>
    public float[] Sky { get; set; } = [0.4f, 0.45f, 0.55f];

    /// <summary>Ground tuple.</summary>
    public float[] Ground { get; set; } = [0.15f, 0.13f, 0.1f];

    /// <summary>Exposure.</summary>
    public float Exposure { get; set; } = 1.0f;

    /// <summary>Gamma.</summary>
    public float Gamma { get; set; } = 2.2f;
}

/// <summary>Serialized light block.</summary>
public sealed class SerializedLightDto
{
    /// <summary>Light id.</summary>
    public ulong Id { get; set; }

    /// <summary>Light kind string.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Enabled flag.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Color tuple.</summary>
    public float[] Color { get; set; } = [1.0f, 1.0f, 1.0f];

    /// <summary>Intensity.</summary>
    public float Intensity { get; set; } = 1.0f;

    /// <summary>Priority.</summary>
    public float Priority { get; set; }

    /// <summary>Direction tuple.</summary>
    public float[]? Direction { get; set; }

    /// <summary>Ambient tuple.</summary>
    public float[]? Ambient { get; set; }

    /// <summary>Position tuple.</summary>
    public float[]? Position { get; set; }

    /// <summary>Range.</summary>
    public float? Range { get; set; }

    /// <summary>Attenuation.</summary>
    public float? Attenuation { get; set; }

    /// <summary>Inner cone cosine.</summary>
    public float? InnerConeCosine { get; set; }

    /// <summary>Outer cone cosine.</summary>
    public float? OuterConeCosine { get; set; }
}

/// <summary>Serialized lighting document.</summary>
public sealed class LightingDocumentDto
{
    /// <summary>Schema version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Rig id.</summary>
    public ulong RigId { get; set; }

    /// <summary>Environment.</summary>
    public SerializedEnvironmentDto Environment { get; set; } = new();

    /// <summary>Lights in insertion order.</summary>
    public List<SerializedLightDto> Lights { get; set; } = [];
}

/// <summary>Parse issue.</summary>
public readonly record struct SerializationIssue(string Code, string Message, string? Path = null);

/// <summary>Deserialization result.</summary>
public sealed class DeserializationResult
{
    /// <summary>Whether parsing succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Parsed rig.</summary>
    public LightingRig? Rig { get; init; }

    /// <summary>Parse issues.</summary>
    public IReadOnlyList<SerializationIssue> Issues { get; init; } = [];
}

/// <summary>AOT JSON source-generation context.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(LightingDocumentDto))]
[JsonSerializable(typeof(SerializedEnvironmentDto))]
[JsonSerializable(typeof(SerializedLightDto))]
[JsonSerializable(typeof(List<SerializedLightDto>))]
[JsonSerializable(typeof(float[]))]
internal sealed partial class LightingAotJsonContext : JsonSerializerContext
{
}
