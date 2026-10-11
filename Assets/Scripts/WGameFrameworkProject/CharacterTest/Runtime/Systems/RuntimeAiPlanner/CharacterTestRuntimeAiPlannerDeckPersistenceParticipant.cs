using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerDeckPersistenceParticipant :
        IRuntimeSaveStateProvider,
        IRuntimeSaveStateRestorer
    {
        public const string ModuleId = "character-test.runtime-ai-planner.deck-persistence";

        private readonly CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog _catalog;
        private readonly int _runtimeContractVersion;
        private readonly CharacterTestRuntimeAiPlannerStableCardIdMigrationMap _migrations;
        private readonly Func<DateTime> _utcNow;

        public CharacterTestRuntimeAiPlannerDeckPersistenceParticipant(
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion,
            CharacterTestRuntimeAiPlannerStableCardIdMigrationMap migrations = null,
            Func<DateTime> utcNow = null)
        {
            CurrentSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (runtimeContractVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(runtimeContractVersion));
            _runtimeContractVersion = runtimeContractVersion;
            _migrations = migrations ?? CharacterTestRuntimeAiPlannerStableCardIdMigrationMap.Empty;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            LastRestoreDiagnostics = Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            ResolvedDecks = Array.Empty<CharacterTestRuntimeAiPlannerResolvedPersistedDeck>();
        }

        public CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot CurrentSnapshot { get; private set; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerResolvedPersistedDeck> ResolvedDecks { get; private set; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> LastRestoreDiagnostics { get; private set; }

        public void ReplaceSnapshot(CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot)
        {
            CurrentSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            RuntimeCustomState customState = CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Capture(CurrentSnapshot);
            var moduleState = new RuntimeModuleSaveState(
                ModuleId,
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.SchemaVersion,
                customState);
            var state = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                _utcNow(),
                "character-test-runtime-ai-planner-deck-persistence-v1",
                string.Empty,
                _catalog.CatalogId + "@" + _catalog.Revision,
                0,
                null,
                null,
                new[] { moduleState });
            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(state);
        }

        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            if (saveState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Runtime AI Planner deck persistence save state cannot be null."));
            }

            RuntimeModuleSaveState moduleState = null;
            for (int i = 0; i < saveState.ModuleStates.Count; i++)
            {
                RuntimeModuleSaveState candidate = saveState.ModuleStates[i];
                if (candidate == null)
                {
                    return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "$.moduleStates[" + i + "]",
                        "Runtime AI Planner deck persistence save state contains a null module entry."));
                }
                if (!string.Equals(candidate.ModuleId, ModuleId, StringComparison.Ordinal))
                    continue;
                if (moduleState != null)
                {
                    return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "$.moduleStates",
                        "Runtime AI Planner deck persistence module state must be unique."));
                }
                moduleState = candidate;
            }
            if (moduleState == null || moduleState.CustomState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates",
                    "Runtime AI Planner deck persistence module state is missing."));
            }
            if (moduleState.SchemaVersion != CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.SchemaVersion
                || moduleState.SchemaVersion != moduleState.CustomState.SchemaVersion)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "$.moduleStates.runtimeAiPlannerDeckPersistence.schemaVersion",
                    "Runtime AI Planner deck persistence module schema version is unsupported.",
                    moduleState.SchemaVersion,
                    CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.SchemaVersion));
            }

            CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult restore =
                CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Restore(
                    moduleState.CustomState,
                    _catalog,
                    _runtimeContractVersion,
                    _migrations);
            LastRestoreDiagnostics = restore.Diagnostics;
            if (!restore.Success)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.CustomStateMismatch,
                    "$.moduleStates.runtimeAiPlannerDeckPersistence.customState",
                    FirstErrorMessage(restore.Diagnostics),
                    moduleState.CustomState.SchemaVersion,
                    CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.SchemaVersion));
            }

            CurrentSnapshot = restore.Snapshot;
            ResolvedDecks = restore.ResolvedDecks;
            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private static string FirstErrorMessage(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error)
                    return diagnostics[i].Code + ": " + diagnostics[i].Message;
            }
            return "Runtime AI Planner deck persistence restore failed.";
        }
    }
}
