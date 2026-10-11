using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace MxFramework.Authoring
{
    /// <summary>单条引擎依赖违规记录。</summary>
    public sealed class AssemblyDependencyViolation
    {
        public AssemblyDependencyViolation(
            string assemblyPath,
            string referencedAssemblyName,
            int depth,
            IReadOnlyList<string> dependencyChain)
        {
            AssemblyPath = assemblyPath ?? string.Empty;
            ReferencedAssemblyName = referencedAssemblyName ?? string.Empty;
            Depth = depth;
            DependencyChain = dependencyChain ?? Array.Empty<string>();
        }

        /// <summary>发现违规引用的程序集路径。</summary>
        public string AssemblyPath { get; }

        /// <summary>被引用的引擎程序集名，例如 <c>UnityEngine.CoreModule</c>。</summary>
        public string ReferencedAssemblyName { get; }

        /// <summary>从根程序集到本程序集的传递深度，根为 0。</summary>
        public int Depth { get; }

        /// <summary>传递依赖链，便于定位"谁是污染源"。</summary>
        public IReadOnlyList<string> DependencyChain { get; }

        public override string ToString()
        {
            return $"'{AssemblyPath}' (depth {Depth}) transitively references engine assembly " +
                   $"'{ReferencedAssemblyName}' via chain: {string.Join(" -> ", DependencyChain)}";
        }
    }

    /// <summary>递归程序集依赖闭包扫描结果。</summary>
    public sealed class AssemblyDependencyScanResult
    {
        public AssemblyDependencyScanResult(
            string rootAssemblyPath,
            IReadOnlyList<string> scannedAssemblies,
            IReadOnlyList<string> unresolvedReferences,
            IReadOnlyList<AssemblyDependencyViolation> violations)
        {
            RootAssemblyPath = rootAssemblyPath ?? string.Empty;
            ScannedAssemblies = scannedAssemblies ?? Array.Empty<string>();
            UnresolvedReferences = unresolvedReferences ?? Array.Empty<string>();
            Violations = violations ?? Array.Empty<AssemblyDependencyViolation>();
        }

        public string RootAssemblyPath { get; }

        /// <summary>实际被扫描的程序集（含根）。</summary>
        public IReadOnlyList<string> ScannedAssemblies { get; }

        /// <summary>无法在探测目录中定位的非系统引用，格式 <c>请求方 -> 引用名</c>。</summary>
        public IReadOnlyList<string> UnresolvedReferences { get; }

        public IReadOnlyList<AssemblyDependencyViolation> Violations { get; }

        /// <summary>是否零引擎依赖。</summary>
        public bool IsClean => Violations.Count == 0;

        /// <summary>是否存在无法解析的传递依赖。</summary>
        public bool HasUnresolvedReferences => UnresolvedReferences.Count > 0;
    }

    /// <summary>
    /// 递归程序集依赖闭包扫描器。对应规范 v1.4 第 6.1 节。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与规范参考实现的三点差异（均为安全语义修正，不改变判定标准）：
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// 规范按 <c>Path.Combine(目录, refName + ".dll")</c> 单点解析传递依赖，
    /// <b>找不到就静默跳过</b>。在 NuGet 缓存 / Unity <c>Library/ScriptAssemblies</c> /
    /// <c>obj</c> 混合布局下这会大量漏判，使"通过"失去意义。本实现改为在
    /// 显式探测目录集合中查找，并把无法解析的引用记录到
    /// <see cref="AssemblyDependencyScanResult.UnresolvedReferences"/>；
    /// <see cref="AssertZeroEngineDependency"/> 在发现未解析引用时默认失败。
    /// </description></item>
    /// <item><description>
    /// 规范只报告"某个程序集引用了引擎"，不给出传递路径。本实现记录完整
    /// <see cref="AssemblyDependencyViolation.DependencyChain"/>，用于定位污染源。
    /// </description></item>
    /// <item><description>
    /// 规范用 <c>visited</c> 判重后 <c>continue</c>，会在同层重复入队时提前返回。
    /// 本实现在出队时判重，语义等价且不会丢失更深层路径的扫描。
    /// </description></item>
    /// </list>
    /// </remarks>
    public static class RecursiveAssemblySecurityValidator
    {
        private static readonly string[] EngineAssemblyPrefixes = { "UnityEngine", "UnityEditor" };

        private static readonly string[] SystemAssemblyPrefixes = { "System.", "Microsoft.", "Microsoft" };

        private static readonly string[] SystemAssemblyExactNames = { "mscorlib", "netstandard", "WindowsBase", "System" };

        /// <summary>
        /// 断言程序集及其全部传递依赖不引用任何引擎程序集。
        /// 发现违规或存在未解析引用时抛出异常。
        /// </summary>
        public static void AssertZeroEngineDependency(string rootAssemblyPath)
        {
            AssertZeroEngineDependency(rootAssemblyPath, null, strictUnresolvedReferences: true);
        }

        public static void AssertZeroEngineDependency(
            string rootAssemblyPath,
            IEnumerable<string> probeDirectories,
            bool strictUnresolvedReferences)
        {
            AssemblyDependencyScanResult result = Scan(rootAssemblyPath, probeDirectories);

            if (result.Violations.Count > 0)
            {
                throw new CharacterDiagnosticException(
                    rootAssemblyPath,
                    "Dependency Violation: " + string.Join("; ", result.Violations.Select(item => item.ToString())) +
                    ". noEngine assemblies must not reference UnityEngine* or UnityEditor* transitively.",
                    CharacterGateCodes.PathTraversalOrSymbolicLink,
                    sourcePath: rootAssemblyPath);
            }

            if (strictUnresolvedReferences && result.HasUnresolvedReferences)
            {
                throw new CharacterDiagnosticException(
                    rootAssemblyPath,
                    "Dependency closure is incomplete: unable to resolve " +
                    string.Join(", ", result.UnresolvedReferences) +
                    ". Zero-engine-dependency cannot be proven; add the owning directories to probeDirectories.",
                    sourcePath: rootAssemblyPath);
            }
        }

        /// <summary>
        /// 执行递归闭包扫描。
        /// </summary>
        /// <param name="rootAssemblyPath">根程序集绝对路径。</param>
        /// <param name="probeDirectories">
        /// 额外程序集探测目录。根程序集所在目录始终会被探测。
        /// </param>
        public static AssemblyDependencyScanResult Scan(
            string rootAssemblyPath,
            IEnumerable<string> probeDirectories = null)
        {
            if (string.IsNullOrEmpty(rootAssemblyPath))
            {
                throw new ArgumentException("rootAssemblyPath is required.", nameof(rootAssemblyPath));
            }

            string rootFullPath = Path.GetFullPath(rootAssemblyPath);
            if (!File.Exists(rootFullPath))
            {
                throw new FileNotFoundException($"Root assembly not found: '{rootFullPath}'.", rootFullPath);
            }

            var probeRoots = new List<string>();
            AddProbeDirectory(probeRoots, Path.GetDirectoryName(rootFullPath));
            if (probeDirectories != null)
            {
                foreach (string directory in probeDirectories)
                {
                    AddProbeDirectory(probeRoots, directory);
                }
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var scanned = new List<string>();
            var unresolved = new List<string>();
            var violations = new List<AssemblyDependencyViolation>();

            var queue = new Queue<ScanNode>();
            queue.Enqueue(new ScanNode(rootFullPath, 0, new[] { Path.GetFileName(rootFullPath) }));

            while (queue.Count > 0)
            {
                ScanNode node = queue.Dequeue();
                if (!visited.Add(node.Path))
                {
                    continue;
                }

                if (!File.Exists(node.Path))
                {
                    continue;
                }

                scanned.Add(node.Path);

                foreach (string referenceName in ReadAssemblyReferences(node.Path))
                {
                    if (IsEngineAssembly(referenceName))
                    {
                        var chain = new List<string>(node.Chain) { referenceName };
                        violations.Add(new AssemblyDependencyViolation(
                            node.Path,
                            referenceName,
                            node.Depth,
                            chain));
                        continue;
                    }

                    if (IsSystemAssembly(referenceName))
                    {
                        continue;
                    }

                    string candidate = ResolveAssemblyPath(referenceName, probeRoots);
                    if (candidate == null)
                    {
                        unresolved.Add($"{Path.GetFileName(node.Path)} -> {referenceName}");
                        continue;
                    }

                    if (!visited.Contains(candidate))
                    {
                        var childChain = new List<string>(node.Chain) { Path.GetFileName(candidate) };
                        queue.Enqueue(new ScanNode(candidate, node.Depth + 1, childChain));
                    }
                }
            }

            scanned.Sort(StringComparer.OrdinalIgnoreCase);
            unresolved.Sort(StringComparer.Ordinal);

            return new AssemblyDependencyScanResult(rootFullPath, scanned, unresolved, violations);
        }

        private static IReadOnlyList<string> ReadAssemblyReferences(string assemblyPath)
        {
            var names = new List<string>();

            using FileStream stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);

            if (!peReader.HasMetadata)
            {
                return names;
            }

            MetadataReader metadataReader = peReader.GetMetadataReader();
            foreach (AssemblyReferenceHandle handle in metadataReader.AssemblyReferences)
            {
                AssemblyReference reference = metadataReader.GetAssemblyReference(handle);
                names.Add(metadataReader.GetString(reference.Name));
            }

            return names;
        }

        private static bool IsEngineAssembly(string name)
        {
            for (int i = 0; i < EngineAssemblyPrefixes.Length; i++)
            {
                if (name.StartsWith(EngineAssemblyPrefixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSystemAssembly(string name)
        {
            for (int i = 0; i < SystemAssemblyExactNames.Length; i++)
            {
                if (name.Equals(SystemAssemblyExactNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            for (int i = 0; i < SystemAssemblyPrefixes.Length; i++)
            {
                if (name.StartsWith(SystemAssemblyPrefixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveAssemblyPath(string referenceName, List<string> probeRoots)
        {
            string fileName = referenceName + ".dll";
            for (int i = 0; i < probeRoots.Count; i++)
            {
                string candidate = Path.Combine(probeRoots[i], fileName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            return null;
        }

        private static void AddProbeDirectory(List<string> probeRoots, string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            string full = Path.GetFullPath(directory);
            if (!probeRoots.Contains(full, StringComparer.OrdinalIgnoreCase))
            {
                probeRoots.Add(full);
            }
        }

        private readonly struct ScanNode
        {
            public ScanNode(string path, int depth, IReadOnlyList<string> chain)
            {
                Path = path;
                Depth = depth;
                Chain = chain;
            }

            public string Path { get; }

            public int Depth { get; }

            public IReadOnlyList<string> Chain { get; }
        }
    }
}
