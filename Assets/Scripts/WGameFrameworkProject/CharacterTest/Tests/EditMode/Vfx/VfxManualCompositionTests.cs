using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Editor;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests
{
    public sealed partial class VfxManualCompositionTests
    {
        private const string TestDraftId = "vfx.composition.test.manual";
        private const string CandidatePath = "Assets/VFX/MxCandidates/Manual/vfx_composition_test_manual/vfx_composition_test_manual.prefab";
        private const string SkillCandidatePath = "Assets/VFX/MxCandidates/Manual/test_skill_candidate.prefab";

        [TearDown]
        public void TearDown()
        {
            string draftPath = VfxCompositionDraftService.DraftRoot + "/" + VfxCompositionDraftService.FileToken(TestDraftId) + ".json";
            if (File.Exists(draftPath)) AssetDatabase.DeleteAsset(draftPath);
            new VfxCompositionRecoveryJournalService().Clear(TestDraftId);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePath) != null) AssetDatabase.DeleteAsset(CandidatePath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SkillCandidatePath) != null) AssetDatabase.DeleteAsset(SkillCandidatePath);
            AssetDatabase.Refresh();
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(parent))
                EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
