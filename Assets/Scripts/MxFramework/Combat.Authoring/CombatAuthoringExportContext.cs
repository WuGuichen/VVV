using System;

namespace MxFramework.Combat.Authoring
{
    public readonly struct CombatAuthoringExportContext
    {
        public CombatAuthoringExportContext(
            string packageId,
            string sourceAssetGuid,
            string authoringHash,
            string runtimeDataHash,
            string jsonPackageHash,
            string toolVersion)
        {
            PackageId = packageId ?? string.Empty;
            SourceAssetGuid = sourceAssetGuid ?? string.Empty;
            AuthoringHash = authoringHash ?? string.Empty;
            RuntimeDataHash = runtimeDataHash ?? string.Empty;
            JsonPackageHash = jsonPackageHash ?? string.Empty;
            ToolVersion = toolVersion ?? string.Empty;
        }

        public string PackageId { get; }

        public string SourceAssetGuid { get; }

        public string AuthoringHash { get; }

        public string RuntimeDataHash { get; }

        public string JsonPackageHash { get; }

        public string ToolVersion { get; }

        public string ContentHash => string.IsNullOrEmpty(JsonPackageHash) ? RuntimeDataHash : JsonPackageHash;
    }

    public readonly struct CombatAuthoringManifest
    {
        public CombatAuthoringManifest(
            string packageId,
            string version,
            string schema,
            string schemaVersion,
            string createdAt,
            string toolVersion,
            string sourceAssetGuid,
            string contentHash)
        {
            PackageId = packageId ?? string.Empty;
            Version = version ?? string.Empty;
            Schema = schema ?? string.Empty;
            SchemaVersion = schemaVersion ?? string.Empty;
            CreatedAt = createdAt ?? string.Empty;
            ToolVersion = toolVersion ?? string.Empty;
            SourceAssetGuid = sourceAssetGuid ?? string.Empty;
            ContentHash = contentHash ?? string.Empty;
        }

        public string PackageId { get; }

        public string Version { get; }

        public string Schema { get; }

        public string SchemaVersion { get; }

        public string CreatedAt { get; }

        public string ToolVersion { get; }

        public string SourceAssetGuid { get; }

        public string ContentHash { get; }

        public static CombatAuthoringManifest CreateDraft(CombatAuthoringExportContext context)
        {
            return new CombatAuthoringManifest(
                context.PackageId,
                version: "0.1.0",
                schema: CombatAuthoringJsonSchema.FileName,
                schemaVersion: CombatActionAuthoringAsset.CurrentSchemaVersion,
                createdAt: DateTime.UtcNow.ToString("O"),
                toolVersion: string.IsNullOrEmpty(context.ToolVersion) ? "M10G" : context.ToolVersion,
                sourceAssetGuid: context.SourceAssetGuid,
                contentHash: context.ContentHash);
        }
    }

    public static class CombatAuthoringJsonSchema
    {
        public const string FileName = "combat_authoring.schema.json";
        public const string CurrentSchemaVersion = CombatActionAuthoringAsset.CurrentSchemaVersion;
        public const string PackageDirectory = "CombatAuthoringPackage";
        public const string ActionsDirectory = "actions";
        public const string SceneBindingsDirectory = "scene_bindings";
        public const string ReportsDirectory = "reports";
        public const string SchemaDirectory = "schema";
    }
}
