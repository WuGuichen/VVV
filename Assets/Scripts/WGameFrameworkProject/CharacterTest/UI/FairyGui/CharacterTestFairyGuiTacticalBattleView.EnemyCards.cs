using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine;
using UnityEngine.InputSystem;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalBattleView
    {
        private sealed class EnemyCard
        {
            internal CharacterTestCharacterEntity Entity;
            internal TacticalDeploymentParticipant Participant;
            internal readonly CharacterTestEnemyCardPresentation Facts = new();
            internal readonly Dictionary<string, TacticalActionCardDefinition> Definitions = new(StringComparer.Ordinal);
            internal TacticalActionCardDefinition Definition;
            internal CharacterTestRuntimeAiLabCompiledLogicProgram Program;
            internal CharacterTestCardUiPreviewData Data;
            internal Fgui.GComponent Marker;
            internal Rect Body, MarkerBounds;
            internal float Depth;
            internal bool Seen, OnScreen;
        }
        private readonly Dictionary<int, EnemyCard> _enemyCards = new();
        private readonly List<EnemyCard> _visibleEnemies = new();
        private readonly List<int> _removedEnemies = new();
        private readonly List<Rect> _enemyMarkerBounds = new();
        private readonly List<Rect> _enemyPopupObstacles = new();
        private TacticalDeployment _enemyDeployment;
        private Fgui.GComponent _enemyPopup, _enemyFace;
        private Fgui.GTextField _enemyStatus;
        private Fgui.GButton _enemyPin, _enemyClose;
        private CharacterTestFairyGuiTacticalDescriptionHints _enemyHints;
        private EnemyCard _viewedEnemy;
        private CharacterTestCardUiPreviewData _shownEnemyData;
        private bool _enemyPinned;
        private float _enemyLeaveTime;

        private void InitializeEnemyCards()
        {
            Get(_root, "enemyMarker").visible = false; // Published prototype; instances are pooled below.
            _enemyPopup = Get(_root, "enemyCard").asCom;
            _enemyFace = Get(_enemyPopup, "face").asCom;
            _enemyStatus = Get(_enemyPopup, "status").asTextField;
            _enemyPin = Get(_enemyPopup, "pin").asButton;
            _enemyClose = Get(_enemyPopup, "close").asButton;
            _enemyPopup.sortingOrder = 80; _enemyPopup.opaque = false; _enemyPopup.visible = false;
            CharacterTestFairyGuiPlayerCardPresentation.Initialize(_enemyFace);
            _enemyHints = new CharacterTestFairyGuiTacticalDescriptionHints(_enemyFace, null, null, _root) { KeepEnemyHover = true };
            _enemyPopup.onClick.Set(ctx => ctx.StopPropagation());
            _enemyPin.onClick.Set(ctx => { ctx.StopPropagation(); _enemyPinned = !_enemyPinned; PresentEnemyCard(); });
            _enemyClose.onClick.Set(ctx => { ctx.StopPropagation(); CloseEnemyCard(); });
        }

        private bool ContainsEnemyCardPoint(Vector2 global)
        {
            if (Contains(_enemyPopup, global)) return true;
            foreach (var enemy in _visibleEnemies) if (Contains(enemy.Marker, global)) return true;
            return false;
        }

        private void UpdateEnemyCards()
        {
            var deployment = _hud.EnemyCardDeployment;
            if (!ReferenceEquals(deployment, _enemyDeployment)) { ResetEnemyCards(); _enemyDeployment = deployment; }
            var camera = UnityEngine.Camera.main;
            var actors = _hud.BattleActors;
            if (deployment == null || camera == null || actors == null || _hud.HasOutcome || _hud.BattleInputBlocked)
            { HideEnemyCards(); return; }
            int team = deployment.PlayerParticipant?.TeamId ?? 1;
            _visibleEnemies.Clear(); _enemyMarkerBounds.Clear();
            foreach (var enemy in _enemyCards.Values) { enemy.Seen = false; enemy.OnScreen = false; enemy.Marker.visible = false; }
            foreach (var entity in actors)
            {
                if (entity.Team.TeamId == team || !entity.CombatState.IsAlive) continue;
                int slot = entity.Control.RuntimeAiPlannerProfileSlot;
                if (slot <= 0 || slot > deployment.Participants.Count) continue;
                var participant = deployment.Participants[slot - 1];
                if (participant.IsPlayerControlled || participant.CharacterId != entity.Identity.CharacterId || participant.TeamId != entity.Team.TeamId) continue;
                if (_enemyCards.TryGetValue(entity.Id.Value, out var enemy) && !ReferenceEquals(enemy.Entity, entity))
                { RemoveEnemy(enemy); _enemyCards.Remove(entity.Id.Value); enemy = null; }
                if (enemy == null)
                {
                    enemy = new EnemyCard { Entity = entity, Participant = participant };
                    foreach (var item in participant.Cards)
                        if (item is TacticalActionCardDefinition action)
                            enemy.Definitions.Add(action.PublishedCardId + "#" + action.DeckEntryInstanceId.ToString(CultureInfo.InvariantCulture), action);
                    enemy.Marker = Fgui.UIPackage.CreateObject("MxCharacterTestTacticalDeckUi", "TacticalEnemyCardMarker").asCom;
                    enemy.Marker.sortingOrder = 30; enemy.Marker.opaque = true; _root.AddChild(enemy.Marker);
                    var captured = enemy;
                    enemy.Marker.onClick.Set(ctx => { ctx.StopPropagation(); ShowEnemyCard(captured, true); });
                    _enemyCards.Add(entity.Id.Value, enemy);
                }
                enemy.Seen = true;
                if (!_hud.ReadEnemyCard(entity, enemy.Facts)
                    || !enemy.Definitions.TryGetValue(enemy.Facts.EntryId, out var definition)) continue;
                if (!ReferenceEquals(enemy.Definition, definition) || !ReferenceEquals(enemy.Program, enemy.Facts.Program))
                {
                    enemy.Definition = definition; enemy.Program = enemy.Facts.Program;
                    enemy.Data = _hud.GetEnemyCardData(definition, participant);
                    enemy.Facts.BindSegments(enemy.Data.Segments);
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.Bind(enemy.Marker, enemy.Data.IconKey,
                        8, 12, 36, new Color32(223, 167, 93, 255), true);
                }
                var head = camera.WorldToScreenPoint(_hud.BattleHeadPosition(entity));
                var foot = camera.WorldToScreenPoint(new Vector3(entity.Transform.X, entity.Transform.Y, entity.Transform.Z));
                if (head.z <= camera.nearClipPlane || foot.z <= camera.nearClipPlane || head.x < 0 || head.x > Screen.width
                    || head.y < 0 || foot.y > Screen.height) continue;
                var top = _root.GlobalToLocal(new Vector2(head.x, Screen.height - head.y));
                var bottom = _root.GlobalToLocal(new Vector2(foot.x, Screen.height - foot.y));
                float halfWidth = Mathf.Clamp(Mathf.Abs(bottom.y - top.y) * .25f, 20, 110);
                enemy.Body = Rect.MinMaxRect(Mathf.Min(top.x, bottom.x) - halfWidth, top.y, Mathf.Max(top.x, bottom.x) + halfWidth, bottom.y);
                enemy.Depth = head.z; enemy.OnScreen = true;
                _visibleEnemies.Add(enemy);
            }
            _removedEnemies.Clear();
            foreach (var pair in _enemyCards) if (!pair.Value.Seen) _removedEnemies.Add(pair.Key);
            foreach (int id in _removedEnemies) { RemoveEnemy(_enemyCards[id]); _enemyCards.Remove(id); }
            // Stable placement keeps overlapping actors readable and gives pointer picking a deterministic nearest-depth rule.
            _visibleEnemies.Sort((a, b) => a.Entity.Id.Value.CompareTo(b.Entity.Id.Value));
            foreach (var enemy in _visibleEnemies)
            {
                var bounds = PlaceEnemyMarker(enemy.Body, _enemyMarkerBounds);
                if (bounds.width <= 0) continue;
                enemy.MarkerBounds = bounds; _enemyMarkerBounds.Add(bounds);
                enemy.Marker.SetXY(bounds.x, bounds.y); enemy.Marker.visible = true;
                enemy.Marker.GetChild("intent").text = EnemyIntentText(enemy);
                enemy.Marker.GetChild("state").text = EnemyStateText(enemy);
            }
            if (_viewedEnemy != null && !_visibleEnemies.Contains(_viewedEnemy)) CloseEnemyCard();
            var mouse = Mouse.current;
            if (mouse != null && !_dragging && !_pointerHeld && !_hud.DirectPlayActive)
            {
                var screen = mouse.position.ReadValue();
                var global = new Vector2(screen.x, Screen.height - screen.y);
                bool inside = Contains(_enemyPopup, global) || _enemyHints.PointerWithin;
                var local = _root.GlobalToLocal(global);
                EnemyCard hovered = null;
                if (!inside)
                {
                    foreach (var enemy in _visibleEnemies)
                        if (enemy.Marker.visible && enemy.MarkerBounds.Contains(local)) { hovered = enemy; break; }
                    // Existing UI always wins over the projected body hit region.
                    if (hovered == null && !ContainsScreenPoint(screen))
                        foreach (var enemy in _visibleEnemies)
                            if (enemy.Body.Contains(local) && (hovered == null || enemy.Depth < hovered.Depth)) hovered = enemy;
                }
                if (hovered != null)
                {
                    if (mouse.leftButton.wasPressedThisFrame) ShowEnemyCard(hovered, true);
                    else if (!_enemyPinned) ShowEnemyCard(hovered, false);
                    _enemyLeaveTime = 0;
                }
                else if (!inside && !_enemyPinned && _viewedEnemy != null)
                {
                    // Presentation grace only; never used for gameplay or the displayed wait duration.
                    if (_enemyLeaveTime == 0) _enemyLeaveTime = Time.unscaledTime;
                    else if (Time.unscaledTime - _enemyLeaveTime > .2f) CloseEnemyCard();
                }
                else _enemyLeaveTime = 0;
                if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) CloseEnemyCard();
            }
            if (_dragging || _pointerHeld || _hud.DirectPlayActive) { CloseEnemyCard(); return; }
            PresentEnemyCard();
        }

        private void PresentEnemyCard()
        {
            if (_viewedEnemy == null) return;
            if (!ReferenceEquals(_shownEnemyData, _viewedEnemy.Data))
            {
                _enemyHints.HideForInteraction();
                BindCard(_enemyFace, _viewedEnemy.Data, QualityId(_viewedEnemy.Definition));
                _enemyHints.Bind(_viewedEnemy.Data, _hud.BattleTextProvider, 34);
                _shownEnemyData = _viewedEnemy.Data;
            }
            _enemyPopup.visible = true; _enemyPopup.SetScale(.62f, .62f);
            _enemyPopupObstacles.Clear(); _enemyPopupObstacles.AddRange(_enemyMarkerBounds);
            foreach (var enemy in _visibleEnemies) _enemyPopupObstacles.Add(enemy.Body);
            var rect = PlaceEnemyPopup(_viewedEnemy.Body, 394 * .62f, 608 * .62f, _enemyPopupObstacles);
            _enemyPopup.SetXY(rect.x, rect.y);
            _enemyStatus.text = EnemyStateText(_viewedEnemy);
            _enemyPin.title = _enemyPinned ? "取消固定" : "固定";
            _enemyHints.ApplyEnemyProgress(_viewedEnemy.Facts);
            _enemyHints.UpdatePointerHold(); _enemyHints.RefreshPlacement();
        }

        private string EnemyStateText(EnemyCard enemy)
        {
            if (enemy.Facts.Pending) return "已选定 · 待推进";
            if (enemy.Facts.State == CharacterTestRuntimeAiLabLogicRuntimeState.Failed) return "执行已中止";
            if (enemy.Facts.State == CharacterTestRuntimeAiLabLogicRuntimeState.Completed) return "已结束";
            return _hud.IsWorldPaused ? "战场暂停 · 进度已冻结" : "正在执行";
        }

        private string EnemyIntentText(EnemyCard enemy)
        {
            if (enemy.Facts.Pending) return enemy.Data.Name;
            foreach (var segment in enemy.Data.Segments ?? Array.Empty<CharacterTestTacticalDescriptionSegment>())
            {
                if (enemy.Facts.Intent(segment) != CharacterTestEnemyIntentState.Running) continue;
                int frames = enemy.Facts.RemainingDelayFrames(segment);
                return segment.Text + (frames < 0 ? string.Empty : " · " + (frames * enemy.Data.FrameSeconds).ToString("0.0", CultureInfo.InvariantCulture) + " 秒");
            }
            return enemy.Data.Name + " · 意图进度不可用";
        }

        internal static Rect PlaceEnemyMarker(Rect body, IReadOnlyList<Rect> occupied)
        {
            const float width = 256, height = 64;
            for (int row = 0; row < 9; row++)
            for (int side = 0; side < 3; side++)
            {
                float offset = side == 0 ? 0 : side == 1 ? width + 8 : -width - 8;
                var rect = new Rect(Mathf.Clamp(body.center.x - width / 2 + offset, 12, 1920 - width - 12),
                    Mathf.Clamp(body.yMin - height - 14 - row * (height + 6), 90, 680), width, height);
                bool overlaps = false;
                foreach (var other in occupied) if (rect.Overlaps(other)) { overlaps = true; break; }
                if (!overlaps) return rect;
            }
            return default;
        }

        internal static Rect PlaceEnemyPopup(Rect body, float width, float height, IReadOnlyList<Rect> occupied)
        {
            float preferred = body.center.x < 960 ? body.xMax + 28 : body.xMin - width - 28;
            float alternate = body.center.x < 960 ? body.xMin - width - 28 : body.xMax + 28;
            float bestScore = float.PositiveInfinity; Rect best = default;
            for (int side = 0; side < 2; side++)
            for (int row = 0; row < 3; row++)
            {
                float x = side == 0 ? preferred : alternate;
                float y = body.center.y - height / 2 + (row == 1 ? -80 : row == 2 ? 80 : 0);
                var rect = new Rect(Mathf.Clamp(x, 12, 1920 - width - 12), Mathf.Clamp(y, 90, 730 - height), width, height);
                float score = Math.Abs(rect.x - x) * 10 + side * 8 + row * 4;
                if (rect.Overlaps(body)) score += 10000;
                foreach (var other in occupied) if (rect.Overlaps(other)) score += 1000;
                if (score < bestScore) { bestScore = score; best = rect; }
            }
            return best;
        }

        private void ShowEnemyCard(EnemyCard enemy, bool pin)
        {
            if (IsDisposed || !_root.visible || _dragging || _pointerHeld || _hud.DirectPlayActive
                || _hud.HasOutcome || _hud.BattleInputBlocked || !enemy.OnScreen || enemy.Data == null) return;
            if (!ReferenceEquals(_viewedEnemy, enemy)) { _enemyHints.HideForInteraction(); _shownEnemyData = null; }
            _viewedEnemy = enemy; _enemyPinned = pin; _enemyLeaveTime = 0;
            // A FairyGUI click can arrive after the camera pass. Bind immediately from the last
            // projected facts; the regular camera pass continues to track position and progress.
            PresentEnemyCard();
        }
        private void CloseEnemyCard()
        {
            _enemyHints?.HideForInteraction();
            if (_enemyPopup != null && !_enemyPopup.isDisposed) _enemyPopup.visible = false;
            _viewedEnemy = null; _shownEnemyData = null; _enemyPinned = false; _enemyLeaveTime = 0;
        }
        private void HideEnemyCards()
        {
            CloseEnemyCard(); _visibleEnemies.Clear();
            foreach (var enemy in _enemyCards.Values) { enemy.Marker.visible = false; enemy.OnScreen = false; }
        }
        private void RemoveEnemy(EnemyCard enemy)
        {
            if (ReferenceEquals(_viewedEnemy, enemy)) CloseEnemyCard();
            if (!enemy.Marker.isDisposed) enemy.Marker.Dispose();
        }
        private void ResetEnemyCards()
        {
            CloseEnemyCard();
            foreach (var enemy in _enemyCards.Values) if (!enemy.Marker.isDisposed) enemy.Marker.Dispose();
            _enemyCards.Clear(); _visibleEnemies.Clear(); _enemyMarkerBounds.Clear(); _enemyPopupObstacles.Clear(); _enemyDeployment = null;
        }
        private void DisposeEnemyCards() { ResetEnemyCards(); _enemyHints?.Dispose(); }
    }
}
