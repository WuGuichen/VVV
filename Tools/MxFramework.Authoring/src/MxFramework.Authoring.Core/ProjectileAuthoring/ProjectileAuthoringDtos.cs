using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    /// <summary>Computes stable hashes for projectile authoring guards.</summary>
    public static class ProjectileAuthoringHashes
    {
        public const string Missing = "missing";

        /// <summary>Computes the lowercase SHA-256 value used by authoring guards.</summary>
        /// <remarks>LowFreqAlloc: authoring-time hashing allocates the digest and formatted string.</remarks>
        public static string ComputeSha256(byte[] content)
        {
            content ??= Array.Empty<byte>();
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha.ComputeHash(content);
            return "sha256:" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    /// <summary>Describes one package-relative projectile authoring diagnostic.</summary>
    /// <remarks>LowFreqAlloc: diagnostics are created only during authoring operations.</remarks>
    public sealed class ProjectileAuthoringDiagnostic
    {
        public string Code { get; set; } = string.Empty;
        public string Severity { get; set; } = "Error";
        public string Message { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string ObjectPath { get; set; } = string.Empty;
        public string FieldPath { get; set; } = string.Empty;
        public List<string> RelatedStableIds { get; set; } = new List<string>();
        public string SuggestedFix { get; set; } = string.Empty;
    }

    /// <summary>Holds one preserved per-source document and its typed projection.</summary>
    /// <remarks>LowFreqAlloc: retains source bytes and a parsed JSON document for an authoring session.</remarks>
    public sealed class ProjectileAuthoringSourceDocument : IDisposable
    {
        public string SourceStableId { get; internal set; } = string.Empty;
        public string RelativePath { get; internal set; } = string.Empty;
        public byte[] OriginalUtf8Bytes { get; internal set; } = Array.Empty<byte>();
        public string Sha256 { get; internal set; } = string.Empty;
        public ProjectileRequestAuthoringDocument Projection { get; internal set; } = new ProjectileRequestAuthoringDocument();
        public JsonDocument PreservedJson { get; internal set; }

        public void Dispose()
        {
            PreservedJson?.Dispose();
            PreservedJson = null;
        }
    }

    /// <summary>Provides immutable identity and reference lookups for one workspace.</summary>
    /// <remarks>LowFreqAlloc: dictionaries and lists are built during workspace load.</remarks>
    public sealed class ProjectileAuthoringReferenceIndex
    {
        public IReadOnlyDictionary<string, ProjectileAuthoringSourceDocument> SourceDocumentsByStableId { get; internal set; }
        public IReadOnlyDictionary<string, ProjectileAuthoringSourceDocument> RequestsByStableId { get; internal set; }
        public IReadOnlyDictionary<string, ProjectileSpawnProfileConfig> SpawnProfilesByStableId { get; internal set; }
        public IReadOnlyDictionary<string, ProjectileDefinitionConfig> DefinitionsByStableId { get; internal set; }
        public IReadOnlyDictionary<int, ProjectileDefinitionConfig> DefinitionsByNumericId { get; internal set; }
        public IReadOnlyDictionary<int, ProjectilePresentationBindingConfig> BindingsByDefinitionId { get; internal set; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> RequestIdsByDefinitionStableId { get; internal set; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> ReferencedVfxResourceKeys { get; internal set; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> ReferencedEffectTimelineIds { get; internal set; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> ReferencedPresentationProfileIds { get; internal set; }
    }

    /// <summary>Contains the three projectile catalog projections and diagnostics.</summary>
    /// <remarks>LowFreqAlloc: compile results are authoring-time objects.</remarks>
    public sealed class ProjectileAuthoringCompileResult
    {
        public AbilityProjectileDefinitionCatalogDocument Definitions { get; set; } = new AbilityProjectileDefinitionCatalogDocument();
        public AbilityProjectileSpawnProfileCatalogDocument SpawnProfiles { get; set; } = new AbilityProjectileSpawnProfileCatalogDocument();
        public AbilityProjectilePresentationBindingDocument PresentationBindings { get; set; } = new AbilityProjectilePresentationBindingDocument();
        public List<ProjectileAuthoringDiagnostic> Diagnostics { get; set; } = new List<ProjectileAuthoringDiagnostic>();
        public bool HasBlockingIssues { get; set; }
    }

    /// <summary>Describes a compiled projectile definition which has no source-authority document.</summary>
    /// <remarks>LowFreqAlloc: generated catalog records are indexed only for authoring workspaces.</remarks>
    public sealed class ProjectileAuthoringGeneratedOnlyEntry
    {
        public string StableId { get; set; } = string.Empty;
        public string Status { get; set; } = "Incomplete";
        public bool Complete { get; set; }
        public int DefinitionId { get; set; }
        public List<string> Diagnostics { get; set; } = new List<string>();
        public ProjectileDefinitionConfig Definition { get; internal set; }
        public ProjectileSpawnProfileConfig SpawnProfile { get; internal set; }
        public ProjectilePresentationBindingConfig PresentationBinding { get; internal set; }
    }

    /// <summary>Defines one guarded full-document source proposal.</summary>
    /// <remarks>LowFreqAlloc: serialized only for authoring apply operations.</remarks>
    public sealed class ProjectileAuthoringDraftDocument
    {
        public string Operation { get; set; } = "Upsert";
        public string SourceRelativePath { get; set; } = string.Empty;
        public string ExpectedSha256 { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public JsonElement Json { get; set; }
    }

    /// <summary>Defines the versioned input to a projectile apply transaction.</summary>
    /// <remarks>LowFreqAlloc: serialized only for authoring apply operations.</remarks>
    public sealed class ProjectileAuthoringDraftBundle
    {
        public string SchemaVersion { get; set; } = "1";
        public List<ProjectileAuthoringDraftDocument> Documents { get; set; } = new List<ProjectileAuthoringDraftDocument>();
        public Dictionary<string, string> ExpectedArtifactHashes { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>Defines one guarded file replacement in an authoring transaction.</summary>
    /// <remarks>LowFreqAlloc: created only while planning an authoring write.</remarks>
    public sealed class ProjectileAuthoringTransactionItem
    {
        public string ReceiptPath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string ExpectedSha256 { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public bool Delete { get; set; }
    }

    /// <summary>Records before and after hashes for one transaction path.</summary>
    /// <remarks>LowFreqAlloc: receipt evidence is created only during authoring writes.</remarks>
    public sealed class ProjectileAuthoringTransactionFileReceipt
    {
        public string Path { get; set; } = string.Empty;
        public string BeforeSha256 { get; set; } = string.Empty;
        public string AfterSha256 { get; set; } = string.Empty;
    }

    /// <summary>Reports commit, rollback, recovery, diagnostics and mirror freshness.</summary>
    /// <remarks>LowFreqAlloc: receipt evidence is created only during authoring writes.</remarks>
    public sealed class ProjectileAuthoringTransactionReceipt
    {
        public string SchemaVersion { get; set; } = "1";
        public string Status { get; set; } = "Blocked";
        public List<ProjectileAuthoringTransactionFileReceipt> TouchedFiles { get; set; } = new List<ProjectileAuthoringTransactionFileReceipt>();
        public List<string> DiagnosticCodes { get; set; } = new List<string>();
        public List<string> RecoveryFiles { get; set; } = new List<string>();
        public string RollbackResult { get; set; } = "NotRequired";
        public string PlayerMirrorFreshness { get; set; } = "NotUpdatedByTransaction";
    }

    /// <summary>Optional authoring transaction fault seam used by deterministic rollback verification.</summary>
    /// <remarks>NoAlloc: callbacks are invoked synchronously and must not mutate transaction targets.</remarks>
    public interface IProjectileAuthoringTransactionFaultInjector
    {
        void BeforeCommit(int committedItemCount, string receiptPath);
        void BeforeRollback(string receiptPath);
    }
}
