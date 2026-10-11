namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal readonly struct TriggerTestManualOperationViewModel
    {
        internal TriggerTestManualOperationViewModel(
            long frame,
            int order,
            string stage,
            string operation,
            string result,
            string technicalId,
            bool succeeded,
            string technicalDetails = "")
        {
            Frame = frame;
            Order = order;
            Stage = stage ?? string.Empty;
            Operation = operation ?? string.Empty;
            Result = result ?? string.Empty;
            TechnicalId = technicalId ?? string.Empty;
            TechnicalDetails = technicalDetails ?? string.Empty;
            Succeeded = succeeded;
        }

        internal long Frame { get; }
        internal int Order { get; }
        internal string Stage { get; }
        internal string Operation { get; }
        internal string Result { get; }
        internal string TechnicalId { get; }
        internal string TechnicalDetails { get; }
        internal bool Succeeded { get; }
    }
}
