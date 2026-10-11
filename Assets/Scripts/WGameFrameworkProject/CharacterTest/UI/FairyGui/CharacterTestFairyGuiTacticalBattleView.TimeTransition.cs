using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalBattleView
    {
        private CharacterTestTacticalHomeWater _battleWater;
        private float _worldStillness, _waterFlowTime, _readyPulse;
        private float _readyAt = -10f;
        private bool _decisionReady;
        private float _filterStillness, _filterStart, _visualTravel, _motionDrive;
        private int _motionDirection;
        private bool _hasTimeVisualState;

        internal static float MomentumProgress(float progress)
        {
            float p = Mathf.Clamp01(progress);
            return p * p * p;
        }

        private void InitializeTimeTransitionPresentation()
        {
            if (!Application.isPlaying) return;
            var configuration = _hud.GetComponent<GameManager>()?.SceneRendering;
            if (configuration?.Feature == null || configuration.TargetCamera == null) return;
            _battleWater = new CharacterTestTacticalHomeWater(configuration.Feature, configuration.TargetCamera, battle: true);
        }

        // Unity presentation boundary: UI real time is intentionally independent of the world clock.
        private void UpdateTimeTransitionPresentation()
        {
            bool active = !_hud.HasOutcome && _hud.Health == TacticalStageHealth.Running && !_hud.BattleInputBlocked;
            bool ready = active && _hud.Phase == TacticalStagePhase.Planning && _hud.IsWorldPaused;
            if (ready && !_decisionReady) _readyAt = Time.unscaledTime;
            _decisionReady = ready;
            _worldStillness = active ? 1f - Mathf.Clamp01(_hud.WorldRateFraction) : 0f;
            int direction = !active ? 0 : _hud.IsWorldAccelerating ? 1 : _hud.IsWorldDecelerating ? -1 : 0;
            if (!_hasTimeVisualState)
            {
                _filterStillness = _worldStillness;
                _filterStart = direction > 0 ? 1f : direction < 0 ? 0f : _worldStillness;
                _motionDirection = direction;
                _hasTimeVisualState = true;
            }
            if (direction != _motionDirection)
            {
                _filterStart = _filterStillness;
                _motionDirection = direction;
            }
            _visualTravel = MomentumProgress(_hud.WorldTransitionProgress);
            _motionDrive = direction == 0 ? 0f : _visualTravel;
            _filterStillness = direction == 0 ? _worldStillness
                : Mathf.Lerp(_filterStart, direction < 0 ? 1f : 0f, _visualTravel);
            float sinceReady = Time.unscaledTime - _readyAt;
            _readyPulse = ready ? Mathf.Clamp01(1f - sinceReady / .28f) : 0f;
            // Integrate a local visual clock: changing the world rate must never jump the water phase.
            _waterFlowTime = Mathf.Repeat(_waterFlowTime + Mathf.Min(Time.unscaledDeltaTime, .1f) * .25f, 120f);
            if (_battleWater != null)
            {
                _battleWater.Active = active && (_filterStillness > .001f || direction != 0);
            }
        }

        private string TimeTransitionLabel()
        {
            if (_hud.HasOutcome) return "战斗结束";
            if (_hud.Health != TacticalStageHealth.Running) return "战斗暂停";
            if (_hud.BattleInputBlocked) return "战场暂停";
            if (_hud.IsWorldDecelerating) return "正在减速";
            if (_hud.IsWorldAccelerating) return "正在加速";
            if (_decisionReady) return "选择行动";
            return _hud.Phase == TacticalStagePhase.TurnSettling ? "正在收势" : "执行中";
        }

        private void HideTimeTransitionPresentation()
        {
            if (_battleWater != null) _battleWater.Active = false;
            _decisionReady = false;
            _readyPulse = _worldStillness = 0f;
            _filterStillness = _motionDrive = 0f;
            _motionDirection = 0;
            _hasTimeVisualState = false;
        }
    }
}
