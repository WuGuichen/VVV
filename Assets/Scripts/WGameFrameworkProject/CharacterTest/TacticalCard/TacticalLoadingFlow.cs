using System;
using System.Threading.Tasks;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal enum TacticalLoadingOperation { Opening, Continue, NewGame, Profile, Return, Deploy, Settlement, Exit, Recover }
    internal enum TacticalLoadingStage { OpeningProgress, PreparingSession, SavingProgress, PublishingSession, InstallingBattle, SavingSettlement, Exiting }
    internal enum TacticalLoadingStatus { Hidden, Running, Failed }

    /// <summary>Application-owned facts. An animation is never reported as loading progress.</summary>
    internal sealed class TacticalLoadingFlow
    {
        internal long Attempt { get; private set; }
        internal int Revision { get; private set; }
        internal TacticalLoadingStatus Status { get; private set; }
        internal TacticalLoadingOperation Operation { get; private set; }
        internal TacticalLoadingStage Stage { get; private set; }
        internal string Issue { get; private set; } = string.Empty;
        internal bool CanRetry { get; private set; }
        internal int CompletedUnits { get; private set; }
        internal int TotalUnits { get; private set; }
        internal bool Visible => Status != TacticalLoadingStatus.Hidden;
        internal bool Running => Status == TacticalLoadingStatus.Running;

        internal bool TryBegin(TacticalLoadingOperation operation, TacticalLoadingStage stage, out long attempt)
        {
            attempt = Attempt;
            if (Running) return false;
            attempt = ++Attempt;
            Operation = operation;
            Stage = stage;
            Status = TacticalLoadingStatus.Running;
            Issue = string.Empty;
            CanRetry = false;
            CompletedUnits = TotalUnits = 0;
            Revision++;
            return true;
        }

        internal bool IsCurrent(long attempt) => Running && attempt == Attempt;

        internal Task<TacticalCheckpointTransition> ReplaceAsync<T>(long attempt, Func<T> prepare,
            Func<Task<bool>> commit, Action<T> publish, Func<bool> isCurrent) where T : class, IDisposable
        {
            return TacticalFlowCheckpointBarrier.ReplaceAsync(
                () => { Report(attempt, TacticalLoadingStage.PreparingSession); return prepare(); },
                () => { Report(attempt, TacticalLoadingStage.SavingProgress); return commit(); },
                candidate => { Report(attempt, TacticalLoadingStage.PublishingSession); publish(candidate); },
                () => IsCurrent(attempt) && isCurrent());
        }

        internal bool Report(long attempt, TacticalLoadingStage stage)
        {
            if (!IsCurrent(attempt)) return false;
            if (Stage != stage) { Stage = stage; CompletedUnits = TotalUnits = 0; Revision++; }
            return true;
        }

        internal bool ReportUnits(long attempt, int completed, int total)
        {
            if (!IsCurrent(attempt) || total <= 0 || completed < 0 || completed > total) return false;
            if (CompletedUnits != completed || TotalUnits != total)
            { CompletedUnits = completed; TotalUnits = total; Revision++; }
            return true;
        }

        internal bool Complete(long attempt)
        {
            if (!IsCurrent(attempt)) return false;
            Status = TacticalLoadingStatus.Hidden;
            Revision++;
            return true;
        }

        internal bool Fail(long attempt, string issue, bool canRetry)
        {
            if (!IsCurrent(attempt)) return false;
            Status = TacticalLoadingStatus.Failed;
            Issue = string.IsNullOrWhiteSpace(issue) ? "加载未完成，请返回后重试。" : issue;
            CanRetry = canRetry;
            Revision++;
            return true;
        }

        internal bool DismissFailure()
        {
            if (Status != TacticalLoadingStatus.Failed) return false;
            Status = TacticalLoadingStatus.Hidden;
            Revision++;
            return true;
        }

        internal void Interrupt()
        {
            Attempt++;
            Status = TacticalLoadingStatus.Hidden;
            Issue = string.Empty;
            CanRetry = false;
            CompletedUnits = TotalUnits = 0;
            Revision++;
        }
    }
}
