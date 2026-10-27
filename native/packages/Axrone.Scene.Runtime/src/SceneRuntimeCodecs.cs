namespace Axrone.Scene.Runtime;

using System.Text.Json;

/// <summary>Codec registration for runtime-owned data components.</summary>
public static class SceneRuntimeCodecs
{
    /// <summary>Registers codecs for every runtime component present in a registry.</summary>
    /// <param name="codecs">Codec catalog to register into.</param>
    /// <param name="components">Component registry that gates which codecs apply.</param>
    public static void Register(SceneCodecRegistry codecs, SceneComponentRegistry components)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        ArgumentNullException.ThrowIfNull(components);

        if (components.TryGetDataByType(typeof(SceneRigidbody3D), out _))
        {
            codecs.Register(new SceneRigidbody3DCodec());
        }

        if (components.TryGetDataByType(typeof(SceneCollider3D), out _))
        {
            codecs.Register(new SceneCollider3DCodec());
        }

        if (components.TryGetDataByType(typeof(SceneParticleSystem), out _))
        {
            codecs.Register(new SceneParticleSystemCodec());
        }
    }
}

/// <summary>JSON codec for <see cref="SceneRigidbody3D"/>.</summary>
public sealed class SceneRigidbody3DCodec : SceneDataCodec<SceneRigidbody3D>
{
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, in SceneRigidbody3D value)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteString("bodyType", SceneRuntimeJson.WriteBodyType(value.BodyType));
        SceneRuntimeJson.WriteVec3(writer, "linearVelocity", value.LinearVelocity);
        SceneRuntimeJson.WriteVec3(writer, "angularVelocity", value.AngularVelocity);
        writer.WriteNumber("linearDamping", value.LinearDamping);
        writer.WriteNumber("angularDamping", value.AngularDamping);
        writer.WriteNumber("gravityScale", value.GravityScale);
        writer.WriteNumber("mass", value.Mass);
        writer.WriteBoolean("enabled", value.Enabled);
    }

    /// <inheritdoc/>
    public override SceneRigidbody3D Read(JsonElement data)
    {
        SceneRigidbody3D value = SceneRigidbody3D.Default;

        if (data.TryGetProperty("bodyType", out JsonElement bodyType))
        {
            value.BodyType = SceneRuntimeJson.ReadBodyType(bodyType);
        }

        if (data.TryGetProperty("linearVelocity", out JsonElement linearVelocity))
        {
            value.LinearVelocity = SceneRuntimeJson.ReadVec3(linearVelocity, "linearVelocity");
        }

        if (data.TryGetProperty("angularVelocity", out JsonElement angularVelocity))
        {
            value.AngularVelocity = SceneRuntimeJson.ReadVec3(angularVelocity, "angularVelocity");
        }

        if (data.TryGetProperty("linearDamping", out JsonElement linearDamping))
        {
            value.LinearDamping = linearDamping.GetSingle();
        }

        if (data.TryGetProperty("angularDamping", out JsonElement angularDamping))
        {
            value.AngularDamping = angularDamping.GetSingle();
        }

        if (data.TryGetProperty("gravityScale", out JsonElement gravityScale))
        {
            value.GravityScale = gravityScale.GetSingle();
        }

        if (data.TryGetProperty("mass", out JsonElement mass))
        {
            value.Mass = mass.GetSingle();
        }

        if (data.TryGetProperty("enabled", out JsonElement enabled))
        {
            value.Enabled = enabled.GetBoolean();
        }

        return value;
    }
}

/// <summary>JSON codec for <see cref="SceneCollider3D"/>.</summary>
public sealed class SceneCollider3DCodec : SceneDataCodec<SceneCollider3D>
{
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, in SceneCollider3D value)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteString("shape", SceneRuntimeJson.WriteShape(value.Shape));
        SceneRuntimeJson.WriteVec3(writer, "boxHalfExtents", value.BoxHalfExtents);
        writer.WriteNumber("radius", value.Radius);
        writer.WriteNumber("capsuleHalfHeight", value.CapsuleHalfHeight);
        SceneRuntimeJson.WriteVec3(writer, "offset", value.Offset);
        writer.WriteNumber("density", value.Density);
        writer.WriteNumber("friction", value.Friction);
        writer.WriteNumber("restitution", value.Restitution);
        writer.WriteBoolean("trigger", value.IsTrigger);
        writer.WriteBoolean("enabled", value.Enabled);
    }

    /// <inheritdoc/>
    public override SceneCollider3D Read(JsonElement data)
    {
        SceneCollider3D value = SceneCollider3D.Default;

        if (data.TryGetProperty("shape", out JsonElement shape))
        {
            value.Shape = SceneRuntimeJson.ReadShape(shape);
        }

        if (data.TryGetProperty("boxHalfExtents", out JsonElement boxHalfExtents))
        {
            value.BoxHalfExtents = SceneRuntimeJson.ReadVec3(boxHalfExtents, "boxHalfExtents");
        }

        if (data.TryGetProperty("radius", out JsonElement radius))
        {
            value.Radius = radius.GetSingle();
        }

        if (data.TryGetProperty("capsuleHalfHeight", out JsonElement capsuleHalfHeight))
        {
            value.CapsuleHalfHeight = capsuleHalfHeight.GetSingle();
        }

        if (data.TryGetProperty("offset", out JsonElement offset))
        {
            value.Offset = SceneRuntimeJson.ReadVec3(offset, "offset");
        }

        if (data.TryGetProperty("density", out JsonElement density))
        {
            value.Density = density.GetSingle();
        }

        if (data.TryGetProperty("friction", out JsonElement friction))
        {
            value.Friction = friction.GetSingle();
        }

        if (data.TryGetProperty("restitution", out JsonElement restitution))
        {
            value.Restitution = restitution.GetSingle();
        }

        if (data.TryGetProperty("trigger", out JsonElement trigger))
        {
            value.IsTrigger = trigger.GetBoolean();
        }

        if (data.TryGetProperty("enabled", out JsonElement enabled))
        {
            value.Enabled = enabled.GetBoolean();
        }

        return value;
    }
}

/// <summary>JSON codec for <see cref="SceneParticleSystem"/>.</summary>
public sealed class SceneParticleSystemCodec : SceneDataCodec<SceneParticleSystem>
{
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, in SceneParticleSystem value)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteBoolean("enabled", value.Enabled);
        writer.WriteBoolean("playing", value.Playing);
        writer.WriteNumber("emissionRate", value.EmissionRate);
        writer.WriteNumber("startLifetime", value.StartLifetime);
        writer.WriteNumber("startSpeed", value.StartSpeed);
        writer.WriteNumber("startSize", value.StartSize);
        SceneRuntimeJson.WriteVec4(writer, "startColor", value.StartColor);
        writer.WriteNumber("spread", value.Spread);
        writer.WriteNumber("maxParticles", value.MaxParticles);
    }

    /// <inheritdoc/>
    public override SceneParticleSystem Read(JsonElement data)
    {
        SceneParticleSystem value = SceneParticleSystem.Default;

        if (data.TryGetProperty("enabled", out JsonElement enabled))
        {
            value.Enabled = enabled.GetBoolean();
        }

        if (data.TryGetProperty("playing", out JsonElement playing))
        {
            value.Playing = playing.GetBoolean();
        }

        if (data.TryGetProperty("emissionRate", out JsonElement emissionRate))
        {
            value.EmissionRate = emissionRate.GetSingle();
        }

        if (data.TryGetProperty("startLifetime", out JsonElement startLifetime))
        {
            value.StartLifetime = startLifetime.GetSingle();
        }

        if (data.TryGetProperty("startSpeed", out JsonElement startSpeed))
        {
            value.StartSpeed = startSpeed.GetSingle();
        }

        if (data.TryGetProperty("startSize", out JsonElement startSize))
        {
            value.StartSize = startSize.GetSingle();
        }

        if (data.TryGetProperty("startColor", out JsonElement startColor))
        {
            value.StartColor = SceneRuntimeJson.ReadVec4(startColor, "startColor");
        }

        if (data.TryGetProperty("spread", out JsonElement spread))
        {
            value.Spread = spread.GetSingle();
        }

        if (data.TryGetProperty("maxParticles", out JsonElement maxParticles))
        {
            value.MaxParticles = maxParticles.GetInt32();
        }

        return value;
    }
}

/// <summary>Shared JSON primitives for the runtime codecs.</summary>
internal static class SceneRuntimeJson
{
    public static void WriteVec3(Utf8JsonWriter writer, string name, in Vec3 value)
    {
        writer.WriteStartArray(name);
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteEndArray();
    }

    public static void WriteVec4(Utf8JsonWriter writer, string name, in Vec4 value)
    {
        writer.WriteStartArray(name);
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteNumberValue(value.W);
        writer.WriteEndArray();
    }

    public static Vec3 ReadVec3(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 3)
        {
            throw Invalid($"'{name}' must be a three-component array.");
        }

        return new Vec3(
            element[0].GetSingle(),
            element[1].GetSingle(),
            element[2].GetSingle());
    }

    public static Vec4 ReadVec4(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 4)
        {
            throw Invalid($"'{name}' must be a four-component array.");
        }

        return new Vec4(
            element[0].GetSingle(),
            element[1].GetSingle(),
            element[2].GetSingle(),
            element[3].GetSingle());
    }

    public static string WriteBodyType(SceneBodyType type) => type switch
    {
        SceneBodyType.Static => "static",
        SceneBodyType.Kinematic => "kinematic",
        _ => "dynamic"
    };

    public static SceneBodyType ReadBodyType(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Invalid("Rigidbody 'bodyType' must be a string.");
        }

        return element.GetString() switch
        {
            "dynamic" => SceneBodyType.Dynamic,
            "static" => SceneBodyType.Static,
            "kinematic" => SceneBodyType.Kinematic,
            _ => throw Invalid(
                $"Rigidbody 'bodyType' must be 'dynamic', 'static' or 'kinematic'; received '{element.GetString()}'.")
        };
    }

    public static string WriteShape(SceneColliderShape3D shape) => shape switch
    {
        SceneColliderShape3D.Box => "box",
        SceneColliderShape3D.Capsule => "capsule",
        _ => "sphere"
    };

    public static SceneColliderShape3D ReadShape(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Invalid("Collider 'shape' must be a string.");
        }

        return element.GetString() switch
        {
            "sphere" => SceneColliderShape3D.Sphere,
            "box" => SceneColliderShape3D.Box,
            "capsule" => SceneColliderShape3D.Capsule,
            _ => throw Invalid(
                $"Collider 'shape' must be 'sphere', 'box' or 'capsule'; received '{element.GetString()}'.")
        };
    }

    [DoesNotReturn]
    public static SceneValidationException Invalid(string message) =>
        throw new SceneValidationException(SceneErrorCodes.ValidationInvalid, message);
}
