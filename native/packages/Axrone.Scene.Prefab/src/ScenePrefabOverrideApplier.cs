namespace Axrone.Scene.Prefab;

using System.Buffers;
using System.Text.Json.Nodes;

/// <summary>
/// Applies override operations to an immutable prefab definition. Every apply
/// pass materializes a fresh definition, so callers can keep the authored base
/// untouched while variants and instances derive their own state.
/// </summary>
public static class ScenePrefabOverrides
{
    /// <summary>Applies operations in order and returns the derived definition.</summary>
    /// <exception cref="ScenePrefabException">
    /// A node, component or property path cannot be resolved, or the resulting
    /// hierarchy is invalid.
    /// </exception>
    public static ScenePrefabDefinition Apply(
        ScenePrefabDefinition definition,
        IReadOnlyList<ScenePrefabOverrideOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(operations);

        var state = new PrefabState(definition);

        for (int i = 0; i < operations.Count; i++)
        {
            ApplyOperation(state, operations[i]);
        }

        return state.ToDefinition();
    }

    private static void ApplyOperation(PrefabState state, ScenePrefabOverrideOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        switch (operation)
        {
            case ScenePrefabAddActorOperation add:
                AddActor(state, add);
                break;
            case ScenePrefabRemoveActorOperation remove:
                RemoveActor(state, remove);
                break;
            case ScenePrefabReparentActorOperation reparent:
                ReparentActor(state, reparent);
                break;
            case ScenePrefabSetActorFieldOperation setField:
                SetActorField(state, setField);
                break;
            case ScenePrefabAddComponentOperation addComponent:
                AddComponent(state, addComponent);
                break;
            case ScenePrefabRemoveComponentOperation removeComponent:
                RemoveComponent(state, removeComponent);
                break;
            case ScenePrefabReplaceComponentOperation replaceComponent:
                ReplaceComponent(state, replaceComponent);
                break;
            case ScenePrefabSetComponentPropertyOperation setProperty:
                SetComponentProperty(state, setProperty);
                break;
            case ScenePrefabUnsetComponentPropertyOperation unsetProperty:
                UnsetComponentProperty(state, unsetProperty);
                break;
            default:
                throw new ScenePrefabException(
                    ScenePrefabErrorCodes.InvalidOverride,
                    $"Unknown override operation '{operation.GetType().Name}'.");
        }
    }

    private static void AddActor(PrefabState state, ScenePrefabAddActorOperation operation)
    {
        ActorState actor = new(operation.Actor);
        if (!state.TryAdd(actor))
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.DuplicateNode,
                $"Actor '{actor.NodeId}' already exists.",
                actor.NodeId);
        }

        if (operation.AfterNodeId is not { } afterNodeId)
        {
            state.Actors.Add(actor);
            return;
        }

        int anchor = state.IndexOf(afterNodeId);
        if (anchor < 0)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.UnknownNode,
                $"Anchor node '{afterNodeId}' does not exist.",
                afterNodeId);
        }

        state.Actors.Insert(anchor + 1, actor);
    }

    private static void RemoveActor(PrefabState state, ScenePrefabRemoveActorOperation operation)
    {
        EnsureActor(state, operation.NodeId);

        var removed = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(operation.NodeId);

        while (pending.Count > 0)
        {
            string nodeId = pending.Pop();
            if (!removed.Add(nodeId))
            {
                continue;
            }

            foreach (ActorState actor in state.Actors)
            {
                if (string.Equals(actor.ParentNodeId, nodeId, StringComparison.Ordinal))
                {
                    pending.Push(actor.NodeId);
                }
            }
        }

        state.Actors.RemoveAll(actor => removed.Contains(actor.NodeId));
        state.RemoveNodes(removed);
    }

    private static void ReparentActor(PrefabState state, ScenePrefabReparentActorOperation operation)
    {
        ActorState actor = EnsureActor(state, operation.NodeId);
        if (operation.ParentNodeId is { } parentNodeId)
        {
            EnsureActor(state, parentNodeId);
        }

        actor.ParentNodeId = operation.ParentNodeId;
    }

    private static void SetActorField(PrefabState state, ScenePrefabSetActorFieldOperation operation)
    {
        ActorState actor = EnsureActor(state, operation.NodeId);

        switch (operation.Field)
        {
            case ScenePrefabActorField.Name:
                actor.Name = operation.Value.AsString();
                break;
            case ScenePrefabActorField.Layer:
                actor.Layer = ToLayer(operation.Value.AsInt32(), actor.NodeId);
                break;
            case ScenePrefabActorField.Tag:
                actor.Tag = operation.Value.AsString();
                break;
            case ScenePrefabActorField.Active:
                actor.Active = operation.Value.AsBoolean();
                break;
            case ScenePrefabActorField.Persistent:
                actor.Persistent = operation.Value.AsBoolean();
                break;
            case ScenePrefabActorField.Pooled:
                actor.Pooled = operation.Value.AsBoolean();
                break;
            default:
                throw new ScenePrefabException(
                    ScenePrefabErrorCodes.InvalidOverride,
                    $"Unknown actor field '{operation.Field}'.",
                    actor.NodeId);
        }
    }

    private static void AddComponent(PrefabState state, ScenePrefabAddComponentOperation operation)
    {
        ActorState actor = EnsureActor(state, operation.NodeId);
        SceneComponentSnapshot component = operation.Component;

        if (component.Id is { } componentId &&
            actor.Components.Exists(
                existing => string.Equals(existing.Id, componentId, StringComparison.Ordinal)))
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.DuplicateComponent,
                $"Component id '{componentId}' is already present.",
                actor.NodeId);
        }

        if (actor.Components.Exists(
                existing => string.Equals(existing.Type, component.Type, StringComparison.Ordinal)))
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.DuplicateComponent,
                $"Component type '{component.Type}' is already present.",
                actor.NodeId);
        }

        int index = operation.Index is { } requested
            ? Math.Clamp(requested, 0, actor.Components.Count)
            : actor.Components.Count;

        actor.Components.Insert(index, component);
    }

    private static void RemoveComponent(PrefabState state, ScenePrefabRemoveComponentOperation operation)
    {
        ActorState actor = EnsureActor(state, operation.NodeId);
        actor.Components.RemoveAt(EnsureComponentIndex(actor, operation.Selector));
    }

    private static void ReplaceComponent(PrefabState state, ScenePrefabReplaceComponentOperation operation)
    {
        ActorState actor = EnsureActor(state, operation.NodeId);
        int index = EnsureComponentIndex(actor, operation.Selector);

        SceneComponentSnapshot replacement = operation.Component;
        if (replacement.Id is { } componentId &&
            actor.Components.Exists(
                existing => !ReferenceEquals(existing, actor.Components[index]) &&
                            string.Equals(existing.Id, componentId, StringComparison.Ordinal)))
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.DuplicateComponent,
                $"Component id '{componentId}' is already present.",
                actor.NodeId);
        }

        actor.Components[index] = replacement;
    }

    private static void SetComponentProperty(
        PrefabState state,
        ScenePrefabSetComponentPropertyOperation operation)
    {
        ActorState actor = EnsureActor(state, operation.NodeId);
        int index = EnsureComponentIndex(actor, operation.Selector);
        SceneComponentSnapshot component = actor.Components[index];

        JsonElement data = SetPayloadValue(component.Data, operation.Path, operation.Value);
        actor.Components[index] = new SceneComponentSnapshot(component.Type, data, component.Id);
    }

    private static void UnsetComponentProperty(
        PrefabState state,
        ScenePrefabUnsetComponentPropertyOperation operation)
    {
        ActorState actor = EnsureActor(state, operation.NodeId);
        int index = EnsureComponentIndex(actor, operation.Selector);
        SceneComponentSnapshot component = actor.Components[index];

        if (operation.Path.Count == 0)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.InvalidPath,
                "Component payload properties cannot unset the payload root.",
                actor.NodeId);
        }

        JsonElement data = UnsetPayloadValue(component.Data, operation.Path);
        if (data.ValueKind != JsonValueKind.Object)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.InvalidPath,
                $"Unsetting '{Describe(operation.Path)}' removes the payload root.",
                actor.NodeId);
        }

        actor.Components[index] = new SceneComponentSnapshot(component.Type, data, component.Id);
    }

    private static ActorState EnsureActor(PrefabState state, string nodeId) =>
        state.TryGet(nodeId, out ActorState? actor)
            ? actor
            : throw new ScenePrefabException(
                ScenePrefabErrorCodes.UnknownNode,
                $"Node '{nodeId}' does not exist.",
                nodeId);

    private static int EnsureComponentIndex(ActorState actor, in ScenePrefabComponentSelector selector)
    {
        int index = FindComponentIndex(actor.Components, selector);
        if (index >= 0)
        {
            return index;
        }

        throw new ScenePrefabException(
            ScenePrefabErrorCodes.UnknownComponent,
            $"Actor '{actor.NodeId}' has no component matching {Describe(selector)}.",
            actor.NodeId);
    }

    private static int FindComponentIndex(
        List<SceneComponentSnapshot> components,
        in ScenePrefabComponentSelector selector)
    {
        if (selector.Kind == ScenePrefabComponentSelectorKind.Id)
        {
            for (int i = 0; i < components.Count; i++)
            {
                if (string.Equals(components[i].Id, selector.ComponentId, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        if (selector.Type is not { } type)
        {
            return -1;
        }

        int targetOccurrence = selector.Kind == ScenePrefabComponentSelectorKind.Type
            ? selector.Occurrence
            : 0;
        int occurrence = 0;

        for (int i = 0; i < components.Count; i++)
        {
            if (!string.Equals(components[i].Type, type, StringComparison.Ordinal))
            {
                continue;
            }

            if (occurrence == targetOccurrence)
            {
                return i;
            }

            occurrence++;
        }

        return -1;
    }

    private static ushort ToLayer(int value, string nodeId)
    {
        if (value is < ushort.MinValue or > ushort.MaxValue)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.InvalidOverride,
                $"Layer must be within [0, {ushort.MaxValue}]; received {value}.",
                nodeId);
        }

        return (ushort)value;
    }

    private static string Describe(in ScenePrefabComponentSelector selector) =>
        selector.Kind == ScenePrefabComponentSelectorKind.Id
            ? $"id '{selector.ComponentId}'"
            : $"type '{selector.Type}' at occurrence {selector.Occurrence}";

    private static string Describe(ScenePrefabPropertyPath path)
    {
        var builder = new System.Text.StringBuilder();
        for (int i = 0; i < path.Count; i++)
        {
            ScenePrefabPropertyPathSegment segment = path[i];
            if (segment.IsIndex)
            {
                builder.Append('[').Append(segment.Index).Append(']');
            }
            else
            {
                if (builder.Length > 0)
                {
                    builder.Append('.');
                }

                builder.Append(segment.Name);
            }
        }

        return builder.ToString();
    }

    private static JsonElement SetPayloadValue(
        JsonElement payload,
        ScenePrefabPropertyPath path,
        JsonElement value)
    {
        if (path.Count == 0)
        {
            return CloneElement(value);
        }

        JsonNode? root = JsonNode.Parse(payload.GetRawText());
        JsonNode? updated = SetNode(root, path, 0, value);
        if (updated is null)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.InvalidPath,
                $"Setting '{Describe(path)}' produced an empty payload.");
        }

        return ToElement(updated);
    }

    private static JsonElement UnsetPayloadValue(JsonElement payload, ScenePrefabPropertyPath path)
    {
        JsonNode? root = JsonNode.Parse(payload.GetRawText());
        JsonNode? updated = UnsetNode(root, path, 0);
        if (updated is null)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.InvalidPath,
                $"Unsetting '{Describe(path)}' produced an empty payload.");
        }

        return ToElement(updated);
    }

    private static JsonNode? SetNode(
        JsonNode? current,
        ScenePrefabPropertyPath path,
        int index,
        JsonElement value)
    {
        if (index == path.Count)
        {
            return ParseValue(value);
        }

        ScenePrefabPropertyPathSegment segment = path[index];

        if (segment.IsIndex)
        {
            JsonArray array = current as JsonArray ?? new JsonArray();
            int target = segment.Index;
            JsonNode? existing = target < array.Count ? Detach(array, target) : null;
            JsonNode? updated = SetNode(existing, path, index + 1, value);

            while (array.Count < target)
            {
                array.Add(null);
            }

            array.Insert(target, updated);
            return array;
        }

        JsonObject obj = current as JsonObject ?? new JsonObject();
        string name = segment.Name;
        JsonNode? child = obj.TryGetPropertyValue(name, out JsonNode? existingNode)
            ? Detach(obj, name)
            : null;
        JsonNode? nested = SetNode(child, path, index + 1, value);
        obj[name] = nested;
        return obj;
    }

    private static JsonNode? UnsetNode(JsonNode? current, ScenePrefabPropertyPath path, int index)
    {
        ScenePrefabPropertyPathSegment segment = path[index];
        bool last = index == path.Count - 1;

        if (segment.IsIndex)
        {
            JsonArray array = current as JsonArray ?? new JsonArray();
            int target = segment.Index;

            if (last)
            {
                if (target < array.Count)
                {
                    array.RemoveAt(target);
                }

                return array;
            }

            JsonNode? existing = target < array.Count ? Detach(array, target) : null;
            JsonNode? updated = UnsetNode(existing, path, index + 1);

            while (array.Count < target)
            {
                array.Add(null);
            }

            array.Insert(target, updated);
            return array;
        }

        JsonObject obj = current as JsonObject ?? new JsonObject();
        string name = segment.Name;

        if (last)
        {
            obj.Remove(name);
            return obj;
        }

        JsonNode? child = obj.TryGetPropertyValue(name, out _) ? Detach(obj, name) : null;
        JsonNode? nested = UnsetNode(child, path, index + 1);
        obj[name] = nested;
        return obj;
    }

    private static JsonNode? Detach(JsonArray array, int index)
    {
        JsonNode? node = array[index];
        array.RemoveAt(index);
        return node;
    }

    private static JsonNode? Detach(JsonObject obj, string name)
    {
        JsonNode? node = obj[name];
        obj.Remove(name);
        return node;
    }

    private static JsonNode? ParseValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.InvalidOverride,
                "Component property values must not be undefined.");
        }

        return JsonNode.Parse(value.GetRawText());
    }

    private static JsonElement CloneElement(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.InvalidPath,
                "Component payloads must remain JSON objects.");
        }

        return value.Clone();
    }

    private static JsonElement ToElement(JsonNode node)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            node.WriteTo(writer);
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private sealed class ActorState
    {
        public ActorState(SceneActorSnapshot snapshot)
        {
            NodeId = snapshot.NodeId;
            ParentNodeId = snapshot.ParentNodeId;
            Name = snapshot.Name;
            Layer = snapshot.Layer;
            Tag = snapshot.Tag;
            Active = snapshot.Active;
            Persistent = snapshot.Persistent;
            Pooled = snapshot.Pooled;
            Components = [.. snapshot.Components];
        }

        public string NodeId { get; }

        public string? ParentNodeId { get; set; }

        public string Name { get; set; }

        public ushort Layer { get; set; }

        public string Tag { get; set; }

        public bool Active { get; set; }

        public bool Persistent { get; set; }

        public bool Pooled { get; set; }

        public List<SceneComponentSnapshot> Components { get; }

        public SceneActorSnapshot ToSnapshot() => new(
            NodeId,
            Name,
            Layer,
            Tag,
            Active,
            Persistent,
            Pooled,
            Components,
            ParentNodeId);
    }

    private sealed class PrefabState
    {
        private readonly Dictionary<string, ActorState> _byNode = new(StringComparer.Ordinal);

        public PrefabState(ScenePrefabDefinition definition)
        {
            Id = definition.Id;
            Kind = definition.Kind;
            Metadata = definition.Metadata;
            Actors = new List<ActorState>(definition.Actors.Count);

            for (int i = 0; i < definition.Actors.Count; i++)
            {
                SceneActorSnapshot snapshot = definition.Actors[i];
                var actor = new ActorState(snapshot);
                Actors.Add(actor);
                _byNode.Add(actor.NodeId, actor);
            }
        }

        public string Id { get; }

        public ScenePrefabKind Kind { get; }

        public ScenePrefabMetadata? Metadata { get; }

        public List<ActorState> Actors { get; }

        public bool TryAdd(ActorState actor) => _byNode.TryAdd(actor.NodeId, actor);

        public bool TryGet(string nodeId, [NotNullWhen(true)] out ActorState? actor) =>
            _byNode.TryGetValue(nodeId, out actor);

        public int IndexOf(string nodeId)
        {
            for (int i = 0; i < Actors.Count; i++)
            {
                if (string.Equals(Actors[i].NodeId, nodeId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public void RemoveNodes(HashSet<string> nodeIds)
        {
            foreach (string nodeId in nodeIds)
            {
                _byNode.Remove(nodeId);
            }
        }

        public ScenePrefabDefinition ToDefinition()
        {
            var snapshots = new SceneActorSnapshot[Actors.Count];
            for (int i = 0; i < Actors.Count; i++)
            {
                snapshots[i] = Actors[i].ToSnapshot();
            }

            return new ScenePrefabDefinition(Id, snapshots, Kind, Metadata);
        }
    }
}
