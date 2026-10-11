using System;

namespace MxFramework.ApplicationStartup
{
    /// <summary>Frozen validated startup plan. Construction is LowFreqAlloc; indexed reads are NoAlloc.</summary>
    public sealed class ApplicationStartupPlan
    {
        public const int MaximumStepCount = 256;

        private readonly ApplicationStartupStepDescriptor[] _descriptors;
        private readonly IApplicationStartupStep[] _steps;
        private readonly int[] _executionOrder;

        private ApplicationStartupPlan(
            ApplicationStartupPlanId planId,
            ApplicationStartupStepDescriptor[] descriptors,
            IApplicationStartupStep[] steps,
            int[] executionOrder,
            IApplicationStartupTransactionFactory transactionFactory,
            float totalWeight)
        {
            PlanId = planId;
            _descriptors = descriptors;
            _steps = steps;
            _executionOrder = executionOrder;
            TransactionFactory = transactionFactory;
            TotalWeight = totalWeight;
        }

        public ApplicationStartupPlanId PlanId { get; }
        public int StepCount => _executionOrder.Length;
        public float TotalWeight { get; }
        public IApplicationStartupTransactionFactory TransactionFactory { get; }

        public ApplicationStartupStepDescriptor GetDescriptorInExecutionOrder(int index) => _descriptors[_executionOrder[index]];
        public IApplicationStartupStep GetStepInExecutionOrder(int index) => _steps[_executionOrder[index]];

        public static bool TryCreate(
            ApplicationStartupPlanId planId,
            ApplicationStartupStepDescriptor[] descriptors,
            IApplicationStartupStep[] steps,
            IApplicationStartupTransactionFactory transactionFactory,
            out ApplicationStartupPlan plan,
            out ApplicationStartupError error)
        {
            plan = null;
            error = ApplicationStartupError.None;

            if (!planId.IsValid || descriptors == null || steps == null || transactionFactory == null ||
                descriptors.Length == 0 || descriptors.Length != steps.Length || descriptors.Length > MaximumStepCount)
            {
                error = CreatePlanError(ApplicationStartupErrorCode.InvalidPlan, default, "Plan inputs are incomplete or exceed limits.");
                return false;
            }

            var descriptorCopy = (ApplicationStartupStepDescriptor[])descriptors.Clone();
            var stepCopy = (IApplicationStartupStep[])steps.Clone();
            var indegree = new int[descriptorCopy.Length];
            var emitted = new bool[descriptorCopy.Length];
            var executionOrder = new int[descriptorCopy.Length];
            float totalWeight = 0f;

            for (int i = 0; i < descriptorCopy.Length; i++)
            {
                ApplicationStartupStepDescriptor descriptor = descriptorCopy[i];
                IApplicationStartupStep step = stepCopy[i];
                if (descriptor == null || step == null || !descriptor.StepId.IsValid || descriptor.StepId != step.StepId)
                {
                    error = CreatePlanError(ApplicationStartupErrorCode.InvalidPlan, descriptor == null ? default : descriptor.StepId, "Descriptor and step identities must match.");
                    return false;
                }

                if (float.IsNaN(descriptor.ProgressWeight) || float.IsInfinity(descriptor.ProgressWeight) || descriptor.ProgressWeight <= 0f)
                {
                    error = CreatePlanError(ApplicationStartupErrorCode.InvalidWeight, descriptor.StepId, "Progress weight must be finite and positive.");
                    return false;
                }

                if (double.IsNaN(descriptor.TimeoutSeconds) || double.IsInfinity(descriptor.TimeoutSeconds) || descriptor.TimeoutSeconds < 0d || descriptor.TimeoutSeconds > 86400d)
                {
                    error = CreatePlanError(ApplicationStartupErrorCode.InvalidPlan, descriptor.StepId, "Timeout is outside the supported range.");
                    return false;
                }

                for (int j = 0; j < i; j++)
                {
                    if (descriptorCopy[j].StepId == descriptor.StepId)
                    {
                        error = CreatePlanError(ApplicationStartupErrorCode.DuplicateStep, descriptor.StepId, "Duplicate step id.");
                        return false;
                    }

                    if (!string.IsNullOrEmpty(descriptor.ExclusiveOwnerId) &&
                        string.Equals(descriptorCopy[j].ExclusiveOwnerId, descriptor.ExclusiveOwnerId, StringComparison.Ordinal))
                    {
                        error = CreatePlanError(ApplicationStartupErrorCode.OwnerConflict, descriptor.StepId, "Exclusive owner is claimed by multiple steps.");
                        return false;
                    }
                }

                for (int dependencyIndex = 0; dependencyIndex < descriptor.DependencyCount; dependencyIndex++)
                {
                    ApplicationStartupStepId dependency = descriptor.GetDependency(dependencyIndex);
                    if (!dependency.IsValid || dependency == descriptor.StepId)
                    {
                        error = CreatePlanError(ApplicationStartupErrorCode.MissingDependency, descriptor.StepId, "Dependency is invalid or self-referential.");
                        return false;
                    }

                    for (int previousDependency = 0; previousDependency < dependencyIndex; previousDependency++)
                    {
                        if (descriptor.GetDependency(previousDependency) == dependency)
                        {
                            error = CreatePlanError(ApplicationStartupErrorCode.InvalidPlan, descriptor.StepId, "Duplicate dependency.");
                            return false;
                        }
                    }

                    if (FindDescriptor(descriptorCopy, dependency) < 0)
                    {
                        error = CreatePlanError(ApplicationStartupErrorCode.MissingDependency, descriptor.StepId, "Dependency does not exist in the plan.");
                        return false;
                    }

                    indegree[i]++;
                }

                totalWeight += descriptor.ProgressWeight;
                if (float.IsInfinity(totalWeight) || float.IsNaN(totalWeight))
                {
                    error = CreatePlanError(ApplicationStartupErrorCode.InvalidWeight, descriptor.StepId, "Total progress weight is not finite.");
                    return false;
                }
            }

            for (int output = 0; output < executionOrder.Length; output++)
            {
                int selected = -1;
                for (int candidate = 0; candidate < descriptorCopy.Length; candidate++)
                {
                    if (emitted[candidate] || indegree[candidate] != 0)
                    {
                        continue;
                    }

                    if (selected < 0 || CompareReady(descriptorCopy[candidate], descriptorCopy[selected]) < 0)
                    {
                        selected = candidate;
                    }
                }

                if (selected < 0)
                {
                    error = CreatePlanError(ApplicationStartupErrorCode.DependencyCycle, default, "Plan contains a dependency cycle.");
                    return false;
                }

                emitted[selected] = true;
                executionOrder[output] = selected;
                ApplicationStartupStepId completedId = descriptorCopy[selected].StepId;
                for (int candidate = 0; candidate < descriptorCopy.Length; candidate++)
                {
                    if (!emitted[candidate] && HasDependency(descriptorCopy[candidate], completedId))
                    {
                        indegree[candidate]--;
                    }
                }
            }

            plan = new ApplicationStartupPlan(planId, descriptorCopy, stepCopy, executionOrder, transactionFactory, totalWeight);
            return true;
        }

        private static int FindDescriptor(ApplicationStartupStepDescriptor[] descriptors, ApplicationStartupStepId stepId)
        {
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i] != null && descriptors[i].StepId == stepId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool HasDependency(ApplicationStartupStepDescriptor descriptor, ApplicationStartupStepId stepId)
        {
            for (int i = 0; i < descriptor.DependencyCount; i++)
            {
                if (descriptor.GetDependency(i) == stepId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareReady(ApplicationStartupStepDescriptor left, ApplicationStartupStepDescriptor right)
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : left.StepId.CompareTo(right.StepId);
        }

        private static ApplicationStartupError CreatePlanError(ApplicationStartupErrorCode code, ApplicationStartupStepId stepId, string message)
        {
            return new ApplicationStartupError(code, ApplicationStartupPhase.Validation, stepId, default, null, message, null);
        }
    }
}
