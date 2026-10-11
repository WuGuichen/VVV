using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests
{
    /// <summary>
    /// 规范 v1.4（角色源数据包组织、Schema 契约与无引擎解耦流转）的源契约回归测试。
    /// 覆盖门禁字典、几何校验与规范化、规范化摘要协议、安全沙箱与递归程序集闭包、
    /// WritePlan 幂等执行器五块。
    /// </summary>
    public static class CharacterSourceContractTests
    {
        public static void RunAll()
        {
            RunGateDictionary();
            RunGeometryValidation();
            RunCanonicalDigest();
            RunPathSandbox();
            RunAssemblyClosure();
            RunWritePlan();
        }

        // ------------------------------------------------------------------
        // §7.1 门禁字典
        // ------------------------------------------------------------------

        private static void RunGateDictionary()
        {
            CharacterGateDefinition fatalGate = CharacterGateCodes.Resolve(CharacterGateCodes.SkeletonHierarchyMissing);
            Require(fatalGate.Severity == CharacterGateSeverity.Fatal, "E-SRC-109 must be Fatal.");
            Require(fatalGate.BlocksAll, "E-SRC-109 must block all subsequent phases.");
            Require(fatalGate.Phase == CharacterGatePhase.Preflight, "E-SRC-109 must be a Preflight gate.");

            CharacterGateDefinition sizeGate = CharacterGateCodes.Resolve(CharacterGateCodes.LegacySizeDeviationExceeded);
            Require(sizeGate.TargetAction == CharacterGateTargetAction.BlockExport, "E-GEO-303 must block export only.");

            Require(CharacterGateCodes.Resolve(CharacterGateCodes.PathTraversalOrSymbolicLink).IsFatal,
                "E-SEC-701 must be Fatal.");
            Require(CharacterGateCodes.Resolve(CharacterGateCodes.WritePlanCircularDependency).IsFatal,
                "E-PLAN-501 must be Fatal.");

            // §1.1 引用但 §7.1 遗漏的代码必须已补齐，否则引用点无法登记门禁。
            string[] referencedBySection11 =
            {
                CharacterGateCodes.ResourceCatalogMissing,
                CharacterGateCodes.BodyCollidersMissing,
                CharacterGateCodes.BodyPartsMissing,
                CharacterGateCodes.ApplicationConfigMissing,
                CharacterGateCodes.AttributesConfigMissing,
                CharacterGateCodes.ActionsConfigMissing,
                CharacterGateCodes.BaseMeshMissing
            };

            for (int i = 0; i < referencedBySection11.Length; i++)
            {
                Require(CharacterGateCodes.TryResolve(referencedBySection11[i], out _),
                    "Gate referenced by section 1.1 is not registered: " + referencedBySection11[i]);
            }

            bool threw = false;
            try
            {
                CharacterGateCodes.Resolve("E-NOT-A-GATE");
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            Require(threw, "Unregistered gate code must throw instead of silently passing.");

            // 字典必须按代码字典序，保证诊断输出稳定。
            for (int i = 1; i < CharacterGateCodes.All.Count; i++)
            {
                Require(string.CompareOrdinal(CharacterGateCodes.All[i - 1].Code, CharacterGateCodes.All[i].Code) < 0,
                    "Gate dictionary must be sorted by ordinal code.");
            }
        }

        // ------------------------------------------------------------------
        // §3.1 / §3.2 几何校验与规范化
        // ------------------------------------------------------------------

        private static void RunGeometryValidation()
        {
            // P0：Scale NaN 穿透门禁。NaN 与 1 的比较恒为 false，必须先做 IsFinite。
            AssertRejected(
                MakeCollider(CharacterColliderShape.Box, size: new CharacterAuthoringVector3(1f, 1f, 1f),
                    scale: new CharacterAuthoringVector3(float.NaN, 1f, 1f)),
                "scale NaN must be rejected");

            AssertRejected(
                MakeCollider(CharacterColliderShape.Box, size: new CharacterAuthoringVector3(1f, 1f, 1f),
                    scale: new CharacterAuthoringVector3(1f, float.PositiveInfinity, 1f)),
                "scale Infinity must be rejected");

            AssertRejected(
                MakeCollider(CharacterColliderShape.Box, size: new CharacterAuthoringVector3(1f, 1f, 1f),
                    scale: new CharacterAuthoringVector3(1.5f, 1f, 1f)),
                "non-uniform scale must be rejected");

            AssertRejected(
                MakeCollider(CharacterColliderShape.Box, size: new CharacterAuthoringVector3(1f, 1f, 1f),
                    position: new CharacterAuthoringVector3(float.NaN, 0f, 0f)),
                "position NaN must be rejected");

            // 容差内缩放通过，但规范化必须锁定为绝对精确的单位缩放。
            var nearUnitScale = MakeCollider(
                CharacterColliderShape.Box,
                size: new CharacterAuthoringVector3(0.5f, 0.5f, 0.5f),
                scale: new CharacterAuthoringVector3(1.00005f, 1f, 1f));
            CharacterColliderValidatorAndNormalizer.ValidateRawCollider(nearUnitScale, "collider[0]");
            CharacterAuthoringPose normalizedPose =
                CharacterColliderValidatorAndNormalizer.NormalizeValidPose(nearUnitScale.LocalPose);
            Require(normalizedPose.Scale.X == 1.0f && normalizedPose.Scale.Y == 1.0f && normalizedPose.Scale.Z == 1.0f,
                "NormalizeValidPose must lock scale to exactly (1,1,1).");
            Require(!normalizedPose.HasEulerHint, "NormalizeValidPose must drop EulerHint.");

            // 四元数：非有限分量与未归一化都必须拦截。
            AssertRejected(
                MakeCollider(CharacterColliderShape.Box, size: new CharacterAuthoringVector3(1f, 1f, 1f),
                    rotation: new CharacterAuthoringQuaternion(float.NaN, 0f, 0f, 1f)),
                "rotation NaN must be rejected");
            AssertRejected(
                MakeCollider(CharacterColliderShape.Box, size: new CharacterAuthoringVector3(1f, 1f, 1f),
                    rotation: new CharacterAuthoringQuaternion(0f, 0f, 0f, 2f)),
                "non-normalized rotation must be rejected");

            // Box：必须声明 size，且全维度在 [0.005, 10] 内。
            AssertRejected(MakeCollider(CharacterColliderShape.Box), "box without size must be rejected");
            AssertRejected(
                MakeCollider(CharacterColliderShape.Box, size: new CharacterAuthoringVector3(0.001f, 1f, 1f)),
                "box size below minimum must be rejected");
            AssertRejected(
                MakeCollider(CharacterColliderShape.Box, size: new CharacterAuthoringVector3(1f, 11f, 1f)),
                "box size above maximum must be rejected");

            // Sphere：遗留 size 必须全三维比对（P0 修复：此前只比对 X）。
            AssertRejected(
                MakeCollider(CharacterColliderShape.Sphere, radius: 0.17f,
                    size: new CharacterAuthoringVector3(0.34f, -100f, 900f)),
                "sphere legacy size must be checked on all three axes");

            AssertRejected(
                MakeCollider(CharacterColliderShape.Sphere, radius: 0.0001f),
                "sphere radius below minimum must be rejected");

            // Capsule：H < 2R 严格拒绝（E-GEO-302）。
            AssertRejected(
                MakeCollider(CharacterColliderShape.Capsule, radius: 0.22f, height: 0.43f),
                "capsule height below diameter must be rejected");

            // H == 2R 边界必须放行，且圆柱段长为 0。
            var degenerateCapsule = MakeCollider(CharacterColliderShape.Capsule, radius: 0.22f, height: 0.44f);
            CanonicalColliderGeometry degenerate =
                CharacterColliderValidatorAndNormalizer.ValidateAndNormalize(degenerateCapsule, "collider[0]");
            Require(degenerate.CylinderHalfHeight == 0f, "capsule at H == 2R must have zero cylinder half height.");
            Require(degenerate.HasCapsuleIdentity, "capsule identity must hold at the H == 2R boundary.");

            // Capsule：遗留 size.y 与权威 height 的偏差超过 10mm 必须拒绝。
            // 该用例取自仓库真实数据 character-000001/geometry/body_colliders.json 的 torso_capsule。
            var torsoLike = MakeCollider(
                CharacterColliderShape.Capsule,
                radius: 0.22f,
                height: 0.699276f,
                size: new CharacterAuthoringVector3(0.44f, 0.56f, 0.44f));
            AssertRejected(torsoLike, "capsule legacy size.y deviation must be rejected");

            // 同一真实数据的几何恒等式必须成立。
            var torsoGeometry = CharacterColliderValidatorAndNormalizer.ValidateAndNormalize(
                MakeCollider(CharacterColliderShape.Capsule, radius: 0.22f, height: 0.699276f),
                "collider[0]");
            Require(torsoGeometry.HasCapsuleIdentity,
                "capsule identity must hold for real data (r=0.22, h=0.699276); deviation=" +
                torsoGeometry.CapsuleIdentityDeviation);
            Require(Math.Abs(torsoGeometry.CylinderHalfHeight - (0.699276f - 0.44f) * 0.5f) < 1e-7f,
                "capsule cylinder half height mismatch.");

            // 规范化在未校验数据上必须抛诊断异常，而不是 NullReferenceException。
            bool threwDiagnostic = false;
            try
            {
                CharacterColliderValidatorAndNormalizer.NormalizeValidCollider(
                    MakeCollider(CharacterColliderShape.Box));
            }
            catch (CharacterDiagnosticException)
            {
                threwDiagnostic = true;
            }
            catch (NullReferenceException)
            {
                Require(false, "NormalizeValidCollider must not throw NullReferenceException on unvalidated input.");
            }

            Require(threwDiagnostic, "NormalizeValidCollider must throw a diagnostic exception on unvalidated input.");

            // 既有持久化 DTO 的适配：零向量 size 必须被识别为"未声明"，否则球体/胶囊体会误报 E-GEO-303。
            Require(!CharacterColliderSourceAdapter.InferLegacySizeDeclared(new CharacterAuthoringVector3(0f, 0f, 0f)),
                "zero size must be inferred as undeclared.");
            Require(CharacterColliderSourceAdapter.InferLegacySizeDeclared(new CharacterAuthoringVector3(0.34f, 0.34f, 0.34f)),
                "non-zero size must be inferred as declared.");

            // 未声明 size 的球体不得触发遗留尺寸审查。
            var sphereWithoutLegacySize = CharacterColliderSourceAdapter.ToRawColliderDto(
                new CharacterBodyColliderProfile
                {
                    ColliderId = "head_sphere",
                    Shape = CharacterColliderShape.Sphere,
                    Radius = 0.17f,
                    Size = new CharacterAuthoringVector3(0f, 0f, 0f)
                });
            Require(sphereWithoutLegacySize.Size == null, "sphere without declared size must map to null size.");
            CharacterColliderValidatorAndNormalizer.ValidateRawCollider(sphereWithoutLegacySize, "collider[0]");
        }

        // ------------------------------------------------------------------
        // §1.3 唯一规范化摘要算法
        // ------------------------------------------------------------------

        private static void RunCanonicalDigest()
        {
            // 步骤 2a：sourcePackageHash 必须被"删除键值对"，而不是置 null。
            string withHash =
                "{\"packageId\":\"000001\",\"hashes\":{\"algorithm\":\"sha256\"," +
                "\"hashStatus\":\"Valid\",\"sourcePackageHash\":\"sha256:" + new string('a', 64) + "\"}}";
            string withoutHash =
                "{\"packageId\":\"000001\",\"hashes\":{\"algorithm\":\"sha256\",\"hashStatus\":\"Valid\"}}";

            Require(
                CharacterSourcePackageCanonicalDigest.ComputeCanonicalManifestDigestHex(withHash) ==
                CharacterSourcePackageCanonicalDigest.ComputeCanonicalManifestDigestHex(withoutHash),
                "Removing sourcePackageHash must be byte-identical to never declaring it.");

            // 步骤 2b：hashStatus 必须被规范化为 Valid，否则会产生时序回环。
            string pending =
                "{\"packageId\":\"000001\",\"hashes\":{\"algorithm\":\"sha256\"," +
                "\"hashStatus\":\"PendingCalculation\",\"sourcePackageHash\":\"sha256:" + new string('b', 64) + "\"}}";
            Require(
                CharacterSourcePackageCanonicalDigest.ComputeCanonicalManifestDigestHex(pending) ==
                CharacterSourcePackageCanonicalDigest.ComputeCanonicalManifestDigestHex(withoutHash),
                "hashStatus must be normalized to Valid before hashing.");

            // 步骤 2c：属性字典序升序——键序不同的等价文档必须得到同一摘要。
            string ordered = "{\"a\":1,\"b\":{\"x\":1,\"y\":2},\"c\":[1,2,3],\"hashes\":{\"algorithm\":\"sha256\",\"hashStatus\":\"Valid\"}}";
            string shuffled = "{\"c\":[1,2,3],\"b\":{\"y\":2,\"x\":1},\"hashes\":{\"hashStatus\":\"Valid\",\"algorithm\":\"sha256\"},\"a\":1}";
            Require(
                CharacterSourcePackageCanonicalDigest.ComputeCanonicalManifestDigestHex(ordered) ==
                CharacterSourcePackageCanonicalDigest.ComputeCanonicalManifestDigestHex(shuffled),
                "Canonical digest must be independent of source property order.");

            // 数字字面量必须原样保留，避免 1 与 1.0 漂移。
            string intForm = "{\"hashes\":{\"algorithm\":\"sha256\",\"hashStatus\":\"Valid\"},\"unitScale\":1}";
            string floatForm = "{\"hashes\":{\"algorithm\":\"sha256\",\"hashStatus\":\"Valid\"},\"unitScale\":1.0}";
            Require(
                CharacterSourcePackageCanonicalDigest.ComputeCanonicalManifestDigestHex(intForm) !=
                CharacterSourcePackageCanonicalDigest.ComputeCanonicalManifestDigestHex(floatForm),
                "Canonical digest must preserve source number literals (1 vs 1.0 are distinct).");

            // 缺少 hashes 对象必须报 E-SRC-101。
            bool threw = false;
            try
            {
                CharacterSourcePackageCanonicalDigest.ComputeCanonicalManifestDigestHex("{\"packageId\":\"000001\"}");
            }
            catch (CharacterDiagnosticException exception)
            {
                threw = true;
                Require(exception.GateCode == CharacterGateCodes.ManifestSchemaInvalid,
                    "Missing hashes must report E-SRC-101.");
            }

            Require(threw, "Manifest without hashes must be rejected.");

            string root = CreateTempDirectory("digest");
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "config"));
                File.WriteAllText(
                    Path.Combine(root, "manifest.json"),
                    "{\"packageId\":\"000001\",\"stableId\":\"charpkg.000001\",\"displayName\":\"Digest Probe\"," +
                    "\"hashes\":{\"algorithm\":\"sha256\",\"hashStatus\":\"PendingCalculation\"}," +
                    "\"coordinateConvention\":{\"unitScaleMeters\":1}}",
                    Encoding.UTF8);
                File.WriteAllText(Path.Combine(root, "config", "character_attributes.json"), "{\"hp\":100}", Encoding.UTF8);

                var closure = new[] { "manifest.json", "config/character_attributes.json" };
                string first = CharacterSourcePackageCanonicalDigest.ComputeSourcePackageHash(root, closure);

                // 步骤 4 回环：把结果写回 manifest 并把 hashStatus 标为 Valid 后，重算必须完全一致。
                File.WriteAllText(
                    Path.Combine(root, "manifest.json"),
                    "{\"packageId\":\"000001\",\"stableId\":\"charpkg.000001\",\"displayName\":\"Digest Probe\"," +
                    "\"hashes\":{\"algorithm\":\"sha256\",\"hashStatus\":\"Valid\",\"sourcePackageHash\":\"" + first + "\"}," +
                    "\"coordinateConvention\":{\"unitScaleMeters\":1}}",
                    Encoding.UTF8);

                string second = CharacterSourcePackageCanonicalDigest.ComputeSourcePackageHash(root, closure);
                Require(first == second, $"Digest round-trip drifted: {first} -> {second}");
                Require(CharacterSourcePackageCanonicalDigest.VerifyRoundTrip(root, closure, first),
                    "VerifyRoundTrip must confirm closure.");

                // 步骤 5：未声明文件绝不参与哈希。
                File.WriteAllText(Path.Combine(root, "config", "draft_scratch.json"), "{\"draft\":true}", Encoding.UTF8);
                string third = CharacterSourcePackageCanonicalDigest.ComputeSourcePackageHash(root, closure);
                Require(third == first, "Undeclared draft files must not affect sourcePackageHash.");

                IReadOnlyList<string> undeclared =
                    CharacterSourcePackageClosureResolver.DetectUndeclaredFiles(root, closure);
                Require(undeclared.Count == 1 && undeclared[0] == "config/draft_scratch.json",
                    "Draft isolation audit must report the undeclared file.");

                // 路径大小写与分隔符差异不得改变摘要。
                string[] upperClosure = { "manifest.json", "CONFIG\\Character_Attributes.json" };
                Require(CharacterSourcePackageCanonicalDigest.ComputeSourcePackageHash(root, upperClosure) == first,
                    "Path normalization must make casing/separator variants hash-identical.");

                // 末尾必须带换行符。
                byte[] payload = CharacterSourcePackageCanonicalDigest.SerializeOrderedEntriesPayload(
                    new[] { "b:2", "a:1" });
                Require(payload[payload.Length - 1] == (byte)'\n', "Ordered entries payload must end with a newline.");
                Require(Encoding.UTF8.GetString(payload) == "a:1\nb:2\n", "Ordered entries must be ordinal sorted.");

                // 声明文件物理缺失必须报 E-SRC-201。
                bool missingThrew = false;
                try
                {
                    CharacterSourcePackageCanonicalDigest.ComputeSourcePackageHash(
                        root, new[] { "manifest.json", "config/does_not_exist.json" });
                }
                catch (CharacterDiagnosticException exception)
                {
                    missingThrew = true;
                    Require(exception.GateCode == CharacterGateCodes.RequiredAssetPhysicallyMissing,
                        "Missing declared asset must report E-SRC-201.");
                }

                Require(missingThrew, "Missing declared asset must block digest computation.");
            }
            finally
            {
                SafeDelete(root);
            }
        }

        // ------------------------------------------------------------------
        // §1.1 展开集合 + §6.2 路径沙箱
        // ------------------------------------------------------------------

        private static void RunPathSandbox()
        {
            // packageId 注入必须前置拦截。
            AssertSecurityBlocked(() => SecurePathSandboxEnforcer.AssertValidPackageId("../../outside"), "packageId traversal");
            AssertSecurityBlocked(() => SecurePathSandboxEnforcer.AssertValidPackageId("00001"), "packageId too short");
            AssertSecurityBlocked(() => SecurePathSandboxEnforcer.AssertValidPackageId("0000011"), "packageId too long");
            AssertSecurityBlocked(() => SecurePathSandboxEnforcer.AssertValidPackageId("00000a"), "packageId non-digit");
            SecurePathSandboxEnforcer.AssertValidPackageId("000001");

            string workspace = CreateTempDirectory("sandbox");
            try
            {
                string packageRoot = Path.Combine(workspace, "000001");
                Directory.CreateDirectory(packageRoot);

                string resolved = SecurePathSandboxEnforcer.ResolveAndVerifyPath(workspace, "000001", "config/a.json");
                Require(resolved == Path.GetFullPath(Path.Combine(packageRoot, "config", "a.json")),
                    "Normal relative path must resolve inside the sandbox.");

                AssertSecurityBlocked(
                    () => SecurePathSandboxEnforcer.ResolveAndVerifyPath(workspace, "000001", "../../outside.txt"),
                    "relative traversal escape");
                AssertSecurityBlocked(
                    () => SecurePathSandboxEnforcer.ResolveAndVerifyPath(workspace, "000001", "config/../../outside.txt"),
                    "nested relative traversal escape");
                AssertSecurityBlocked(
                    () => SecurePathSandboxEnforcer.ResolveAndVerifyPath(
                        workspace, "000001", Path.Combine(workspace, "000002", "x.txt")),
                    "absolute path escape");

                // 目录边界：兄弟目录 0000011 不得被当作 000001 的后代。
                Directory.CreateDirectory(Path.Combine(workspace, "0000011"));
                bool siblingBlocked = false;
                try
                {
                    SecurePathSandboxEnforcer.ResolveAndVerifyPath(workspace, "000001", "../0000011/x.txt");
                }
                catch (CharacterDiagnosticException)
                {
                    siblingBlocked = true;
                }

                Require(siblingBlocked, "Sibling directory 0000011 must not be reachable from 000001.");

                // 符号链接：末端文件自身与祖先目录都必须被检出。
                string linkTarget = Path.Combine(workspace, "outside-target");
                Directory.CreateDirectory(linkTarget);
                File.WriteAllText(Path.Combine(linkTarget, "secret.txt"), "secret", Encoding.UTF8);

                string directoryLink = Path.Combine(packageRoot, "linked-dir");
                if (TryCreateDirectorySymbolicLink(directoryLink, linkTarget))
                {
                    bool blocked = false;
                    try
                    {
                        SecurePathSandboxEnforcer.ResolveAndVerifyPath(workspace, "000001", "linked-dir/secret.txt");
                    }
                    catch (CharacterDiagnosticException exception)
                    {
                        blocked = true;
                        Require(exception.GateCode == CharacterGateCodes.PathTraversalOrSymbolicLink,
                            "Symbolic link must report E-SEC-701.");
                    }

                    Require(blocked, "Symbolic-linked ancestor directory must be rejected.");
                }

                string fileLink = Path.Combine(packageRoot, "linked-file.json");
                if (TryCreateFileSymbolicLink(fileLink, Path.Combine(linkTarget, "secret.txt")))
                {
                    bool blocked = false;
                    try
                    {
                        SecurePathSandboxEnforcer.ResolveAndVerifyPath(workspace, "000001", "linked-file.json");
                    }
                    catch (CharacterDiagnosticException)
                    {
                        blocked = true;
                    }

                    Require(blocked, "Symbolic-linked target file itself must be rejected.");
                }
            }
            finally
            {
                SafeDelete(workspace);
            }

            // §1.1 展开集合
            var request = new CharacterSourcePackageClosureRequest();
            request.AlwaysIncludedEntries.Add("manifest.json");
            request.AlwaysIncludedEntries.Add("resource_catalog.json");
            request.RequiredEntries["baseMesh"] = "resources/models/000001_lod0.fbx";
            request.RequiredEntries["bodyColliders"] = "geometry/body_colliders.json";
            request.OptionalEntries["clothConfig"] = "config/character_cloth_magica.json";
            request.OptionalEntries["damageZones"] = null;
            request.OptionalEntryFeatureGates["clothConfig"] = "enableClothPhysics";
            request.FeatureFlags["enableClothPhysics"] = false;
            request.AnimationEntries.Add("resources/animations/idle.fbx");
            request.AnimationEntries.Add("resources/animations/run.fbx");

            IReadOnlyList<string> closure = CharacterSourcePackageClosureResolver.Resolve(request, out IReadOnlyList<CharacterClosureExclusion> exclusions);
            Require(closure.Contains("manifest.json"), "manifest.json must be in the closure.");
            Require(closure.Contains("resource_catalog.json"), "resource_catalog.json must be in the closure.");
            Require(closure.Contains("resources/animations/idle.fbx"), "Declared animations must be in the closure.");
            Require(!closure.Contains("config/character_cloth_magica.json"),
                "Optional entry with a disabled feature flag must be excluded.");
            Require(exclusions.Count == 2, "Exclusions must report the null optional entry and the disabled flag.");

            for (int i = 1; i < closure.Count; i++)
            {
                Require(string.CompareOrdinal(closure[i - 1], closure[i]) < 0, "Closure must be ordinal sorted.");
            }

            request.FeatureFlags["enableClothPhysics"] = true;
            IReadOnlyList<string> enabledClosure = CharacterSourcePackageClosureResolver.Resolve(request);
            Require(enabledClosure.Contains("config/character_cloth_magica.json"),
                "Optional entry must be included once its feature flag is enabled.");
        }

        // ------------------------------------------------------------------
        // §6.1 递归程序集依赖闭包
        // ------------------------------------------------------------------

        private static void RunAssemblyClosure()
        {
            string coreAssembly = typeof(CharacterGateCodes).Assembly.Location;
            Require(File.Exists(coreAssembly), "Core assembly must exist for the closure scan.");

            string coreDirectory = Path.GetDirectoryName(coreAssembly);
            AssemblyDependencyScanResult result = RecursiveAssemblySecurityValidator.Scan(
                coreAssembly,
                new[] { coreDirectory, AppContext.BaseDirectory });

            Require(result.IsClean,
                "MxFramework.Authoring.Core must have zero engine dependencies. Violations: " +
                string.Join("; ", result.Violations.Select(item => item.ToString())));

            Require(result.ScannedAssemblies.Count >= 1, "Closure scan must scan at least the root assembly.");

            bool missingThrew = false;
            try
            {
                RecursiveAssemblySecurityValidator.Scan(Path.Combine(coreDirectory, "definitely-not-here.dll"));
            }
            catch (FileNotFoundException)
            {
                missingThrew = true;
            }

            Require(missingThrew, "Scanning a missing root assembly must throw FileNotFoundException.");

            RunAssemblyClosureRecursion();
        }

        /// <summary>
        /// 验证"程序集依赖扫描仅一层"这一 P0 缺陷已修复。
        /// 用运行期生成的合成程序集构造 <c>Consumer -> Helper -> UnityEngine.CoreModule</c>
        /// 三级链：只扫描直接引用的实现会漏掉 Helper 中的引擎引用。
        /// </summary>
        private static void RunAssemblyClosureRecursion()
        {
            string root = CreateTempDirectory("asmchain");
            try
            {
                string enginePath = Path.Combine(root, "UnityEngine.CoreModule.dll");
                string helperPath = Path.Combine(root, "Helper.dll");
                string consumerPath = Path.Combine(root, "Consumer.dll");
                string cleanPath = Path.Combine(root, "Clean.dll");

                SyntheticAssemblyBuilder.Create(enginePath, "UnityEngine.CoreModule", Array.Empty<string>());
                SyntheticAssemblyBuilder.Create(helperPath, "Helper", new[] { "UnityEngine.CoreModule" });
                SyntheticAssemblyBuilder.Create(consumerPath, "Consumer", new[] { "Helper" });
                SyntheticAssemblyBuilder.Create(cleanPath, "Clean", new[] { "System.Runtime" });

                var probes = new[] { root };

                // 二级传递依赖必须被检出，且报告完整依赖链。
                AssemblyDependencyScanResult consumer = RecursiveAssemblySecurityValidator.Scan(consumerPath, probes);
                Require(!consumer.IsClean, "Consumer -> Helper -> UnityEngine.CoreModule must be detected.");
                Require(consumer.ScannedAssemblies.Count == 2,
                    "Recursion must descend into Helper.dll; scanned=" + consumer.ScannedAssemblies.Count);
                Require(consumer.Violations.Count == 1, "Exactly one violation is expected.");
                Require(consumer.Violations[0].ReferencedAssemblyName == "UnityEngine.CoreModule",
                    "Violation must name the engine assembly.");
                Require(consumer.Violations[0].Depth == 1, "Violation must be reported at depth 1.");
                Require(consumer.Violations[0].DependencyChain.Count == 3,
                    "Dependency chain must contain all three hops.");

                // 一级直接依赖同样必须被检出。
                AssemblyDependencyScanResult helper = RecursiveAssemblySecurityValidator.Scan(helperPath, probes);
                Require(!helper.IsClean, "Helper -> UnityEngine.CoreModule must be detected.");
                Require(helper.Violations[0].Depth == 0, "Direct violation must be at depth 0.");

                // 干净程序集不得误报。
                AssemblyDependencyScanResult clean = RecursiveAssemblySecurityValidator.Scan(cleanPath, probes);
                Require(clean.IsClean, "Clean assembly must pass.");

                // 断言入口：违规必须阻断。
                bool blocked = false;
                try
                {
                    RecursiveAssemblySecurityValidator.AssertZeroEngineDependency(consumerPath, probes, true);
                }
                catch (CharacterDiagnosticException)
                {
                    blocked = true;
                }

                Require(blocked, "AssertZeroEngineDependency must block a transitively polluted assembly.");

                // 关键回归：无法解析的传递依赖必须被显式记录并默认阻断，
                // 而不是像规范参考实现那样"找不到就跳过"从而静默放行。
                string orphanPath = Path.Combine(root, "Orphan.dll");
                SyntheticAssemblyBuilder.Create(orphanPath, "Orphan", new[] { "Missing.Dependency" });

                AssemblyDependencyScanResult orphan = RecursiveAssemblySecurityValidator.Scan(orphanPath, probes);
                Require(orphan.HasUnresolvedReferences,
                    "Unresolvable transitive references must be reported, not silently skipped.");
                Require(orphan.UnresolvedReferences.Count == 1,
                    "Exactly one unresolved reference is expected.");

                bool strictBlocked = false;
                try
                {
                    RecursiveAssemblySecurityValidator.AssertZeroEngineDependency(orphanPath, probes, true);
                }
                catch (CharacterDiagnosticException)
                {
                    strictBlocked = true;
                }

                Require(strictBlocked, "Strict mode must refuse to certify an incomplete dependency closure.");

                // 非严格模式允许调用方显式承担该风险。
                RecursiveAssemblySecurityValidator.AssertZeroEngineDependency(orphanPath, probes, false);
            }
            finally
            {
                SafeDelete(root);
            }
        }

        // ------------------------------------------------------------------
        // §6.3 WritePlan 状态机与幂等执行器
        // ------------------------------------------------------------------

        private static void RunWritePlan()
        {
            // 拓扑排序：无依赖节点按 OperationId 字典序稳定执行。
            var plan = new CharacterWritePlan { PackageId = "000001", SourceSnapshotId = "sha256:" + new string('c', 64) };
            plan.Operations.Add(MakeCreateOperation("op-c", "c.txt", "c"));
            plan.Operations.Add(MakeCreateOperation("op-a", "a.txt", "a"));

            CharacterWritePlanOperation dependentOperation = MakeCreateOperation("op-b", "b.txt", "b");
            dependentOperation.DependsOnOperations.Add("op-a");
            plan.Operations.Add(dependentOperation);

            IReadOnlyList<string> order = CharacterWritePlanExecutor.TopologicalSort(plan);
            var orderedIds = new List<string>(order);
            Require(order.Count == 3, "Topological sort must include every operation.");
            Require(orderedIds.IndexOf("op-a") < orderedIds.IndexOf("op-b"), "Dependency must precede its dependent.");
            Require(order[0] == "op-a", "Ready nodes must be ordered by OperationId ordinal.");
            Require(order[1] == "op-b", "op-b becomes ready before op-c by ordinal ordering.");
            Require(order[2] == "op-c", "op-c must execute last.");

            // 环依赖必须抛 E-PLAN-501。
            var cyclic = new CharacterWritePlan { PackageId = "000001" };
            cyclic.Operations.Add(MakeCreateOperation("op-1", "1.txt", "1"));
            cyclic.Operations.Add(MakeCreateOperation("op-2", "2.txt", "2"));
            cyclic.Operations[0].DependsOnOperations.Add("op-2");
            cyclic.Operations[1].DependsOnOperations.Add("op-1");

            bool cycleThrew = false;
            try
            {
                CharacterWritePlanExecutor.TopologicalSort(cyclic);
            }
            catch (CharacterDiagnosticException exception)
            {
                cycleThrew = true;
                Require(exception.GateCode == CharacterGateCodes.WritePlanCircularDependency,
                    "Cycle must report E-PLAN-501.");
            }

            Require(cycleThrew, "Circular dependency must be rejected.");

            // 未知依赖必须被拒绝，不能静默忽略。
            var unknownDependency = new CharacterWritePlan { PackageId = "000001" };
            unknownDependency.Operations.Add(MakeCreateOperation("op-x", "x.txt", "x"));
            unknownDependency.Operations[0].DependsOnOperations.Add("op-missing");
            bool unknownThrew = false;
            try
            {
                CharacterWritePlanExecutor.TopologicalSort(unknownDependency);
            }
            catch (CharacterDiagnosticException)
            {
                unknownThrew = true;
            }

            Require(unknownThrew, "Unknown dependency must be rejected.");

            // 待决项与状态机一致性。
            var inconsistent = new CharacterWritePlan { PackageId = "000001" };
            inconsistent.PendingChecks.Add("skeleton-semantic-rebind");
            IReadOnlyList<CharacterAuthoringValidationIssue> issues = CharacterWritePlanExecutor.Validate(inconsistent);
            Require(issues.Count > 0, "PendingChecks with ReadyToApply status must be reported.");

            var pendingPlan = new CharacterWritePlan
            {
                PackageId = "000001",
                Status = CharacterWritePlanStatus.VerificationPending
            };
            pendingPlan.PendingChecks.Add("skeleton-semantic-rebind");

            bool pendingRefused = false;
            string pendingRoot = CreateTempDirectory("writeplan-pending");
            try
            {
                CharacterWritePlanExecutor.Execute(pendingPlan, pendingRoot);
            }
            catch (CharacterDiagnosticException)
            {
                pendingRefused = true;
            }
            finally
            {
                SafeDelete(pendingRoot);
            }

            Require(pendingRefused, "VerificationPending plan must not be executed.");

            // 幂等执行语义。
            string root = CreateTempDirectory("writeplan");
            try
            {
                var executable = new CharacterWritePlan { PackageId = "000001" };
                executable.Operations.Add(MakeCreateOperation("op-1", "config/new.json", "payload-v1"));

                CharacterWritePlanExecutionResult first = CharacterWritePlanExecutor.Execute(executable, root);
                Require(first.Results[0].Outcome == CharacterWritePlanOperationOutcome.Applied,
                    "First CreateFile must be applied.");

                CharacterWritePlanExecutionResult second = CharacterWritePlanExecutor.Execute(executable, root);
                Require(second.Results[0].Outcome == CharacterWritePlanOperationOutcome.SkippedIdentical,
                    "Repeating an identical CreateFile must be skipped idempotently.");

                // 已存在且内容不同 + FailIfExists -> E-PLAN-401
                var conflicting = new CharacterWritePlan { PackageId = "000001" };
                conflicting.Operations.Add(MakeCreateOperation("op-1", "config/new.json", "payload-v2"));
                bool conflictThrew = false;
                try
                {
                    CharacterWritePlanExecutor.Execute(conflicting, root);
                }
                catch (CharacterDiagnosticException exception)
                {
                    conflictThrew = true;
                    Require(exception.GateCode == CharacterGateCodes.CreateFileExistsWithoutOverwrite,
                        "Existing file with FailIfExists must report E-PLAN-401.");
                }

                Require(conflictThrew, "FailIfExists must block overwriting an existing file.");

                // OverwriteAllowed -> 覆写成功
                conflicting.Operations[0].OverwritePolicy = CharacterWritePlanOverwritePolicy.OverwriteAllowed;
                CharacterWritePlanExecutionResult overwritten = CharacterWritePlanExecutor.Execute(conflicting, root);
                Require(overwritten.Results[0].Outcome == CharacterWritePlanOperationOutcome.Applied,
                    "OverwriteAllowed must overwrite the existing file.");

                // UpdateFile 幂等与前置摘要
                string currentHash = CharacterSourcePackageCanonicalDigest.ComputeFileHash(
                    Path.Combine(root, "000001", "config", "new.json"));

                var update = new CharacterWritePlan { PackageId = "000001" };
                update.Operations.Add(new CharacterWritePlanOperation
                {
                    OperationId = "op-u",
                    Kind = CharacterWritePlanOperationKind.UpdateFile,
                    TargetRelativePath = "config/new.json",
                    PreconditionExpectedSha256 = currentHash,
                    PayloadSha256 = CharacterSourcePackageCanonicalDigest.ComputeFileHash(
                        Path.Combine(root, "000001", "config", "new.json")),
                    PayloadBytes = Encoding.UTF8.GetBytes("payload-v2")
                });

                CharacterWritePlanExecutionResult updateResult = CharacterWritePlanExecutor.Execute(update, root);
                Require(updateResult.Results[0].Outcome == CharacterWritePlanOperationOutcome.SkippedIdentical,
                    "UpdateFile with identical payload must be skipped.");

                update.Operations[0].PreconditionExpectedSha256 = "sha256:" + new string('d', 64);
                update.Operations[0].PayloadBytes = Encoding.UTF8.GetBytes("payload-v3");
                update.Operations[0].PayloadSha256 = string.Empty;
                bool preconditionThrew = false;
                try
                {
                    CharacterWritePlanExecutor.Execute(update, root);
                }
                catch (CharacterDiagnosticException)
                {
                    preconditionThrew = true;
                }

                Require(preconditionThrew, "UpdateFile precondition mismatch must be rejected.");

                // UpdateFile 目标缺失必须被拒绝（规范未定义专用错误码）
                var updateMissing = new CharacterWritePlan { PackageId = "000001" };
                updateMissing.Operations.Add(new CharacterWritePlanOperation
                {
                    OperationId = "op-um",
                    Kind = CharacterWritePlanOperationKind.UpdateFile,
                    TargetRelativePath = "config/never-existed.json",
                    PreconditionExpectedSha256 = "sha256:" + new string('e', 64),
                    PayloadSha256 = string.Empty,
                    PayloadBytes = Encoding.UTF8.GetBytes("x")
                });
                bool updateMissingThrew = false;
                try
                {
                    CharacterWritePlanExecutor.Execute(updateMissing, root);
                }
                catch (CharacterDiagnosticException)
                {
                    updateMissingThrew = true;
                }

                Require(updateMissingThrew, "UpdateFile on a missing target must be rejected.");

                // DeleteFile 幂等
                var delete = new CharacterWritePlan { PackageId = "000001" };
                delete.Operations.Add(new CharacterWritePlanOperation
                {
                    OperationId = "op-d",
                    Kind = CharacterWritePlanOperationKind.DeleteFile,
                    TargetRelativePath = "config/new.json"
                });

                CharacterWritePlanExecutionResult deleteResult = CharacterWritePlanExecutor.Execute(delete, root);
                Require(deleteResult.Results[0].Outcome == CharacterWritePlanOperationOutcome.Deleted,
                    "DeleteFile on an existing target must delete it.");

                CharacterWritePlanExecutionResult deleteAgain = CharacterWritePlanExecutor.Execute(delete, root);
                Require(deleteAgain.Results[0].Outcome == CharacterWritePlanOperationOutcome.SkippedMissing,
                    "DeleteFile on a missing target must be idempotent success.");

                // 沙箱预检必须阻断整批写入，不留半成品。
                var escaping = new CharacterWritePlan { PackageId = "000001" };
                escaping.Operations.Add(MakeCreateOperation("op-ok", "config/ok.json", "ok"));
                escaping.Operations.Add(MakeCreateOperation("op-bad", "../../escaped.json", "bad"));
                escaping.Operations[1].DependsOnOperations.Add("op-ok");

                bool escapeThrew = false;
                try
                {
                    CharacterWritePlanExecutor.Execute(escaping, root);
                }
                catch (CharacterDiagnosticException exception)
                {
                    escapeThrew = true;
                    Require(exception.GateCode == CharacterGateCodes.PathTraversalOrSymbolicLink,
                        "Path escape must report E-SEC-701.");
                }

                Require(escapeThrew, "Path escape must be blocked.");
                Require(!File.Exists(Path.Combine(root, "000001", "config", "ok.json")),
                    "Sandbox preflight must block the whole batch before any write happens.");
            }
            finally
            {
                SafeDelete(root);
            }
        }

        // ------------------------------------------------------------------
        // 辅助
        // ------------------------------------------------------------------

        private static RawColliderDto MakeCollider(
            CharacterColliderShape shape,
            CharacterAuthoringVector3 size = null,
            float? radius = null,
            float? height = null,
            CharacterAuthoringVector3 scale = null,
            CharacterAuthoringVector3 position = null,
            CharacterAuthoringQuaternion rotation = null)
        {
            return new RawColliderDto
            {
                ColliderId = "probe",
                Shape = shape,
                Size = size,
                Radius = radius,
                Height = height,
                LocalPose = new CharacterAuthoringPose
                {
                    Scale = scale ?? new CharacterAuthoringVector3(1f, 1f, 1f),
                    Position = position ?? new CharacterAuthoringVector3(0f, 0f, 0f),
                    Rotation = rotation ?? new CharacterAuthoringQuaternion(0f, 0f, 0f, 1f)
                }
            };
        }

        private static CharacterWritePlanOperation MakeCreateOperation(
            string operationId,
            string targetRelativePath,
            string payload)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            return new CharacterWritePlanOperation
            {
                OperationId = operationId,
                Kind = CharacterWritePlanOperationKind.CreateFile,
                TargetRelativePath = targetRelativePath,
                PayloadBytes = bytes,
                PayloadSha256 = ComputeSha256Hex(bytes)
            };
        }

        private static string ComputeSha256Hex(byte[] bytes)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return CharacterSourcePackageCanonicalDigest.HashPrefix + builder;
        }

        private static void AssertRejected(RawColliderDto collider, string message)
        {
            bool threw = false;
            try
            {
                CharacterColliderValidatorAndNormalizer.ValidateRawCollider(collider, "collider[0]");
            }
            catch (CharacterDiagnosticException)
            {
                threw = true;
            }

            Require(threw, message);
        }

        private static void AssertSecurityBlocked(Action action, string message)
        {
            bool threw = false;
            try
            {
                action();
            }
            catch (CharacterDiagnosticException exception)
            {
                threw = true;
                Require(exception.GateCode == CharacterGateCodes.PathTraversalOrSymbolicLink,
                    message + ": expected E-SEC-701, got '" + exception.GateCode + "'.");
            }

            Require(threw, message + ": expected the operation to be blocked.");
        }

        private static bool TryCreateDirectorySymbolicLink(string linkPath, string targetPath)
        {
            try
            {
                Directory.CreateSymbolicLink(linkPath, targetPath);
                return true;
            }
            catch (Exception)
            {
                // 非管理员或未开启开发者模式时无法创建符号链接，跳过该项检查。
                return false;
            }
        }

        private static bool TryCreateFileSymbolicLink(string linkPath, string targetPath)
        {
            try
            {
                File.CreateSymbolicLink(linkPath, targetPath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string CreateTempDirectory(string label)
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "mx-source-contract-" + label + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void SafeDelete(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (Exception)
            {
                // 测试清理失败不应导致用例失败。
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }
    }

    /// <summary>
    /// 运行期生成最小合成程序集，用于构造程序集依赖链夹具。
    /// 避免在仓库中提交二进制夹具，同时让递归闭包扫描可被真实覆盖。
    /// </summary>
    internal static class SyntheticAssemblyBuilder
    {
        public static void Create(string outputPath, string assemblyName, string[] assemblyReferences)
        {
            var metadata = new System.Reflection.Metadata.Ecma335.MetadataBuilder();

            metadata.AddModule(
                0,
                metadata.GetOrAddString(assemblyName + ".dll"),
                metadata.GetOrAddGuid(Guid.NewGuid()),
                default(System.Reflection.Metadata.GuidHandle),
                default(System.Reflection.Metadata.GuidHandle));

            metadata.AddAssembly(
                metadata.GetOrAddString(assemblyName),
                new Version(1, 0, 0, 0),
                default(System.Reflection.Metadata.StringHandle),
                default(System.Reflection.Metadata.BlobHandle),
                default(System.Reflection.AssemblyFlags),
                System.Reflection.AssemblyHashAlgorithm.Sha1);

            for (int i = 0; i < assemblyReferences.Length; i++)
            {
                metadata.AddAssemblyReference(
                    metadata.GetOrAddString(assemblyReferences[i]),
                    new Version(1, 0, 0, 0),
                    default(System.Reflection.Metadata.StringHandle),
                    default(System.Reflection.Metadata.BlobHandle),
                    default(System.Reflection.AssemblyFlags),
                    default(System.Reflection.Metadata.BlobHandle));
            }

            var rootBuilder = new System.Reflection.Metadata.Ecma335.MetadataRootBuilder(metadata);
            var peBuilder = new System.Reflection.PortableExecutable.ManagedPEBuilder(
                System.Reflection.PortableExecutable.PEHeaderBuilder.CreateLibraryHeader(),
                rootBuilder,
                new System.Reflection.Metadata.BlobBuilder());

            var blob = new System.Reflection.Metadata.BlobBuilder();
            peBuilder.Serialize(blob);

            using FileStream stream = File.Create(outputPath);
            blob.WriteContentTo(stream);
        }
    }
}
