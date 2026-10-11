using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    /// <summary>Transfers due Trigger-owned reliable actions before their target modules tick.</summary>
    public sealed class GameplayTriggerReliableOutboxModule : RuntimeModule
    {
        public const string DefaultModuleId = "mxframework.gameplay.triggers.reliableOutbox";
        public const int DefaultPriority = 0;

        private readonly GameplayTriggerRuntime _runtime;
        private readonly FrozenTransfer[] _transfers;
        private bool _isEnabled = true;
        private bool _initialized;

        /// <remarks>LowFreqAlloc. Copies, validates, and ordinal-sorts transfer registrations.</remarks>
        public GameplayTriggerReliableOutboxModule(
            GameplayTriggerRuntime runtime,
            IGameplayTriggerReliableTargetTransfer[] transfers = null,
            string moduleId = DefaultModuleId,
            RuntimeTickStage tickStage = RuntimeTickStage.PreSimulation,
            int priority = DefaultPriority)
            : base(moduleId, tickStage, priority)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new ArgumentException("Reliable outbox module id cannot be empty.", nameof(moduleId));
            transfers = transfers ?? Array.Empty<IGameplayTriggerReliableTargetTransfer>();
            _transfers = new FrozenTransfer[transfers.Length];
            GameplayTriggerCatalog catalog = runtime.AuthorityCatalog;
            for (int i = 0; i < transfers.Length; i++)
            {
                IGameplayTriggerReliableTargetTransfer transfer = transfers[i];
                if (transfer == null)
                    throw new ArgumentException("Reliable target transfer cannot be null.", nameof(transfers));
                GameplayTriggerActionKindId actionKindId = transfer.ActionKindId;
                string targetModuleId = transfer.TargetModuleId;
                if (!actionKindId.IsValid)
                    throw new ArgumentException("Reliable target transfer action kind is invalid.", nameof(transfers));
                if (string.IsNullOrWhiteSpace(targetModuleId))
                    throw new ArgumentException("Reliable target transfer module id cannot be empty.", nameof(transfers));
                int sinkIndex = catalog.FindSink(actionKindId);
                if (sinkIndex < 0)
                    throw new ArgumentException("Reliable target transfer action kind is unknown to the catalog.", nameof(transfers));
                if (catalog.GetSinkEntry(sinkIndex).DeliveryContract !=
                    GameplayTriggerDeliveryContract.ReliableDeferred)
                    throw new ArgumentException("Reliable target transfer action kind is not ReliableDeferred.", nameof(transfers));

                _transfers[i] = new FrozenTransfer(actionKindId, targetModuleId, transfer);
            }

            Array.Sort(_transfers, CompareTransfers);
            for (int i = 1; i < _transfers.Length; i++)
            {
                if (_transfers[i - 1].ActionKindId == _transfers[i].ActionKindId)
                    throw new ArgumentException("Reliable target transfer action kind is duplicated.", nameof(transfers));
            }

            _runtime.BindReliableOutboxModule();
        }

        /// <remarks>NoAlloc.</remarks>
        public bool IsEnabled => _isEnabled;

        /// <remarks>NoAlloc. Internal exact-composition identity.</remarks>
        internal GameplayTriggerRuntime RuntimeIdentity => _runtime;

        /// <remarks>NoAlloc. Internal immutable composition-verification read seam.</remarks>
        internal int FrozenTransferCount => _transfers.Length;

        /// <remarks>NoAlloc. Internal immutable composition-verification read seam.</remarks>
        internal GameplayTriggerActionKindId GetFrozenTransferActionKindId(int index) =>
            _transfers[index].ActionKindId;

        /// <remarks>NoAlloc. Internal immutable composition-verification read seam.</remarks>
        internal string GetFrozenTransferTargetModuleId(int index) =>
            _transfers[index].TargetModuleId;

        /// <remarks>NoAlloc. Internal immutable composition-verification read seam.</remarks>
        internal IGameplayTriggerReliableTargetTransfer GetFrozenTransfer(int index) =>
            _transfers[index].Transfer;

        /// <remarks>NoAlloc.</remarks>
        public void SetEnabled(bool enabled) => _isEnabled = enabled;

        /// <remarks>LowFreqAlloc. Validates FailFast ownership and target scheduling tuples.</remarks>
        public override void Initialize(RuntimeHostContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_initialized)
                throw new InvalidOperationException("Reliable outbox module is already initialized.");
            if (context.Host.ErrorPolicy != RuntimeHostErrorPolicy.FailFast)
                throw new InvalidOperationException(
                    "Gameplay Trigger reliable outbox requires RuntimeHost FailFast error policy.");

            RuntimeHostDiagnostics diagnostics = context.Host.CaptureDiagnostics();
            for (int i = 0; i < _transfers.Length; i++)
            {
                FrozenTransfer transfer = _transfers[i];
                transfer.TargetModuleRegistered = false;
                for (int moduleIndex = 0; moduleIndex < diagnostics.Modules.Count; moduleIndex++)
                {
                    RuntimeModuleDiagnostics target = diagnostics.Modules[moduleIndex];
                    if (!string.Equals(
                            target.ModuleId,
                            transfer.TargetModuleId,
                            StringComparison.Ordinal))
                        continue;
                    if (CompareSchedulingTuple(
                            target.TickStage,
                            target.Priority,
                            target.ModuleId,
                            TickStage,
                            Priority,
                            ModuleId) <= 0)
                        throw new InvalidOperationException(
                            "Reliable target module must be scheduled strictly after the outbox module: " +
                            target.ModuleId);
                    transfer.TargetModuleRegistered = true;
                    break;
                }
                _transfers[i] = transfer;
            }
            _initialized = true;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            if (!_initialized)
                throw new InvalidOperationException("Reliable outbox module is not initialized.");
            if (!_isEnabled) return;

            RuntimeFrame frame = new RuntimeFrame(context.FrameIndex);
            int index = 0;
            while (index < _runtime.ReliableOutboxRecordCount)
            {
                GameplayTriggerReliableOutboxRecord record =
                    _runtime.GetReliableOutboxRecord(index);
                if (record.OriginalDueFrame > frame)
                {
                    index++;
                    continue;
                }

                int transferIndex = FindTransfer(record.ActionKindId);
                GameplayTriggerReliableTransferResult result;
                if (transferIndex < 0)
                {
                    result = new GameplayTriggerReliableTransferResult(
                        GameplayTriggerReliableTransferStatus.TargetMissing);
                }
                else
                {
                    FrozenTransfer transfer = _transfers[transferIndex];
                    if (!transfer.TargetModuleRegistered)
                    {
                        result = new GameplayTriggerReliableTransferResult(
                            GameplayTriggerReliableTransferStatus.TargetModuleUnavailable);
                    }
                    else if (!transfer.Transfer.IsEnabled)
                    {
                        result = new GameplayTriggerReliableTransferResult(
                            GameplayTriggerReliableTransferStatus.TargetDisabled);
                    }
                    else
                    {
                        result = _runtime.InvokeReliableTransfer(
                            frame,
                            in record,
                            transfer.Transfer);
                    }
                }

                int status = (int)result.Status;
                if (status < (int)GameplayTriggerReliableTransferStatus.Accepted ||
                    status > (int)GameplayTriggerReliableTransferStatus.TargetModuleUnavailable)
                    throw new InvalidOperationException(
                        "Reliable target transfer returned an invalid status: " + status);

                _runtime.RecordReliableExecution(frame, in record, result);
                if (!result.Accepted)
                {
                    index++;
                    continue;
                }

                _runtime.BeginReliableOutboxScan();
                _runtime.MarkReliableOutboxAccepted(index);
                _runtime.CompleteReliableOutboxScan();
            }
        }

        private int FindTransfer(GameplayTriggerActionKindId actionKindId)
        {
            int low = 0;
            int high = _transfers.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison = _transfers[middle].ActionKindId.CompareTo(actionKindId);
                if (comparison == 0) return middle;
                if (comparison < 0) low = middle + 1;
                else high = middle - 1;
            }
            return -1;
        }

        private static int CompareTransfers(FrozenTransfer left, FrozenTransfer right) =>
            left.ActionKindId.CompareTo(right.ActionKindId);

        private static int CompareSchedulingTuple(
            RuntimeTickStage leftStage,
            int leftPriority,
            string leftModuleId,
            RuntimeTickStage rightStage,
            int rightPriority,
            string rightModuleId)
        {
            int stage = leftStage.CompareTo(rightStage);
            if (stage != 0) return stage;
            int priority = leftPriority.CompareTo(rightPriority);
            if (priority != 0) return priority;
            return string.Compare(leftModuleId, rightModuleId, StringComparison.Ordinal);
        }

        private struct FrozenTransfer
        {
            public FrozenTransfer(
                GameplayTriggerActionKindId actionKindId,
                string targetModuleId,
                IGameplayTriggerReliableTargetTransfer transfer)
            {
                ActionKindId = actionKindId;
                TargetModuleId = targetModuleId;
                Transfer = transfer;
                TargetModuleRegistered = false;
            }

            public GameplayTriggerActionKindId ActionKindId;
            public string TargetModuleId;
            public IGameplayTriggerReliableTargetTransfer Transfer;
            public bool TargetModuleRegistered;
        }
    }
}
