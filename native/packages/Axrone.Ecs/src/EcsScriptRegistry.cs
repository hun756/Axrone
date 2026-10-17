namespace Axrone.Ecs;

using System.Text.RegularExpressions;

/// <summary>
/// One registry entry: resolved metadata plus the component type.
/// </summary>
public sealed record ScriptRegistration(ScriptMetadata Metadata, Type ComponentType);

/// <summary>
/// Validation outcome: errors fail registration, warnings do not.
/// </summary>
public readonly record struct ScriptValidation(bool IsValid, string[] Errors, string[] Warnings);

/// <summary>
/// Global script registry: explicit per-type registration with validation,
/// duplicate-name warnings, dependency graph and queries. No assembly
/// scanning by design, keeping trimmed and AOT builds intact.
/// </summary>
public static class ScriptRegistry
{
    private static readonly Dictionary<string, ScriptRegistration> _byName = new(StringComparer.Ordinal);
    private static readonly Dictionary<Type, ScriptMetadata> _byType = new();
    private static readonly Dictionary<Type, HashSet<Type>> _dependencies = new();
    private static readonly Dictionary<Type, Dictionary<string, PropertyMetadata>> _properties = new();
    private static readonly Lock _gate = new();

    /// <summary>Registers a component type.</summary>
    public static ScriptMetadata Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
        T>()
        where T : unmanaged => Register(typeof(T));

    /// <summary>Registers a component type. Only unmanaged component types
    /// are accepted; classes, interfaces and managed structs are rejected.</summary>
    public static ScriptMetadata Register(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
        Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (!IsUnmanagedComponent(componentType))
        {
            throw new EcsConfigurationException(
                "ecs.invalid-script",
                $"Type '{componentType.Name}' is not an unmanaged component type.");
        }

        object[] attributes = componentType.GetCustomAttributes(typeof(ScriptAttribute), false);
        ScriptAttribute? attribute = attributes.Length > 0 ? (ScriptAttribute)attributes[0] : null;

        string name = attribute?.ScriptName ?? componentType.Name;
        Type[] dependencies = attribute?.Dependencies ?? [];
        var metadata = new ScriptMetadata(
            name,
            attribute?.Priority ?? 0,
            dependencies,
            attribute?.Singleton ?? false,
            attribute?.AllowMultiple ?? false,
            attribute?.ExecuteInEditMode ?? false,
            attribute?.TrackInstances ?? true,
            attribute?.Version,
            attribute?.Author,
            attribute?.Description,
            attribute?.Tags ?? [],
            attribute?.Experimental ?? false,
            attribute?.Deprecated ?? false,
            attribute?.DeprecationMessage);

        ScriptValidation validation = Validate(componentType, metadata, dependencies);
        if (!validation.IsValid)
        {
            throw new EcsConfigurationException(
                "ecs.invalid-script",
                $"Script validation failed for {componentType.Name}: {string.Join("; ", validation.Errors)}");
        }

        lock (_gate)
        {
            if (_byName.TryGetValue(name, out ScriptRegistration? existing) &&
                existing.ComponentType != componentType)
            {
                validation = new ScriptValidation(
                    true,
                    [],
                    [.. validation.Warnings, $"Script name '{name}' is already declared by {existing.ComponentType.Name}."]);
            }

            _byName[name] = new ScriptRegistration(metadata, componentType);
            _byType[componentType] = metadata;
            CollectPropertyMetadata(componentType);
            if (dependencies.Length > 0)
            {
                _dependencies[componentType] = new HashSet<Type>(dependencies);
            }
        }

        return metadata;
    }

    /// <summary>Looks up metadata by type.</summary>
    public static bool TryGetMetadata(Type componentType, out ScriptMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        lock (_gate)
        {
            return _byType.TryGetValue(componentType, out metadata);
        }
    }

    /// <summary>Looks up a registration by script name.</summary>
    public static bool TryGetByName(string name, out ScriptRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            return _byName.TryGetValue(name, out registration);
        }
    }

    /// <summary>Lists registrations, optionally filtered.</summary>
    public static IReadOnlyList<ScriptRegistration> GetAll(
        string? tag = null,
        string? author = null,
        bool? deprecated = null,
        bool? experimental = null)
    {
        lock (_gate)
        {
            List<ScriptRegistration> entries = new(_byName.Values);
            entries.RemoveAll(entry =>
                (tag != null && Array.IndexOf(entry.Metadata.Tags, tag) < 0) ||
                (author != null && entry.Metadata.Author != author) ||
                (deprecated.HasValue && entry.Metadata.Deprecated != deprecated.Value) ||
                (experimental.HasValue && entry.Metadata.Experimental != experimental.Value));
            entries.Sort(static (a, b) => string.Compare(a.Metadata.ScriptName, b.Metadata.ScriptName, StringComparison.Ordinal));
            return entries;
        }
    }

    /// <summary>Returns the transitive dependency closure, excluding the root.</summary>
    public static IReadOnlyList<Type> GetDependencyTree(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        HashSet<Type> visited = new();
        List<Type> result = new();
        lock (_gate)
        {
            Traverse(componentType, visited, result, true);
        }

        return result;
    }

    /// <summary>Revalidates every registration.</summary>
    public static ScriptValidation ValidateAll()
    {
        List<string> errors = new();
        List<string> warnings = new();
        lock (_gate)
        {
            foreach ((Type type, ScriptMetadata metadata) in new Dictionary<Type, ScriptMetadata>(_byType))
            {
                Type[] dependencies = _dependencies.TryGetValue(type, out HashSet<Type>? deps) ? [.. deps] : [];
                ScriptValidation result = Validate(type, metadata, dependencies);
                foreach (string error in result.Errors)
                {
                    errors.Add($"{metadata.ScriptName}: {error}");
                }

                foreach (string warning in result.Warnings)
                {
                    warnings.Add($"{metadata.ScriptName}: {warning}");
                }
            }
        }

        return new ScriptValidation(errors.Count == 0, [.. errors], [.. warnings]);
    }

    /// <summary>Clears the registry; test infrastructure.</summary>
    internal static void Clear()
    {
        lock (_gate)
        {
            _byName.Clear();
            _byType.Clear();
            _dependencies.Clear();
            _properties.Clear();
        }
    }

    /// <summary>Whether a runtime type qualifies as an unmanaged component.</summary>
    private static bool IsUnmanagedComponent(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
        Type type)
    {
        if (!type.IsValueType)
        {
            return false;
        }

        if (type.IsPrimitive || type.IsEnum || type.IsGenericTypeDefinition)
        {
            return type.IsPrimitive || type.IsEnum;
        }

        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Type fieldType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
            if (fieldType.IsPointer)
            {
                return false;
            }

            // FieldType carries no member annotations: the root call is guarded by
            // Register's annotations, nested types resolve from runtime metadata.
#pragma warning disable IL2072
            if (!IsUnmanagedComponent(fieldType))
#pragma warning restore IL2072
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Lists property metadata for a type, base types first.</summary>
    public static IReadOnlyList<PropertyMetadata> GetProperties(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        lock (_gate)
        {
            return _properties.TryGetValue(componentType, out Dictionary<string, PropertyMetadata>? stored)
                ? new List<PropertyMetadata>(stored.Values)
                : [];
        }
    }

    /// <summary>Looks up one property by field name.</summary>
    public static bool TryGetProperty(Type componentType, string propertyKey, out PropertyMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentException.ThrowIfNullOrEmpty(propertyKey);
        lock (_gate)
        {
            metadata = null;
            return _properties.TryGetValue(componentType, out Dictionary<string, PropertyMetadata>? stored) &&
                stored.TryGetValue(propertyKey, out metadata);
        }
    }

    /// <summary>Replaces property metadata wholesale.</summary>
    public static void SetProperties(Type componentType, IReadOnlyList<PropertyMetadata> metadata)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(metadata);
        lock (_gate)
        {
            Dictionary<string, PropertyMetadata> next = new(StringComparer.Ordinal);
            foreach (PropertyMetadata entry in metadata)
            {
                next[entry.PropertyKey] = entry;
            }

            _properties[componentType] = next;
        }
    }

    private static void CollectPropertyMetadata(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
        Type componentType)
    {
        Dictionary<string, PropertyMetadata> collected = new(StringComparer.Ordinal);
        CollectChain(componentType, collected);
        _properties[componentType] = collected;
    }

    private static void CollectChain(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)]
        Type? current,
        Dictionary<string, PropertyMetadata> collected)
    {
        if (current == null || current == typeof(object) || current == typeof(ValueType))
        {
            return;
        }

        // Type.BaseType carries no member annotations: roots stay alive through
        // explicit Register calls, so the residual AOT caveat is limited to
        // private fields on unreferenced base classes.
#pragma warning disable IL2072
        CollectChain(current.BaseType, collected);
#pragma warning restore IL2072

        foreach (FieldInfo field in current.GetFields(
            BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            object[] attributes = field.GetCustomAttributes(typeof(PropertyAttribute), false);
            if (attributes.Length == 0)
            {
                continue;
            }

            PropertyAttribute attribute = (PropertyAttribute)attributes[0];
            collected[field.Name] = new PropertyMetadata(
                field.Name,
                attribute.Label,
                attribute.Description,
                attribute.Type,
                attribute.ClrType,
                attribute.DefaultValue,
                attribute.Serializable,
                attribute.Visible,
                attribute.Min,
                attribute.Max,
                attribute.Step);
        }
    }

    private static void Traverse(Type current, HashSet<Type> visited, List<Type> result, bool isRoot)
    {
        if (!visited.Add(current))
        {
            return;
        }

        if (!isRoot)
        {
            result.Add(current);
        }

        if (_dependencies.TryGetValue(current, out HashSet<Type>? deps))
        {
            foreach (Type dep in deps)
            {
                Traverse(dep, visited, result, false);
            }
        }
    }

    private static ScriptValidation Validate(Type componentType, ScriptMetadata metadata, Type[] dependencies)
    {
        List<string> errors = new();
        List<string> warnings = new();

        if (string.IsNullOrWhiteSpace(metadata.ScriptName))
        {
            errors.Add("Script name is required.");
        }
        else if (metadata.ScriptName.Length > 100)
        {
            warnings.Add("Script name is unusually long (>100 characters).");
        }

        if (metadata.Priority is < -1000 or > 1000)
        {
            warnings.Add("Priority outside recommended range (-1000 to 1000).");
        }

        foreach (Type dep in dependencies)
        {
            if (dep == null)
            {
                errors.Add("Dependencies must be component types.");
            }
        }

        if (HasCircularDependency(componentType, dependencies))
        {
            errors.Add("Circular dependency detected.");
        }

        if (metadata.Version != null &&
            !Regex.IsMatch(metadata.Version, @"^\d+\.\d+\.\d+(-[\w\.-]+)?$", RegexOptions.CultureInvariant))
        {
            warnings.Add("Version should follow semantic versioning (x.y.z).");
        }

        if (metadata.Deprecated && metadata.DeprecationMessage == null)
        {
            warnings.Add("Deprecated scripts should include a deprecation message.");
        }

        return new ScriptValidation(errors.Count == 0, [.. errors], [.. warnings]);
    }

    private static bool HasCircularDependency(Type componentType, Type[] dependencies)
    {
        HashSet<Type> visited = new();
        HashSet<Type> stack = new();

        lock (_gate)
        {
            _dependencies[componentType] = new HashSet<Type>(dependencies);
            bool cycle = Dfs(componentType, visited, stack);
            if (cycle)
            {
                _dependencies.Remove(componentType);
            }

            return cycle;
        }

        bool Dfs(Type current, HashSet<Type> seen, HashSet<Type> path)
        {
            if (path.Contains(current))
            {
                return true;
            }

            if (!seen.Add(current))
            {
                return false;
            }

            path.Add(current);
            if (_dependencies.TryGetValue(current, out HashSet<Type>? deps))
            {
                foreach (Type dep in deps)
                {
                    if (Dfs(dep, seen, path))
                    {
                        return true;
                    }
                }
            }

            path.Remove(current);
            return false;
        }
    }
}
