using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using MxFramework.Editor.CharacterAction;
using Object = UnityEngine.Object;

namespace MxFramework.Editor
{
    internal sealed partial class VfxManualCompositionStudioController
    {
        private void ReloadCatalog()
        {
            _catalog = _curationService.Load();
            BindDraftLocalElements();
            _propertyRegistry = new VfxSemanticPropertyRegistryService().Load(out _);
            RefreshElements();
        }

        private void BindDraftLocalElements()
        {
            _catalog?.SetLocalElements(_session?.Draft?.LocalElements);
        }

        private void RefreshElements()
        {
            if (_catalog == null) return;
            bool draftLocalOnly = _catalog.Curation?.DraftLocalOnly == true;
            bool explicitAuthored = _catalog.Curation?.ExplicitAuthored == true;
            if (!draftLocalOnly && !explicitAuthored) AddProjectPrimitiveElements();
            IEnumerable<VfxSourceElementView> all = draftLocalOnly
                ? _catalog.LocalElements
                : _catalog.LocalElements.Concat(_catalog.Elements);
            ApplyTaxonomy(all);
            SetChoices(_sourceFilter, SourceChoices(draftLocalOnly || explicitAuthored));
            SetFilterChoices(_roleFilter, all.SelectMany(value => value.Roles));
            SetFilterChoices(_themeFilter, all.Select(value => value.Theme));
            SetFilterChoices(_familyFilter, all.Select(value => value.Family));
            SetFilterChoices(_anchorFilter, all.Select(value => value.Anchor));
            SetFilterChoices(_qualityFilter, all.Select(value => value.QualityTier));
            SetFilterChoices(_packageFilter, _catalog.Index?.Packages.Select(value => value.SourcePackageId) ?? Array.Empty<string>());
            SetFilterChoices(_shapeFilter, _catalog.UniqueElements.Select(value => value.Shape));
            SetFilterChoices(_lifecycleFilter, _catalog.UniqueElements.Select(value => value.Lifecycle));
            SetFilterChoices(_extractionFilter, new[] { "NotExtracted", "ReuseExisting", "PartiallyExtracted", "VariantRequired", "Stale", "Conflict" });
            RefreshTaxonomyChoices(all);
            _elementSummary.text = draftLocalOnly
                ? _catalog.Message + " · 当前草稿与全部素材均只读取保留的 Element"
                : _catalog.Message + " · 独特 " + _catalog.UniqueElementCount + " · 原始 " + (_catalog.Index?.ParticleSystemCount ?? 0);
            RefreshSourceIndexAvailability();
            FilterElements();
        }

        private void RefreshSourceIndexAvailability()
        {
            if (_elementIndexUnavailable == null || _elementIndexUnavailableStatus == null)
                return;
            bool unavailable = _catalog?.SourceIndexAvailable != true;
            _elementIndexUnavailable.style.display = unavailable ? DisplayStyle.Flex : DisplayStyle.None;
            _elementIndexUnavailableStatus.text = unavailable
                ? "版本化 Element 仍可浏览；成员预览、语义编辑、导入与烘焙需要重建 Source Index。"
                : string.Empty;
            _elementIndexRebuild?.SetEnabled(unavailable);
        }

        internal static bool CanUseSourceIndex(VfxSourceElementCatalog catalog, out string message)
        {
            if (catalog?.SourceIndexAvailable == true && catalog.Index != null)
            {
                message = string.Empty;
                return true;
            }
            message = "Source Index 未建立；请先重建全量 Source Index。";
            return false;
        }

        private bool RequireSourceIndex(string operation)
        {
            if (CanUseSourceIndex(_catalog, out string message)) return true;
            SetStatus(operation + "不可用。" + message, true);
            return false;
        }

        internal static IReadOnlyList<string> SourceChoices(bool includeDraftLocal)
        {
            return includeDraftLocal
                ? new[] { "全部素材", "当前草稿素材", "已收藏" }
                : new[] { "全部素材", "已收藏" };
        }

        internal static IEnumerable<VfxSourceElementView> ElementsForSource(
            VfxSourceElementCatalog catalog,
            string source,
            ISet<string> favoriteElementIds = null,
            ISet<string> currentDraftElementIds = null)
        {
            if (catalog == null) return Array.Empty<VfxSourceElementView>();
            IEnumerable<VfxSourceElementView> elements = source == "当前草稿素材"
                ? catalog.LocalElements.Concat(catalog.Elements.Where(value => currentDraftElementIds?.Contains(value.ElementId) == true))
                : catalog.LocalElements.Concat(catalog.Elements);
            return source == "已收藏"
                ? elements.Where(value => favoriteElementIds?.Contains(value.ElementId) == true)
                : elements;
        }

        private void AddProjectPrimitiveElements()
        {
            AddRegisteredPrimitiveElements();
            if (_workspace == null) return;
            foreach (VfxLibraryPrimitive primitive in _workspace.PrimitiveItems.Where(value => value.VisualMaturity == "Production" && value.ReviewStatus == "Ready"))
            {
                string id = "vfx.element.project." + VfxCompositionDraftService.FileToken(primitive.PrimitiveId);
                if (_catalog.Elements.Any(value => value.ElementId == id)) continue;
                _catalog.Elements.Add(new VfxSourceElementView
                {
                    ElementId = id, DisplayName = primitive.DisplayName, ElementKind = "ProductionPrimitive", ExistingPrimitiveId = primitive.PrimitiveId,
                    Roles = primitive.Roles.ToList(), Theme = primitive.PrimaryTheme, Family = "Project", Anchor = primitive.CompatibleAnchors.FirstOrDefault() ?? "World",
                    QualityTier = primitive.CostTier, CurationStatus = "Curated", TechnicalFingerprint = primitive.PrimitiveId
                });
            }
        }

        private void AddRegisteredPrimitiveElements()
        {
            if (_catalog?.Registry == null) return;
            foreach (VfxSourceExtractionRegistryEntryView entry in _catalog.Registry.Entries.Where(value => !value.Stale))
            {
                string id = "vfx.element.extracted." + VfxCompositionDraftService.FileToken(entry.PrimitiveId);
                if (_catalog.Elements.Any(value => value.ElementId == id)) continue;
                List<VfxSourceComponentView> members = entry.SourceComponentIds.Select(componentId => _catalog.Components.TryGetValue(componentId, out VfxSourceComponentView component) ? component : null).Where(value => value != null).ToList();
                VfxSourceComponentView primary = members.FirstOrDefault();
                _catalog.Elements.Add(new VfxSourceElementView
                {
                    ElementId = id, DisplayName = entry.PrimitiveId, ElementKind = "RegisteredPrimitive", ExistingPrimitiveId = entry.PrimitiveId,
                    MemberComponentIds = entry.SourceComponentIds.ToList(), EquivalentComponentIds = entry.EquivalentComponentIds.ToList(),
                    Roles = members.SelectMany(value => value.Roles).Distinct(StringComparer.Ordinal).ToList(), Theme = primary?.Theme ?? "Generic",
                    Family = "Extracted", Anchor = primary?.Anchor ?? "World", Shape = primary?.Shape ?? string.Empty, Lifecycle = members.Any(value => value.Loop) ? "Loop" : "OneShot",
                    QualityTier = primary?.QualityTier ?? "Normal", BudgetMaxParticles = members.Sum(value => value.MaxParticles),
                    SourcePackageIds = members.Select(value => value.SourcePackageId).Distinct(StringComparer.Ordinal).ToList(), ExtractionStatus = "ReuseExisting", CurationStatus = "Curated"
                });
            }
        }

        private void FilterElements()
        {
            if (_catalog == null) return;
            string source = _sourceFilter.value;
            _filteredElements.Clear();
            string search = _search.value?.Trim() ?? string.Empty;
            var currentDraftElementIds = new HashSet<string>(
                _session?.Draft?.Layers?.Select(value => value.ElementId) ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            IEnumerable<VfxSourceElementView> palette = ElementsForSource(_catalog, source, _favoriteElementIds, currentDraftElementIds);
            foreach (VfxSourceElementView element in palette)
            {
                if (element.Stale || !Matches(_roleFilter, element.Roles) || !Matches(_themeFilter, element.Theme) || !Matches(_familyFilter, element.Family) || !Matches(_anchorFilter, element.Anchor) || !Matches(_qualityFilter, element.QualityTier) || !Matches(_packageFilter, element.SourcePackageIds) || !Matches(_shapeFilter, element.Shape) || !Matches(_lifecycleFilter, element.Lifecycle) || !Matches(_extractionFilter, element.ExtractionStatus)
                    || !Matches(_useCaseFilter, element.UseCases) || !Matches(_motionFilter, element.Motions) || !Matches(_timingFilter, element.TimingPhase) || !Matches(_spaceFilter, element.SimulationSpaces) || !Matches(_rendererFilter, element.RendererModes) || !Matches(_budgetTierFilter, element.BudgetTier) || !Matches(_granularityFilter, element.Granularity)) continue;
                if (search.Length > 0 && !MatchesSearch(element, search)) continue;
                _filteredElements.Add(element);
            }
            _filteredElements.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal));
            RebuildElementRows();
            _elementVisibleCount.text = _filteredElements.Count + " 项";
            if (_selectedElement == null || !_filteredElements.Any(value => value.ElementId == _selectedElement.ElementId))
            {
                string preferredId = EditorPrefs.GetString(WorkspacePreferencePrefix + "SelectedElementId", string.Empty);
                SelectElement(_filteredElements.FirstOrDefault(value => value.ElementId == preferredId) ?? _filteredElements.FirstOrDefault());
            }
        }

        private static bool MatchesSearch(VfxSourceElementView element, string search)
        {
            if (element.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || element.ElementId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            IEnumerable<string> values = element.Roles.Concat(element.UseCases).Concat(element.Motions).Concat(element.SimulationSpaces).Concat(element.RendererModes)
                .Concat(new[] { element.Family, element.Theme, element.Anchor, element.Shape, element.Lifecycle, element.TimingPhase, element.QualityTier, element.BudgetTier, element.Granularity, element.ExtractionStatus });
            return values.Any(value => !string.IsNullOrWhiteSpace(value)
                && (value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || CategoryLabel(value).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private void RebuildElementRows()
        {
            _elementRows.Clear();
            int columns = _compactElementGrid ? 1 : 2;
            for (int i = 0; i < _filteredElements.Count; i += columns)
                _elementRows.Add(new VfxElementCardRow(_filteredElements[i], columns == 2 && i + 1 < _filteredElements.Count ? _filteredElements[i + 1] : null));
            _elementList.Rebuild();
        }

        private void ApplyTaxonomy(IEnumerable<VfxSourceElementView> elements)
        {
            if (_catalog?.Index == null) return;
            Dictionary<string, VfxSourceEffectView> effects = _catalog.Index.Effects.ToDictionary(value => value.SourceEffectId, StringComparer.Ordinal);
            VfxSourceElementView[] values = elements.ToArray();
            foreach (VfxSourceElementView element in values) VfxSourceElementTaxonomy.Apply(element, _catalog.Components, effects);
            VfxSourceElementCurationService.ApplyManualElementOverrides(values, _catalog.Components, _catalog.Curation);
        }

        private void RefreshTaxonomyChoices(IEnumerable<VfxSourceElementView> elements)
        {
            VfxSourceElementView[] values = elements.ToArray();
            SetFilterChoices(_useCaseFilter, values.SelectMany(value => value.UseCases));
            SetFilterChoices(_motionFilter, values.SelectMany(value => value.Motions));
            SetFilterChoices(_timingFilter, values.Select(value => value.TimingPhase));
            SetFilterChoices(_spaceFilter, values.SelectMany(value => value.SimulationSpaces));
            SetFilterChoices(_rendererFilter, values.SelectMany(value => value.RendererModes));
            SetFilterChoices(_budgetTierFilter, values.Select(value => value.BudgetTier));
            SetFilterChoices(_granularityFilter, values.Select(value => value.Granularity));
        }

        private void SelectElement(VfxSourceElementView element)
        {
            _selectedElement = element;
            BindElementSemanticEditor(element);
            if (!_restoringWorkspace)
                EditorPrefs.SetString(WorkspacePreferencePrefix + "SelectedElementId", element?.ElementId ?? string.Empty);
            _elementDetail.text = element == null ? "请选择 Element" : element.DisplayName + "\n职责 " + CategoryLabels(element.Roles) + " · 主题 " + CategoryLabel(element.Theme)
                + " · 锚点 " + CategoryLabel(element.Anchor) + " · 播放 " + CategoryLabel(element.Lifecycle)
                + "\n用途 " + CategoryLabels(element.UseCases) + " · 运动 " + CategoryLabels(element.Motions) + " · 形态 " + CategoryLabel(element.Shape)
                + "\n空间 " + CategoryLabels(element.SimulationSpaces) + " · 渲染 " + CategoryLabels(element.RendererModes) + " · 预算 " + CategoryLabel(element.BudgetTier)
                + "\n" + element.MemberComponentIds.Count + " 个成员 · " + element.EquivalentComponentIds.Count + " 个等价来源 · 最大粒子 " + element.BudgetMaxParticles + " · " + ExtractionLabel(element);
        }

        private void BindElementSemanticEditor(VfxSourceElementView element)
        {
            _semanticBinding = true;
            _semanticMembersByLabel.Clear();
            var choices = new List<string>();
            if (element != null && _catalog != null)
            {
                for (int i = 0; i < element.MemberComponentIds.Count; i++)
                {
                    if (!_catalog.Components.TryGetValue(element.MemberComponentIds[i], out VfxSourceComponentView component)) continue;
                    string label = component.DisplayName + " · " + ShortObjectPath(component.ObjectPath) + " · " + component.LocalFileId;
                    _semanticMembersByLabel[label] = component;
                    choices.Add(label);
                }
            }
            _elementSemanticMember.choices = choices;
            _elementSemanticMember.SetValueWithoutNotify(choices.FirstOrDefault() ?? string.Empty);
            _semanticBinding = false;
            SetSemanticEditorEnabled(choices.Count > 0);
            BindSelectedSemanticMember();
        }

        private void BindSelectedSemanticMember()
        {
            if (!_semanticMembersByLabel.TryGetValue(_elementSemanticMember.value ?? string.Empty, out VfxSourceComponentView component))
            {
                _elementSemanticStatus.text = "该 Element 没有可修正的 Piloto 源组件。";
                _elementSemanticStatus.EnableInClassList("warning", true);
                return;
            }

            _semanticBinding = true;
            VfxSourceCurationEntryView entry = _catalog.Curation?.Entries.FirstOrDefault(value => string.Equals(value.ComponentId, component.ComponentId, StringComparison.Ordinal));
            _elementSemanticName.SetValueWithoutNotify(_selectedElement == null ? component.DisplayName : DisplayElementName(_selectedElement));
            _elementSemanticMemberName.SetValueWithoutNotify(component.DisplayName);
            SetOptionalSemanticChoices(_elementSemanticRolePrimary, SemanticRoles, component.Roles.ElementAtOrDefault(0));
            SetOptionalSemanticChoices(_elementSemanticRoleSecondary, SemanticRoles, component.Roles.ElementAtOrDefault(1));
            SetOptionalSemanticChoices(_elementSemanticRoleTertiary, SemanticRoles, component.Roles.ElementAtOrDefault(2));
            SetSemanticChoices(_elementSemanticTheme, SemanticThemes, component.Theme);
            SetSemanticChoices(_elementSemanticFamily, SemanticFamilies, entry?.Family ?? _selectedElement?.Family);
            SetSemanticChoices(_elementSemanticShape, SemanticShapes, component.Shape);
            SetSemanticChoices(_elementSemanticAnchor, SemanticAnchors, component.Anchor);
            SetOptionalSemanticChoices(_elementSemanticUseCasePrimary, SemanticUseCases, entry?.UseCases?.ElementAtOrDefault(0) ?? _selectedElement?.UseCases.ElementAtOrDefault(0));
            SetOptionalSemanticChoices(_elementSemanticUseCaseSecondary, SemanticUseCases, entry?.UseCases?.ElementAtOrDefault(1) ?? _selectedElement?.UseCases.ElementAtOrDefault(1));
            SetSemanticChoices(_elementSemanticTiming, SemanticTimingPhases, entry?.TimingPhase ?? _selectedElement?.TimingPhase);
            SetOptionalSemanticChoices(_elementSemanticMotion, SemanticMotions, entry?.Motions?.FirstOrDefault() ?? _selectedElement?.Motions.FirstOrDefault());
            SetSemanticChoices(_elementSemanticQuality, SemanticQualityTiers, component.QualityTier);
            _elementSemanticPreferred.SetValueWithoutNotify(component.Preferred);
            _elementSemanticDeprecated.SetValueWithoutNotify(component.Deprecated);
            _elementSemanticNote.SetValueWithoutNotify(component.Note ?? string.Empty);
            _elementSemanticTechnical.text = "只读技术事实 · " + CategoryLabel(component.Loop ? "Loop" : "OneShot")
                + " · " + CategoryLabel(component.SimulationSpace) + " · " + CategoryLabel(component.RenderMode)
                + " · 最大粒子 " + component.MaxParticles;
            _semanticBinding = false;

            bool overridden = entry != null && (!string.IsNullOrWhiteSpace(entry.DisplayName) || entry.Roles.Count > 0 || !string.IsNullOrWhiteSpace(entry.Family)
                || !string.IsNullOrWhiteSpace(entry.Theme) || !string.IsNullOrWhiteSpace(entry.Shape) || !string.IsNullOrWhiteSpace(entry.Anchor)
                || !string.IsNullOrWhiteSpace(entry.TimingPhase) || (entry.UseCases?.Count ?? 0) > 0 || (entry.Motions?.Count ?? 0) > 0
                || !string.IsNullOrWhiteSpace(entry.QualityTier) || entry.Preferred || entry.Deprecated || !string.IsNullOrWhiteSpace(entry.Note));
            _elementSemanticStatus.text = (overridden ? "当前使用人工修正" : "当前使用自动识别") + " · " + component.ComponentId;
            _elementSemanticStatus.EnableInClassList("warning", false);
        }

        private void SetSemanticEditorEnabled(bool enabled)
        {
            foreach (VisualElement control in new VisualElement[]
                     {
                         _elementSemanticMember, _elementSemanticName, _elementSemanticMemberName, _elementSemanticRolePrimary, _elementSemanticRoleSecondary,
                         _elementSemanticRoleTertiary, _elementSemanticTheme, _elementSemanticFamily, _elementSemanticShape,
                         _elementSemanticAnchor, _elementSemanticUseCasePrimary, _elementSemanticUseCaseSecondary,
                         _elementSemanticTiming, _elementSemanticMotion, _elementSemanticQuality, _elementSemanticPreferred,
                         _elementSemanticDeprecated, _elementSemanticNote, _elementSemanticSave, _elementSemanticReset
                     }) control.SetEnabled(enabled);
        }

        private static void SetSemanticChoices(DropdownField field, IEnumerable<string> standardValues, string currentValue)
        {
            var choices = standardValues.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToList();
            if (!string.IsNullOrWhiteSpace(currentValue) && !choices.Contains(currentValue)) choices.Insert(0, currentValue);
            field.choices = choices;
            field.SetValueWithoutNotify(string.IsNullOrWhiteSpace(currentValue) ? choices.FirstOrDefault() ?? string.Empty : currentValue);
        }

        private static void SetOptionalSemanticChoices(DropdownField field, IEnumerable<string> standardValues, string currentValue)
        {
            var choices = new List<string> { NoSemanticChoice };
            choices.AddRange(standardValues.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal));
            if (!string.IsNullOrWhiteSpace(currentValue) && !choices.Contains(currentValue)) choices.Insert(1, currentValue);
            field.choices = choices;
            field.SetValueWithoutNotify(string.IsNullOrWhiteSpace(currentValue) ? NoSemanticChoice : currentValue);
        }

        private static string ShortObjectPath(string objectPath)
        {
            if (string.IsNullOrWhiteSpace(objectPath)) return "Root";
            int separator = objectPath.LastIndexOf('/');
            return separator < 0 ? objectPath : objectPath.Substring(separator + 1);
        }

        internal static List<string> SelectedSemanticValues(params string[] values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value) && value != NoSemanticChoice)
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
        }

        private void SaveSemanticOverride()
        {
            if (!_semanticMembersByLabel.TryGetValue(_elementSemanticMember.value ?? string.Empty, out VfxSourceComponentView component)) return;
            List<string> roles = SelectedSemanticValues(_elementSemanticRolePrimary.value, _elementSemanticRoleSecondary.value, _elementSemanticRoleTertiary.value);
            if (string.IsNullOrWhiteSpace(_elementSemanticName.value) || string.IsNullOrWhiteSpace(_elementSemanticMemberName.value) || roles.Count == 0)
            {
                _elementSemanticStatus.text = string.IsNullOrWhiteSpace(_elementSemanticName.value) ? "卡片展示名不能为空。"
                    : string.IsNullOrWhiteSpace(_elementSemanticMemberName.value) ? "成员名称不能为空。" : "至少选择一项职责。";
                _elementSemanticStatus.EnableInClassList("warning", true);
                return;
            }

            string elementId = _selectedElement?.ElementId ?? string.Empty;
            List<string> useCases = SelectedSemanticValues(_elementSemanticUseCasePrimary.value, _elementSemanticUseCaseSecondary.value);
            List<string> motions = SelectedSemanticValues(_elementSemanticMotion.value);
            var service = new VfxSourceLibraryEditorService();
            if (!service.SaveCurationOverride(component.ComponentId, elementId, _elementSemanticName.value, _elementSemanticMemberName.value,
                    roles, _elementSemanticFamily.value,
                    _elementSemanticTheme.value, _elementSemanticShape.value, _elementSemanticAnchor.value, _elementSemanticTiming.value,
                    useCases, motions, _elementSemanticQuality.value, _elementSemanticPreferred.value, _elementSemanticDeprecated.value,
                    _elementSemanticNote.value, out string saveMessage))
            {
                _elementSemanticStatus.text = saveMessage;
                _elementSemanticStatus.EnableInClassList("warning", true);
                return;
            }
            if (!service.RefreshCuration(out string refreshMessage))
            {
                _elementSemanticStatus.text = saveMessage + " " + refreshMessage;
                _elementSemanticStatus.EnableInClassList("warning", true);
                return;
            }
            ReloadAfterSemanticChange(elementId, component.ComponentId, saveMessage + " " + refreshMessage);
        }

        private void ResetSemanticOverride()
        {
            if (!_semanticMembersByLabel.TryGetValue(_elementSemanticMember.value ?? string.Empty, out VfxSourceComponentView component)) return;
            string elementId = _selectedElement?.ElementId ?? string.Empty;
            var service = new VfxSourceLibraryEditorService();
            if (!service.ClearCurationOverride(component.ComponentId, elementId, out string message))
            {
                _elementSemanticStatus.text = message;
                _elementSemanticStatus.EnableInClassList("warning", true);
                return;
            }
            ReloadAfterSemanticChange(elementId, component.ComponentId, message);
        }

        private void ReloadAfterSemanticChange(string elementId, string componentId, string message)
        {
            _catalog = _curationService.Load();
            BindDraftLocalElements();
            _selectedElement = null;
            RefreshElements();
            VfxSourceElementView element = _catalog.Find(elementId);
            if (element != null) SelectElement(element);
            string memberLabel = _semanticMembersByLabel.FirstOrDefault(pair => string.Equals(pair.Value.ComponentId, componentId, StringComparison.Ordinal)).Key;
            if (!string.IsNullOrWhiteSpace(memberLabel))
            {
                _semanticBinding = true;
                _elementSemanticMember.SetValueWithoutNotify(memberLabel);
                _semanticBinding = false;
                BindSelectedSemanticMember();
            }
            _elementSemanticStatus.text = message;
            _elementSemanticStatus.EnableInClassList("warning", false);
            SetStatus(message, false);
        }

        private Texture2D LoadThumbnail(VfxSourceElementView element)
        {
            string path = VfxElementManualIconStore.ResolvePreferredIconPath(element);
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (_thumbnailCache.TryGetValue(path, out Texture2D cached)) return cached;
            if (!File.Exists(path)) { _thumbnailCache[path] = null; return null; }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            if (!texture.LoadImage(File.ReadAllBytes(path), true)) { Object.DestroyImmediate(texture); texture = null; }
            _thumbnailCache[path] = texture; return texture;
        }

        private void OpenElementPreview(VfxSourceElementView element)
        {
            if (element == null || _catalog == null) return;
            if (!RequireSourceIndex("Element 动态预览")) return;
            GameObject instance = VfxCompositionCandidateBaker.CreateLayer(_catalog, element);
            if (instance == null) return;
            _elementPreviewElement = element;
            _elementPreview.SelectInstance(instance);
            _elementPreview.SetLoop(_elementPreviewLoop.value);
            BeginContinuousRefresh();
            _elementPreview.Restart();
            _elementPreviewTitle.text = DisplayElementName(element);
            string roles = element.Roles.Count == 0 ? "未分类" : string.Join(" / ", element.Roles.Select(RoleLabel));
            _elementPreviewMeta.text = roles + " · " + ThemeLabel(element.Theme).TrimEnd('·', ' ') + " · S 试放 · A 切换合成";
            _elementPreviewCard.style.display = DisplayStyle.Flex;
            _elementPreviewCard.BringToFront();
            _elementPreviewCard.Focus();
            RefreshElementPreviewUi();
            _elementPreviewCanvas.MarkDirtyRepaint();
        }

        private void SaveElementPreviewAsIcon()
        {
            if (_elementPreviewElement == null || !_elementPreview.HasPreviewContent)
            {
                SetStatus("请先单击一个 Element 打开动态快速预览。", true);
                return;
            }
            _elementPreview.CenterCameraOnContent();
            _elementPreviewCanvas.MarkDirtyRepaint();
            Texture2D capture = _elementPreview.CaptureFrame(512, 288);
            if (capture == null)
            {
                SetStatus("当前小窗没有可截图的预览内容。", true);
                return;
            }
            string iconPath = string.Empty;
            try
            {
                var metadata = new JObject
                {
                    ["sourceEvidencePath"] = (_elementPreviewElement.PreviewEvidencePath ?? string.Empty).Replace('\\', '/'),
                    ["elapsedSeconds"] = _elementPreview.ElapsedSeconds,
                    ["cameraOrbit"] = new JArray(_elementPreview.CameraOrbit.x, _elementPreview.CameraOrbit.y),
                    ["cameraDistance"] = _elementPreview.CameraDistance,
                    ["cameraTarget"] = new JArray(_elementPreview.CameraTarget.x, _elementPreview.CameraTarget.y, _elementPreview.CameraTarget.z),
                    ["width"] = capture.width,
                    ["height"] = capture.height,
                    ["capturedUtc"] = DateTime.UtcNow.ToString("O")
                };
                if (!VfxElementManualIconStore.Save(_elementPreviewElement, capture.EncodeToPNG(), metadata, out iconPath, out string message))
                {
                    SetStatus(message, true);
                    return;
                }
            }
            finally
            {
                Object.DestroyImmediate(capture);
            }

            if (_thumbnailCache.TryGetValue(iconPath, out Texture2D cached))
            {
                if (cached != null) Object.DestroyImmediate(cached);
                _thumbnailCache.Remove(iconPath);
            }
            _elementList.RefreshItems();
            _layerList.RefreshItems();
            SetStatus("已将当前小窗画面保存为持久 Element 图标：" + DisplayElementName(_elementPreviewElement) + " · " + iconPath, false);
        }

        private void PreviewElementInComposition(VfxSourceElementView element)
        {
            if (element == null || _catalog == null) return;
            if (!RequireSourceIndex("Element 临时试放")) return;
            GameObject instance = VfxCompositionCandidateBaker.CreateLayer(_catalog, element);
            if (instance == null)
            {
                SetStatus("无法创建临时试放组件：" + DisplayElementName(element), true);
                return;
            }
            _preview.SetTemporaryOverlay(instance);
            if (_temporaryElementName != null) _temporaryElementName.text = DisplayElementName(element);
            if (_temporaryElementBanner != null)
            {
                _temporaryElementBanner.style.display = DisplayStyle.Flex;
                _temporaryElementBanner.BringToFront();
            }
            RefreshTemporaryElementModeLabel();
            BeginContinuousRefresh();
            RefreshCompositionPreviewVisibility();
            _previewCanvas.MarkDirtyRepaint();
            RefreshPlaybackUi();
            SetStatus("正在临时试放“" + DisplayElementName(element) + "”；Layer Stack 与草稿未改变。", false);
        }

        private void ClearTemporaryElementPreview()
        {
            _preview.ClearTemporaryOverlay();
            if (_temporaryElementBanner != null) _temporaryElementBanner.style.display = DisplayStyle.None;
            RefreshCompositionPreviewVisibility();
            _previewCanvas.MarkDirtyRepaint();
            RefreshPlaybackUi();
        }

        private void RefreshCompositionPreviewVisibility()
        {
            bool hasContent = _preview.HasPreviewContent;
            _previewEmpty.style.display = hasContent ? DisplayStyle.None : DisplayStyle.Flex;
            _previewCanvas.style.display = hasContent ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasContent || (_preview.HasTemporaryOverlay && _preview.TemporaryOverlaySolo)) _comparisonCanvas.style.display = DisplayStyle.None;
            else if (_abToggle.value && _comparisonDraft != null) _comparisonCanvas.style.display = DisplayStyle.Flex;
        }

        private void ToggleTemporaryElementCompositionMode()
        {
            if (!_preview.HasTemporaryOverlay) return;
            _preview.SetTemporaryOverlaySolo(!_preview.TemporaryOverlaySolo);
            RefreshTemporaryElementModeLabel();
            RefreshCompositionPreviewVisibility();
            _previewCanvas.MarkDirtyRepaint();
            SetStatus(_preview.TemporaryOverlaySolo
                ? "临时试放：仅显示当前选中组件；Layer Stack 未被修改。"
                : "临时试放：当前选中组件与 Layer Stack 一起播放。", false);
        }

        private void RefreshTemporaryElementModeLabel()
        {
            if (_temporaryElementMode == null) return;
            _temporaryElementMode.text = _preview.TemporaryOverlaySolo ? "仅组件 · A 切换" : "与 Layer Stack 一起 · A 切换";
            _temporaryElementMode.EnableInClassList("solo", _preview.TemporaryOverlaySolo);
        }

        private void CloseElementPreview()
        {
            _elementPreview.SelectInstance(null);
            _elementPreviewElement = null;
            _elementPreviewCard.style.display = DisplayStyle.None;
            _elementList.Focus();
        }

        private void RefreshElementPreviewUi()
        {
            VfxLibraryPlaybackState state = _elementPreview.State;
            _elementPreviewPlay.EnableInClassList("playback-active", state == VfxLibraryPlaybackState.Playing);
            _elementPreviewPause.EnableInClassList("playback-active", state == VfxLibraryPlaybackState.Paused);
            _elementPreviewStatus.text = state == VfxLibraryPlaybackState.Playing
                ? "播放中"
                : state == VfxLibraryPlaybackState.Paused
                    ? "已暂停 · 第 " + Mathf.RoundToInt(_elementPreview.ElapsedSeconds * 60f) + " 帧"
                    : "已停止";
            _elementPreviewStatus.RemoveFromClassList("playing");
            _elementPreviewStatus.RemoveFromClassList("paused");
            _elementPreviewStatus.RemoveFromClassList("stopped");
            _elementPreviewStatus.AddToClassList(state == VfxLibraryPlaybackState.Playing ? "playing" : state == VfxLibraryPlaybackState.Paused ? "paused" : "stopped");
        }

        private void StepElementPreviewFrame()
        {
            _elementPreview.StepFrame();
            _elementPreviewCanvas.MarkDirtyRepaint();
            RefreshElementPreviewUi();
        }

        private void RestorePreviewLayout()
        {
            _expandedLayerRegionHeight = Mathf.Clamp(
                EditorPrefs.GetFloat(LayerRegionHeightPreference, DefaultLayerRegionHeight),
                112f,
                520f);
            _layerRegionCollapsed = EditorPrefs.GetBool(LayerRegionCollapsedPreference, false);
            _previewMaximized = EditorPrefs.GetBool(PreviewMaximizedPreference, false);
            ApplyLayerRegionLayout();
            ApplyMaximizedPreviewLayout();
        }

        private void ResetPreviewLayout()
        {
            _expandedLayerRegionHeight = DefaultLayerRegionHeight;
            _layerRegionCollapsed = false;
            _previewMaximized = false;
            SetCharacterPreviewDrawerExpanded(false);
            ApplyLayerRegionLayout();
            ApplyMaximizedPreviewLayout();
            SavePreviewLayout();
            SetStatus("已恢复预览优先的默认布局。", false);
        }

        private void ToggleMaximizedPreview()
        {
            _previewMaximized = !_previewMaximized;
            ApplyMaximizedPreviewLayout();
            SavePreviewLayout();
            _previewCanvas.MarkDirtyRepaint();
        }

        private void ApplyMaximizedPreviewLayout()
        {
            _manualWorkspace.EnableInClassList("preview-maximized", _previewMaximized);
            _maximizePreviewButton.text = _previewMaximized ? "退出最大化" : "最大化预览";
            _maximizePreviewButton.EnableInClassList("active", _previewMaximized);
            _layerSplitter.style.display = _previewMaximized || _layerRegionCollapsed
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private void ToggleLayerRegionCollapsed()
        {
            _layerRegionCollapsed = !_layerRegionCollapsed;
            ApplyLayerRegionLayout();
            SavePreviewLayout();
            _previewCanvas.MarkDirtyRepaint();
        }

        private void ApplyLayerRegionLayout()
        {
            _layerRegion.EnableInClassList("layer-region-collapsed", _layerRegionCollapsed);
            _layerRegion.style.height = _layerRegionCollapsed ? 39f : _expandedLayerRegionHeight;
            _collapseLayersButton.text = _layerRegionCollapsed ? "展开" : "收起";
            _layerSplitter.style.display = _previewMaximized || _layerRegionCollapsed
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private void SavePreviewLayout()
        {
            EditorPrefs.SetFloat(LayerRegionHeightPreference, _expandedLayerRegionHeight);
            EditorPrefs.SetBool(LayerRegionCollapsedPreference, _layerRegionCollapsed);
            EditorPrefs.SetBool(PreviewMaximizedPreference, _previewMaximized);
        }

        private void RegisterLayerSplitter()
        {
            _layerSplitter.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || _layerRegionCollapsed || _previewMaximized) return;
                _draggingLayerSplitter = true;
                _layerSplitterPointerId = evt.pointerId;
                _layerSplitterStartY = evt.position.y;
                _layerSplitterStartHeight = _layerRegion.resolvedStyle.height;
                if (float.IsNaN(_layerSplitterStartHeight) || _layerSplitterStartHeight < 1f)
                    _layerSplitterStartHeight = _expandedLayerRegionHeight;
                _layerSplitter.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            _layerSplitter.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingLayerSplitter || evt.pointerId != _layerSplitterPointerId) return;
                float availableHeight = Mathf.Max(112f, _root.resolvedStyle.height - 300f);
                _expandedLayerRegionHeight = Mathf.Clamp(
                    _layerSplitterStartHeight + (_layerSplitterStartY - evt.position.y),
                    112f,
                    Mathf.Min(520f, availableHeight));
                _layerRegion.style.height = _expandedLayerRegionHeight;
                _previewCanvas.MarkDirtyRepaint();
                evt.StopPropagation();
            });
            _layerSplitter.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_draggingLayerSplitter || evt.pointerId != _layerSplitterPointerId) return;
                _draggingLayerSplitter = false;
                if (_layerSplitter.HasPointerCapture(evt.pointerId))
                    _layerSplitter.ReleasePointer(evt.pointerId);
                SavePreviewLayout();
                evt.StopPropagation();
            });
            _layerSplitter.RegisterCallback<PointerCaptureOutEvent>(_ => _draggingLayerSplitter = false);
        }

        private void OnElementPreviewKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                if (_preview.HasTemporaryOverlay)
                {
                    ClearTemporaryElementPreview();
                    evt.StopPropagation();
                    return;
                }
                CloseElementPreview();
                evt.StopPropagation();
                return;
            }
            if (evt.keyCode == KeyCode.S)
            {
                if (_preview.HasTemporaryOverlay)
                {
                    ClearTemporaryElementPreview();
                    SetStatus("已移除中央临时试放；Layer Stack 未改变。", false);
                }
                else if (_selectedElement != null) PreviewElementInComposition(_selectedElement);
                else return;
                evt.StopPropagation();
                return;
            }
            if (evt.keyCode == KeyCode.A && _preview.HasTemporaryOverlay)
            {
                ToggleTemporaryElementCompositionMode();
                evt.StopPropagation();
                return;
            }
            if (_preview.HasTemporaryOverlay)
            {
                if (evt.keyCode == KeyCode.Space)
                {
                    if (_preview.State == VfxLibraryPlaybackState.Playing) _preview.Pause();
                    else { BeginContinuousRefresh(); _preview.Play(); }
                    RefreshPlaybackUi();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.R)
                {
                    BeginContinuousRefresh();
                    _preview.RestartTemporaryOverlay();
                    RefreshPlaybackUi();
                    evt.StopPropagation();
                }
                return;
            }
            if (!_elementPreview.HasInstance) return;
            if (evt.keyCode == KeyCode.Space)
            {
                if (_elementPreview.State == VfxLibraryPlaybackState.Playing) _elementPreview.Pause();
                else { BeginContinuousRefresh(); _elementPreview.Play(); }
                RefreshElementPreviewUi();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.R)
            {
                BeginContinuousRefresh();
                _elementPreview.Restart();
                RefreshElementPreviewUi();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.RightArrow)
            {
                StepElementPreviewFrame();
                evt.StopPropagation();
            }
        }

        private void ConfigureElementPreviewDragging()
        {
            VisualElement handle = _root.Q("element-quick-preview-drag");
            bool dragging = false;
            int pointerId = -1;
            Vector2 pointerStart = Vector2.zero;
            Vector2 cardStart = Vector2.zero;
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                VisualElement eventTarget = evt.target as VisualElement;
                if (evt.button != 0 || eventTarget is Button || eventTarget?.GetFirstAncestorOfType<Button>() != null) return;
                dragging = true;
                pointerId = evt.pointerId;
                pointerStart = evt.position;
                cardStart = new Vector2(_elementPreviewCard.resolvedStyle.left, _elementPreviewCard.resolvedStyle.top);
                handle.CapturePointer(pointerId);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging || evt.pointerId != pointerId) return;
                Vector2 desired = cardStart + (Vector2)evt.position - pointerStart;
                Rect bounds = _elementPreviewCard.parent.contentRect;
                float maxLeft = Mathf.Max(0f, bounds.width - _elementPreviewCard.resolvedStyle.width);
                float maxTop = Mathf.Max(0f, bounds.height - _elementPreviewCard.resolvedStyle.height);
                _elementPreviewCard.style.left = Mathf.Clamp(desired.x, 0f, maxLeft);
                _elementPreviewCard.style.top = Mathf.Clamp(desired.y, 0f, maxTop);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!dragging || evt.pointerId != pointerId) return;
                if (handle.HasPointerCapture(pointerId)) handle.ReleasePointer(pointerId);
                dragging = false;
                EditorPrefs.SetFloat(ElementPreviewLeftPreference, _elementPreviewCard.resolvedStyle.left);
                EditorPrefs.SetFloat(ElementPreviewTopPreference, _elementPreviewCard.resolvedStyle.top);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerCaptureOutEvent>(_ => dragging = false);
        }
    }
}
