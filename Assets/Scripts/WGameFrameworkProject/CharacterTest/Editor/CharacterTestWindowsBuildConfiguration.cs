using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal enum CharacterTestWindowsBuildProfile { Release, Development }

    /// <summary>Single Windows x64 IL2CPP policy shared by menus, package tests and BatchMode.</summary>
    internal static class CharacterTestWindowsBuildConfiguration
    {
        internal const string Argument = "-character-test-package-test-configuration";
        internal const string ExperimentArgument = "-character-test-package-test-experiment";
        internal static string Experiment
        {
            get
            {
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, ExperimentArgument);
                if (index < 0) return "None";
                if (index + 1 >= args.Length || !new[] { "None", "OptimizeSize", "MediumStripping" }.Contains(args[index + 1]))
                    throw new BuildFailedException("Use one explicit experiment: None, OptimizeSize, or MediumStripping.");
                return args[index + 1];
            }
        }
        internal static void RequireExperimentPolicy(CharacterTestWindowsBuildProfile profile)
        {
            if (profile != CharacterTestWindowsBuildProfile.Release && Experiment != "None")
                throw new BuildFailedException("Optimization experiments require the Release baseline configuration.");
        }
        private static Il2CppCodeGeneration Generation => Experiment == "OptimizeSize" ? Il2CppCodeGeneration.OptimizeSize : Il2CppCodeGeneration.OptimizeSpeed;
        private static ManagedStrippingLevel Stripping => Experiment == "MediumStripping" ? ManagedStrippingLevel.Medium : ManagedStrippingLevel.Low;

        internal static CharacterTestWindowsBuildProfile Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return CharacterTestWindowsBuildProfile.Release;
            if (string.Equals(value, "Release", StringComparison.OrdinalIgnoreCase)) return CharacterTestWindowsBuildProfile.Release;
            if (string.Equals(value, "Development", StringComparison.OrdinalIgnoreCase)) return CharacterTestWindowsBuildProfile.Development;
            throw new ArgumentException("Unknown Windows build configuration: " + value + ". Use Release or Development.");
        }

        internal static BuildOptions Options(CharacterTestWindowsBuildProfile profile) =>
            profile == CharacterTestWindowsBuildProfile.Development
                ? BuildOptions.Development | BuildOptions.CompressWithLz4 : BuildOptions.CompressWithLz4HC;

        internal static Il2CppCompilerConfiguration Compiler(CharacterTestWindowsBuildProfile profile) =>
            Il2CppCompilerConfiguration.Release;

#if UNITY_6000_6_OR_NEWER
        internal static ManagedCodeVariant ManagedVariant(CharacterTestWindowsBuildProfile profile) =>
            profile == CharacterTestWindowsBuildProfile.Development
                ? ManagedCodeVariant.Checked : ManagedCodeVariant.Release;
#endif

        [MenuItem("MxFramework/Build/Windows64/Apply IL2CPP Release Defaults")]
        internal static void ApplyReleaseDefaults()
        {
            Apply(CharacterTestWindowsBuildProfile.Release);
            // PlayerSettings is a global manager; SaveAssetIfDirty does not persist it.
            // This saves project settings/assets, never open scenes.
            AssetDatabase.SaveAssets();
        }

        private static void Apply(CharacterTestWindowsBuildProfile profile)
        {
            var target = NamedBuildTarget.Standalone;
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
#if UNITY_6000_6_OR_NEWER
            PlayerSettings.SetManagedCodeVariant(target, ManagedVariant(profile));
#endif
            PlayerSettings.SetIl2CppCompilerConfiguration(target, Compiler(profile));
            RequireExperimentPolicy(profile); PlayerSettings.SetIl2CppCodeGeneration(target, Generation);
            PlayerSettings.SetManagedStrippingLevel(target, Stripping);
            PlayerSettings.SetIl2CppStacktraceInformation(target,
                profile == CharacterTestWindowsBuildProfile.Development
                    ? Il2CppStacktraceInformation.MethodFileLineNumber : Il2CppStacktraceInformation.MethodOnly);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.gcIncremental = true;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            EditorUserBuildSettings.SetPlatformSettings("Standalone", "Architecture", "x64");
            EditorUserBuildSettings.development = profile == CharacterTestWindowsBuildProfile.Development;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
            EditorUserBuildSettings.waitForManagedDebugger = false;
        }

        internal sealed class Scope : IDisposable
        {
#if UNITY_6000_6_OR_NEWER
            private readonly ManagedCodeVariant _managedVariant = PlayerSettings.GetManagedCodeVariant(NamedBuildTarget.Standalone);
#endif
            private readonly Il2CppCompilerConfiguration _compiler = PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.Standalone);
            private readonly Il2CppCodeGeneration _generation = PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.Standalone);
            private readonly ManagedStrippingLevel _stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone);
            private readonly Il2CppStacktraceInformation _stack = PlayerSettings.GetIl2CppStacktraceInformation(NamedBuildTarget.Standalone);
            private readonly bool _development = EditorUserBuildSettings.development;
            private readonly bool _debugging = EditorUserBuildSettings.allowDebugging;
            private readonly bool _profiler = EditorUserBuildSettings.connectProfiler;
            private readonly bool _deep = EditorUserBuildSettings.buildWithDeepProfilingSupport;
            private readonly bool _wait = EditorUserBuildSettings.waitForManagedDebugger;

            internal Scope(CharacterTestWindowsBuildProfile profile) { Apply(profile); }
            public void Dispose()
            {
                // IL2CPP, x64, D3D11 and engine stripping are project policy, never restore Mono.
#if UNITY_6000_6_OR_NEWER
                PlayerSettings.SetManagedCodeVariant(NamedBuildTarget.Standalone, _managedVariant);
#endif
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Standalone, _compiler);
                PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Standalone, _generation);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, _stripping);
                PlayerSettings.SetIl2CppStacktraceInformation(NamedBuildTarget.Standalone, _stack);
                EditorUserBuildSettings.development = _development;
                EditorUserBuildSettings.allowDebugging = _debugging;
                EditorUserBuildSettings.connectProfiler = _profiler;
                EditorUserBuildSettings.buildWithDeepProfilingSupport = _deep;
                EditorUserBuildSettings.waitForManagedDebugger = _wait;
            }
        }

        internal static void RequireToolchain(CharacterTestWindowsBuildProfile profile)
        {
            string variation = profile == CharacterTestWindowsBuildProfile.Development
                ? "win64_player_development_il2cpp" : "win64_player_nondevelopment_il2cpp";
            string support = Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines/windowsstandalonesupport/Variations", variation);
            if (!Directory.Exists(support))
                throw new BuildFailedException("Install Windows Build Support (IL2CPP) for this Unity Editor: " + support);
            string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft Visual Studio/Installer/vswhere.exe");
            if (!File.Exists(vswhere)) throw new BuildFailedException("Visual Studio C++ Build Tools detection is unavailable (vswhere.exe).");
            using var process = Process.Start(new ProcessStartInfo(vswhere,
                "-products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
            string installations = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            bool compilerReady = installations.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => Path.Combine(path.Trim(), "VC/Tools/MSVC"))
                .Where(Directory.Exists).SelectMany(Directory.GetDirectories)
                .Any(path => File.Exists(Path.Combine(path, "bin/Hostx64/x64/cl.exe"))
                    && File.Exists(Path.Combine(path, "bin/Hostx64/x64/link.exe")));
            if (process.ExitCode != 0 || !compilerReady)
                throw new BuildFailedException("Windows IL2CPP requires Visual Studio C++ x64 tools.");
            string sdk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Kits/10/Lib");
            bool sdkReady = Directory.Exists(sdk) && Directory.GetDirectories(sdk).Any(path =>
                Version.TryParse(Path.GetFileName(path), out Version version) && version >= new Version(10, 0, 19041, 0)
                && File.Exists(Path.Combine(path, "um/x64/kernel32.lib")));
            if (!sdkReady) throw new BuildFailedException("Windows IL2CPP requires Windows SDK 10.0.19041.0 or newer with x64 libraries.");
        }

        internal static string ReceiptPath(string editorRunReportPath) => Path.ChangeExtension(editorRunReportPath, ".configuration.json");

        internal static void RequirePlayerPaths(string playerDirectory, IEnumerable<string> streamingFiles)
        {
            foreach (string relative in streamingFiles)
            {
                string output = Path.GetFullPath(Path.Combine(playerDirectory,
                    "CharacterTestPerformance_Data/StreamingAssets", relative));
                if (output.Length >= 260)
                    throw new BuildFailedException("Windows IL2CPP runtime file path is too long (" + output.Length
                        + "). Choose a shorter output root, for example E:/WGFBuilds: " + output);
            }
        }

        internal static void FinishBuild(BuildReport report, CharacterTestWindowsBuildProfile profile,
            string playerDirectory, string editorRunReportPath)
        {
            if (!File.Exists(Path.Combine(playerDirectory, "GameAssembly.dll"))
                || !File.Exists(Path.Combine(playerDirectory, "CharacterTestPerformance_Data/il2cpp_data/Metadata/global-metadata.dat"))
                || Directory.Exists(Path.Combine(playerDirectory, "MonoBleedingEdge")))
                throw new BuildFailedException("Player output does not match the required IL2CPP backend.");
            if (Directory.GetFiles(playerDirectory, "*.dll", SearchOption.AllDirectories)
                .Any(path => path.Replace('\\', '/').IndexOf("/ARM64/", StringComparison.OrdinalIgnoreCase) >= 0))
                throw new BuildFailedException("Windows x64 Player contains an ARM64 native plugin.");
            string reportRoot = Path.GetDirectoryName(Path.GetFullPath(editorRunReportPath));
            string symbols = Path.Combine(reportRoot, "Symbols", profile + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            JArray archived = ArchiveSymbols(playerDirectory, symbols);
            var receipt = new JObject
            {
                ["schemaVersion"] = 1, ["configuration"] = profile.ToString(),
                ["scriptingBackend"] = "IL2CPP", ["target"] = "StandaloneWindows64", ["architecture"] = "x86_64",
                ["developmentBuild"] = profile == CharacterTestWindowsBuildProfile.Development,
#if UNITY_6000_6_OR_NEWER
                ["managedCodeVariant"] = ManagedVariant(profile).ToString(),
#endif
                ["scriptDebugging"] = false, ["autoconnectProfiler"] = false, ["deepProfiling"] = false,
                ["cppCompilerConfiguration"] = Compiler(profile).ToString(), ["il2cppCodeGeneration"] = Generation.ToString(),
                ["managedStrippingLevel"] = Stripping.ToString(), ["stripEngineCode"] = true, ["experiment"] = Experiment,
                ["apiCompatibility"] = PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone).ToString(),
                ["graphicsApi"] = "Direct3D11", ["incrementalGc"] = true,
                ["compression"] = profile == CharacterTestWindowsBuildProfile.Release ? "LZ4HC" : "LZ4",
                ["unityVersion"] = Application.unityVersion, ["buildGuid"] = report.summary.guid.ToString(),
                ["buildSeconds"] = report.summary.totalTime.TotalSeconds,
                ["rawUnityBuildSizeBytes"] = report.summary.totalSize,
                ["playerFileBytes"] = Directory.GetFiles(playerDirectory, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length),
                ["symbolsDirectory"] = Path.GetRelativePath(reportRoot, symbols).Replace('\\', '/'),
                ["symbolFileCount"] = archived.Count,
                ["symbolBytes"] = archived.Sum(entry => (long)entry["bytes"]), ["symbols"] = archived
            };
            File.WriteAllText(ReceiptPath(editorRunReportPath), receipt.ToString());
            CharacterTestPackageDetailEvidence.CaptureForBuild(report, playerDirectory, editorRunReportPath);
        }

        internal static JArray ArchiveSymbols(string playerDirectory, string symbolsDirectory)
        {
            string root = Path.GetFullPath(playerDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string archiveRoot = Path.GetFullPath(symbolsDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (archiveRoot.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Symbols must be outside the distributable Player directory.");
            var result = new JArray();
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
            {
                string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (!IsSymbol(relative)) continue;
                string archiveRelative = relative;
                int segmentEnd = relative.IndexOf('/');
                if (segmentEnd >= 0 && relative.Substring(0, segmentEnd).Contains("_BackUpThisFolder_ButDontShipItWithYourGame"))
                    archiveRelative = "Il2CppSources/" + relative.Substring(segmentEnd + 1);
                else if (segmentEnd >= 0 && relative.Substring(0, segmentEnd).Contains("_BurstDebugInformation_DoNotShip"))
                    archiveRelative = "BurstDebug/" + relative.Substring(segmentEnd + 1);
                string destination = Path.GetFullPath(Path.Combine(archiveRoot, archiveRelative));
                if (!destination.StartsWith(archiveRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Symbol archive path escaped its output directory.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                long bytes = new FileInfo(file).Length;
                string hash;
                using (var stream = File.OpenRead(file))
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                File.Move(file, destination);
                result.Add(new JObject { ["path"] = relative, ["archivePath"] = archiveRelative, ["bytes"] = bytes, ["sha256"] = hash });
            }
            return result;
        }

        private static bool IsSymbol(string relative) => relative.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
            || relative.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase)
            || relative.IndexOf("_BackUpThisFolder_ButDontShipItWithYourGame/", StringComparison.OrdinalIgnoreCase) >= 0
            || relative.IndexOf("_BurstDebugInformation_DoNotShip/", StringComparison.OrdinalIgnoreCase) >= 0;

        internal static void AppendReceiptFields(StringBuilder builder, string editorRunReportPath)
        {
            string path = ReceiptPath(editorRunReportPath);
            if (!File.Exists(path)) return;
            var receipt = JObject.Parse(File.ReadAllText(path));
            builder.Append("buildConfiguration: ").AppendLine((string)receipt["configuration"]);
            builder.Append("scriptingBackend: ").AppendLine((string)receipt["scriptingBackend"]);
            builder.Append("buildConfigurationReport: ").AppendLine(path);
            builder.Append("symbolBytes: ").AppendLine(receipt["symbolBytes"].ToString());
        }
    }
}
