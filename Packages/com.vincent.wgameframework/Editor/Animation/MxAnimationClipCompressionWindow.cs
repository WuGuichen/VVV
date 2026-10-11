using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.Animation
{
    public sealed class MxAnimationClipCompressionWindow : EditorWindow
    {
        private const string MenuPath = "MxFramework/Assets/批量压缩动画片段";
        private const string AssetMenuPath = "Assets/MxFramework/动画/批量压缩动画片段";
        private const string QuickMenuPath = "Assets/MxFramework/动画/快速压缩动画（默认）";
        private const string StylePath = "Packages/com.vincent.wgameframework/Editor/Animation/MxAnimationClipCompressionWindow.uss";
        private const string SelectionSourceId = "selection";
        private static readonly Dictionary<string, CompressionSource> Sources = new Dictionary<string, CompressionSource>(StringComparer.Ordinal);

        [SerializeField] private float _tolerance = MxAnimationClipCompressor.DefaultTolerance;
        [SerializeField] private bool _preserveRootMotion = true;
        [SerializeField] private string _sourceId = SelectionSourceId;
        private readonly List<string> _paths = new List<string>();
        private readonly StringBuilder _report = new StringBuilder();
        private Label _selectionLabel;
        private Label _status;
        private VisualElement _controls;
        private TextField _reportField;
        private Button _cancelButton;
        private bool _running;
        private bool _apply;
        private bool _cancelRequested;
        private int _next;
        private int _changed;
        private int _failed;
        private long _beforeBytes;
        private long _afterBytes;
        private long _originalKeys;
        private long _reducedKeys;

        /// <summary>LowFreqAlloc: registers an Editor-owned source resolver, invoked afresh before each batch.</summary>
        public static void RegisterSource(string id, string displayName, Func<IReadOnlyList<string>> resolvePaths)
        {
            if (string.IsNullOrWhiteSpace(id) || id == SelectionSourceId)
                throw new ArgumentException("A distinct source id is required.", nameof(id));
            if (resolvePaths == null)
                throw new ArgumentNullException(nameof(resolvePaths));
            Sources[id] = new CompressionSource(displayName, resolvePaths);
        }

        /// <summary>LowFreqAlloc: opens a registered source; optionally starts compression with recommended settings.</summary>
        public static void OpenForSource(string id, bool compressImmediately = false)
        {
            if (!Sources.ContainsKey(id))
                throw new ArgumentException("Unknown animation compression source.", nameof(id));
            var window = GetWindow<MxAnimationClipCompressionWindow>("动画批量压缩");
            window.Show();
            if (window._running)
                return;
            window._sourceId = id;
            if (compressImmediately)
            {
                window._tolerance = MxAnimationClipCompressor.DefaultTolerance;
                window._preserveRootMotion = true;
            }
            window.CreateGUI();
            if (compressImmediately)
                window.StartBatch(true);
        }

        [MenuItem(MenuPath, priority = 131)]
        [MenuItem(AssetMenuPath, priority = 131)]
        private static void Open()
        {
            GetWindow<MxAnimationClipCompressionWindow>("动画批量压缩").Show();
        }

        [MenuItem(QuickMenuPath, priority = 132)]
        private static void QuickCompress()
        {
            var window = GetWindow<MxAnimationClipCompressionWindow>("动画批量压缩");
            window.Show();
            if (window._running)
                return;
            window._tolerance = MxAnimationClipCompressor.DefaultTolerance;
            window._preserveRootMotion = true;
            window._sourceId = SelectionSourceId;
            window.CreateGUI();
            window.StartBatch(true);
        }

        [MenuItem(MenuPath, true)]
        [MenuItem(AssetMenuPath, true)]
        [MenuItem(QuickMenuPath, true)]
        private static bool CanOpen()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

        [InitializeOnLoadMethod]
        private static void RegisterTool()
        {
            FrameworkManagerToolRegistry.Register(new FrameworkManagerToolInfo(
                "animation.clip_compression", "动画批量压缩", "资源工具",
                "对选中的独立动画或目录精简关键帧，可先分析并保留根运动。",
                "可用", MenuPath, 131, Open));
        }

        public void CreateGUI()
        {
            minSize = new Vector2(620, 440);
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("animation-compression");
            StyleSheet stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (stylesheet != null)
                rootVisualElement.styleSheets.Add(stylesheet);

            rootVisualElement.Add(new HelpBox(
                "可从已注册配置直接读取动画，或使用 Project 选中资源。压缩直接保存原片段，原资源引用继续有效；可用 Ctrl+Z 撤销后保存，或从 Git 恢复。",
                HelpBoxMessageType.Info));
            _selectionLabel = new Label();
            rootVisualElement.Add(_selectionLabel);
            _controls = new VisualElement();
            rootVisualElement.Add(_controls);
            var sourceIds = new List<string> { SelectionSourceId };
            var sourceNames = new List<string> { "Project 选中资源" };
            foreach (KeyValuePair<string, CompressionSource> source in Sources)
            {
                sourceIds.Add(source.Key);
                sourceNames.Add(source.Value.DisplayName);
            }
            int sourceIndex = sourceIds.IndexOf(_sourceId);
            if (sourceIndex < 0)
                sourceIndex = 0;
            var sourceField = new PopupField<string>("动画来源", sourceNames, sourceIndex);
            sourceField.RegisterValueChangedCallback(change =>
            {
                _sourceId = sourceIds[sourceField.index];
                UpdateSelectionLabel();
            });
            _controls.Add(sourceField);
            var tolerance = new FloatField("最大曲线误差") { value = _tolerance };
            tolerance.tooltip = "每条浮点曲线的绝对值误差。推荐 0.001；越小越保真，越大压缩越多。肌肉、位置和旋转分量各按自身单位计算，不是角度或整体姿态误差。";
            tolerance.RegisterValueChangedCallback(change => _tolerance = change.newValue);
            _controls.Add(tolerance);
            var preserveRoot = new Toggle("保留根运动曲线") { value = _preserveRootMotion };
            preserveRoot.RegisterValueChangedCallback(change => _preserveRootMotion = change.newValue);
            _controls.Add(preserveRoot);
            _controls.Add(new HelpBox("事件、对象引用、阶梯曲线与加权曲线保持原样。压缩属于有损精简；压缩后请预览动作，使用这些片段的姿态／武器轨迹烘焙需按原流程更新。", HelpBoxMessageType.Info));

            var buttons = new VisualElement();
            buttons.AddToClassList("animation-compression-buttons");
            buttons.Add(new Button(() => StartBatch(false)) { text = "分析动画" });
            buttons.Add(new Button(() => StartBatch(true)) { text = "压缩并保存" });
            buttons.Add(new Button(() =>
            {
                _tolerance = MxAnimationClipCompressor.DefaultTolerance;
                _preserveRootMotion = true;
                CreateGUI();
            }) { text = "恢复推荐参数" });
            _controls.Add(buttons);
            _cancelButton = new Button(() => _cancelRequested = true) { text = "取消剩余片段" };
            rootVisualElement.Add(_cancelButton);
            _status = new Label();
            _status.AddToClassList("animation-compression-status");
            rootVisualElement.Add(_status);
            _reportField = new TextField { multiline = true, isReadOnly = true };
            _reportField.AddToClassList("animation-compression-report");
            rootVisualElement.Add(_reportField);
            rootVisualElement.Add(new Button(() => EditorGUIUtility.systemCopyBuffer = _report.ToString()) { text = "复制报告" });
            _reportField.SetValueWithoutNotify(_report.ToString());
            SetRunningUI();
            UpdateSelectionLabel();
        }

        private void OnSelectionChange()
        {
            if (!_running)
                UpdateSelectionLabel();
        }

        private void UpdateSelectionLabel()
        {
            if (_selectionLabel != null)
                _selectionLabel.text = _running ? $"本批次 {_paths.Count} 个片段" : _sourceId == SelectionSourceId
                    ? $"当前选中 {Selection.objects.Length} 项（文件夹会递归查找独立 .anim）"
                    : "每次分析／压缩时重新读取配置，按实际引用去重，无需选择资源。";
        }

        private void StartBatch(bool apply)
        {
            if (_running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            if (float.IsNaN(_tolerance) || float.IsInfinity(_tolerance) || _tolerance <= 0)
            {
                _status.text = "请输入大于 0 的有限误差值。";
                return;
            }

            _paths.Clear();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                CollectPaths(paths);
            }
            catch (Exception exception)
            {
                _status.text = "读取动画来源失败：" + exception.Message;
                _report.Clear();
                _report.AppendLine(_status.text);
                _reportField.SetValueWithoutNotify(_report.ToString());
                return;
            }
            _paths.AddRange(paths);
            _paths.Sort(StringComparer.Ordinal);
            if (_paths.Count == 0)
            {
                _status.text = _sourceId == SelectionSourceId
                    ? "请选择 Assets 下的独立 .anim 或其文件夹。FBX 子片段请先提取。"
                    : "配置未引用可压缩的独立 .anim。";
                return;
            }

            _apply = apply;
            _running = true;
            _cancelRequested = false;
            _next = _changed = _failed = 0;
            _beforeBytes = _afterBytes = _originalKeys = _reducedKeys = 0;
            _report.Clear();
            string sourceName = _sourceId == SelectionSourceId ? "Project 选中资源" : Sources[_sourceId].DisplayName;
            _report.AppendLine($"来源：{sourceName} | {_paths.Count} 个片段");
            _report.AppendLine($"{(apply ? "压缩并保存" : "仅分析，不写入资源")} | 误差 {_tolerance:G6} | 保留根运动 {_preserveRootMotion}");
            EditorApplication.update += ProcessNext;
            AssemblyReloadEvents.beforeAssemblyReload += StopBatch;
            SetRunningUI();
            UpdateSelectionLabel();
        }

        private void CollectPaths(HashSet<string> paths)
        {
            if (_sourceId != SelectionSourceId)
            {
                if (!Sources.TryGetValue(_sourceId, out CompressionSource source))
                    throw new InvalidOperationException("配置来源未注册，请重新打开对应菜单。");
                IReadOnlyList<string> resolved = source.ResolvePaths();
                if (resolved == null)
                    throw new InvalidOperationException("配置来源未返回动画列表。");
                foreach (string path in resolved)
                {
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal)
                        || !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("配置引用不是 Assets 下的独立 .anim：" + path);
                    paths.Add(path);
                }
                return;
            }

            foreach (UnityEngine.Object selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (AssetDatabase.IsValidFolder(path) && path.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { path }))
                        AddNativeClip(paths, AssetDatabase.GUIDToAssetPath(guid));
                }
                else if (!AssetDatabase.IsSubAsset(selected))
                    AddNativeClip(paths, path);
            }
        }

        private readonly struct CompressionSource
        {
            internal readonly string DisplayName;
            internal readonly Func<IReadOnlyList<string>> ResolvePaths;

            internal CompressionSource(string displayName, Func<IReadOnlyList<string>> resolvePaths)
            {
                DisplayName = displayName;
                ResolvePaths = resolvePaths;
            }
        }

        private static void AddNativeClip(HashSet<string> paths, string path)
        {
            if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal)
                && path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                paths.Add(path);
        }

        private void ProcessNext()
        {
            if (_cancelRequested || _next >= _paths.Count || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                FinishBatch(_next < _paths.Count);
                return;
            }

            string path = _paths[_next++];
            _status.text = $"{(_apply ? "压缩" : "分析")} {_next}/{_paths.Count}：{Path.GetFileName(path)}";
            try
            {
                ProcessClip(path);
            }
            catch (Exception exception)
            {
                _failed++;
                _report.AppendLine($"失败 | {path} | {exception.Message}");
            }
            _reportField.SetValueWithoutNotify(_report.ToString());
            Repaint();
        }

        private void ProcessClip(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null || !AssetDatabase.IsMainAsset(clip))
                throw new InvalidOperationException("不是独立 AnimationClip 资产。");
            if (_apply && (EditorUtility.IsDirty(clip) || !AssetDatabase.IsOpenForEdit(clip)
                || (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0))
            {
                _report.AppendLine($"跳过 | {path} | 动画有未保存修改或文件只读，请保存／解锁后重试。");
                return;
            }

            long before = new FileInfo(path).Length;
            MxAnimationClipCompressionPlan plan = MxAnimationClipCompressor.Analyze(clip, _tolerance, _preserveRootMotion);
            if (_apply && plan.Bindings.Count > 0)
            {
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("压缩动画片段");
                Undo.RegisterCompleteObjectUndo(clip, "压缩动画片段");
                try
                {
                    AnimationUtility.SetEditorCurves(clip, plan.Bindings.ToArray(), plan.Curves.ToArray());
                    EditorUtility.SetDirty(clip);
                    AssetDatabase.SaveAssetIfDirty(clip);
                    if (EditorUtility.IsDirty(clip))
                        throw new IOException("动画未能保存。");
                    Undo.CollapseUndoOperations(undoGroup);
                }
                catch
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    EditorUtility.SetDirty(clip);
                    AssetDatabase.SaveAssetIfDirty(clip);
                    throw;
                }
            }

            long after = _apply ? new FileInfo(path).Length : before;
            int reducedKeys = plan.ReducedKeys;
            if (_apply && plan.Bindings.Count > 0)
            {
                // Unity can synchronize Transform channels during serialization; report the saved result.
                reducedKeys = 0;
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve != null)
                        reducedKeys += curve.length;
                }
            }
            _beforeBytes += before;
            _afterBytes += after;
            _originalKeys += plan.OriginalKeys;
            _reducedKeys += reducedKeys;
            if (plan.Bindings.Count > 0)
                _changed++;
            string size = _apply ? $"{Size(before)} → {Size(after)}，减少 {Reduction(before, after):F1}%" : $"原文件 {Size(before)}";
            _report.AppendLine($"{(plan.Bindings.Count > 0 ? (_apply ? "已压缩" : "可压缩") : "无需精简")} | {path}");
            _report.AppendLine($"  {size} | {(_apply ? "浮点关键帧" : "预计浮点关键帧")} {plan.OriginalKeys:N0} → {reducedKeys:N0} | 保留根运动曲线 {plan.ProtectedCurves} | 跳过特殊／未减少曲线 {plan.UnsupportedCurves}");
        }

        private void FinishBatch(bool cancelled)
        {
            string summary = $"{(cancelled ? "已取消剩余片段" : "完成")}：处理 {_next}/{_paths.Count}，{(_apply ? "压缩" : "可压缩")} {_changed}，失败 {_failed}。{(_apply ? "浮点关键帧" : "预计浮点关键帧")} {_originalKeys:N0} → {_reducedKeys:N0}（减少 {Reduction(_originalKeys, _reducedKeys):F1}%）。";
            if (_apply)
                summary += $" 文件 {Size(_beforeBytes)} → {Size(_afterBytes)}（减少 {Reduction(_beforeBytes, _afterBytes):F1}%）。";
            _report.AppendLine(summary);
            _status.text = summary;
            _reportField.SetValueWithoutNotify(_report.ToString());
            StopBatch();
            Debug.Log("[动画批量压缩] " + _report);
        }

        private void SetRunningUI()
        {
            _controls?.SetEnabled(!_running);
            _cancelButton?.SetEnabled(_running);
        }

        private void StopBatch()
        {
            EditorApplication.update -= ProcessNext;
            AssemblyReloadEvents.beforeAssemblyReload -= StopBatch;
            _running = false;
            SetRunningUI();
        }

        private void OnDisable()
        {
            StopBatch();
        }

        private static string Size(long bytes)
        {
            return bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024):F2} MB" : $"{bytes / 1024d:F1} KB";
        }

        private static double Reduction(long before, long after)
        {
            return before > 0 ? 100d * (before - after) / before : 0;
        }
    }
}
