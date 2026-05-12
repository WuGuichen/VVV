using System;
using System.Reflection;
using System.Text;
using MxFramework.Config.Runtime;
using MxFramework.Buffs;
using MxFramework.Preview;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.Preview
{
    public sealed class ScenePreviewWorldDynamicTargetTests
    {
        private const string RuntimeBuffId = "100101";
        private const string RuntimeTargetId = "TestTarget";

        [Test]
        public void ScenePreviewWorld_ConfigOnlyScene_GeneratesRuntimeTarget()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var configObject = new GameObject("PreviewTargetConfig_Test");
            try
            {
                var config = configObject.AddComponent<MxPreviewSceneTargetConfig>();

                var serialized = new UnityEditor.SerializedObject(config);
                serialized.FindProperty("_targetId").stringValue = "TestTarget";
                serialized.FindProperty("_initialHp").intValue = 1200;
                serialized.FindProperty("_initialAttack").intValue = 150;
                serialized.FindProperty("_initialDefense").intValue = 30;
                serialized.FindProperty("_createRuntimeTarget").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var world = new ScenePreviewWorld(new PreviewLogBuffer());

                Assert.IsTrue(world.HasSceneTargets);
                Assert.AreEqual("scene", world.PreviewMode);

                IBuffTarget target = world.GetOrCreateTarget("TestTarget");
                Assert.IsNotNull(target);
                Assert.AreEqual(1200, target.Attributes.GetAttribute(1));

                var runtimeTarget = target as MxPreviewSceneTarget;
                Assert.IsNotNull(runtimeTarget);
                Assert.AreEqual("TestTarget", runtimeTarget.TargetId);
            }
            finally
            {
                foreach (var target in UnityEngine.Object.FindObjectsByType<MxPreviewSceneTarget>(FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(target.gameObject);
                UnityEngine.Object.DestroyImmediate(configObject);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
        }

        [Test]
        public void ScenePreviewWorld_ProfileOnlyScene_GeneratesRuntimeTargetFromDefaultProfile()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            try
            {
                MxPreviewSceneTargetProfile profile = MxPreviewSceneTargetProfile.LoadDefault();
                Assert.IsNotNull(profile);
                Assert.IsTrue(profile.Enabled);

                var world = new ScenePreviewWorld(new PreviewLogBuffer());

                Assert.IsTrue(world.HasSceneTargets);
                Assert.AreEqual("scene", world.PreviewMode);

                IBuffTarget target = world.GetOrCreateTarget("TestTarget");
                Assert.IsNotNull(target);

                var runtimeTarget = target as MxPreviewSceneTarget;
                Assert.IsNotNull(runtimeTarget);
                Assert.AreEqual("TestTarget", runtimeTarget.TargetId);
            }
            finally
            {
                foreach (var target in UnityEngine.Object.FindObjectsByType<MxPreviewSceneTarget>(FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(target.gameObject);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
        }

        [Test]
        public void ScenePreviewWorld_RuntimePatchV1_ValidPatchAffectsLaterConfigBuffAndReportsMetadata()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject configObject = CreateSceneTargetConfig(RuntimeTargetId, hp: 1000, attack: 100, defense: 20);
            try
            {
                var world = new ScenePreviewWorld(new PreviewLogBuffer());
                world.LoadPreviewPatch(CreateLoadPatchParamsJson(CreateRuntimePatchJson("preview-fixture-v1", 100101, 200101, -77)));

                Assert.AreEqual("preview-fixture-v1", world.CurrentConfigMetadata.SourceId);
                CollectionAssert.Contains(world.CurrentConfigMetadata.ChangedConfigIds, "BasicModifierConfig:200101");
                CollectionAssert.Contains(world.CurrentConfigMetadata.ChangedConfigIds, "BasicBuffConfig:100101");

                bool applied = world.ApplyBuff(RuntimeBuffId, "TestCaster", RuntimeTargetId, stack: 1, durationOverrideMs: null);
                var buffs = world.SnapshotBuffs(RuntimeTargetId);
                var changes = world.SnapshotAttributeChanges(RuntimeTargetId);

                Assert.IsTrue(applied);
                Assert.AreEqual(1, buffs.Count);
                Assert.AreEqual(RuntimeBuffId, buffs[0].BuffId);
                Assert.AreEqual(1, changes.Count);
                Assert.AreEqual("Hp", changes[0].Attribute);
                Assert.AreEqual(1000, changes[0].Before);
                Assert.AreEqual(923, changes[0].After);
            }
            finally
            {
                CleanupPreviewScene(configObject);
            }
        }

        [Test]
        public void ScenePreviewWorld_RuntimePatchV1_MalformedPatchReturnsParseFailureAndKeepsPreviousConfig()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject configObject = CreateSceneTargetConfig(RuntimeTargetId, hp: 1000, attack: 100, defense: 20);
            try
            {
                var world = new ScenePreviewWorld(new PreviewLogBuffer());
                world.LoadPreviewPatch(CreateLoadPatchParamsJson(CreateRuntimePatchJson("preview-fixture-v1", 100101, 200101, -40)));

                Assert.Throws<RuntimeConfigPatchParseException>(() => world.LoadPreviewPatch(CreateLoadPatchParamsJson("{not-json")));
                Assert.AreEqual("preview-fixture-v1", world.CurrentConfigMetadata.SourceId);

                Assert.IsTrue(world.ApplyBuff(RuntimeBuffId, "TestCaster", RuntimeTargetId, stack: 1, durationOverrideMs: null));
                var changes = world.SnapshotAttributeChanges(RuntimeTargetId);
                Assert.AreEqual(1, changes.Count);
                Assert.AreEqual(960, changes[0].After);
            }
            finally
            {
                CleanupPreviewScene(configObject);
            }
        }

        [Test]
        public void ScenePreviewWorld_RuntimePatchV1_InvalidMergeReturnsLoadRejectedAndPreservesPreviousConfig()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject configObject = CreateSceneTargetConfig(RuntimeTargetId, hp: 1000, attack: 100, defense: 20);
            try
            {
                var world = new ScenePreviewWorld(new PreviewLogBuffer());
                world.LoadPreviewPatch(CreateLoadPatchParamsJson(CreateRuntimePatchJson("preview-fixture-v1", 100101, 200101, -25)));

                string invalid = CreateRuntimePatchJson("preview-invalid-v1", 100102, 299999, -90, includeModifier: false);
                Assert.Throws<InvalidOperationException>(() => world.LoadPreviewPatch(CreateLoadPatchParamsJson(invalid)));

                RuntimePreviewConfigMetadata metadata = world.CurrentConfigMetadata;
                Assert.AreEqual("preview-fixture-v1", metadata.SourceId);
                CollectionAssert.Contains(metadata.FailedConfigIds, "BasicBuffConfig:100102");
                Assert.IsTrue(ContainsText(metadata.MergeWarnings, "Missing config reference"));

                Assert.IsTrue(world.ApplyBuff(RuntimeBuffId, "TestCaster", RuntimeTargetId, stack: 1, durationOverrideMs: null));
                Assert.IsFalse(world.ApplyBuff("100102", "TestCaster", RuntimeTargetId, stack: 1, durationOverrideMs: null));
            }
            finally
            {
                CleanupPreviewScene(configObject);
            }
        }

        [Test]
        public void ScenePreviewWorld_ResetClearsTargetStateAndCurrentConfigSource()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject configObject = CreateSceneTargetConfig(RuntimeTargetId, hp: 1000, attack: 100, defense: 20);
            try
            {
                var world = new ScenePreviewWorld(new PreviewLogBuffer());
                world.LoadPreviewPatch(CreateLoadPatchParamsJson(CreateRuntimePatchJson("preview-fixture-v1", 100101, 200101, -10)));
                Assert.IsTrue(world.ApplyBuff(RuntimeBuffId, "TestCaster", RuntimeTargetId, stack: 1, durationOverrideMs: null));
                Assert.AreEqual(1, world.SnapshotBuffs(RuntimeTargetId).Count);

                world.Reset(reloadBase: false);

                Assert.IsFalse(world.CurrentConfigMetadata.HasSource);
                Assert.AreEqual(0, world.SnapshotBuffs(RuntimeTargetId).Count);
                Assert.IsFalse(world.ApplyBuff(RuntimeBuffId, "TestCaster", RuntimeTargetId, stack: 1, durationOverrideMs: null));
            }
            finally
            {
                CleanupPreviewScene(configObject);
            }
        }

        [Test]
        public void PreviewRpcServer_LoadPatchMapsParseAndMergeFailuresToStableCodes()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject configObject = CreateSceneTargetConfig(RuntimeTargetId, hp: 1000, attack: 100, defense: 20);
            var dispatcher = new PreviewMainThreadDispatcher { ExecuteInline = true };
            var world = new ScenePreviewWorld(new PreviewLogBuffer());
            using (var server = new PreviewRpcServer(world, new MemoryBuffPatchLoader(), dispatcher))
            {
                try
                {
                    string ok = InvokeProcessRequest(server, CreateLoadPatchRequest("1", CreateRuntimePatchJson("preview-fixture-v1", 100101, 200101, -10)));
                    StringAssert.Contains("\"configMetadata\"", ok);
                    StringAssert.Contains("\"sourceId\":\"preview-fixture-v1\"", ok);
                    StringAssert.Contains("\"changedConfigIds\"", ok);

                    string parseFailed = InvokeProcessRequest(server, CreateLoadPatchRequest("2", "{not-json"));
                    StringAssert.Contains("\"code\":2001", parseFailed);

                    string invalid = CreateRuntimePatchJson("preview-invalid-v1", 100102, 299999, -90, includeModifier: false);
                    string mergeRejected = InvokeProcessRequest(server, CreateLoadPatchRequest("3", invalid));
                    StringAssert.Contains("\"code\":2002", mergeRejected);

                    string snapshot = InvokeProcessRequest(server, CreateSnapshotRequest("4"));
                    StringAssert.Contains("\"sourceId\":\"preview-fixture-v1\"", snapshot);
                    StringAssert.Contains("\"failedConfigIds\":[\"BasicBuffConfig:100102\"]", snapshot);
                }
                finally
                {
                    CleanupPreviewScene(configObject);
                }
            }
        }

        [Test]
        public void PreviewRpcServer_ApplyBuffMapsRuntimeSnapshotMetadataAndPerformance()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject configObject = CreateSceneTargetConfig(RuntimeTargetId, hp: 1000, attack: 100, defense: 20);
            var dispatcher = new PreviewMainThreadDispatcher { ExecuteInline = true };
            var logs = new PreviewLogBuffer();
            var world = new ScenePreviewWorld(logs);
            using (var server = new PreviewRpcServer(world, new MemoryBuffPatchLoader(), dispatcher, logs: logs))
            {
                try
                {
                    InvokeProcessRequest(server, CreateLoadPatchRequest("1", CreateRuntimePatchJson("preview-fixture-v1", 100101, 200101, -30)));

                    string applied = InvokeProcessRequest(server, CreateApplyBuffRequest("2", RuntimeBuffId, RuntimeTargetId, waitTicks: 0));

                    StringAssert.Contains("\"success\":true", applied);
                    StringAssert.Contains("\"previewMode\":\"scene\"", applied);
                    StringAssert.Contains("\"appliedBuffId\":\"100101\"", applied);
                    StringAssert.Contains("\"buffSnapshots\":[{\"buffId\":\"100101\"", applied);
                    StringAssert.Contains("\"attributeChanges\":[{\"ownerId\":\"TestTarget\",\"attribute\":\"Hp\",\"before\":1000,\"after\":970", applied);
                    StringAssert.Contains("\"damageTicks\":[]", applied);
                    StringAssert.Contains("no damageTicks captured", applied);
                    StringAssert.Contains("\"performance\":{\"loadMs\":0,\"applyMs\":", applied);
                    StringAssert.Contains("\"configMetadata\":{\"sourceId\":\"preview-fixture-v1\"", applied);
                    StringAssert.Contains("\"changedConfigIds\":[\"BasicModifierConfig:200101\",\"BasicBuffConfig:100101\"]", applied);
                }
                finally
                {
                    CleanupPreviewScene(configObject);
                }
            }
        }

        [Test]
        public void PreviewRpcServer_ApplyBuffWithoutAttributeDeltaAddsExplanationLog()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject configObject = CreateSceneTargetConfig(RuntimeTargetId, hp: 1000, attack: 100, defense: 20);
            var dispatcher = new PreviewMainThreadDispatcher { ExecuteInline = true };
            var logs = new PreviewLogBuffer();
            var world = new ScenePreviewWorld(logs);
            using (var server = new PreviewRpcServer(world, new MemoryBuffPatchLoader(), dispatcher, logs: logs))
            {
                try
                {
                    InvokeProcessRequest(server, CreateLoadPatchRequest("1", CreateRuntimePatchJson("preview-no-delta-v1", 100101, 200101, 0)));

                    string applied = InvokeProcessRequest(server, CreateApplyBuffRequest("2", RuntimeBuffId, RuntimeTargetId, waitTicks: 0));

                    StringAssert.Contains("\"success\":true", applied);
                    StringAssert.Contains("\"buffSnapshots\":[{\"buffId\":\"100101\"", applied);
                    StringAssert.Contains("\"attributeChanges\":[]", applied);
                    StringAssert.Contains("no attributeChanges captured", applied);
                    StringAssert.Contains("delta=0", applied);
                }
                finally
                {
                    CleanupPreviewScene(configObject);
                }
            }
        }

        [Test]
        public void PreviewRpcServer_ApplyBuffFailuresExposeStableErrorAndResultErrors()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject configObject = CreateSceneTargetConfig(RuntimeTargetId, hp: 1000, attack: 100, defense: 20);
            var dispatcher = new PreviewMainThreadDispatcher { ExecuteInline = true };
            var logs = new PreviewLogBuffer();
            var world = new ScenePreviewWorld(logs);
            using (var server = new PreviewRpcServer(world, new MemoryBuffPatchLoader(), dispatcher, logs: logs))
            {
                try
                {
                    InvokeProcessRequest(server, CreateLoadPatchRequest("1", CreateRuntimePatchJson("preview-fixture-v1", 100101, 200101, -10)));

                    string unknownBuff = InvokeProcessRequest(server, CreateApplyBuffRequest("2", "999999", RuntimeTargetId, waitTicks: 0));
                    StringAssert.Contains("\"error\":{\"code\":2003", unknownBuff);
                    StringAssert.Contains("\"reason\":\"unknown_buff_or_config\"", unknownBuff);
                    StringAssert.Contains("\"result\":{\"requestId\":\"apply-2\",\"success\":false", unknownBuff);
                    StringAssert.Contains("\"errors\":[{\"code\":2003", unknownBuff);
                    StringAssert.Contains("\"buffId\":\"999999\"", unknownBuff);
                    StringAssert.Contains("\"configMetadata\":{\"sourceId\":\"preview-fixture-v1\"", unknownBuff);

                    string missingTarget = InvokeProcessRequest(server, CreateApplyBuffRequest("3", RuntimeBuffId, "MissingTarget", waitTicks: 0));
                    StringAssert.Contains("\"error\":{\"code\":2003", missingTarget);
                    StringAssert.Contains("\"reason\":\"missing_target\"", missingTarget);
                    StringAssert.Contains("Preview target 'MissingTarget' was not found", missingTarget);
                    StringAssert.Contains("\"targetId\":\"MissingTarget\"", missingTarget);
                }
                finally
                {
                    CleanupPreviewScene(configObject);
                }
            }
        }

        [Test]
        public void PreviewRpcServer_MalformedPatchDoesNotPoisonLaterApplyResultMetadata()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject configObject = CreateSceneTargetConfig(RuntimeTargetId, hp: 1000, attack: 100, defense: 20);
            var dispatcher = new PreviewMainThreadDispatcher { ExecuteInline = true };
            var logs = new PreviewLogBuffer();
            var world = new ScenePreviewWorld(logs);
            using (var server = new PreviewRpcServer(world, new MemoryBuffPatchLoader(), dispatcher, logs: logs))
            {
                try
                {
                    InvokeProcessRequest(server, CreateLoadPatchRequest("1", CreateRuntimePatchJson("preview-fixture-v1", 100101, 200101, -15)));
                    string parseFailed = InvokeProcessRequest(server, CreateLoadPatchRequest("2", "{not-json"));
                    StringAssert.Contains("\"code\":2001", parseFailed);

                    string applied = InvokeProcessRequest(server, CreateApplyBuffRequest("3", RuntimeBuffId, RuntimeTargetId, waitTicks: 0));

                    StringAssert.Contains("\"success\":true", applied);
                    StringAssert.Contains("\"configMetadata\":{\"sourceId\":\"preview-fixture-v1\"", applied);
                    StringAssert.Contains("\"after\":985", applied);
                }
                finally
                {
                    CleanupPreviewScene(configObject);
                }
            }
        }

        [Test]
        public void PreviewRpcServer_ResultPayloadTruncatesLogsUnderSoftLimit()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject configObject = CreateSceneTargetConfig(RuntimeTargetId, hp: 1000, attack: 100, defense: 20);
            var dispatcher = new PreviewMainThreadDispatcher { ExecuteInline = true };
            var logs = new PreviewLogBuffer();
            var world = new ScenePreviewWorld(logs);
            using (var server = new PreviewRpcServer(world, new MemoryBuffPatchLoader(), dispatcher, logs: logs))
            {
                try
                {
                    for (int i = 0; i < 40; i++)
                        server.Logs.Append("info", new string('x', 40000));

                    string snapshot = InvokeProcessRequest(server, CreateSnapshotRequest("1"));

                    Assert.LessOrEqual(Encoding.UTF8.GetByteCount(snapshot), 1024 * 1024);
                    StringAssert.Contains("\"truncated\":true", snapshot);
                }
                finally
                {
                    CleanupPreviewScene(configObject);
                }
            }
        }

        private static GameObject CreateSceneTargetConfig(string targetId, int hp, int attack, int defense)
        {
            var configObject = new GameObject("PreviewTargetConfig_Test");
            var config = configObject.AddComponent<MxPreviewSceneTargetConfig>();

            var serialized = new UnityEditor.SerializedObject(config);
            serialized.FindProperty("_targetId").stringValue = targetId;
            serialized.FindProperty("_initialHp").intValue = hp;
            serialized.FindProperty("_initialAttack").intValue = attack;
            serialized.FindProperty("_initialDefense").intValue = defense;
            serialized.FindProperty("_createRuntimeTarget").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return configObject;
        }

        private static void CleanupPreviewScene(GameObject configObject)
        {
            foreach (var target in UnityEngine.Object.FindObjectsByType<MxPreviewSceneTarget>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(target.gameObject);
            if (configObject != null)
                UnityEngine.Object.DestroyImmediate(configObject);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        private static string CreateLoadPatchRequest(string id, string rawSource)
        {
            return new PreviewJson.Writer().Begin()
                .ObjStart()
                    .KeyStr("jsonrpc", "2.0")
                    .KeyStr("id", id)
                    .KeyStr("method", "preview.loadPatch")
                    .Key("params").ObjStart()
                        .KeyStr("packageId", "preview-package")
                        .KeyStr("kind", "runtimeConfigPatch")
                        .KeyStr("schemaVersion", "1.0")
                        .KeyBool("discardPrevious", true)
                        .KeyStr("rawSource", rawSource)
                    .ObjEnd()
                .ObjEnd()
                .ToString();
        }

        private static string CreateSnapshotRequest(string id)
        {
            return new PreviewJson.Writer().Begin()
                .ObjStart()
                    .KeyStr("jsonrpc", "2.0")
                    .KeyStr("id", id)
                    .KeyStr("method", "preview.getSnapshot")
                    .Key("params").ObjStart()
                        .KeyStr("targetId", RuntimeTargetId)
                    .ObjEnd()
                .ObjEnd()
                .ToString();
        }

        private static string CreateApplyBuffRequest(string id, string buffId, string targetId, int waitTicks)
        {
            return new PreviewJson.Writer().Begin()
                .ObjStart()
                    .KeyStr("jsonrpc", "2.0")
                    .KeyStr("id", id)
                    .KeyStr("method", "preview.applyBuff")
                    .Key("params").ObjStart()
                        .KeyStr("buffId", buffId)
                        .KeyStr("casterId", "TestCaster")
                        .KeyStr("targetId", targetId)
                        .KeyNum("stack", 1)
                        .KeyNum("waitTicks", waitTicks)
                        .KeyStr("requestId", "apply-" + id)
                    .ObjEnd()
                .ObjEnd()
                .ToString();
        }

        private static string CreateLoadPatchParamsJson(string rawSource)
        {
            return new PreviewJson.Writer().Begin()
                .ObjStart()
                    .KeyStr("packageId", "preview-package")
                    .KeyStr("kind", "runtimeConfigPatch")
                    .KeyStr("schemaVersion", "1.0")
                    .KeyBool("discardPrevious", true)
                    .KeyStr("rawSource", rawSource)
                .ObjEnd()
                .ToString();
        }

        private static string CreateRuntimePatchJson(
            string sourceId,
            int buffId,
            int modifierId,
            int hpDelta,
            bool includeModifier = true)
        {
            PreviewJson.Writer writer = new PreviewJson.Writer().Begin()
                .ObjStart()
                    .KeyStr("format", "mx.runtimeConfigPatch.v1")
                    .KeyStr("sourceId", sourceId)
                    .KeyStr("layer", "Patch")
                    .Key("modifiers").ArrStart();

            if (includeModifier)
            {
                writer.ObjStart()
                    .KeyStr("operation", "Upsert")
                    .KeyNum("id", modifierId)
                    .KeyStr("nameText", "mod." + modifierId + ".name")
                    .KeyStr("descriptionText", "mod." + modifierId + ".desc")
                    .KeyNum("paramIndex", 1)
                    .Key("parameters").ArrStart().Num(hpDelta).ArrEnd()
                .ObjEnd();
            }

            writer.ArrEnd()
                    .Key("buffs").ArrStart()
                        .ObjStart()
                            .KeyStr("operation", "Upsert")
                            .KeyNum("id", buffId)
                            .KeyStr("nameText", "buff." + buffId + ".name")
                            .KeyStr("descriptionText", "buff." + buffId + ".desc")
                            .KeyNum("duration", 5.0)
                            .KeyNum("maxLayers", 3)
                            .KeyBool("isPermanent", false)
                            .KeyNum("modifierId", modifierId)
                        .ObjEnd()
                    .ArrEnd()
                .ObjEnd();

            return writer.ToString();
        }

        private static string InvokeProcessRequest(PreviewRpcServer server, string request)
        {
            MethodInfo method = typeof(PreviewRpcServer).GetMethod("ProcessRequest", BindingFlags.Instance | BindingFlags.NonPublic);
            object[] args = { request, true };
            return (string)method.Invoke(server, args);
        }

        private static bool ContainsText(System.Collections.Generic.IReadOnlyList<string> values, string expected)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] != null && values[i].Contains(expected))
                    return true;
            }

            return false;
        }
    }
}
