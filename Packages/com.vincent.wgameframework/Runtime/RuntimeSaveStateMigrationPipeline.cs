using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public interface IRuntimeSaveStateMigration
    {
        int FromSchemaVersion { get; }
        int ToSchemaVersion { get; }
        RuntimeSaveStateResult<RuntimeSaveState> Migrate(RuntimeSaveState saveState);
    }

    public sealed class RuntimeSaveStateMigrationPipeline
    {
        private sealed class MigrationEntry
        {
            internal MigrationEntry(
                int fromSchemaVersion,
                int toSchemaVersion,
                IRuntimeSaveStateMigration migration)
            {
                FromSchemaVersion = fromSchemaVersion;
                ToSchemaVersion = toSchemaVersion;
                Migration = migration;
            }

            internal int FromSchemaVersion { get; }
            internal int ToSchemaVersion { get; }
            internal IRuntimeSaveStateMigration Migration { get; }
        }

        private readonly Dictionary<int, MigrationEntry> _migrationsByFromVersion;

        public RuntimeSaveStateMigrationPipeline()
            : this(null)
        {
        }

        public RuntimeSaveStateMigrationPipeline(IReadOnlyList<IRuntimeSaveStateMigration> migrations)
        {
            _migrationsByFromVersion = new Dictionary<int, MigrationEntry>();
            if (migrations == null)
            {
                return;
            }

            for (int i = 0; i < migrations.Count; i++)
            {
                Register(migrations[i]);
            }
        }

        public void Register(IRuntimeSaveStateMigration migration)
        {
            if (migration == null)
            {
                throw new ArgumentNullException(nameof(migration));
            }

            int fromSchemaVersion = migration.FromSchemaVersion;
            int toSchemaVersion = migration.ToSchemaVersion;
            if (fromSchemaVersion < 0 || toSchemaVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(migration), "Save state schema versions cannot be negative.");
            }

            if (toSchemaVersion != fromSchemaVersion + 1)
            {
                throw new ArgumentException("Save state migrations must advance exactly one schema version.", nameof(migration));
            }

            if (_migrationsByFromVersion.ContainsKey(fromSchemaVersion))
            {
                throw new InvalidOperationException("A save state migration from schema version " + fromSchemaVersion + " is already registered.");
            }

            _migrationsByFromVersion.Add(
                fromSchemaVersion,
                new MigrationEntry(fromSchemaVersion, toSchemaVersion, migration));
        }

        public RuntimeSaveStateResult<RuntimeSaveState> Migrate(RuntimeSaveState saveState, int targetSchemaVersion)
        {
            return MigrateCore(saveState, targetSchemaVersion, false);
        }

        /// <summary>Migrates while rejecting a checkpoint frame change after every individual step.</summary>
        /// <remarks>LowFreqAlloc. Used by frozen durable-restore profiles; legacy migration semantics remain unchanged.</remarks>
        internal RuntimeSaveStateResult<RuntimeSaveState> MigratePreservingFrame(
            RuntimeSaveState saveState,
            int targetSchemaVersion)
        {
            return MigrateCore(saveState, targetSchemaVersion, true);
        }

        private RuntimeSaveStateResult<RuntimeSaveState> MigrateCore(
            RuntimeSaveState saveState,
            int targetSchemaVersion,
            bool preserveFrame)
        {
            if (saveState == null)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Save state document is null.",
                    -1,
                    targetSchemaVersion));
            }

            if (targetSchemaVersion < 0)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.schemaVersion",
                    "Target save state schema version cannot be negative.",
                    saveState.SchemaVersion,
                    targetSchemaVersion));
            }

            if (saveState.SchemaVersion < 0)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.schemaVersion",
                    "Save state schema version cannot be negative.",
                    saveState.SchemaVersion,
                    targetSchemaVersion));
            }

            if (saveState.SchemaVersion > targetSchemaVersion)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "$.schemaVersion",
                    "Save state schema version is newer than the requested target schema.",
                    saveState.SchemaVersion,
                    targetSchemaVersion));
            }

            long sourceFrame = saveState.Frame;
            RuntimeSaveState current = saveState;
            while (current.SchemaVersion < targetSchemaVersion)
            {
                if (!_migrationsByFromVersion.TryGetValue(current.SchemaVersion, out MigrationEntry entry))
                {
                    return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.MissingMigration,
                        "$.schemaVersion",
                        "Missing save state migration from schema " + current.SchemaVersion + " to schema " + (current.SchemaVersion + 1) + ".",
                        current.SchemaVersion,
                        targetSchemaVersion));
                }

                RuntimeSaveStateResult<RuntimeSaveState> result;
                try
                {
                    result = entry.Migration.Migrate(current);
                }
                catch (Exception ex)
                {
                    return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.CustomStateMigrationFailed,
                        "$",
                        "Save state migration threw an exception: " + ex.Message,
                        entry.FromSchemaVersion,
                        entry.ToSchemaVersion,
                        ex));
                }

                if (!result.Success)
                {
                    return result;
                }

                if (result.Value == null)
                {
                    return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "$",
                        "Save state migration returned a null document.",
                        entry.FromSchemaVersion,
                        entry.ToSchemaVersion));
                }

                if (result.Value.SchemaVersion != entry.ToSchemaVersion)
                {
                    return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "$.schemaVersion",
                        "Save state migration returned an unexpected schema version.",
                        result.Value.SchemaVersion,
                        entry.ToSchemaVersion));
                }

                if (preserveFrame && result.Value.Frame != sourceFrame)
                {
                    return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.FrameChangedDuringMigration,
                        "$.frame",
                        "Save-state migration changed the completed Runtime frame.",
                        entry.FromSchemaVersion,
                        entry.ToSchemaVersion));
                }

                current = result.Value;
            }

            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(current);
        }

        /// <summary>Creates an independent snapshot of the registered migration catalog.</summary>
        /// <remarks>
        /// AllocByDesign. Intended for composition-time freezing; later registrations on the source pipeline
        /// cannot change the returned pipeline.
        /// </remarks>
        internal RuntimeSaveStateMigrationPipeline CreateSnapshot()
        {
            var snapshot = new RuntimeSaveStateMigrationPipeline();
            foreach (KeyValuePair<int, MigrationEntry> pair in _migrationsByFromVersion)
            {
                snapshot._migrationsByFromVersion.Add(pair.Key, pair.Value);
            }

            return snapshot;
        }
    }
}
