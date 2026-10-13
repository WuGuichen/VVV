using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Editor;
using MxFramework.Editor.Levels;
using MxFramework.Resources;
using Newtonsoft.Json.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule : IMxLevelEditorAssetSavePaths
    {
        private CharacterTestWorldObjectDefinitionCatalog ActiveDefinitionCatalog =>
            _definitionCompileResult?.Catalog ?? CharacterTestWorldObjectSceneSource.BuiltInCatalog;

        private CharacterTestWorldObjectPresentationCatalog ActivePresentationCatalog =>
            _definitionCompileResult?.Presentations;

        private CharacterTestWorldObjectContentProjection ContentProjection =>
            _contentProjection ??= new CharacterTestWorldObjectContentProjection();

        private void LoadDefinitions()
        {
            // Reload invalidates the low-frequency resource projection just as the former eager
            // construction did; the next Definitions/Placement use rebuilds and revalidates it.
            _contentProjection = null;
            string path = CharacterTestWorldObjectDefinitionCatalogLoader.DefaultAuthoringPath;
            string source = File.Exists(path) ? File.ReadAllText(path) : null;
            _definitionSourceHash = source == null ? null : CharacterTestLevelCatalog.Hash(source);
            if (source != null
                && CharacterTestWorldObjectDefinitionJson.TryLoadAuthoring(
                    source,
                    out CharacterTestWorldObjectDefinitionAuthoringDocument document,
                    out _))
            {
                _definitionDocument = document;
            }
            else
            {
                _definitionDocument = CharacterTestWorldObjectDefinitionMigration.ExportBuiltInCatalog();
            }

            CompileDefinitions();
        }

        /// <summary>LowFreqAlloc: declares every definition file written by the coordinated save.</summary>
        public void CollectAssetSavePaths(List<string> paths)
        {
            if (!_definitionsDirty)
                return;
            paths.Add(CharacterTestWorldObjectDefinitionCatalogLoader.DefaultAuthoringPath);
            paths.Add(CharacterTestWorldObjectDefinitionCatalogLoader.DefaultGeneratedRuntimePath);
            paths.Add("Assets/StreamingAssets/"
                + CharacterTestWorldObjectDefinitionCatalogLoader.DefaultPlayerMirrorRelativePath);
        }

        private string ReadDefinitionSourceHash()
        {
            string path = CharacterTestWorldObjectDefinitionCatalogLoader.DefaultAuthoringPath;
            return File.Exists(path) ? CharacterTestLevelCatalog.Hash(File.ReadAllText(path)) : null;
        }

        private void RequireDefinitionSourceUnchanged()
        {
            if (_definitionsDirty && !string.Equals(_definitionSourceHash, ReadDefinitionSourceHash(), StringComparison.Ordinal))
                throw new InvalidOperationException("对象定义源文件已被其他入口修改或移除："
                    + CharacterTestWorldObjectDefinitionCatalogLoader.DefaultAuthoringPath
                    + "。定义草稿保留，请先处理冲突再显式重载。");
        }

        private JObject CaptureDefinitionReloadState()
        {
            return new JObject {
                ["document"] = CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(_definitionDocument),
                ["dirty"] = _definitionsDirty, ["sourceHash"] = _definitionSourceHash,
                ["undoBaseline"] = _definitionUndoSession?.SavedBaseline };
        }

        private void RestoreDefinitionReloadState(JObject state)
        {
            if (state == null)
                return; // Recovery records written before definitions were included.
            CharacterTestWorldObjectDefinitionJson.TryLoadAuthoring(
                (string)state["document"], out var document, out _);
            // Semantic errors are valid unsaved drafts; malformed recovery data must fail closed.
            if (document == null || document.Format != CharacterTestWorldObjectDefinitionFormats.AuthoringV1
                || document.SchemaVersion != CharacterTestWorldObjectDefinitionFormats.SchemaVersion)
                throw new InvalidOperationException("对象定义恢复草稿无法解析；恢复记录保留。");
            bool dirty = (bool)state["dirty"];
            string sourceHash = (string)state["sourceHash"];
            _definitionUndoSession?.Retire();
            _definitionUndoSession = null;
            _definitionDocument = document;
            _definitionSourceHash = sourceHash;
            _definitionsDirty = dirty;
            _contentProjection = null;
            CompileDefinitions();
            EnsureDefinitionUndoSession();
            if (state["undoBaseline"]?.Type == JTokenType.String)
                _definitionUndoSession.SetSavedBaseline((string)state["undoBaseline"]);
        }

        internal void InitializeForCombatLevelPrepareTests(
            LevelWorldObjectAuthoringDocument document)
        {
            // This seam deliberately avoids the machine-local authoring definition file. That file
            // validates unrelated published VFX receipts; Combat Level save-preflight tests need the
            // real module/prepare/compiler path with the stable built-in world-object catalog only.
            // ContentProjection is intentionally left null: only UI/placement methods consume it;
            // definition compile, document validation, and save prepare do not.
            _contentProjection = null;
            _definitionDocument = CharacterTestWorldObjectDefinitionMigration.ExportBuiltInCatalog();
            CompileDefinitions();
            SetDocumentForTests(document);
        }

        private void CompileDefinitions()
        {
            _definitionCompileResult =
                CharacterTestWorldObjectDefinitionCompiler.Compile(_definitionDocument);
        }

        private IReadOnlyList<MxLevelEditorListRow> BuildDefinitionRows()
        {
            var rows = new List<MxLevelEditorListRow>();
            IReadOnlyList<CharacterTestWorldObjectDefinitionAuthoringEntry> definitions =
                _definitionDocument?.Definitions;
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                CharacterTestWorldObjectDefinitionAuthoringEntry entry = definitions[i];
                if (entry == null)
                    continue;
                var row = new DefinitionRow(i, entry);
                rows.Add(new MxLevelEditorListRow(
                    row.Key,
                    "对象定义",
                    entry.Category,
                    entry.DisplayName,
                    entry.DefinitionId.ToString(),
                    entry.StableId,
                    ResolveDefinitionStatus(entry),
                    CreateDefinitionSearchText(entry),
                    row));
            }

            return rows;
        }

        private VisualElement CreateDefinitionWorkspaceInspector(DefinitionRow row)
        {
            var root = new VisualElement { name = "character-test-definition-workspace" };
            root.Add(CreateInspectorTitle("对象定义"));

            var actions = new VisualElement();
            actions.AddToClassList("mx-level-editor__inspector-actions");
            actions.Add(CreateSmallButton("新建", CreateDefinition));
            actions.Add(CreateSmallButton(
                row.Entry.Enabled ? "停用" : "启用",
                () => MutateDefinition(
                    "切换世界对象定义状态",
                    row.Entry,
                    entry => entry.Enabled = !entry.Enabled)));
            root.Add(actions);

            VisualElement identity = CreateInspectorSection("标识与分类");
            identity.Add(CreateDefinitionIntegerField(
                "定义 ID",
                row.Entry.DefinitionId,
                value => row.Entry.DefinitionId = value));
            identity.Add(CreateDefinitionTextField(
                "稳定 ID",
                row.Entry.StableId,
                value => row.Entry.StableId = value));
            identity.Add(CreateDefinitionTextField(
                "名称",
                row.Entry.DisplayName,
                value => row.Entry.DisplayName = value));
            identity.Add(CreateDefinitionTextField(
                "分类",
                row.Entry.Category,
                value => row.Entry.Category = value));
            identity.Add(CreateDefinitionTextField(
                "说明",
                row.Entry.Description,
                value => row.Entry.Description = value));
            root.Add(identity);

            VisualElement presentation = CreateInspectorSection("场景表现");
            var presentationKinds = new List<string>
            {
                CharacterTestWorldObjectPresentationKind.PrefabResource.ToString(),
                CharacterTestWorldObjectPresentationKind.PrimitiveAuthoring.ToString()
            };
            int presentationIndex = Mathf.Max(
                0,
                presentationKinds.IndexOf(row.Entry.Presentation.Kind));
            var kindField = new PopupField<string>(
                "来源",
                presentationKinds,
                presentationIndex);
            kindField.RegisterValueChangedCallback(evt =>
                MutateDefinition(
                    "修改世界对象表现来源",
                    row.Entry,
                    entry => entry.Presentation.Kind = evt.newValue));
            presentation.Add(kindField);

            IReadOnlyList<ResourceCatalogEntry> prefabEntries =
                ContentProjection.FindPrefabEntries(string.Empty, string.Empty);
            var prefabLabels = new List<string> { "未绑定" };
            var prefabKeys = new List<ResourceKey> { default };
            int selectedPrefabIndex = 0;
            ResourceKey currentKey = row.Entry.Presentation.ResourceKey?.ToResourceKey() ?? default;
            for (int i = 0; i < prefabEntries.Count; i++)
            {
                ResourceKey key = prefabEntries[i].CreateKey(
                    SampleResourceCatalogBuilder.PackageId);
                prefabLabels.Add(prefabEntries[i].Id);
                prefabKeys.Add(key);
                if (key.Equals(currentKey))
                    selectedPrefabIndex = prefabKeys.Count - 1;
            }

            var prefabField = new PopupField<string>(
                "Prefab 资源",
                prefabLabels,
                selectedPrefabIndex);
            prefabField.RegisterValueChangedCallback(evt =>
            {
                int index = prefabLabels.IndexOf(evt.newValue);
                ResourceKey key = index >= 0 && index < prefabKeys.Count
                    ? prefabKeys[index]
                    : default;
                MutateDefinition(
                    "绑定世界对象 Prefab",
                    row.Entry,
                    entry => SetResourceKey(entry.Presentation.ResourceKey, key));
            });
            presentation.Add(prefabField);

            Texture2D thumbnail = ContentProjection.ResolveThumbnail(currentKey);
            if (thumbnail != null)
            {
                var image = new Image
                {
                    image = thumbnail,
                    scaleMode = ScaleMode.ScaleToFit
                };
                image.style.height = 96;
                image.style.marginTop = 4;
                presentation.Add(image);
            }

            if (string.Equals(
                    row.Entry.Presentation.Kind,
                    CharacterTestWorldObjectPresentationKind.PrefabResource.ToString(),
                    StringComparison.Ordinal)
                && !ContentProjection.TryResolvePrefab(currentKey, out _, out string diagnostic))
            {
                presentation.Add(new HelpBox(diagnostic, HelpBoxMessageType.Error));
            }
            root.Add(presentation);

            VisualElement facets = CreateInspectorSection("玩法属性");
            facets.Add(CreateDefinitionTextField(
                "对象类型",
                row.Entry.ObjectKind,
                value => row.Entry.ObjectKind = value));
            facets.Add(CreateDefinitionTextField(
                "材质类型",
                row.Entry.Material.Kind,
                value => row.Entry.Material.Kind = value));
            facets.Add(CreateDefinitionTextField(
                "形状类型",
                row.Entry.Shape.Kind,
                value => row.Entry.Shape.Kind = value));
            facets.Add(CreateDefinitionIntegerField(
                "质量（克）",
                row.Entry.MovableNaturalObject.MassGrams,
                value => row.Entry.MovableNaturalObject.MassGrams = value));
            root.Add(facets);
            return root;
        }

        private void CreateDefinition()
        {
            CharacterTestWorldObjectDefinitionAuthoringEntry source =
                _definitionDocument.Definitions.Count > 0
                    ? _definitionDocument.Definitions[0]
                    : CharacterTestWorldObjectDefinitionMigration.ExportBuiltInCatalog().Definitions[0];
            CharacterTestWorldObjectDefinitionAuthoringEntry entry = CloneDefinition(source);
            entry.DefinitionId = AllocateDefinitionId();
            entry.StableId = "character_test.world_object.new_" + entry.DefinitionId;
            entry.DisplayName = "新对象定义";
            entry.Enabled = true;
            entry.Deprecated = false;
            AddDefinition("新建世界对象定义", entry);
        }

        private void AddDefinition(
            string undoName,
            CharacterTestWorldObjectDefinitionAuthoringEntry entry)
        {
            EnsureDefinitionUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _definitionUndoSession.BeginTransaction(undoName))
            {
                _definitionDocument.Definitions.Add(entry);
                MarkDefinitionsDirty();
                transaction.Commit();
            }
            _activeContext?.RevealRow("definitions", "definition:" + entry.DefinitionId);
        }

        private void MutateDefinition(
            string undoName,
            CharacterTestWorldObjectDefinitionAuthoringEntry entry,
            Action<CharacterTestWorldObjectDefinitionAuthoringEntry> mutation)
        {
            if (entry == null || mutation == null)
                return;
            EnsureDefinitionUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _definitionUndoSession.BeginTransaction(undoName))
            {
                mutation(entry);
                MarkDefinitionsDirty();
                transaction.Commit();
            }
        }

        private TextField CreateDefinitionTextField(
            string label,
            string value,
            Action<string> setter)
        {
            var field = new TextField(label) { value = value ?? string.Empty };
            field.RegisterValueChangedCallback(evt =>
                MutateDefinition(
                    "修改世界对象定义",
                    _selectedDefinitionRow?.Entry,
                    _ => setter(evt.newValue ?? string.Empty)));
            return field;
        }

        private IntegerField CreateDefinitionIntegerField(
            string label,
            int value,
            Action<int> setter)
        {
            var field = new IntegerField(label) { value = value };
            field.RegisterValueChangedCallback(evt =>
                MutateDefinition(
                    "修改世界对象定义",
                    _selectedDefinitionRow?.Entry,
                    _ => setter(Math.Max(0, evt.newValue))));
            return field;
        }

        private void MarkDefinitionsDirty()
        {
            _definitionsDirty = true;
            CompileDefinitions();
            _definitionUndoSession?.SynchronizeCurrentDocument();
            _status = _definitionCompileResult.Success
                ? "对象定义已修改，尚未保存。"
                : "对象定义存在校验错误，请修正后保存。";
            _activeContext?.RequestRefresh?.Invoke();
        }

        private void EnsureDefinitionUndoSession()
        {
            if (_definitionUndoSession != null || _definitionDocument == null)
                return;
            _definitionUndoSession = new CharacterTestLevelEditorDocumentUndoSession(
                "CharacterTest 对象定义",
                () => CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(_definitionDocument),
                RestoreDefinitionSnapshot,
                RefreshAfterDefinitionUndo);
            _definitionUndoSession.ResetBaseline();
        }

        private void RestoreDefinitionSnapshot(string snapshot)
        {
            CharacterTestWorldObjectDefinitionJson.TryLoadAuthoring(
                snapshot,
                out CharacterTestWorldObjectDefinitionAuthoringDocument document,
                out _);
            // Undo restores editing history, including semantic errors; save validity is separate.
            if (document == null || document.Format != CharacterTestWorldObjectDefinitionFormats.AuthoringV1
                || document.SchemaVersion != CharacterTestWorldObjectDefinitionFormats.SchemaVersion)
                throw new InvalidOperationException("对象定义 Undo 草稿无法解析或格式不受支持；当前草稿保留。");
            _definitionDocument = document;
            CompileDefinitions();
        }

        private void RefreshAfterDefinitionUndo()
        {
            _definitionsDirty = !_definitionUndoSession.IsAtSavedBaseline;
            CompileDefinitions();
            _selectedDefinitionRow = null;
            _activeContext?.RequestRefresh?.Invoke();
        }

        private int AllocateDefinitionId()
        {
            int highest = 0;
            for (int i = 0; i < _definitionDocument.Definitions.Count; i++)
                highest = Math.Max(highest, _definitionDocument.Definitions[i]?.DefinitionId ?? 0);
            return highest + 1;
        }

        private static CharacterTestWorldObjectDefinitionAuthoringEntry CloneDefinition(
            CharacterTestWorldObjectDefinitionAuthoringEntry source)
        {
            var document = new CharacterTestWorldObjectDefinitionAuthoringDocument();
            document.Definitions.Add(source);
            string json = CharacterTestWorldObjectDefinitionJson.SerializeAuthoring(document);
            CharacterTestWorldObjectDefinitionJson.TryLoadAuthoring(
                json,
                out CharacterTestWorldObjectDefinitionAuthoringDocument clone,
                out _);
            return clone.Definitions[0];
        }

        private static void SetResourceKey(
            CharacterTestResourceKeyAuthoring target,
            ResourceKey value)
        {
            target.Id = value.Id ?? string.Empty;
            target.TypeId = value.TypeId ?? string.Empty;
            target.Variant = value.Variant ?? string.Empty;
            target.PackageId = value.PackageId ?? string.Empty;
        }

        private static string ResolveDefinitionStatus(
            CharacterTestWorldObjectDefinitionAuthoringEntry entry)
        {
            if (!entry.Enabled)
                return "已停用";
            if (entry.Deprecated)
                return "已弃用";
            return string.Empty;
        }

        private static string CreateDefinitionSearchText(
            CharacterTestWorldObjectDefinitionAuthoringEntry entry)
        {
            return entry.DefinitionId
                + " "
                + entry.StableId
                + " "
                + entry.DisplayName
                + " "
                + entry.Category
                + " "
                + entry.ObjectKind
                + " "
                + entry.Description
                + " "
                + entry.Presentation?.ResourceKey?.Id;
        }

        private sealed class DefinitionRow
        {
            public DefinitionRow(
                int index,
                CharacterTestWorldObjectDefinitionAuthoringEntry entry)
            {
                Index = index;
                Entry = entry;
            }

            public int Index { get; }
            public CharacterTestWorldObjectDefinitionAuthoringEntry Entry { get; }
            public string Key => "definition:" + Entry.DefinitionId;
        }
    }
}
