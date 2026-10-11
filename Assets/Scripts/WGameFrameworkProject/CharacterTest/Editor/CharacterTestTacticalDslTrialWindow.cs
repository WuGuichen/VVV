using System;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed class CharacterTestTacticalDslTrialWindow : EditorWindow
    {
        private CharacterTestTacticalDslTrialWorkspace _workspace;
        private CharacterTestTacticalCardInputs _inputs;
        private CharacterTestTacticalDslTrialWorkspace.SharedChange _pending;
        private string _cardId = "trial.assault", _referenceId = "attack", _eventId = "approach";
        private string _message = "试用工作集与正式目录分开。修改可撤销，尚未发布到游戏。";
        private int _character = CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId;
        private int _offset = 4;

        [MenuItem("MxFramework/CharacterTest/Tactical DSL/制作试用")]
        public static void Open()
        {
            var window = GetWindow<CharacterTestTacticalDslTrialWindow>();
            window.titleContent = new GUIContent("战术卡制作试用"); window.minSize = new Vector2(900, 720); window.Show();
        }

        public void CreateGUI()
        {
            if (_workspace == null) _workspace = new CharacterTestTacticalDslTrialWorkspace();
            Draw();
        }
        private Label Text(string text, int size = 13)
        {
            var label = new Label(text); label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = size; label.style.marginBottom = 8; return label;
        }
        private void Run(Action action)
        {
            try { action(); } catch (Exception error) { _message = error.Message; }
            Draw();
        }
        private Button Button(string name, string title, Action action)
        { var button = new Button(() => Run(action)) { name = name, text = title }; button.style.marginBottom = 6; return button; }
        private void Draw()
        {
            rootVisualElement.Clear();
            var scroll = new ScrollView(); scroll.style.paddingLeft = 14; scroll.style.paddingRight = 14; scroll.style.paddingTop = 12;
            rootVisualElement.Add(scroll);
            scroll.Add(Text("局部结束 · 共享封装 · 自动玩家说明", 20));
            scroll.Add(Text(_message));
            var cards = _workspace.Cards;
            if (!cards.Any(c => c.CardId == _cardId)) _cardId = cards[0].CardId;
            var cardPicker = new DropdownField("制作中的卡", cards.Select(c => c.CardId).ToList(), cards.ToList().FindIndex(c => c.CardId == _cardId)) { name = "card" };
            cardPicker.RegisterValueChangedCallback(e => { _cardId = e.newValue; _pending = null; Draw(); }); scroll.Add(cardPicker);
            var card = _workspace.Card(_cardId);
            scroll.Add(Text("编排：" + Structure(card.Root)));
            var references = CharacterTestTacticalDslTrialWorkspace.Nodes(card.Root).Where(n => n.Kind == "Reference").ToArray();
            if (!references.Any(n => n.NodeId == _referenceId)) _referenceId = references[0].NodeId;
            var referencePicker = new DropdownField("展开这一次封装使用", references.Select(n => n.NodeId).ToList(), Array.FindIndex(references, n => n.NodeId == _referenceId)) { name = "reference" };
            referencePicker.RegisterValueChangedCallback(e => { _referenceId = e.newValue; _pending = null; Draw(); }); scroll.Add(referencePicker);
            var reference = references.Single(n => n.NodeId == _referenceId);
            var definition = card.Definitions.Single(d => d.DefinitionId == reference.DefinitionId);
            scroll.Add(Text("正在编辑共享封装：" + definition.Source.Name + "（" + definition.DefinitionId + "）"));
            var events = definition.Source.Timelines[0].Events;
            if (!events.Any(e => e.EventId == _eventId)) _eventId = events[0].EventId;
            var eventPicker = new DropdownField("内部事件", events.Select(e => e.EventId).ToList(), events.FindIndex(e => e.EventId == _eventId)) { name = "event" };
            eventPicker.RegisterValueChangedCallback(e => { _eventId = e.newValue; _pending = null; Draw(); }); scroll.Add(eventPicker);
            var offset = new IntegerField("结束位置增加帧数") { value = _offset, name = "offset" };
            offset.RegisterValueChangedCallback(e => _offset = e.newValue); scroll.Add(offset);
            var edit = Button("prepare-shared", "查看共享修改影响", () => {
                var source = definition.Source.Clone();
                var evt = source.Timelines[0].Events.Single(e => e.EventId == _eventId);
                if (evt.End == null) throw new InvalidOperationException("动作时长与等待标记不能手工拉长。");
                if (evt.End.Kind == "Absolute") evt.End.Frame = checked(evt.End.Frame + _offset);
                else evt.End.OffsetFrames = checked(evt.End.OffsetFrames + _offset);
                _pending = _workspace.PrepareShared(definition.DefinitionId, CharacterTestTacticalAuthoringJson.Write(source));
                _message = "尚未修改。请核对下面的共享影响。";
            });
            edit.SetEnabled(events.Single(e => e.EventId == _eventId).End != null); scroll.Add(edit);
            if (_pending != null)
            {
                scroll.Add(Text("本次共享修改会影响：\n" + string.Join("\n", _pending.Impact)));
                scroll.Add(Button("confirm-shared", "确认修改共享封装", () => { _workspace.ApplyShared(_pending); _pending = null; _message = "共享源已修改；所有引用卡的说明与编译预览随源重新生成。"; }));
                scroll.Add(Button("cancel-shared", "取消本次修改", () => { _pending = null; _message = "已取消，源与撤销历史未改变。"; }));
            }
            scroll.Add(Button("duplicate-definition", "另存封装，并替换当前这一次引用", () => {
                _workspace.DuplicateDefinitionAndReplace(_workspace.Version, _cardId, _referenceId, "shared." + Guid.NewGuid().ToString("N"));
                _pending = null; _message = "已创建独立封装，只替换所选引用。其它引用仍指向原共享定义。";
            }));
            scroll.Add(Button("duplicate-card", "另存卡（保持共享引用）", () => { _workspace.DuplicateCard(_workspace.Version, _cardId, "card." + Guid.NewGuid().ToString("N")); _pending = null; }));
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row;
            row.Add(Button("undo", "撤销", () => { _workspace.Undo(); _pending = null; }));
            row.Add(Button("redo", "重做", () => { _workspace.Undo(true); _pending = null; })); scroll.Add(row);
            var localRow = new VisualElement(); localRow.style.flexDirection = FlexDirection.Row;
            localRow.Add(Button("enable-end", "进攻组：血量不高于30%时局部结束", () => _workspace.SetEndCondition(_workspace.Version, _cardId, "assault", CharacterTestTacticalDslTrialTemplates.LowHealth())));
            localRow.Add(Button("disable-end", "移除进攻组局部结束条件", () => _workspace.SetEndCondition(_workspace.Version, _cardId, "assault", null))); scroll.Add(localRow);
            var character = new IntegerField("实际角色编号") { value = _character, name = "character" };
            character.RegisterValueChangedCallback(e => { _character = e.newValue; _inputs = null; }); scroll.Add(character);
            scroll.Add(Button("prepare-binding", "解析该角色装配并显式准备摘要", PrepareBinding));
            var description = CharacterTestTacticalDslDescription.Generate(card, _inputs);
            scroll.Add(Text("自动生成的玩家说明", 17));
            var output = Text(description.Text); output.name = "generated-description"; scroll.Add(output);
            scroll.Add(Text(description.Executable ? "编译通过；说明警告不阻止合法卡执行。" : "尚不能执行：" + description.CompilationIssue));
            if (description.Warnings.Length > 0) scroll.Add(Text("说明警告：\n" + string.Join("\n", description.Warnings)));
            scroll.Add(Button("export-evidence", "导出当前试用源与实际说明到本地证据", () => {
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/DSL-LocalEnd"));
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "trial-workspace.json"), _workspace.Capture());
                File.WriteAllText(Path.Combine(directory, "local-end-source.json"), CharacterTestTacticalDslJson.Write(card));
                File.WriteAllText(Path.Combine(directory, "generated-description.txt"), description.Text + "\n\n说明警告：\n" + string.Join("\n", description.Warnings));
                _message = "已导出本地试用证据；这不是正式保存或发布。";
            }));
        }
        private void PrepareBinding()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先结束 Play Mode，再进行显式摘要准备。");
            var measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
            using (var probe = CharacterActionReferenceProbeSession.ForCurrentOpener(_character, measurement.ReferenceDummy))
            {
                var store = new CharacterActionTacticalMetricsStore(Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/DSL-LocalEnd/Metrics")));
                var measured = store.Refresh(probe, measurement);
                if (measured.Status != CharacterActionTacticalMetricsLoadStatus.Valid) throw new InvalidOperationException(measured.Issue);
                if (!CharacterTestTacticalCardInputs.TryPrepare(probe.Runtime.ActionRuntime, probe.Source, probe.Runtime.PhysicsRuntime,
                    probe.Runtime.CombatTraceSystem.BakedTraceSource, probe.Runtime.CombatTraceSystem.WeaponTraceBindingResolver,
                    store, measurement, out _inputs, out string issue)) throw new InvalidOperationException(issue);
                _message = "实际装配已解析：" + _inputs.Action.DisplayName + "。修改卡源不重建摘要。";
            }
        }
        private static string Structure(CharacterTestTacticalDslNode node)
        {
            string body = node.Kind == "Reference" ? node.DefinitionId : node.Kind == "Exit" ? "整卡退出"
                : node.Kind + "〔" + string.Join(" → ", node.Children.Select(Structure)) + "〕";
            return node.EndCondition == null ? body : body + "（带局部结束条件）";
        }
    }
}
