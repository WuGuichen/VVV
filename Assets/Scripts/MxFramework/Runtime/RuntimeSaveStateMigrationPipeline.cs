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
        private readonly Dictionary<int, IRuntimeSaveStateMigration> _migrationsByFromVersion;

        public RuntimeSaveStateMigrationPipeline()
            : this(null)
        {
        }

        public RuntimeSaveStateMigrationPipeline(IReadOnlyList<IRuntimeSaveStateMigration> migrations)
        {
            _migrationsByFromVersion = new Dictionary<int, IRuntimeSaveStateMigration>();
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

            if (migration.FromSchemaVersion < 0 || migration.ToSchemaVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(migration), "Save state schema versions cannot be negative.");
            }

            if (migration.ToSchemaVersion != migration.FromSchemaVersion + 1)
            {
                throw new ArgumentException("Save state migrations must advance exactly one schema version.", nameof(migration));
            }

            if (_migrationsByFromVersion.ContainsKey(migration.FromSchemaVersion))
            {
                throw new InvalidOperationException("A save state migration from schema version " + migration.FromSchemaVersion + " is already registered.");
            }

            _migrationsByFromVersion.Add(migration.FromSchemaVersion, migration);
        }

        public RuntimeSaveStateResult<RuntimeSaveState> Migrate(RuntimeSaveState saveState, int targetSchemaVersion)
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

            RuntimeSaveState current = saveState;
            while (current.SchemaVersion < targetSchemaVersion)
            {
                if (!_migrationsByFromVersion.TryGetValue(current.SchemaVersion, out IRuntimeSaveStateMigration migration))
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
                    result = migration.Migrate(current);
                }
                catch (Exception ex)
                {
                    return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.CustomStateMigrationFailed,
                        "$",
                        "Save state migration threw an exception: " + ex.Message,
                        migration.FromSchemaVersion,
                        migration.ToSchemaVersion,
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
                        migration.FromSchemaVersion,
                        migration.ToSchemaVersion));
                }

                if (result.Value.SchemaVersion != migration.ToSchemaVersion)
                {
                    return RuntimeSaveStateResult<RuntimeSaveState>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "$.schemaVersion",
                        "Save state migration returned an unexpected schema version.",
                        result.Value.SchemaVersion,
                        migration.ToSchemaVersion));
                }

                current = result.Value;
            }

            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(current);
        }
    }
}
