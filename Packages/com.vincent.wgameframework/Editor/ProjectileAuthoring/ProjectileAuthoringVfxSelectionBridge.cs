using System;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Editor.ProjectileAuthoring
{
    internal sealed class ProjectileAuthoringVfxTransientView
    {
        internal string DisplayName = string.Empty;
        internal string Status = string.Empty;
        internal string ActualPrefabPath = string.Empty;
        internal string ActualPrefabLabel = string.Empty;
        internal string Diagnostic = string.Empty;
        internal string Role = string.Empty;
        internal string Anchor = string.Empty;
        internal string Space = string.Empty;
        internal string Lifecycle = string.Empty;
        internal string Review = string.Empty;
        internal Texture Thumbnail;
        internal bool IsCandidate;
        internal bool IsStale;
        internal bool HasSemanticSuggestion;
        internal string ContractDiagnostic = string.Empty;
    }

    internal interface IProjectileAuthoringVfxSelectionBridge
    {
        ProjectileAuthoringVfxTransientView Resolve(string serializedResourceKey);
        void SelectFormal(ProjectileResourceKeyView resource);
    }

    /// <summary>Shares CharacterAction's published/candidate resolver without adding Candidate fields to projectile commands.</summary>
    /// <remarks>LowFreqAlloc: Editor card refresh and explicit picker selection only.</remarks>
    internal sealed class ProjectileAuthoringVfxSelectionBridge : IProjectileAuthoringVfxSelectionBridge
    {
        public ProjectileAuthoringVfxTransientView Resolve(string serializedResourceKey)
        {
            CharacterActionVfxCardSnapshot snapshot = CharacterActionVfxCardSnapshot.Resolve(serializedResourceKey);
            return new ProjectileAuthoringVfxTransientView
            {
                DisplayName = snapshot.DisplayName,
                Status = snapshot.IsCandidate ? "Candidate" : snapshot.VisualMaturity,
                ActualPrefabPath = snapshot.ActualPrefabPath,
                ActualPrefabLabel = snapshot.ActualPrefabLabel,
                Diagnostic = snapshot.Diagnostic,
                Role = snapshot.Role,
                Anchor = ReadSummary(snapshot.Anchor, "anchor"),
                Space = ReadSummary(snapshot.Anchor, "space"),
                Lifecycle = ReadSummary(snapshot.Lifecycle, "mode"),
                Review = snapshot.Review,
                Thumbnail = snapshot.Thumbnail,
                IsCandidate = snapshot.IsCandidate,
                IsStale = snapshot.IsStale
            };
        }

        public void SelectFormal(ProjectileResourceKeyView resource)
        {
            if (resource == null || string.IsNullOrWhiteSpace(resource.id)) return;
            var key = new ResourceKey(resource.id, resource.typeId, resource.variant, resource.packageId);
            VfxPublishedBindingEditorService.ClearPlaytestOverride(key, out _);
        }

        internal static string ContractDiagnostic(string slotKind, CharacterActionVfxSelectionOption option)
        {
            if (option == null)
                return "VFX catalog entry is missing.";
            string anchor = ReadSummary(option.Anchor, "anchor");
            string space = ReadSummary(option.Anchor, "space");
            string lifecycle = ReadSummary(option.Lifecycle, "mode");
            return ContractDiagnostic(slotKind, option.Role, anchor, space, lifecycle);
        }

        internal static string PickerDiagnostic(
            string slotKind,
            string currentSerializedResourceKey,
            CharacterActionVfxSelectionOption option)
        {
            if (option != null && option.IsCandidate
                && !string.Equals(option.ResourceKey.ToString(), currentSerializedResourceKey, StringComparison.Ordinal))
                return "A local Candidate can preview only the slot's existing stable ResourceKey; choose the formal binding first.";
            return string.Empty;
        }

        internal static string ContractDiagnostic(
            string slotKind,
            string role,
            string anchor,
            string space,
            string lifecycle)
        {
            bool persistent = string.Equals(slotKind, "flightBody", StringComparison.Ordinal)
                || string.Equals(slotKind, "trail", StringComparison.Ordinal);
            if (!RoleAllowed(slotKind, role))
                return "Recommended role differs from this projectile slot.";
            if (!AnchorAllowed(slotKind, anchor))
                return "Recommended anchor differs from this projectile slot.";
            if (persistent
                ? !IsAny(lifecycle, "Persistent", "ManualStop")
                : !IsAny(lifecycle, "OneShot"))
                return "Recommended lifecycle differs from this projectile slot.";
            if (!persistent && !string.IsNullOrWhiteSpace(space) && !IsAny(space, "World"))
                return "Recommended space is World for this one-shot projectile slot.";
            return string.Empty;
        }

        private static bool RoleAllowed(string slotKind, string role)
        {
            if (string.Equals(slotKind, "spawn", StringComparison.Ordinal)) return IsAny(role, "Spawn", "Charge");
            if (string.Equals(slotKind, "flightBody", StringComparison.Ordinal)) return IsAny(role, "Flight");
            if (string.Equals(slotKind, "trail", StringComparison.Ordinal)) return IsAny(role, "Trail", "Flight");
            if (string.Equals(slotKind, "expire", StringComparison.Ordinal)) return IsAny(role, "Expire", "Impact");
            return IsAny(role, "Impact");
        }

        private static bool AnchorAllowed(string slotKind, string anchor)
        {
            if (string.Equals(slotKind, "spawn", StringComparison.Ordinal)) return IsAny(anchor, "Caster", "World");
            if (string.Equals(slotKind, "flightBody", StringComparison.Ordinal)
                || string.Equals(slotKind, "trail", StringComparison.Ordinal)) return IsAny(anchor, "Path", "World");
            if (string.Equals(slotKind, "areaImpact", StringComparison.Ordinal)) return IsAny(anchor, "HitPoint", "Ground", "World");
            if (string.Equals(slotKind, "expire", StringComparison.Ordinal)) return IsAny(anchor, "Path", "HitPoint", "World");
            return IsAny(anchor, "HitPoint");
        }

        private static bool IsAny(string value, params string[] expected)
        {
            for (int i = 0; i < expected.Length; i++)
                if (string.Equals(value, expected[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ReadSummary(string summary, string property)
        {
            if (string.IsNullOrWhiteSpace(summary) || summary[0] != '{') return string.Empty;
            try { return JObject.Parse(summary).Value<string>(property) ?? string.Empty; }
            catch { return string.Empty; }
        }
    }

    internal readonly struct ProjectileAuthoringVfxProductionGateFinding
    {
        internal ProjectileAuthoringVfxProductionGateFinding(string code, string message)
        {
            Code = code;
            Message = message;
        }

        internal string Code { get; }
        internal string Message { get; }
    }

    /// <summary>Reusable Editor-side Production/Player gate for one resolved projectile VFX slot.</summary>
    /// <remarks>LowFreqAlloc: explicit validation and future publish flows append findings to caller-owned storage.</remarks>
    internal static class ProjectileAuthoringVfxProductionGate
    {
        internal static int EvaluateSlot(
            string slotKind,
            ProjectileAuthoringVfxTransientView view,
            System.Collections.Generic.List<ProjectileAuthoringVfxProductionGateFinding> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            int start = output.Count;
            if (view == null)
            {
                output.Add(new ProjectileAuthoringVfxProductionGateFinding("PROJAUTH017", "Published Binding could not be resolved."));
                return 1;
            }
            if (view.IsCandidate)
                output.Add(new ProjectileAuthoringVfxProductionGateFinding("PROJAUTH016", "Local Candidate-only VFX cannot be published to Production/Player."));
            if (view.IsStale)
                output.Add(new ProjectileAuthoringVfxProductionGateFinding("PROJAUTH017", "Stale Published Binding or Candidate receipt cannot be published to Production/Player."));
            if (!view.IsCandidate && (!string.Equals(view.Status, "Production", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(view.Review, "Ready", StringComparison.OrdinalIgnoreCase)))
                output.Add(new ProjectileAuthoringVfxProductionGateFinding("PROJAUTH012", "Production/Player requires a Production + Ready published VFX."));
            return output.Count - start;
        }
    }
}
