using MxFramework.Combat.Animation;

namespace MxFramework.Runtime.Unity
{
    public interface ICombatAnimatorDriver
    {
        void OnActionStarted(ActionStartedEvent evt);

        void OnActionPhaseChanged(ActionPhaseChangedEvent evt);

        void OnActionFinished(ActionFinishedEvent evt);

        void OnActionCanceled(ActionCanceledEvent evt);

        void OnActionCancelRejected(ActionCancelRejectedEvent evt);
    }
}
