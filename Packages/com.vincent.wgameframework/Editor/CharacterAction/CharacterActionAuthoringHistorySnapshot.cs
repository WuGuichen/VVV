using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed class CharacterActionAuthoringHistorySnapshot
    {
        private readonly string _modelJson;
        private readonly string _referenceContextJson;

        private CharacterActionAuthoringHistorySnapshot(
            CharacterActionAuthoringModel model,
            CharacterActionAuthoringFileSnapshot[] mechanicTuningFiles,
            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft[]
                pendingMechanicTuningDrafts,
            string abilityAuthoringJsonPath,
            string abilityAuthoringRootJson,
            string abilityEffectTimelinesJson,
            string selectedAbilityEffectTimelineId,
            string selectedAbilityEffectTimelineItemId,
            bool abilityEffectTimelineDirty,
            bool restoreAuthoringFilesOnApply)
        {
            CharacterActionAuthoringModel source = model ?? CharacterActionAuthoringModel.CreateDefault();
            // Capture once on mouse-down; deserialize only if the user actually undoes the edit.
            _modelJson = JsonConvert.SerializeObject(source, CharacterActionAuthoringJsonService.JsonSettings);
            _referenceContextJson = JsonConvert.SerializeObject(
                source.ReferenceContext, CharacterActionAuthoringJsonService.JsonSettings);
            MechanicTuningFiles = mechanicTuningFiles ?? Array.Empty<CharacterActionAuthoringFileSnapshot>();
            PendingMechanicTuningDrafts = pendingMechanicTuningDrafts
                ?? Array.Empty<CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>();
            AbilityAuthoringJsonPath = abilityAuthoringJsonPath ?? string.Empty;
            AbilityAuthoringRootJson = abilityAuthoringRootJson ?? string.Empty;
            AbilityEffectTimelinesJson = abilityEffectTimelinesJson ?? string.Empty;
            SelectedAbilityEffectTimelineId = selectedAbilityEffectTimelineId ?? string.Empty;
            SelectedAbilityEffectTimelineItemId = selectedAbilityEffectTimelineItemId ?? string.Empty;
            AbilityEffectTimelineDirty = abilityEffectTimelineDirty;
            RestoreAuthoringFilesOnApply = restoreAuthoringFilesOnApply;
        }

        public CharacterActionAuthoringFileSnapshot[] MechanicTuningFiles { get; }
        public CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft[]
            PendingMechanicTuningDrafts { get; }
        public string AbilityAuthoringJsonPath { get; }
        public string AbilityAuthoringRootJson { get; }
        public string AbilityEffectTimelinesJson { get; }
        public string SelectedAbilityEffectTimelineId { get; }
        public string SelectedAbilityEffectTimelineItemId { get; }
        public bool AbilityEffectTimelineDirty { get; }
        public bool RestoreAuthoringFilesOnApply { get; }

        public static CharacterActionAuthoringHistorySnapshot Capture(
            CharacterActionAuthoringModel model,
            CharacterActionMechanicTuningAuthoringStoreOptions mechanicTuningOptions = null,
            IReadOnlyDictionary<string, CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>
                pendingMechanicTuningDrafts = null,
            string abilityAuthoringJsonPath = null,
            string abilityAuthoringRootJson = null,
            string abilityEffectTimelinesJson = null,
            string selectedAbilityEffectTimelineId = null,
            string selectedAbilityEffectTimelineItemId = null,
            bool abilityEffectTimelineDirty = false,
            bool restoreAuthoringFilesOnApply = true)
        {
            mechanicTuningOptions = mechanicTuningOptions ?? CharacterActionMechanicTuningAuthoringStoreOptions.Default;
            return new CharacterActionAuthoringHistorySnapshot(
                model,
                restoreAuthoringFilesOnApply
                    ? CaptureMechanicTuningFiles(mechanicTuningOptions)
                    : Array.Empty<CharacterActionAuthoringFileSnapshot>(),
                CapturePendingMechanicTuningDrafts(pendingMechanicTuningDrafts),
                abilityAuthoringJsonPath,
                abilityAuthoringRootJson,
                abilityEffectTimelinesJson,
                selectedAbilityEffectTimelineId,
                selectedAbilityEffectTimelineItemId,
                abilityEffectTimelineDirty,
                restoreAuthoringFilesOnApply);
        }

        public CharacterActionAuthoringModel CreateModelClone()
        {
            CharacterActionAuthoringModel copy = JsonConvert.DeserializeObject<CharacterActionAuthoringModel>(
                _modelJson) ?? CharacterActionAuthoringModel.CreateDefault();
            // The model constructor supplies a default context, so replace it with the saved one.
            copy.ReferenceContext = JsonConvert.DeserializeObject<CharacterActionAuthoringReferenceContext>(
                _referenceContextJson) ?? CharacterActionAuthoringReferenceContext.CreateDefault();
            return copy;
        }

        public void RestoreMechanicTuningFiles()
        {
            for (int i = 0; i < MechanicTuningFiles.Length; i++)
                MechanicTuningFiles[i].Restore();
        }

        public CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft[]
            CreatePendingMechanicTuningDraftClones()
        {
            return CapturePendingMechanicTuningDrafts(PendingMechanicTuningDrafts);
        }

        private static CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft[]
            CapturePendingMechanicTuningDrafts(
                IEnumerable<CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft> drafts)
        {
            if (drafts == null)
            {
                return Array.Empty<
                    CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>();
            }

            var values =
                new List<CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>();
            foreach (CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft draft in drafts)
            {
                if (draft != null)
                    values.Add(draft.Clone());
            }

            return values.ToArray();
        }

        private static CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft[]
            CapturePendingMechanicTuningDrafts(
                IReadOnlyDictionary<string, CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft> drafts)
        {
            if (drafts == null || drafts.Count == 0)
            {
                return Array.Empty<
                    CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>();
            }

            var values =
                new List<CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>(
                    drafts.Count);
            foreach (KeyValuePair<string, CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft> pair
                in drafts)
            {
                if (pair.Value != null)
                    values.Add(pair.Value.Clone());
            }

            return values.ToArray();
        }

        private static CharacterActionAuthoringFileSnapshot[] CaptureMechanicTuningFiles(
            CharacterActionMechanicTuningAuthoringStoreOptions options)
        {
            return new[]
            {
                CharacterActionAuthoringFileSnapshot.Capture(options.AbilityAuthoringPath),
                CharacterActionAuthoringFileSnapshot.Capture(options.GeneratedMechanicTuningsPath),
                CharacterActionAuthoringFileSnapshot.Capture(options.PlayerMirrorMechanicTuningsPath),
            };
        }

    }

    internal readonly struct CharacterActionAuthoringFileSnapshot
    {
        private CharacterActionAuthoringFileSnapshot(string path, bool exists, string text)
        {
            Path = path ?? string.Empty;
            Exists = exists;
            Text = text ?? string.Empty;
        }

        public string Path { get; }
        public bool Exists { get; }
        public string Text { get; }

        public static CharacterActionAuthoringFileSnapshot Capture(string path)
        {
            path = path ?? string.Empty;
            bool exists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            return new CharacterActionAuthoringFileSnapshot(
                path,
                exists,
                exists ? File.ReadAllText(path) : string.Empty);
        }

        public void Restore()
        {
            if (string.IsNullOrWhiteSpace(Path))
                return;

            if (Exists)
            {
                string directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(Path, Text);
            }
            else if (File.Exists(Path))
            {
                File.Delete(Path);
            }

            ImportAssetIfNeeded(Path);
        }

        private static void ImportAssetIfNeeded(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || string.Equals(normalized, "Assets", StringComparison.Ordinal))
            {
                AssetDatabase.ImportAsset(normalized, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
