using System;

namespace MxFramework.Runtime
{
    /// <summary>Immutable restore authority shared by prepare-load and one authoritative coordinator.</summary>
    /// <remarks>
    /// AllocByDesign. Creation copies the descriptor values and snapshots the migration topology. The
    /// profile object itself is the opaque composition identity; callers cannot recreate that identity from
    /// descriptor values.
    /// </remarks>
    public sealed class RuntimeSaveStateRestoreProfile
    {
        private readonly RuntimeSaveStateMigrationPipeline _migrationPipeline;
        private readonly object _compositionClaimLock = new object();
        private bool _compositionClaimed;

        private RuntimeSaveStateRestoreProfile(
            RuntimeSaveStateRootHeaderDescriptor descriptor,
            RuntimeSaveStateMigrationPipeline migrationPipeline)
        {
            Descriptor = descriptor;
            _migrationPipeline = migrationPipeline;
        }

        public RuntimeSaveStateRootHeaderDescriptor Descriptor { get; }

        /// <summary>Creates an unclaimed sibling with the same frozen descriptor and migration topology.</summary>
        /// <remarks>
        /// AllocByDesign. The sibling has a distinct opaque composition identity so one prepared load can be bound
        /// to exactly one candidate Runtime composition without sharing the live composition profile.
        /// </remarks>
        public RuntimeSaveStateRestoreProfile CreateSibling()
        {
            return new RuntimeSaveStateRestoreProfile(
                new RuntimeSaveStateRootHeaderDescriptor(
                    Descriptor.SchemaVersion,
                    Descriptor.FrameworkVersion,
                    Descriptor.ConfigVersion,
                    Descriptor.ResourceCatalogVersion),
                _migrationPipeline.CreateSnapshot());
        }

        /// <summary>Creates a frozen restore profile for one Runtime composition.</summary>
        /// <remarks>AllocByDesign. The supplied migration topology is copied and later registrations cannot affect this profile.</remarks>
        public static RuntimeSaveStateRestoreProfile Create(
            RuntimeSaveStateRootHeaderDescriptor descriptor,
            RuntimeSaveStateMigrationPipeline migrationPipeline)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var descriptorCopy = new RuntimeSaveStateRootHeaderDescriptor(
                descriptor.SchemaVersion,
                descriptor.FrameworkVersion,
                descriptor.ConfigVersion,
                descriptor.ResourceCatalogVersion);
            RuntimeSaveStateMigrationPipeline pipelineCopy =
                (migrationPipeline ?? new RuntimeSaveStateMigrationPipeline()).CreateSnapshot();
            return new RuntimeSaveStateRestoreProfile(descriptorCopy, pipelineCopy);
        }

        /// <summary>Migrates and validates a document without invoking any participant restorer.</summary>
        /// <remarks>LowFreqAlloc. Success returns an immutable plan bound to this exact profile instance.</remarks>
        public RuntimeSaveStateResult<RuntimeSaveStateRestorePlan> Prepare(RuntimeSaveState saveState)
        {
            long sourceFrame = saveState != null ? saveState.Frame : -1L;
            if (saveState != null && sourceFrame < 0L)
            {
                return RuntimeSaveStateResult<RuntimeSaveStateRestorePlan>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.frame",
                    "Save-state frame cannot be negative.",
                    saveState.SchemaVersion,
                    Descriptor.SchemaVersion));
            }

            RuntimeSaveStateResult<RuntimeSaveState> migration =
                _migrationPipeline.MigratePreservingFrame(saveState, Descriptor.SchemaVersion);
            if (!migration.Success)
            {
                RuntimeSaveStateError migrationError = migration.Error;
                if (migrationError == null || migrationError.IsNone)
                {
                    migrationError = new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "$.schemaVersion",
                        "Save-state migration returned an invalid failure result.",
                        saveState != null ? saveState.SchemaVersion : -1,
                        Descriptor.SchemaVersion);
                }

                return RuntimeSaveStateResult<RuntimeSaveStateRestorePlan>.Failed(migrationError);
            }

            RuntimeSaveState migrated = migration.Value;
            if (migrated.Frame != sourceFrame)
            {
                return RuntimeSaveStateResult<RuntimeSaveStateRestorePlan>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.FrameChangedDuringMigration,
                    "$.frame",
                    "Save-state migration changed the completed Runtime frame.",
                    saveState.SchemaVersion,
                    Descriptor.SchemaVersion));
            }

            RuntimeSaveStateError mismatch = CreateDescriptorMismatch(migrated);
            if (mismatch != null)
            {
                return RuntimeSaveStateResult<RuntimeSaveStateRestorePlan>.Failed(mismatch);
            }

            return RuntimeSaveStateResult<RuntimeSaveStateRestorePlan>.Succeeded(
                new RuntimeSaveStateRestorePlan(migrated, this));
        }

        internal bool Owns(RuntimeSaveStateRestorePlan plan)
        {
            return plan != null && ReferenceEquals(plan.Profile, this);
        }

        internal bool TryClaimComposition()
        {
            lock (_compositionClaimLock)
            {
                if (_compositionClaimed)
                {
                    return false;
                }

                _compositionClaimed = true;
                return true;
            }
        }

        internal void ReleaseCompositionClaim()
        {
            lock (_compositionClaimLock)
            {
                _compositionClaimed = false;
            }
        }

        private RuntimeSaveStateError CreateDescriptorMismatch(RuntimeSaveState saveState)
        {
            string path;
            if (saveState.SchemaVersion != Descriptor.SchemaVersion)
            {
                path = "$.schemaVersion";
            }
            else if (!string.Equals(saveState.FrameworkVersion, Descriptor.FrameworkVersion, StringComparison.Ordinal))
            {
                path = "$.frameworkVersion";
            }
            else if (!string.Equals(saveState.ConfigVersion, Descriptor.ConfigVersion, StringComparison.Ordinal))
            {
                path = "$.configVersion";
            }
            else if (!string.Equals(saveState.ResourceCatalogVersion, Descriptor.ResourceCatalogVersion, StringComparison.Ordinal))
            {
                path = "$.resourceCatalogVersion";
            }
            else
            {
                return null;
            }

            return new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.RootHeaderMismatch,
                path,
                "The prepared save-state root header does not match the restore profile descriptor.",
                saveState.SchemaVersion,
                Descriptor.SchemaVersion);
        }
    }

    /// <summary>Immutable migrated save-state plan bound to one opaque restore profile identity.</summary>
    /// <remarks>LowFreqAlloc. The state and profile are immutable references owned by the plan.</remarks>
    public sealed class RuntimeSaveStateRestorePlan
    {
        internal RuntimeSaveStateRestorePlan(
            RuntimeSaveState state,
            RuntimeSaveStateRestoreProfile profile)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public RuntimeSaveState State { get; }
        public RuntimeSaveStateRestoreProfile Profile { get; }
    }
}
