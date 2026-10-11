using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>Immutable claims presented to a trusted Ready-approval authority verifier.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only; this object never enters Runtime state or hashes.</remarks>
    public sealed class VfxReadyApprovalAuthorityRequest
    {
        public string StableId { get; set; } = string.Empty;
        public string ApprovalId { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;
        public string ApprovalRole { get; set; } = string.Empty;
        public string AuthorityReceiptHash { get; set; } = string.Empty;
        public string AssetContentHash { get; set; } = string.Empty;
        public string DependencyHash { get; set; } = string.Empty;
        public string RecipeInputHash { get; set; } = string.Empty;
        public string EvaluationReportHash { get; set; } = string.Empty;
        public string QualityProfileVersion { get; set; } = string.Empty;
        public string RecipeReuseQualificationHash { get; set; } = string.Empty;
        public List<VfxAuthoringHumanApproval> HumanApprovals { get; set; } = new List<VfxAuthoringHumanApproval>();
    }

    /// <summary>Trust boundary for Ready approval receipts.</summary>
    /// <remarks>
    /// LowFreqAlloc. The verifier is supplied by an authenticated host integration and must validate
    /// the external receipt identified by <see cref="VfxReadyApprovalAuthorityRequest.AuthorityReceiptHash"/>.
    /// Catalog strings alone never satisfy this boundary.
    /// </remarks>
    public interface IVfxReadyApprovalAuthorityVerifier
    {
        string AuthorityId { get; }
        string AuthorityVersion { get; }

        /// <summary>Verifies an external authority receipt against all immutable Ready claims.</summary>
        /// <remarks>LowFreqAlloc. Implementations may perform authenticated local or remote I/O.</remarks>
        bool Verify(VfxReadyApprovalAuthorityRequest request, out string rejectionReason);
    }

    /// <summary>Immutable claims presented to a trusted candidate-selection authority verifier.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only promotion governance.</remarks>
    public sealed class VfxPromotionSelectionAuthorityRequest
    {
        public string CandidateId { get; set; } = string.Empty;
        public string TargetKind { get; set; } = string.Empty;
        public string TargetStableId { get; set; } = string.Empty;
        public string GenerationPlanHash { get; set; } = string.Empty;
        public string CandidateReceiptHash { get; set; } = string.Empty;
        public string EvaluationReportHash { get; set; } = string.Empty;
        public string SelectedBy { get; set; } = string.Empty;
        public string SelectionEvidenceHash { get; set; } = string.Empty;
        public string AuthorityReceiptHash { get; set; } = string.Empty;
        public bool StrategicFoundationExceptionApproved { get; set; }
        public List<string> ConsumerRecipeIds { get; set; } = new List<string>();
    }

    /// <summary>Trust boundary for human candidate-selection receipts.</summary>
    /// <remarks>
    /// LowFreqAlloc. Without an explicitly injected trusted implementation, Core promotion planning
    /// deliberately fails closed; selectedBy and arbitrary content hashes are not authorization.
    /// </remarks>
    public interface IVfxPromotionSelectionAuthorityVerifier
    {
        string AuthorityId { get; }
        string AuthorityVersion { get; }

        /// <summary>Verifies an external selection receipt against the exact promotion inputs.</summary>
        /// <remarks>LowFreqAlloc. Implementations may perform authenticated local or remote I/O.</remarks>
        bool Verify(VfxPromotionSelectionAuthorityRequest request, out string rejectionReason);
    }
}
