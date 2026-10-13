using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    internal static void RunCharacterAuthorityPose()
    {
        AuthorityPoseRig_RoundTripOrderAndRawStatesAreStable();
        AuthorityPoseRig_ValidationAndCompilerAreAdditiveAndFailClosed();
        AuthorityPoseRig_Dragon14OwnsFourFormalChains();
    }

    private static void AuthorityPoseRig_RoundTripOrderAndRawStatesAreStable()
    {
        JsonSerializerOptions options = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        CharacterAuthorityPoseRigLoadResult missing = CharacterAuthorityPoseRigDocumentCodec.Missing();
        Require(missing.State == CharacterAuthorityPoseRigDocumentState.Missing
                && !missing.WriteEligible && missing.Document == null,
            "missing authority-pose rig must not synthesize authoring data.");

        CharacterAuthorityPoseRigDocument first = AuthorityPoseRigDocument();
        CharacterAuthorityPoseRigDocument reordered = AuthorityPoseRigDocument();
        reordered.Channels.Reverse();
        reordered.Limbs.Reverse();
        reordered.Attachments.Reverse();
        reordered.Limbs[0].AllowedMotionIds.Reverse();
        string firstCanonical = CharacterAuthorityPoseRigDocumentCodec.SerializeCanonical(first);
        string reorderedCanonical = CharacterAuthorityPoseRigDocumentCodec.SerializeCanonical(reordered);
        Require(firstCanonical == reorderedCanonical
                && CharacterAuthorityPoseRigDocumentCodec.ComputeContentHash(first)
                    == CharacterAuthorityPoseRigDocumentCodec.ComputeContentHash(reordered),
            "authority-pose source order must not change canonical bytes or content hash.");

        CharacterAuthorityPoseRigLoadResult current = CharacterAuthorityPoseRigDocumentCodec.Read(
            JsonSerializer.Serialize(first, options), options);
        Require(current.State == CharacterAuthorityPoseRigDocumentState.Current
                && current.WriteEligible && current.Document.Limbs.Count == 2,
            "current authority-pose rig must round-trip as write-eligible.");

        const string futurePayload =
            "{\n  \"format\": \"mx.characterAuthorityPoseRig.v2\",\n  \"schemaVersion\": \"2.0\",\n  \"future\": true\n}\n";
        CharacterAuthorityPoseRigLoadResult future = CharacterAuthorityPoseRigDocumentCodec.Read(
            futurePayload, options);
        Require(future.State == CharacterAuthorityPoseRigDocumentState.Future
                && !future.WriteEligible && future.Document == null
                && future.RawPayload == futurePayload,
            "future authority-pose rig must preserve exact raw text and remain read-only.");

        const string invalidPayload = "{ not-json";
        CharacterAuthorityPoseRigLoadResult invalid = CharacterAuthorityPoseRigDocumentCodec.Read(
            invalidPayload, options);
        Require(invalid.State == CharacterAuthorityPoseRigDocumentState.Invalid
                && !invalid.WriteEligible && invalid.RawPayload == invalidPayload,
            "invalid authority-pose rig must preserve raw text and remain read-only.");
        CharacterAuthorityPoseRigLoadResult unrelated = CharacterAuthorityPoseRigDocumentCodec.Read(
            "{\"format\":\"not.authority-pose\",\"schemaVersion\":\"1.0\"}", options);
        Require(unrelated.State == CharacterAuthorityPoseRigDocumentState.Invalid,
            "an unrelated format must be Invalid rather than Future.");
    }

    private static void AuthorityPoseRig_ValidationAndCompilerAreAdditiveAndFailClosed()
    {
        CharacterResourcePackage baselinePackage = LoadSample("character-codex-flow-test");
        CharacterAuthoringCompileResult baseline = Compile(baselinePackage);
        CharacterResourcePackage missingPackage = LoadSample("character-codex-flow-test");
        missingPackage.Geometry.AuthorityPoseRigDocumentState =
            CharacterAuthorityPoseRigDocumentState.Missing;
        missingPackage.Geometry.AuthorityPoseRig = AuthorityPoseRigDocument();
        CharacterAuthoringCompileResult missing = Compile(missingPackage);
        Require(missing.GeometryBinding.Format == baseline.GeometryBinding.Format
                && missing.GeometryBinding.AuthorityPoseRig == null
                && missing.Hashes.SourcePackageHash == baseline.Hashes.SourcePackageHash
                && missing.Hashes.GeometryBindingHash == baseline.Hashes.GeometryBindingHash,
            "Missing authority-pose rig must preserve pre-v6 binding and hash compatibility.");

        CharacterResourcePackage firstPackage = LoadSample("character-codex-flow-test");
        firstPackage.Geometry.AuthorityPoseRigDocumentState = CharacterAuthorityPoseRigDocumentState.Current;
        firstPackage.Geometry.AuthorityPoseRig = AuthorityPoseRigDocument();
        CharacterResourcePackage secondPackage = LoadSample("character-codex-flow-test");
        secondPackage.Geometry.AuthorityPoseRigDocumentState = CharacterAuthorityPoseRigDocumentState.Current;
        secondPackage.Geometry.AuthorityPoseRig = AuthorityPoseRigDocument();
        secondPackage.Geometry.AuthorityPoseRig.Channels.Reverse();
        secondPackage.Geometry.AuthorityPoseRig.Limbs.Reverse();
        secondPackage.Geometry.AuthorityPoseRig.Attachments.Reverse();

        CharacterAuthoringValidationReport currentReport =
            CharacterResourcePackageValidator.Validate(firstPackage);
        CharacterAuthoringCompileResult first = Compile(firstPackage);
        CharacterAuthoringCompileResult second = Compile(secondPackage);
        Require(!currentReport.HasBlockingIssues
                && first.Status == CharacterAuthoringCompilerStatus.Ready
                && first.GeometryBinding.Format == CharacterAuthoringCompilerFormats.GeometryBindingV8
                && first.GeometryBinding.SchemaVersion == "8.0"
                && first.GeometryBinding.AuthorityPoseRig != null
                && first.GeometryBinding.AuthorityPoseRig.Format == "mx.characterAuthorityPoseRig.v2"
                && first.GeometryBinding.AuthorityPoseRig.Channels.TrueForAll(channel =>
                    channel.ChannelId > 0 && channel.PartId > 0 && !string.IsNullOrEmpty(channel.ChannelLabel))
                && first.GeometryBinding.AuthorityPoseRig.Limbs.TrueForAll(limb =>
                    limb.ProbeId > 0 && limb.IkBindingId > 0 && limb.RootChannelId > 0
                    && limb.MidChannelId > 0 && limb.TipChannelId > 0 && limb.MirrorPeerProbeId > 0)
                && first.GeometryBinding.AuthorityPoseRig.Channels.Count == 6
                && first.GeometryBinding.AuthorityPoseRig.Limbs.Count == 2
                && first.GeometryBinding.AuthorityPoseRig.Attachments.Count == 2
                && first.GeometryBinding.AuthorityPoseRig.ContentHash.StartsWith("sha256:", StringComparison.Ordinal)
                && first.Hashes.SourcePackageHash == second.Hashes.SourcePackageHash
                && first.Hashes.GeometryBindingHash == second.Hashes.GeometryBindingHash
                && JsonSerializer.Serialize(first.GeometryBinding, options: MxFramework.Authoring.Cli.Program.CreateJsonOptions())
                    == JsonSerializer.Serialize(second.GeometryBinding, options: MxFramework.Authoring.Cli.Program.CreateJsonOptions()),
            "valid authority-pose authoring must compile to canonical numeric v8 binding with a v2 rig.");
        Require(first.SourceMappings.Exists(mapping =>
                mapping.SourcePath == "geometry/authority_pose_rig.json"
                && mapping.SourceObjectPath == "authorityPoseRig/limbs/probe.test.left"
                && mapping.TargetPath == "authorityPoseRig/limbs/0"),
            "authority-pose limb must retain stable source mapping.");

        CharacterResourcePackage conflictPackage = LoadSample("character-codex-flow-test");
        conflictPackage.Geometry.AuthorityPoseRigDocumentState = CharacterAuthorityPoseRigDocumentState.Current;
        conflictPackage.Geometry.AuthorityPoseRig = AuthorityPoseRigDocument();
        conflictPackage.Geometry.AuthorityPoseRig.Limbs[1].RootChannelId = "channel.test.left.root";
        CharacterAuthoringValidationReport conflictReport =
            CharacterResourcePackageValidator.Validate(conflictPackage);
        CharacterAuthoringCompileResult conflict = Compile(conflictPackage);
        Require(HasValidationIssue(
                    conflictReport,
                    CharacterAuthoringValidationCodes.AuthorityPoseChannelConflict,
                    CharacterAuthoringValidationGate.ExportBlocked)
                && conflict.Status == CharacterAuthoringCompilerStatus.ExportBlocked
                && conflict.GeometryBinding.Format != CharacterAuthoringCompilerFormats.GeometryBindingV6
                && conflict.GeometryBinding.AuthorityPoseRig == null,
            "duplicate channel control must fail closed without a partial v6 binding.");

        CharacterResourcePackage futurePackage = LoadSample("character-codex-flow-test");
        futurePackage.Geometry.AuthorityPoseRigDocumentState = CharacterAuthorityPoseRigDocumentState.Future;
        futurePackage.Geometry.AuthorityPoseRigRawPayload =
            "{\"format\":\"mx.characterAuthorityPoseRig.v2\",\"schemaVersion\":\"2.0\"}";
        CharacterAuthoringCompileResult future = Compile(futurePackage);
        Require(future.Status == CharacterAuthoringCompilerStatus.ExportBlocked
                && future.GeometryBinding.Format != CharacterAuthoringCompilerFormats.GeometryBindingV6
                && future.GeometryBinding.AuthorityPoseRig == null,
            "future authority-pose source must fail closed and preserve its raw-only boundary.");
    }

    private static void AuthorityPoseRig_Dragon14OwnsFourFormalChains()
    {
        string packagePath = Path.Combine(
            FindRepoRoot(), "Authoring", "CharacterPackages", "character-000003");
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(
            packagePath, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);
        CharacterAuthoringCompileResult compiled = CharacterAuthoringCompiler.Compile(
            new CharacterAuthoringCompileRequest
            {
                Package = package,
                PackageRootPath = packagePath,
                IdentityCatalog = CharacterContentIdentityCatalogSnapshot.LoadFromFile(
                    Path.Combine(FindRepoRoot(), "Tools", "RuntimeHash", "character_definition_ids.json"))
            });

        Require(!report.HasBlockingIssues
                && package.Geometry.AuthorityPoseRigDocumentState == CharacterAuthorityPoseRigDocumentState.Current
                && package.Geometry.AuthorityPoseRig.Limbs.Count == 4
                && package.Geometry.AuthorityPoseRig.Channels.Count == 12
                && !compiled.GateReport.ExportBlocked
                && compiled.GeometryBinding.Format == CharacterAuthoringCompilerFormats.GeometryBindingV8
                && compiled.GeometryBinding.AuthorityPoseRig.Limbs.Count == 4
                && compiled.GeometryBinding.AuthorityPoseRig.Channels.Count == 12,
            "Dragon14 must validate and compile exactly four formal root/mid/tip limbs.");
        Require(package.Geometry.AuthorityPoseRig.Channels.Exists(channel =>
                    channel.ChannelId == "channel.000003.left_fore.tip"
                    && channel.BonePath.EndsWith("/Dragon15_l_hand", StringComparison.Ordinal))
                && package.Geometry.AuthorityPoseRig.Channels.Exists(channel =>
                    channel.ChannelId == "channel.000003.right_fore.tip"
                    && channel.BonePath.EndsWith("/Dragon15_r_hand", StringComparison.Ordinal))
                && package.Geometry.AuthorityPoseRig.Channels.Exists(channel =>
                    channel.ChannelId == "channel.000003.left_hind.tip"
                    && channel.BonePath.EndsWith("/Dragon15_l_foot", StringComparison.Ordinal))
                && package.Geometry.AuthorityPoseRig.Channels.Exists(channel =>
                    channel.ChannelId == "channel.000003.right_hind.tip"
                    && channel.BonePath.EndsWith("/Dragon15_r_foot", StringComparison.Ordinal)),
            "Dragon14 formal channels must terminate at the imported hands and feet.");
    }

    private static CharacterAuthorityPoseRigDocument AuthorityPoseRigDocument()
    {
        return new CharacterAuthorityPoseRigDocument
        {
            Revision = 3,
            Channels = new List<CharacterAuthorityPoseChannelAuthoring>
            {
                Channel("channel.test.left.root", "test/left/root", "upper_arm_l"),
                Channel("channel.test.left.mid", "test/left/root/mid", "forearm_l"),
                Channel("channel.test.left.tip", "test/left/root/mid/tip", "hand_l"),
                Channel("channel.test.right.root", "test/right/root", "upper_arm_r"),
                Channel("channel.test.right.mid", "test/right/root/mid", "forearm_r"),
                Channel("channel.test.right.tip", "test/right/root/mid/tip", "hand_r")
            },
            Limbs = new List<CharacterAuthorityPoseLimbAuthoring>
            {
                Limb("left", CharacterAuthorityPoseHandedness.Left, "right"),
                Limb("right", CharacterAuthorityPoseHandedness.Right, "left")
            },
            Attachments = new List<CharacterAuthorityPoseAttachmentAuthoring>
            {
                new CharacterAuthorityPoseAttachmentAuthoring
                {
                    Kind = CharacterAuthorityPoseAttachmentKind.Collider,
                    StableId = "upper_arm_l_capsule",
                    ChannelId = "channel.test.left.root"
                },
                new CharacterAuthorityPoseAttachmentAuthoring
                {
                    Kind = CharacterAuthorityPoseAttachmentKind.Socket,
                    StableId = "mainHand",
                    ChannelId = "channel.test.right.tip"
                }
            }
        };
    }

    private static CharacterAuthorityPoseChannelAuthoring Channel(
        string id, string path, string partId) =>
        new CharacterAuthorityPoseChannelAuthoring
        {
            ChannelId = id,
            BonePath = path,
            PartId = partId
        };

    private static CharacterAuthorityPoseLimbAuthoring Limb(
        string side,
        CharacterAuthorityPoseHandedness handedness,
        string mirrorSide) =>
        new CharacterAuthorityPoseLimbAuthoring
        {
            ProbeId = "probe.test." + side,
            IkBindingId = "ik.test." + side,
            Handedness = handedness,
            MirrorPeerProbeId = "probe.test." + mirrorSide,
            RootChannelId = "channel.test." + side + ".root",
            MidChannelId = "channel.test." + side + ".mid",
            TipChannelId = "channel.test." + side + ".tip",
            TipProbeOriginMillimeters = new CharacterAuthoringIntVector3(0, 0, 0),
            TipProbeAxisSnorm = new CharacterAuthoringIntVector3(0, -32767, 0),
            MaxUpDistanceMillimeters = 200,
            MaxDownDistanceMillimeters = 500,
            RadiusMillimeters = 100,
            ClearanceMillimeters = 20,
            StaticLayerMask = 1,
            MinimumSlopeCosineMilli = 500,
            PoleDirectionSnorm = new CharacterAuthoringIntVector3(
                handedness == CharacterAuthorityPoseHandedness.Left ? -32767 : 32767, 0, 0),
            RootToMidLengthMillimeters = 500,
            MidToTipLengthMillimeters = 400,
            MinimumReachMillimeters = 100,
            MaximumReachMillimeters = 850,
            WorldNormalTargetOffsetMillimeters = 20,
            AllowedMotionIds = new List<string> { "motion.walk", "motion.idle" },
            AllowedPostureIds = new List<string> { "posture.grounded" },
            DisabledActionTags = new List<string> { "action.airborne" },
            EnterFrames = 2,
            ExitFrames = 2,
            LostFrames = 3
        };
}
