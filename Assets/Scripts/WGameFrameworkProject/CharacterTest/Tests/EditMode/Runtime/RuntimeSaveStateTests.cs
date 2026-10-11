using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeSaveStateTests
    {
        [Test]
        public void RuntimeSaveState_CopiesInputListsAndRoundtripsJson()
        {
            var attributes = new List<RuntimeAttributeSaveState>
            {
                new RuntimeAttributeSaveState(1, 100d, "Recalculate")
            };
            var entity = new RuntimeEntitySaveState(
                entityId: 10,
                definitionId: 20,
                teamId: 1,
                isAlive: true,
                attributes: attributes,
                modifiers: new[]
                {
                    new RuntimeModifierSaveState(40, 400L, 10, 3, new[] { new RuntimeCounterSaveState(7, 8) }, new RuntimeCustomState(TestCustomStateTypeIds.Modifier, "modifier.runtime", 0, "{\"ticks\":2}"))
                },
                abilities: new[]
                {
                    new RuntimeAbilitySaveState(50, 0.75d, 2, 99L, 500, new RuntimeCustomState(TestCustomStateTypeIds.Ability, "ability.runtime", 1, "{\"chain\":1}"))
                },
                counters: new[] { new RuntimeCounterSaveState(9, 12) },
                customState: new RuntimeCustomState(TestCustomStateTypeIds.Entity, "entity.runtime", 0, "{\"flag\":true}"));
            var state = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                new DateTime(2026, 5, 10, 1, 2, 3, DateTimeKind.Utc),
                "0.1",
                "config-a",
                "resources-a",
                123L,
                new[] { entity },
                new[] { new RuntimeCounterSaveState(100, 2) },
                new[] { new RuntimeModuleSaveState("runtime", 0, new RuntimeCustomState(TestCustomStateTypeIds.Module, "module.runtime", 0, "{}")) },
                new Dictionary<string, string> { { "slot", "auto" } });

            attributes.Add(new RuntimeAttributeSaveState(2, 200d, "Mutated"));

            Assert.AreEqual(1, entity.Attributes.Count);
            Assert.AreEqual(1, state.Entities.Count);

            string json = RuntimeSaveStateJson.SaveToJson(state);
            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);

            Assert.IsTrue(loaded.Success, loaded.Error.ToString());
            Assert.AreEqual(123L, loaded.Value.Frame);
            Assert.AreEqual("config-a", loaded.Value.ConfigVersion);
            Assert.AreEqual("auto", loaded.Value.Metadata["slot"]);
            Assert.AreEqual(10, loaded.Value.Entities[0].EntityId);
            Assert.AreEqual(100d, loaded.Value.Entities[0].Attributes[0].BaseValue);
            Assert.AreEqual("modifier.runtime", loaded.Value.Entities[0].Modifiers[0].CustomState.TypeId);
            Assert.AreEqual("{\"ticks\":2}", loaded.Value.Entities[0].Modifiers[0].CustomState.PayloadJson);
            Assert.AreEqual(8, loaded.Value.Entities[0].Modifiers[0].Counters[0].Value);
            Assert.AreEqual(99L, loaded.Value.Entities[0].Abilities[0].LastCastFrame);
        }

        [Test]
        public void RuntimeSaveStateMigrationPipeline_RunsSuccessfulSingleStepChain()
        {
            RuntimeSaveState state = CreateState(0);
            var pipeline = new RuntimeSaveStateMigrationPipeline(new IRuntimeSaveStateMigration[]
            {
                new SchemaBumpMigration(0, 1),
                new SchemaBumpMigration(1, 2)
            });

            RuntimeSaveStateResult<RuntimeSaveState> result = pipeline.Migrate(state, 2);

            Assert.IsTrue(result.Success, result.Error.ToString());
            Assert.AreEqual(2, result.Value.SchemaVersion);
            Assert.AreEqual("migrated-2", result.Value.FrameworkVersion);
        }

        [Test]
        public void RuntimeSaveStateMigrationPipeline_ReturnsMissingMigrationError()
        {
            RuntimeSaveState state = CreateState(0);
            var pipeline = new RuntimeSaveStateMigrationPipeline(new IRuntimeSaveStateMigration[]
            {
                new SchemaBumpMigration(1, 2)
            });

            RuntimeSaveStateResult<RuntimeSaveState> result = pipeline.Migrate(state, 2);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.MissingMigration, result.Error.Code);
            Assert.AreEqual(0, result.Error.SourceSchemaVersion);
            Assert.AreEqual(2, result.Error.TargetSchemaVersion);
        }

        [Test]
        public void RuntimeSaveStateMigrationPipeline_PreservesMigrationFailureError()
        {
            RuntimeSaveState state = CreateState(0);
            var expected = new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.CustomStateMigrationFailed,
                "$.entities[0].customState",
                "custom state failed",
                0,
                1);
            var pipeline = new RuntimeSaveStateMigrationPipeline(new IRuntimeSaveStateMigration[]
            {
                new FailingMigration(0, 1, expected)
            });

            RuntimeSaveStateResult<RuntimeSaveState> result = pipeline.Migrate(state, 1);

            Assert.IsFalse(result.Success);
            Assert.AreSame(expected, result.Error);
            Assert.AreEqual(RuntimeSaveStateErrorCode.CustomStateMigrationFailed, result.Error.Code);
        }

        [Test]
        public void RuntimeSaveStateJson_ReturnsInvalidAndUnsupportedDocumentErrors()
        {
            RuntimeSaveStateResult<RuntimeSaveState> invalid = RuntimeSaveStateJson.LoadFromJson("{\"frame\":0}");
            RuntimeSaveStateResult<RuntimeSaveState> unsupported = RuntimeSaveStateJson.LoadFromJson("{\"schemaVersion\":99,\"frame\":0}");

            Assert.IsFalse(invalid.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, invalid.Error.Code);
            Assert.AreEqual("$.schemaVersion", invalid.Error.Path);

            Assert.IsFalse(unsupported.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.UnsupportedVersion, unsupported.Error.Code);
            Assert.AreEqual(99, unsupported.Error.SourceSchemaVersion);
        }

        [Test]
        public void RuntimeCustomState_PreservesTypeAndVersionFieldsOnModifier()
        {
            var customState = new RuntimeCustomState(TestCustomStateTypeIds.CustomModifier, "custom.modifier.state", 3, "{\"stackSeed\":42}");
            var state = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                "0.1",
                "config",
                "resources",
                0L,
                new[]
                {
                    new RuntimeEntitySaveState(
                        1,
                        2,
                        3,
                        true,
                        null,
                        new[] { new RuntimeModifierSaveState(4, 5L, 1, 2, null, customState) },
                        null,
                        null,
                        null)
                },
                null,
                null,
                null);

            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(RuntimeSaveStateJson.SaveToJson(state));

            Assert.IsTrue(loaded.Success, loaded.Error.ToString());
            RuntimeCustomState loadedCustomState = loaded.Value.Entities[0].Modifiers[0].CustomState;
            Assert.AreEqual("custom.modifier.state", loadedCustomState.TypeId);
            Assert.AreEqual(3, loadedCustomState.SchemaVersion);
            Assert.AreEqual("{\"stackSeed\":42}", loadedCustomState.PayloadJson);
        }

        [Test]
        public void RuntimeSaveStateJson_BoundedUtf8RoundtripPreservesOpaqueCustomPayload()
        {
            string opaquePayload = "{\"nested\":" + new string('[', 30) + "0" + new string(']', 30) + "}";
            var state = new RuntimeSaveState(
                0,
                new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc),
                "framework",
                "config",
                "resources",
                12L,
                null,
                null,
                new[] { new RuntimeModuleSaveState("module", 0, new RuntimeCustomState(TestCustomStateTypeIds.Opaque, "opaque", 0, opaquePayload)) },
                null);

            RuntimeSaveStateResult<byte[]> encoded = RuntimeSaveStateJson.SaveToUtf8(state, 65536, 16);
            Assert.IsTrue(encoded.Success, encoded.Error.ToString());
            RuntimeSaveStateResult<RuntimeSaveState> decoded = RuntimeSaveStateJson.LoadFromUtf8(
                encoded.Value,
                encoded.Value.Length,
                16);

            Assert.IsTrue(decoded.Success, decoded.Error.ToString());
            Assert.AreEqual(opaquePayload, decoded.Value.ModuleStates[0].CustomState.PayloadJson);
        }

        [Test]
        public void RuntimeSaveStateJson_BoundedUtf8HonorsExactByteBoundary()
        {
            RuntimeSaveState state = CreateState(0);
            RuntimeSaveStateResult<byte[]> initial = RuntimeSaveStateJson.SaveToUtf8(state, 65536, 16);
            int exactLength = initial.Value.Length;

            RuntimeSaveStateResult<byte[]> exactSave = RuntimeSaveStateJson.SaveToUtf8(state, exactLength, 16);
            RuntimeSaveStateResult<byte[]> shortSave = RuntimeSaveStateJson.SaveToUtf8(state, exactLength - 1, 16);
            RuntimeSaveStateResult<RuntimeSaveState> exactLoad = RuntimeSaveStateJson.LoadFromUtf8(
                initial.Value,
                exactLength,
                16);
            byte[] oneByteLarger = new byte[exactLength + 1];
            Array.Copy(initial.Value, oneByteLarger, exactLength);
            oneByteLarger[exactLength] = (byte)' ';
            RuntimeSaveStateResult<RuntimeSaveState> largerLoad = RuntimeSaveStateJson.LoadFromUtf8(
                oneByteLarger,
                exactLength,
                16);

            Assert.IsTrue(exactSave.Success, exactSave.Error.ToString());
            Assert.IsFalse(shortSave.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.DocumentLimitExceeded, shortSave.Error.Code);
            Assert.IsTrue(exactLoad.Success, exactLoad.Error.ToString());
            Assert.IsFalse(largerLoad.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.DocumentLimitExceeded, largerLoad.Error.Code);
        }

        [Test]
        public void RuntimeSaveStateJson_BoundedUtf8RejectsNonStrictSyntaxAndOversizedSchema()
        {
            byte[] invalidUtf8 = { (byte)'{', 0xff, (byte)'}' };
            RuntimeSaveStateResult<RuntimeSaveState> invalid = RuntimeSaveStateJson.LoadFromUtf8(invalidUtf8, 1024, 16);
            Assert.IsFalse(invalid.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, invalid.Error.Code);

            string[] invalidDocuments =
            {
                "{\"schemaVersion\":0,\"schemaVersion\":0,\"frame\":0}",
                "{\"schemaVersion\":0,/*not strict JSON*/\"frame\":0}",
                "{\"schemaVersion\":0,\"frame\":0,}",
                "{schemaVersion:0,\"frame\":0}",
                "{'schemaVersion':0,\"frame\":0}",
                "{\"schemaVersion\":0,\"frame\":0,\"extra\":undefined}",
                "{\"schemaVersion\":0,\"frame\":0} {}",
                "{\"schemaVersion\":999999999999999999999999999999999,\"frame\":0}"
            };
            for (int i = 0; i < invalidDocuments.Length; i++)
            {
                byte[] document = Encoding.UTF8.GetBytes(invalidDocuments[i]);
                RuntimeSaveStateResult<RuntimeSaveState> result =
                    RuntimeSaveStateJson.LoadFromUtf8(document, 4096, 16);
                Assert.IsFalse(result.Success, "Document index " + i + " unexpectedly succeeded.");
                Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code, "Document index " + i);
            }
        }

        [Test]
        public void RuntimeSaveStateJson_BoundedUtf8AcceptsDepth16AndRejectsDepth17()
        {
            byte[] depth16 = Encoding.UTF8.GetBytes(CreateNestedDocument(15));
            byte[] depth17 = Encoding.UTF8.GetBytes(CreateNestedDocument(16));

            RuntimeSaveStateResult<RuntimeSaveState> accepted = RuntimeSaveStateJson.LoadFromUtf8(depth16, 4096, 16);
            RuntimeSaveStateResult<RuntimeSaveState> rejected = RuntimeSaveStateJson.LoadFromUtf8(depth17, 4096, 16);

            Assert.IsTrue(accepted.Success, accepted.Error.ToString());
            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.DocumentLimitExceeded, rejected.Error.Code);
            Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeSaveStateJson.LoadFromUtf8(depth16, 4096, 65));

            RuntimeSaveStateResult<byte[]> shallowSave = RuntimeSaveStateJson.SaveToUtf8(CreateState(0), 4096, 1);
            Assert.IsFalse(shallowSave.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.DocumentLimitExceeded, shallowSave.Error.Code);
        }

        private static string CreateNestedDocument(int nestedArrays)
        {
            var builder = new StringBuilder();
            builder.Append("{\"schemaVersion\":0,\"frame\":0,\"extra\":");
            for (int i = 0; i < nestedArrays; i++)
            {
                builder.Append('[');
            }

            builder.Append('0');
            for (int i = 0; i < nestedArrays; i++)
            {
                builder.Append(']');
            }

            builder.Append('}');
            return builder.ToString();
        }

        private static RuntimeSaveState CreateState(int schemaVersion)
        {
            return new RuntimeSaveState(
                schemaVersion,
                new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                "0.1",
                "config",
                "resources",
                0L,
                null,
                null,
                null,
                null);
        }

        private sealed class SchemaBumpMigration : IRuntimeSaveStateMigration
        {
            public SchemaBumpMigration(int fromSchemaVersion, int toSchemaVersion)
            {
                FromSchemaVersion = fromSchemaVersion;
                ToSchemaVersion = toSchemaVersion;
            }

            public int FromSchemaVersion { get; }
            public int ToSchemaVersion { get; }

            public RuntimeSaveStateResult<RuntimeSaveState> Migrate(RuntimeSaveState saveState)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(new RuntimeSaveState(
                    ToSchemaVersion,
                    saveState.CreatedAtUtc,
                    "migrated-" + ToSchemaVersion,
                    saveState.ConfigVersion,
                    saveState.ResourceCatalogVersion,
                    saveState.Frame,
                    saveState.Entities,
                    saveState.GlobalCounters,
                    saveState.ModuleStates,
                    saveState.Metadata));
            }
        }

        private sealed class FailingMigration : IRuntimeSaveStateMigration
        {
            private readonly RuntimeSaveStateError _error;

            public FailingMigration(int fromSchemaVersion, int toSchemaVersion, RuntimeSaveStateError error)
            {
                FromSchemaVersion = fromSchemaVersion;
                ToSchemaVersion = toSchemaVersion;
                _error = error;
            }

            public int FromSchemaVersion { get; }
            public int ToSchemaVersion { get; }

            public RuntimeSaveStateResult<RuntimeSaveState> Migrate(RuntimeSaveState saveState)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(_error);
            }
        }
    }
}
