using System;

namespace MxFramework.Runtime
{
    /// <summary>Describes the composition-owned durable Runtime SaveState root header.</summary>
    /// <remarks>
    /// LowFreqAlloc. Construction validates and retains immutable schema, framework, configuration, and
    /// resource-catalog descriptor values. Capture timestamps and checkpoint frames are deliberately not
    /// part of this descriptor.
    /// </remarks>
    public sealed class RuntimeSaveStateRootHeaderDescriptor : IEquatable<RuntimeSaveStateRootHeaderDescriptor>
    {
        public RuntimeSaveStateRootHeaderDescriptor(
            int schemaVersion,
            string frameworkVersion,
            string configVersion,
            string resourceCatalogVersion)
        {
            if (schemaVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Save state schema version cannot be negative.");
            }

            SchemaVersion = schemaVersion;
            FrameworkVersion = RequireValue(frameworkVersion, nameof(frameworkVersion));
            ConfigVersion = RequireValue(configVersion, nameof(configVersion));
            ResourceCatalogVersion = RequireValue(resourceCatalogVersion, nameof(resourceCatalogVersion));
        }

        public int SchemaVersion { get; }
        public string FrameworkVersion { get; }
        public string ConfigVersion { get; }
        public string ResourceCatalogVersion { get; }

        /// <summary>Returns whether a save-state root exactly matches this descriptor.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool Matches(RuntimeSaveState saveState)
        {
            return saveState != null &&
                   saveState.SchemaVersion == SchemaVersion &&
                   string.Equals(saveState.FrameworkVersion, FrameworkVersion, StringComparison.Ordinal) &&
                   string.Equals(saveState.ConfigVersion, ConfigVersion, StringComparison.Ordinal) &&
                   string.Equals(saveState.ResourceCatalogVersion, ResourceCatalogVersion, StringComparison.Ordinal);
        }

        /// <remarks>NoAlloc.</remarks>
        public bool Equals(RuntimeSaveStateRootHeaderDescriptor other)
        {
            return other != null &&
                   other.SchemaVersion == SchemaVersion &&
                   string.Equals(other.FrameworkVersion, FrameworkVersion, StringComparison.Ordinal) &&
                   string.Equals(other.ConfigVersion, ConfigVersion, StringComparison.Ordinal) &&
                   string.Equals(other.ResourceCatalogVersion, ResourceCatalogVersion, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RuntimeSaveStateRootHeaderDescriptor);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SchemaVersion;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(FrameworkVersion);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ConfigVersion);
                return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ResourceCatalogVersion);
            }
        }

        private static string RequireValue(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Root-header descriptor values cannot be empty.", parameterName);
            }

            return value;
        }
    }

    /// <summary>Marks the unique provider that owns the merged Runtime SaveState root header.</summary>
    public interface IRuntimeSaveStateRootHeaderAuthority : IRuntimeSaveStateProvider
    {
        /// <summary>Gets the immutable descriptor owned by this authority.</summary>
        /// <remarks>NoAlloc.</remarks>
        RuntimeSaveStateRootHeaderDescriptor Descriptor { get; }
    }

    /// <summary>Default composition-owned provider for the durable Runtime SaveState root header.</summary>
    /// <remarks>
    /// LowFreqAlloc. Each capture creates a header-only save-state document. Gameplay entities, counters,
    /// module bodies, metadata, and the final checkpoint frame remain owned by their existing authorities.
    /// </remarks>
    public sealed class RuntimeSaveStateRootHeaderProvider : IRuntimeSaveStateRootHeaderAuthority
    {
        public const string ParticipantId = "mxframework.runtime.save-header";
        public const int ParticipantOrder = 0;

        public RuntimeSaveStateRootHeaderProvider(RuntimeSaveStateRootHeaderDescriptor descriptor)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        }

        public RuntimeSaveStateRootHeaderDescriptor Descriptor { get; }

        /// <summary>Creates the reserved registry participant for this authority.</summary>
        /// <remarks>AllocByDesign. Called once while composing a Runtime session.</remarks>
        public RuntimeSaveStateParticipant CreateParticipant()
        {
            return new RuntimeSaveStateParticipant(ParticipantId, ParticipantOrder, this, null);
        }

        /// <summary>Captures an authority-owned header-only document.</summary>
        /// <remarks>LowFreqAlloc. UTC time is non-authoritative display metadata; checkpoint owns final Frame.</remarks>
        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(new RuntimeSaveState(
                Descriptor.SchemaVersion,
                DateTime.UtcNow,
                Descriptor.FrameworkVersion,
                Descriptor.ConfigVersion,
                Descriptor.ResourceCatalogVersion,
                0L,
                null,
                null,
                null,
                null));
        }
    }
}
