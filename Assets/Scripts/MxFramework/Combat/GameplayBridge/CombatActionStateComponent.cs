using MxFramework.Combat.Animation;
using MxFramework.Gameplay;

namespace MxFramework.Combat.GameplayBridge
{
    /// <summary>Read-only snapshot of this frame's Combat action state. Transient; not part of SaveState or runtime hash.</summary>
    public readonly struct CombatActionStateComponent : IGameplayComponent
    {
        public CombatActionStateComponent(
            bool isActive,
            int actionId,
            CombatActionPhase phase,
            int localFrame,
            bool isFinished)
        {
            IsActive = isActive;
            ActionId = actionId;
            Phase = phase;
            LocalFrame = localFrame;
            IsFinished = isFinished;
        }

        public bool IsActive { get; }
        public int ActionId { get; }
        public CombatActionPhase Phase { get; }
        public int LocalFrame { get; }
        public bool IsFinished { get; }

        public static CombatActionStateComponent Inactive()
        {
            return new CombatActionStateComponent(
                isActive: false,
                actionId: 0,
                phase: CombatActionPhase.None,
                localFrame: 0,
                isFinished: false);
        }

        public static CombatActionStateComponent Active(CombatActionState state)
        {
            return new CombatActionStateComponent(
                isActive: true,
                actionId: state.ActionId,
                phase: state.Phase,
                localFrame: state.LocalFrame,
                isFinished: state.IsFinished);
        }
    }
}
