using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;

namespace MxFramework.Authoring.Cli;

/// <summary>LowFreqAlloc. Freezes a verified managed dependency closure before loading any code.</summary>
/// <remarks>Only memory streams reach the CLR loader. No source image or file handle survives capture.</remarks>
internal sealed class TacticalCardAssemblyLoadContext : AssemblyLoadContext, IDisposable
{
    private sealed record Image(string Path, byte[] Bytes, AssemblyName Identity, Guid ModuleId,
        AssemblyName[] References, string Hash, DateTime LastWriteUtc);
    private readonly Dictionary<string, Image> _images;
    private static readonly HashSet<string> Framework = FrameworkNames();
    private bool _disposed;
    internal Assembly MainAssembly { get; }
    internal string RuntimeHash { get; }
    internal string SnapshotHash { get; }
    internal DateTime RuntimeLastWriteUtc { get; }
    internal DateTime CapturedUtc { get; }
    internal string[] SourcePaths => _images.Values.Select(i => i.Path).ToArray();
    internal int AssemblyCount => _images.Count;

    internal TacticalCardAssemblyLoadContext(string runtimePath, IReadOnlyDictionary<string, string> candidates,
        params string[] additionalAssemblies) : base(isCollectible: true)
    {
        try
        {
            _images = Capture(Path.GetFullPath(runtimePath), candidates, additionalAssemblies);
            var main = _images.Values.Single(i => string.Equals(i.Path, Path.GetFullPath(runtimePath), StringComparison.OrdinalIgnoreCase));
            RuntimeHash = main.Hash; RuntimeLastWriteUtc = main.LastWriteUtc; CapturedUtc = DateTime.UtcNow;
            SnapshotHash = Hash(Encoding.UTF8.GetBytes(string.Join("\n", _images.Values.OrderBy(i => i.Identity.Name, StringComparer.Ordinal)
                .Select(i => i.Identity.FullName + "|" + i.ModuleId + "|" + i.Path + "|" + i.Hash))));
            MainAssembly = LoadImage(main);
        }
        catch { Unload(); throw; }
    }

    protected override Assembly Load(AssemblyName name)
    {
        if (Framework.Contains(name.Name)) return null;
        if (!_images.TryGetValue(name.Name, out var image))
            throw new FileNotFoundException("ASSEMBLY_NOT_IN_VERIFIED_SNAPSHOT: " + name.FullName);
        RequireIdentity(name, image.Identity);
        return LoadImage(image);
    }

    private Assembly LoadImage(Image image)
    {
        using var memory = new MemoryStream(image.Bytes, writable: false);
        return LoadFromStream(memory);
    }

    private static Dictionary<string, Image> Capture(string mainPath, IReadOnlyDictionary<string, string> candidates, string[] additional)
    {
        Exception last = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string> aliases = null;
                string Resolve(AssemblyName reference)
                {
                    if (candidates.TryGetValue(reference.Name, out string direct)) return direct;
                    // Resolve renamed managed images by metadata identity without loading code.
                    if (aliases == null)
                    {
                        aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (string candidate in candidates.Values.Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            using var stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            using var pe = new PEReader(stream, PEStreamOptions.PrefetchMetadata);
                            if (!pe.HasMetadata) continue;
                            var reader = pe.GetMetadataReader();
                            if (reader.IsAssembly) aliases.TryAdd(reader.GetString(reader.GetAssemblyDefinition().Name), candidate);
                        }
                    }
                    if (aliases.TryGetValue(reference.Name, out string alias)) return alias;
                    throw new FileNotFoundException("ASSEMBLY_DEPENDENCY_UNAVAILABLE: " + reference.FullName);
                }
                void ReadClosure(string path, AssemblyName requested = null)
                {
                    var image = ReadImage(Path.GetFullPath(path));
                    if (requested != null) RequireIdentity(requested, image.Identity);
                    if (images.TryGetValue(image.Identity.Name, out var existing))
                    {
                        if (existing.Hash != image.Hash) throw new IOException("ASSEMBLY_CHANGED_DURING_CAPTURE: " + image.Identity.Name);
                        return;
                    }
                    images.Add(image.Identity.Name, image);
                    foreach (var reference in image.References)
                    {
                        if (Framework.Contains(reference.Name)) continue;
                        if (images.TryGetValue(reference.Name, out var captured)) { RequireIdentity(reference, captured.Identity); continue; }
                        ReadClosure(Resolve(reference), reference);
                    }
                }
                ReadClosure(mainPath);
                foreach (string name in additional)
                {
                    if (images.ContainsKey(name) || Framework.Contains(name)) continue;
                    ReadClosure(Resolve(new AssemblyName(name)));
                }
                // A second complete read verifies bytes and timestamps across the whole closure. A moving,
                // missing or half-written compiler output is rejected; no old snapshot is reused as fallback.
                foreach (var image in images.Values)
                {
                    byte[] verify = ReadShared(image.Path);
                    if (Hash(verify) != image.Hash || File.GetLastWriteTimeUtc(image.Path) != image.LastWriteUtc)
                        throw new IOException("ASSEMBLY_CHANGED_DURING_CAPTURE: " + image.Path);
                }
                return images;
            }
            catch (Exception error) when (error is IOException || error is BadImageFormatException || error is InvalidOperationException)
            { last = error; if (attempt < 2) Thread.Sleep(50 * (attempt + 1)); }
        }
        throw new IOException("ASSEMBLY_SNAPSHOT_UNAVAILABLE: compiler output or dependencies are unavailable/inconsistent; retry after compilation. " + last?.Message, last);
    }

    private static Image ReadImage(string path)
    {
        DateTime before = File.GetLastWriteTimeUtc(path);
        byte[] bytes = ReadShared(path);
        DateTime after = File.GetLastWriteTimeUtc(path);
        if (before != after) throw new IOException("ASSEMBLY_CHANGED_DURING_CAPTURE: " + path);
        using var memory = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(memory);
        if (!pe.HasMetadata) throw new BadImageFormatException("Managed assembly metadata missing: " + path);
        var metadata = pe.GetMetadataReader();
        if (!metadata.IsAssembly) throw new BadImageFormatException("Managed assembly definition missing: " + path);
        var definition = metadata.GetAssemblyDefinition();
        var identity = new AssemblyName { Name = metadata.GetString(definition.Name), Version = definition.Version,
            CultureName = metadata.GetString(definition.Culture) };
        identity.SetPublicKey(metadata.GetBlobBytes(definition.PublicKey));
        var references = metadata.AssemblyReferences.Select(handle =>
        {
            var reference = metadata.GetAssemblyReference(handle);
            var name = new AssemblyName { Name = metadata.GetString(reference.Name), Version = reference.Version,
                CultureName = metadata.GetString(reference.Culture) };
            byte[] key = metadata.GetBlobBytes(reference.PublicKeyOrToken);
            if ((reference.Flags & AssemblyFlags.PublicKey) != 0) name.SetPublicKey(key); else name.SetPublicKeyToken(key);
            return name;
        }).ToArray();
        return new Image(path, bytes, identity, metadata.GetGuid(metadata.GetModuleDefinition().Mvid), references, Hash(bytes), after);
    }

    private static byte[] ReadShared(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var output = new MemoryStream(); input.CopyTo(output); return output.ToArray();
    }

    private static void RequireIdentity(AssemblyName reference, AssemblyName definition)
    {
        if (!string.Equals(reference.Name, definition.Name, StringComparison.OrdinalIgnoreCase)
            // Match AssemblyLoadContext's equal-or-higher version rule, while preserving name/key/culture.
            || reference.Version != null && definition.Version < reference.Version
            || reference.Version != null && reference.CultureName != null && !string.Equals(reference.CultureName, definition.CultureName ?? "", StringComparison.OrdinalIgnoreCase)
            || reference.Version != null && reference.GetPublicKeyToken() is byte[] token && !token.SequenceEqual(definition.GetPublicKeyToken() ?? Array.Empty<byte>()))
            throw new IOException("ASSEMBLY_DEPENDENCY_IDENTITY_MISMATCH: " + reference.FullName + " versus " + definition.FullName);
    }

    private static HashSet<string> FrameworkNames()
    {
        string directory = Path.GetDirectoryName(typeof(object).Assembly.Location) + Path.DirectorySeparatorChar;
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mscorlib", "netstandard" };
        foreach (string path in ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator))
            if (path.StartsWith(directory, StringComparison.OrdinalIgnoreCase)) result.Add(Path.GetFileNameWithoutExtension(path));
        return result;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public void Dispose() { if (_disposed) return; _disposed = true; Unload(); }
}
