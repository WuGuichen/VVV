using System;

namespace MxFramework.Combat.Authoring
{
    public readonly struct CombatAuthoringQuickAction
    {
        public CombatAuthoringQuickAction(
            string id,
            string label,
            CombatAuthoringSeverity severityAllowed,
            string targetAssetGuid,
            string targetPath,
            CombatAuthoringQuickActionKind kind)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Quick action id is required.", nameof(id));
            }

            Id = id;
            Label = label ?? string.Empty;
            SeverityAllowed = severityAllowed;
            TargetAssetGuid = targetAssetGuid ?? string.Empty;
            TargetPath = targetPath ?? string.Empty;
            Kind = kind;
        }

        public string Id { get; }

        public string Label { get; }

        public CombatAuthoringSeverity SeverityAllowed { get; }

        public string TargetAssetGuid { get; }

        public string TargetPath { get; }

        public CombatAuthoringQuickActionKind Kind { get; }
    }
}
