namespace MxFramework.Combat.Animation
{
    public readonly struct ActionResult
    {
        private ActionResult(bool success, string reason, int actionInstanceId)
        {
            Success = success;
            Reason = reason;
            ActionInstanceId = actionInstanceId;
        }

        public bool Success { get; }

        public string Reason { get; }

        public int ActionInstanceId { get; }

        public static ActionResult Succeeded(int actionInstanceId)
        {
            return new ActionResult(true, string.Empty, actionInstanceId);
        }

        public static ActionResult Failed(string reason)
        {
            return new ActionResult(false, reason ?? string.Empty, 0);
        }
    }
}
