using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.UI
{
    public sealed class MxUiViewContract
    {
        public MxUiViewContract(MxUiViewDescriptor descriptor)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            ViewModelType = string.Empty;
            RequiredResources = Array.Empty<string>();
            Commands = Array.Empty<MxUiCommandDescriptor>();
            DiagnosticsTags = Array.Empty<string>();
        }

        public MxUiViewDescriptor Descriptor { get; }
        public string ViewModelType { get; set; }
        public IReadOnlyList<string> RequiredResources { get; set; }
        public IReadOnlyList<MxUiCommandDescriptor> Commands { get; set; }
        public IReadOnlyList<string> DiagnosticsTags { get; set; }

        public MxUiContractValidationResult Validate()
        {
            var issues = new List<string>();
            if (!Descriptor.Id.IsValid)
            {
                issues.Add("View id is required.");
            }

            if (string.IsNullOrWhiteSpace(Descriptor.PackageKey))
            {
                issues.Add("View package key is required.");
            }

            if (string.IsNullOrWhiteSpace(Descriptor.ComponentName))
            {
                issues.Add("View component name is required.");
            }

            if (Commands != null)
            {
                var commandIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < Commands.Count; i++)
                {
                    MxUiCommandDescriptor command = Commands[i];
                    if (command == null || !command.IsValid())
                    {
                        issues.Add("Command at index " + i + " must have a non-empty id and coherent flags.");
                        continue;
                    }

                    if (!commandIds.Add(command.CommandId))
                    {
                        issues.Add("Duplicate command id: " + command.CommandId + ".");
                    }
                }
            }

            return new MxUiContractValidationResult(issues);
        }
    }

    public readonly struct MxUiContractValidationResult
    {
        public MxUiContractValidationResult(IReadOnlyList<string> errors)
        {
            Errors = errors ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;

        public static MxUiContractValidationResult Valid()
        {
            return new MxUiContractValidationResult(Array.Empty<string>());
        }
    }

    public sealed class MxUiViewContractRegistry
    {
        private readonly Dictionary<MxUiViewId, MxUiViewContract> _contracts = new Dictionary<MxUiViewId, MxUiViewContract>();

        public int Count => _contracts.Count;

        public void Register(MxUiViewContract contract)
        {
            if (contract == null)
            {
                throw new ArgumentNullException(nameof(contract));
            }

            MxUiContractValidationResult validation = contract.Validate();
            if (!validation.Success)
            {
                throw new ArgumentException(validation.Errors[0], nameof(contract));
            }

            if (_contracts.ContainsKey(contract.Descriptor.Id))
            {
                throw new ArgumentException("UI view id is already registered: " + contract.Descriptor.Id + ".", nameof(contract));
            }

            _contracts.Add(contract.Descriptor.Id, contract);
        }

        public bool TryGet(MxUiViewId id, out MxUiViewContract contract)
        {
            return _contracts.TryGetValue(id, out contract);
        }

        public IReadOnlyList<MxUiViewContract> ListContracts()
        {
            if (_contracts.Count == 0)
            {
                return Array.Empty<MxUiViewContract>();
            }

            var contracts = new List<MxUiViewContract>(_contracts.Values);
            contracts.Sort(CompareContracts);
            return new ReadOnlyCollection<MxUiViewContract>(contracts);
        }

        private static int CompareContracts(MxUiViewContract left, MxUiViewContract right)
        {
            return string.Compare(left.Descriptor.Id.Value, right.Descriptor.Id.Value, StringComparison.Ordinal);
        }
    }
}
