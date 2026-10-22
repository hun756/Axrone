using System.Runtime.InteropServices;

namespace Axrone.AssetGltf.Codecs;

/// <summary>
/// Raw P/Invoke surface for the bundled <c>axrone_draco</c> native library.
/// The library is a thin C ABI bridge over the official google/draco C++
/// sources (Apache-2.0), built per RID from <c>native/extern/draco-bridge</c>.
/// All functions use integer status codes so no exception or string marshaling
/// crosses the boundary: 0 is success, anything else is failure.
/// </summary>
internal static partial class NativeDracoInterop
{
    internal const string LibraryName = "axrone_draco";

    internal const int Ok = 0;

    static NativeDracoInterop()
    {
        // NativeAOT has no deps.json, so the default probing never looks in
        // runtimes/<rid>/native. Resolve the RID asset explicitly; returning
        // zero falls back to the host's normal resolution.
        NativeLibrary.SetDllImportResolver(
            typeof(NativeDracoInterop).Assembly,
            static (libraryName, _, _) =>
                string.Equals(libraryName, LibraryName, StringComparison.Ordinal) &&
                FindNativeLibraryPath() is { } path
                    ? NativeLibrary.Load(path)
                    : 0);
    }

    /// <summary>Decodes one Draco bitstream into a heap-allocated native mesh.</summary>
    /// <returns>0 on success; 1 on bad arguments; 2 when the data is not a Draco mesh.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibraryName, EntryPoint = "axr_draco_decode")]
    internal static unsafe partial int Decode(byte* data, nuint size, out nint mesh);

    /// <summary>Frees a mesh previously returned by <see cref="Decode"/>.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibraryName, EntryPoint = "axr_draco_free")]
    internal static unsafe partial void FreeMesh(nint mesh);

    /// <summary>Returns the vertex count of a decoded mesh.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibraryName, EntryPoint = "axr_draco_point_count")]
    internal static unsafe partial uint PointCount(nint mesh);

    /// <summary>Returns the triangle count of a decoded mesh.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibraryName, EntryPoint = "axr_draco_face_count")]
    internal static unsafe partial uint FaceCount(nint mesh);

    /// <summary>Returns the number of point attributes of a decoded mesh.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibraryName, EntryPoint = "axr_draco_attribute_count")]
    internal static unsafe partial int AttributeCount(nint mesh);

    /// <summary>
    /// Describes one attribute: its Draco unique id, component count (1-4),
    /// and attribute type (0 POSITION, 1 NORMAL, 2 COLOR, 3 TEX_COORD, 4 GENERIC).
    /// </summary>
    /// <returns>0 on success; 1 on bad arguments; 3 when the layout is unsupported.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibraryName, EntryPoint = "axr_draco_attribute_info")]
    internal static unsafe partial int AttributeInfo(
        nint mesh,
        int attributeIndex,
        out uint uniqueId,
        out int components,
        out int attributeType);

    /// <summary>
    /// Converts every point of one attribute to float and writes
    /// <paramref name="floatCount"/> values (points times components).
    /// </summary>
    /// <returns>0 on success; 1 on bad arguments; 3 when conversion fails.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibraryName, EntryPoint = "axr_draco_attribute_floats")]
    internal static unsafe partial int AttributeFloats(
        nint mesh,
        int attributeIndex,
        float* values,
        int floatCount);

    /// <summary>Writes <paramref name="indexCount"/> (faces times 3) triangle indices.</summary>
    /// <returns>0 on success; 1 on bad arguments.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibraryName, EntryPoint = "axr_draco_face_indices")]
    internal static unsafe partial int FaceIndices(nint mesh, uint* indices, int indexCount);

    /// <summary>
    /// Locates the RID-specific native library without loading it.
    /// Mirrors the .NET <c>runtimes/&lt;rid&gt;/native</c> probing layout used at pack time.
    /// </summary>
    /// <remarks>
    /// <see cref="RuntimeInformation.RuntimeIdentifier"/> can be distro-specific
    /// (for example <c>ubuntu.24.04-x64</c>) while the package ships portable
    /// RIDs, so the portable RID and the host's native search directories are
    /// probed as fallbacks.
    /// </remarks>
    internal static string? FindNativeLibraryPath()
    {
        string fileName = OperatingSystem.IsWindows()
            ? "axrone_draco.dll"
            : OperatingSystem.IsMacOS()
                ? "libaxrone_draco.dylib"
                : "libaxrone_draco.so";

        string baseDirectory = AppContext.BaseDirectory;
        string rid = RuntimeInformation.RuntimeIdentifier;

        string? path = Probe(Path.Combine(baseDirectory, "runtimes", rid, "native"), fileName);
        if (path is not null)
        {
            return path;
        }

        string portableRid = GetPortableRuntimeIdentifier();
        if (!string.Equals(portableRid, rid, StringComparison.Ordinal))
        {
            path = Probe(Path.Combine(baseDirectory, "runtimes", portableRid, "native"), fileName);
            if (path is not null)
            {
                return path;
            }
        }

        if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string searchDirectories)
        {
            foreach (string directory in searchDirectories.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                path = Probe(directory, fileName);
                if (path is not null)
                {
                    return path;
                }
            }
        }

        return Probe(baseDirectory, fileName);

        static string? Probe(string directory, string fileName)
        {
            string candidate = Path.Combine(directory, fileName);
            return File.Exists(candidate) ? candidate : null;
        }
    }

    /// <summary>Portable RID for the current OS and process architecture.</summary>
    internal static string GetPortableRuntimeIdentifier()
    {
        string architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => "x64",
        };

        if (OperatingSystem.IsWindows())
        {
            return "win-" + architecture;
        }

        if (OperatingSystem.IsAndroid())
        {
            return "android-" + architecture;
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx-" + architecture;
        }

        return "linux-" + architecture;
    }

    internal static bool IsOperatingSystemSupported =>
        OperatingSystem.IsWindows() ||
        OperatingSystem.IsLinux() ||
        OperatingSystem.IsMacOS() ||
        OperatingSystem.IsAndroid();

    internal static bool IsNativeLibraryAvailable =>
        IsOperatingSystemSupported && FindNativeLibraryPath() is not null;
}
