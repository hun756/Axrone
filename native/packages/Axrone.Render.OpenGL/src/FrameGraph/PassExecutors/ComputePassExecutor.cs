namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// Specifies the type of a compute resource binding.
/// </summary>
public enum ComputeBindingType
{
    /// <summary>Shader Storage Buffer Object (SSBO) bound via <c>glBindBufferBase</c>.</summary>
    ShaderStorageBuffer = 0,

    /// <summary>Texture image bound via <c>glBindImageTexture</c>.</summary>
    Image = 1,

    /// <summary>Uniform Buffer Object (UBO) bound via <c>glBindBufferBase</c>.</summary>
    UniformBuffer = 2,
}

/// <summary>
/// Describes a single resource binding for a compute pass.
/// </summary>
/// <param name="BindingPoint">The shader binding point (layout qualifier value).</param>
/// <param name="ResourceName">The resource name in the <see cref="PassExecutionContext"/>.</param>
/// <param name="Type">The binding type determining how the resource is bound.</param>
public readonly record struct ComputeResourceBinding(
    uint BindingPoint,
    string ResourceName,
    ComputeBindingType Type);

/// <summary>
/// GPU compute shader dispatch pass. Dispatches a compute shader with configurable
/// work group counts and resource bindings (SSBOs, images, UBOs).
/// </summary>
/// <remarks>
/// <para>Resources are resolved from the <see cref="PassExecutionContext"/> by name at
/// execution time. <see cref="GLBuffer"/> instances are bound as SSBOs or UBOs;
/// <see cref="GLTexture"/> instances are bound as images.</para>
/// </remarks>
public sealed class ComputePassExecutor : RenderPass
{
    private readonly GLProgram _computeShader;
    private readonly List<ComputeResourceBinding> _bindings = new();

    private uint _groupCountX = 1;
    private uint _groupCountY = 1;
    private uint _groupCountZ = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComputePassExecutor"/> class.
    /// </summary>
    /// <param name="name">The pass name.</param>
    /// <param name="computeShader">The compute shader program.</param>
    public ComputePassExecutor(string name, GLProgram computeShader)
        : base(name, FramePassKind.Compute)
    {
        ArgumentNullException.ThrowIfNull(computeShader);
        _computeShader = computeShader;
    }

    /// <summary>Gets the X dimension of the dispatch work group count.</summary>
    public uint GroupCountX
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _groupCountX;
    }

    /// <summary>Gets the Y dimension of the dispatch work group count.</summary>
    public uint GroupCountY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _groupCountY;
    }

    /// <summary>Gets the Z dimension of the dispatch work group count.</summary>
    public uint GroupCountZ
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _groupCountZ;
    }

    /// <summary>Gets the resource bindings as a read-only list.</summary>
    public IReadOnlyList<ComputeResourceBinding> Bindings
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bindings;
    }

    /// <summary>
    /// Sets the dispatch work group count.
    /// </summary>
    /// <param name="x">X dimension. Must be at least 1.</param>
    /// <param name="y">Y dimension. Must be at least 1.</param>
    /// <param name="z">Z dimension. Must be at least 1.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ComputePassExecutor WithDispatchSize(uint x, uint y = 1, uint z = 1)
    {
        _groupCountX = x;
        _groupCountY = y;
        _groupCountZ = z;
        return this;
    }

    /// <summary>
    /// Adds a Shader Storage Buffer Object binding.
    /// </summary>
    /// <param name="bindingPoint">The shader binding point.</param>
    /// <param name="resourceName">The buffer resource name in the pass context.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ComputePassExecutor BindStorageBuffer(uint bindingPoint, string resourceName)
    {
        _bindings.Add(new ComputeResourceBinding(bindingPoint, resourceName, ComputeBindingType.ShaderStorageBuffer));
        Reads(resourceName);
        return this;
    }

    /// <summary>
    /// Adds a texture image binding.
    /// </summary>
    /// <param name="bindingPoint">The image unit.</param>
    /// <param name="resourceName">The texture resource name in the pass context.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ComputePassExecutor BindImage(uint bindingPoint, string resourceName)
    {
        _bindings.Add(new ComputeResourceBinding(bindingPoint, resourceName, ComputeBindingType.Image));
        Reads(resourceName);
        return this;
    }

    /// <summary>
    /// Adds a Uniform Buffer Object binding.
    /// </summary>
    /// <param name="bindingPoint">The uniform block binding point.</param>
    /// <param name="resourceName">The buffer resource name in the pass context.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ComputePassExecutor BindUniformBuffer(uint bindingPoint, string resourceName)
    {
        _bindings.Add(new ComputeResourceBinding(bindingPoint, resourceName, ComputeBindingType.UniformBuffer));
        Reads(resourceName);
        return this;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        if (_groupCountX < 1 || _groupCountY < 1 || _groupCountZ < 1)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                $"Dispatch group counts must be at least 1, got ({_groupCountX}, {_groupCountY}, {_groupCountZ})",
                nameof(ComputePassExecutor));
        }

        for (int i = 0; i < _bindings.Count; i++)
        {
            if (string.IsNullOrEmpty(_bindings[i].ResourceName))
            {
                ThrowHelper.Throw(RenderErrorCode.InvalidPassConfiguration,
                    $"Compute resource binding at index {i} has an empty resource name",
                    nameof(ComputePassExecutor));
            }
        }
    }

    /// <inheritdoc/>
    public override void Execute(GLContext context, PassExecutionContext ctx)
    {
        context.AssertRenderThread();

        var gl = context.GL;

        // Bind compute shader.
        context.State.UseProgram(_computeShader.Id);

        // Bind resources.
        for (int i = 0; i < _bindings.Count; i++)
        {
            var binding = _bindings[i];

            switch (binding.Type)
            {
                case ComputeBindingType.ShaderStorageBuffer:
                    {
                        var buffer = ctx.GetBuffer(binding.ResourceName);
                        gl.BindBufferBase(GLConst.ShaderStorageBuffer, binding.BindingPoint, buffer.Id);
                        break;
                    }

                case ComputeBindingType.UniformBuffer:
                    {
                        var buffer = ctx.GetBuffer(binding.ResourceName);
                        gl.BindBufferBase(GLConst.UniformBuffer, binding.BindingPoint, buffer.Id);
                        break;
                    }

                case ComputeBindingType.Image:
                    {
                        var texture = ctx.GetTexture(binding.ResourceName);
                        gl.BindImageTexture(binding.BindingPoint, texture.Id, 0, false, 0, 0x88B8, texture.FormatInfo.InternalFormat); // GL_WRITE_ONLY
                        break;
                    }
            }
        }

        gl.DispatchCompute(_groupCountX, _groupCountY, _groupCountZ);
        gl.MemoryBarrier(GLConst.AllShaderStorageBits);
    }
}
