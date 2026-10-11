using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    internal static void RunCharacterBodyPartDamage()
    {
        BodyPartDamage_FourStatesAndCanonicalIdentityAreStable();
        BodyPartDamage_ValidationAndCompilerFailClosedDeterministically();
        BodyPartDamage_SeverPolicyIsOptionalTypedAndRootSafe();
        BodyPartDamage_DedicatedSaveOwnsItsFileAndPreservesFutureBytes();
        BodyPartDamage_Dragon14OwnsFourStableGroups();
    }

    private static void BodyPartDamage_FourStatesAndCanonicalIdentityAreStable()
    {
        JsonSerializerOptions options = MxFramework.Authoring.Cli.Program.CreateJsonOptions();
        CharacterBodyPartDamageLoadResult missing = CharacterBodyPartDamageDocumentCodec.Missing();
        Require(missing.State == CharacterBodyPartDamageDocumentState.Missing
                && !missing.WriteEligible && missing.Document == null,
            "missing body-part-damage must not synthesize a document.");

        CharacterBodyPartDamageDocument first = DamageDocument(
            DamageDefinition(
                "partstate.zeta",
                new[] { "hand_l", "forearm_l" },
                severable: true,
                severRequiredTag: "action.test.sever",
                presentationEventId: "event.test.severed"),
            DamageDefinition("partstate.alpha", new[] { "head" }, "action.focus.head"));
        CharacterBodyPartDamageDocument second = DamageDocument(
            DamageDefinition("partstate.alpha", new[] { "head" }, "action.focus.head"),
            DamageDefinition(
                "partstate.zeta",
                new[] { "forearm_l", "hand_l" },
                severable: true,
                severRequiredTag: "action.test.sever",
                presentationEventId: "event.test.severed"));
        string firstCanonical = CharacterBodyPartDamageDocumentCodec.SerializeCanonical(first);
        string secondCanonical = CharacterBodyPartDamageDocumentCodec.SerializeCanonical(second);
        Require(firstCanonical == secondCanonical
                && CharacterBodyPartDamageDocumentCodec.ComputeContentHash(first)
                    == CharacterBodyPartDamageDocumentCodec.ComputeContentHash(second)
                && firstCanonical.IndexOf("partstate.alpha", StringComparison.Ordinal)
                    < firstCanonical.IndexOf("partstate.zeta", StringComparison.Ordinal)
                && firstCanonical.IndexOf("forearm_l", StringComparison.Ordinal)
                    < firstCanonical.IndexOf("hand_l", StringComparison.Ordinal),
            "definition and member input order must not change canonical bytes or hash.");

        CharacterBodyPartDamageLoadResult current = CharacterBodyPartDamageDocumentCodec.Read(
            JsonSerializer.Serialize(first, options), options);
        Require(current.State == CharacterBodyPartDamageDocumentState.Current
                && current.WriteEligible && current.Document.PartStates.Count == 2
                && current.Document.PartStates[0].Severable
                && current.Document.PartStates[0].SeverRequiredTag == "action.test.sever"
                && current.Document.PartStates[0].PresentationEventId == "event.test.severed",
            "current body-part-damage must round-trip as write-eligible.");

        const string legacyPayload =
            "{\"format\":\"mx.characterBodyPartDamage.v1\",\"schemaVersion\":\"1.0\",\"revision\":1," +
            "\"partStates\":[{\"partStateId\":\"partstate.legacy\",\"memberPartIds\":[\"head\"]," +
            "\"damageZoneId\":\"\",\"woundCapacity\":0,\"openDamageScaleMilli\":0," +
            "\"focusConsumeTag\":\"\",\"maxIntegrity\":1,\"integrityDamageScaleMilli\":1000," +
            "\"breakReactionId\":\"\",\"authoredPriorityDeltaOpen\":0," +
            "\"authoredPriorityDeltaBroken\":0}]}";
        CharacterBodyPartDamageLoadResult legacy = CharacterBodyPartDamageDocumentCodec.Read(
            legacyPayload,
            options);
        Require(legacy.State == CharacterBodyPartDamageDocumentState.Current
                && !legacy.Document.PartStates[0].Severable
                && legacy.Document.PartStates[0].SeverRequiredTag == string.Empty
                && legacy.Document.PartStates[0].PresentationEventId == string.Empty,
            "v1 documents without optional sever fields must retain M2 non-severable behavior.");

        const string futurePayload =
            "{\n  \"format\": \"mx.characterBodyPartDamage.v2\",\n  \"schemaVersion\": \"2.0\",\n  \"future\": true\n}\n";
        CharacterBodyPartDamageLoadResult future = CharacterBodyPartDamageDocumentCodec.Read(
            futurePayload, options);
        Require(future.State == CharacterBodyPartDamageDocumentState.Future
                && !future.WriteEligible && future.Document == null
                && future.RawPayload == futurePayload,
            "future body-part-damage must preserve exact raw text and remain read-only.");

        const string invalidPayload = "{ not-json";
        CharacterBodyPartDamageLoadResult invalid = CharacterBodyPartDamageDocumentCodec.Read(
            invalidPayload, options);
        Require(invalid.State == CharacterBodyPartDamageDocumentState.Invalid
                && !invalid.WriteEligible && invalid.RawPayload == invalidPayload,
            "invalid body-part-damage must preserve raw text and remain read-only.");

        const string unknownFormatPayload =
            "{\"format\":\"not.body-part-damage\",\"schemaVersion\":\"1.0\"}";
        CharacterBodyPartDamageLoadResult unknownFormat = CharacterBodyPartDamageDocumentCodec.Read(
            unknownFormatPayload, options);
        Require(unknownFormat.State == CharacterBodyPartDamageDocumentState.Invalid
                && unknownFormat.RawPayload == unknownFormatPayload,
            "an unrelated format must be Invalid rather than masquerading as a future body-part-damage schema.");
    }

    private static void BodyPartDamage_ValidationAndCompilerFailClosedDeterministically()
    {
        CharacterResourcePackage baselinePackage = LoadSample("character-codex-flow-test");
        CharacterAuthoringCompileResult baseline = Compile(baselinePackage);
        CharacterResourcePackage explicitMissingPackage = LoadSample("character-codex-flow-test");
        explicitMissingPackage.Geometry.BodyPartDamageDocumentState =
            CharacterBodyPartDamageDocumentState.Missing;
        explicitMissingPackage.Geometry.BodyPartDamage = DamageDocument(
            DamageDefinition("partstate.ignored", new[] { "head" }));
        CharacterAuthoringCompileResult explicitMissing = Compile(explicitMissingPackage);
        Require(explicitMissing.GeometryBinding.Format == baseline.GeometryBinding.Format
                && explicitMissing.Hashes.SourcePackageHash == baseline.Hashes.SourcePackageHash
                && explicitMissing.Hashes.GeometryBindingHash == baseline.Hashes.GeometryBindingHash,
            "Missing body-part-damage must preserve v1-v4 binding compatibility and identities.");

        CharacterResourcePackage emptyCurrentPackage = LoadSample("character-codex-flow-test");
        emptyCurrentPackage.Geometry.BodyPartDamageDocumentState = CharacterBodyPartDamageDocumentState.Current;
        emptyCurrentPackage.Geometry.BodyPartDamage = DamageDocument();
        CharacterAuthoringValidationReport emptyCurrentReport =
            CharacterResourcePackageValidator.Validate(emptyCurrentPackage);
        Require(HasValidationIssue(
                emptyCurrentReport,
                CharacterAuthoringValidationCodes.BodyPartDamageEmptyDefinitions,
                CharacterAuthoringValidationGate.ExportBlocked),
            "Current zero-definition body-part-damage must report a typed export blocker.");
        CharacterAuthoringCompileResult emptyCurrent = Compile(emptyCurrentPackage);
        Require(emptyCurrent.Status == CharacterAuthoringCompilerStatus.ExportBlocked
                && emptyCurrent.GeometryBinding.Format != CharacterAuthoringCompilerFormats.GeometryBindingV5
                && emptyCurrent.GeometryBinding.BodyPartDamageDefinitions == null,
            "Current zero-definition body-part-damage must fail closed without an Authored v5 binding.");

        CharacterResourcePackage firstPackage = LoadSample("character-codex-flow-test");
        firstPackage.Geometry.BodyPartDamageDocumentState = CharacterBodyPartDamageDocumentState.Current;
        firstPackage.Geometry.BodyPartDamage = DamageDocument(
            DamageDefinition(
                "partstate.zeta",
                new[] { "hand_l", "forearm_l" },
                severable: true,
                severRequiredTag: "action.test.sever",
                presentationEventId: "event.test.severed"),
            DamageDefinition("partstate.alpha", new[] { "head" }, "action.focus.head"));
        CharacterResourcePackage secondPackage = LoadSample("character-codex-flow-test");
        secondPackage.Geometry.BodyPartDamageDocumentState = CharacterBodyPartDamageDocumentState.Current;
        secondPackage.Geometry.BodyPartDamage = DamageDocument(
            DamageDefinition("partstate.alpha", new[] { "head" }, "action.focus.head"),
            DamageDefinition(
                "partstate.zeta",
                new[] { "forearm_l", "hand_l" },
                severable: true,
                severRequiredTag: "action.test.sever",
                presentationEventId: "event.test.severed"));
        CharacterAuthoringCompileResult first = Compile(firstPackage);
        CharacterAuthoringCompileResult second = Compile(secondPackage);
        Require(first.Status == CharacterAuthoringCompilerStatus.Ready
                && first.GeometryBinding.Format == CharacterAuthoringCompilerFormats.GeometryBindingV8
                && first.GeometryBinding.SchemaVersion == "8.0"
                && first.GeometryBinding.BodyPartDamageMode == CharacterBodyPartDamageBindingMode.Authored
                && first.GeometryBinding.BodyPartDamageFormat == "mx.characterBodyPartDamage.v3"
                && first.GeometryBinding.BodyPartDamageSchemaVersion == "3.0"
                && first.GeometryBinding.BodyPartDamageContentEncodingVersion == 1
                && first.GeometryBinding.BodyPartDamageRevision == 7
                && first.GeometryBinding.BodyPartDamageDefinitions.Count == 2
                && first.GeometryBinding.BodyPartDamageDefinitions[0].DefinitionIndex == 0
                && first.GeometryBinding.BodyPartDamageDefinitions[0].PartStateLabel == "partstate.alpha"
                && first.GeometryBinding.BodyPartDamageDefinitions[0].FocusConsumeTag == "action.focus.head"
                && first.GeometryBinding.BodyPartDamageDefinitions[1].Severable
                && first.GeometryBinding.BodyPartDamageDefinitions[1].SeverRequiredTag == "action.test.sever"
                && first.GeometryBinding.BodyPartDamageDefinitions[1].PresentationEventId == "event.test.severed"
                && first.GeometryBinding.BodyPartDamagePartLookup[0].PartId
                    < first.GeometryBinding.BodyPartDamagePartLookup[1].PartId
                && first.GeometryBinding.BodyPartDamagePartLookup[1].PartId
                    < first.GeometryBinding.BodyPartDamagePartLookup[2].PartId
                && first.Hashes.SourcePackageHash == second.Hashes.SourcePackageHash
                && first.Hashes.GeometryBindingHash == second.Hashes.GeometryBindingHash
                && JsonSerializer.Serialize(first.GeometryBinding, MxFramework.Authoring.Cli.Program.CreateJsonOptions())
                    == JsonSerializer.Serialize(second.GeometryBinding, MxFramework.Authoring.Cli.Program.CreateJsonOptions()),
            "Current body-part-damage must compile to numeric v8 definitions and lookup with a v3 envelope and source content witness.");
        Require(first.SourceMappings.Exists(mapping =>
                mapping.SourcePath == "geometry/body_part_damage.json"
                && mapping.SourceObjectPath == "bodyPartDamage/partStates/partstate.alpha"
                && mapping.TargetPath == "bodyPartDamageDefinitions/0")
                && first.SourceMappings.Exists(mapping =>
                    mapping.SourcePath == "geometry/body_part_damage.json"
                    && mapping.SourceObjectPath.EndsWith("/memberPartIds/head", StringComparison.Ordinal)
                    && mapping.TargetPath == "bodyPartDamagePartLookup/head"),
            "compiled definitions and lookup must retain exact source mappings.");

        CharacterResourcePackage invalidPackage = LoadSample("character-codex-flow-test");
        invalidPackage.Geometry.BodyPartDamageDocumentState = CharacterBodyPartDamageDocumentState.Current;
        invalidPackage.Geometry.BodyPartDamage = DamageDocument();
        for (int i = 0; i < 9; i++)
        {
            invalidPackage.Geometry.BodyPartDamage.PartStates.Add(new CharacterBodyPartDamageAuthoring
            {
                PartStateId = "partstate.invalid." + i,
                MemberPartIds = new List<string> { i == 0 ? "unknown" : "torso" },
                DamageZoneId = "zone.unknown",
                WoundCapacity = CharacterBodyPartDamageLimits.MaximumAxisCapacity + 1,
                OpenDamageScaleMilli = 0,
                FocusConsumeTag = "damage.focus",
                MaxIntegrity = -1,
                IntegrityDamageScaleMilli = CharacterBodyPartDamageLimits.MaximumScaleMilli + 1,
                BreakReactionId = "Bad Reaction",
                AuthoredPriorityDeltaOpen = CharacterBodyPartDamageLimits.MaximumPriorityDelta + 1,
                AuthoredPriorityDeltaBroken = 0
            });
        }
        CharacterAuthoringValidationReport invalidReport =
            CharacterResourcePackageValidator.Validate(invalidPackage);
        string[] expectedCodes =
        {
            CharacterAuthoringValidationCodes.BodyPartDamageTooManyDefinitions,
            CharacterAuthoringValidationCodes.BodyPartDamageDuplicateMember,
            CharacterAuthoringValidationCodes.BodyPartDamageUnknownMember,
            CharacterAuthoringValidationCodes.BodyPartDamageUnknownZone,
            CharacterAuthoringValidationCodes.BodyPartDamageInvalidWound,
            CharacterAuthoringValidationCodes.BodyPartDamageInvalidStructure,
            CharacterAuthoringValidationCodes.BodyPartDamageInvalidPriority,
            CharacterAuthoringValidationCodes.BodyPartDamageInvalidFocusTag,
            CharacterAuthoringValidationCodes.BodyPartDamageInvalidBreakReaction
        };
        for (int i = 0; i < expectedCodes.Length; i++)
        {
            Require(HasValidationIssue(
                    invalidReport,
                    expectedCodes[i],
                    CharacterAuthoringValidationGate.ExportBlocked),
                "invalid body-part-damage must report typed blocker " + expectedCodes[i] + ".");
        }
        CharacterAuthoringCompileResult invalid = Compile(invalidPackage);
        Require(invalid.Status == CharacterAuthoringCompilerStatus.ExportBlocked
                && invalid.GeometryBinding.Format != CharacterAuthoringCompilerFormats.GeometryBindingV5
                && invalid.GeometryBinding.BodyPartDamageDefinitions == null,
            "invalid Current body-part-damage must fail closed without a partial v5 binding.");

        CharacterResourcePackage futurePackage = LoadSample("character-codex-flow-test");
        futurePackage.Geometry.BodyPartDamageDocumentState = CharacterBodyPartDamageDocumentState.Future;
        futurePackage.Geometry.BodyPartDamageRawPayload =
            "{\"format\":\"mx.characterBodyPartDamage.v2\",\"schemaVersion\":\"2.0\"}";
        CharacterAuthoringCompileResult future = Compile(futurePackage);
        Require(future.Status == CharacterAuthoringCompilerStatus.ExportBlocked
                && future.GeometryBinding.Format != CharacterAuthoringCompilerFormats.GeometryBindingV5
                && future.GeometryBinding.BodyPartDamageDefinitions == null,
            "future body-part-damage must fail closed without a partial v5 binding.");
    }

    private static void BodyPartDamage_SeverPolicyIsOptionalTypedAndRootSafe()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.Geometry.BodyPartDamageDocumentState = CharacterBodyPartDamageDocumentState.Current;
        package.Geometry.BodyPartDamage = DamageDocument(
            DamageDefinition(
                "partstate.root",
                new[] { "torso" },
                severable: true,
                severRequiredTag: "damage.guessed",
                presentationEventId: "bad_event"));

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);
        Require(HasValidationIssue(
                    report,
                    CharacterAuthoringValidationCodes.BodyPartDamageInvalidSeverPolicy,
                    CharacterAuthoringValidationGate.ExportBlocked)
                && HasValidationIssue(
                    report,
                    CharacterAuthoringValidationCodes.BodyPartDamageInvalidPresentationEvent,
                    CharacterAuthoringValidationGate.ExportBlocked)
                && HasValidationIssue(
                    report,
                    CharacterAuthoringValidationCodes.BodyPartDamageSeverRootPart,
                    CharacterAuthoringValidationGate.ExportBlocked),
            "sever authoring must fail closed on guessed tags, invalid events, and root body parts.");

        CharacterAuthoringCompileResult compiled = Compile(package);
        Require(compiled.Status == CharacterAuthoringCompilerStatus.ExportBlocked
                && compiled.GeometryBinding.BodyPartDamageDefinitions == null,
            "invalid sever authoring must not emit a partial runtime binding.");

        package.Geometry.BodyPartDamage = DamageDocument(
            DamageDefinition(
                "partstate.head",
                new[] { "head" },
                severable: true,
                severRequiredTag: "action.test.head.sever",
                presentationEventId: "event.test.shared.severed"),
            DamageDefinition(
                "partstate.hand",
                new[] { "hand_l" },
                severable: true,
                severRequiredTag: "action.test.hand.sever",
                presentationEventId: "event.test.shared.severed"));
        report = CharacterResourcePackageValidator.Validate(package);
        Require(HasValidationIssue(
                report,
                CharacterAuthoringValidationCodes.BodyPartDamageDuplicatePresentationEvent,
                CharacterAuthoringValidationGate.ExportBlocked),
            "presentation event identity must be unique across severable definitions.");
    }

    private static void BodyPartDamage_DedicatedSaveOwnsItsFileAndPreservesFutureBytes()
    {
        WithTemporaryPackage((tempRoot, relative, packagePath, options) =>
        {
            string target = Path.Combine(packagePath, "geometry", "body_part_damage.json");
            string beforeRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            EditorServer.SaveCharacterBodyPartDamageAtPath(
                packagePath,
                CurrentDamage(DamageDocument(
                    DamageDefinition("partstate.alpha", new[] { "head" }, "action.focus.head"))),
                beforeRevision,
                options);
            byte[] saved = File.ReadAllBytes(target);
            CharacterResourcePackage loaded = CharacterPackageCommands.ReadPackage(packagePath, options);
            Require(loaded.Geometry.BodyPartDamageDocumentState == CharacterBodyPartDamageDocumentState.Current
                    && loaded.Geometry.BodyPartDamage.PartStates.Count == 1,
                "dedicated save must create a canonical body_part_damage.json that package load can read.");

            string emptyRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            try
            {
                EditorServer.SaveCharacterBodyPartDamageAtPath(
                    packagePath,
                    CurrentDamage(DamageDocument()),
                    emptyRevision,
                    options);
                Require(false, "dedicated save must reject a Current body-part-damage document with no part states.");
            }
            catch (EditorServer.CharacterPackageValidationException exception)
            {
                Require(HasValidationIssue(
                        exception.Validation,
                        CharacterAuthoringValidationCodes.BodyPartDamageEmptyDefinitions,
                        CharacterAuthoringValidationGate.ExportBlocked),
                    "dedicated save must return the typed empty-definition blocker.");
            }
            Require(BytesEqual(saved, File.ReadAllBytes(target)),
                "rejected empty Current save must preserve the existing body_part_damage.json bytes.");

            string currentRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            loaded.Geometry.BodyPartDamage.Revision = 99;
            loaded.Geometry.BodyPartDamage.PartStates.Clear();
            EditorServer.SaveCharacterStudioPackage(tempRoot, relative, loaded, options, currentRevision);
            Require(BytesEqual(saved, File.ReadAllBytes(target)),
                "whole-package save must preserve body_part_damage.json bytes and cannot be a second writer.");

            const string future =
                "{\n  \"format\": \"mx.characterBodyPartDamage.v2\",\n  \"schemaVersion\": \"2.0\",\n  \"future\": true\n}\n";
            File.WriteAllText(target, future);
            string futureRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            try
            {
                EditorServer.SaveCharacterBodyPartDamageAtPath(
                    packagePath,
                    CurrentDamage(DamageDocument(
                        DamageDefinition("partstate.alpha", new[] { "head" }))),
                    futureRevision,
                    options);
                Require(false, "future body-part-damage must reject replacement by a current document.");
            }
            catch (EditorServer.CharacterPackageValidationException)
            {
                Require(File.ReadAllText(target) == future,
                    "rejected future-schema save must preserve exact source text.");
            }
        });
    }

    private static void BodyPartDamage_Dragon14OwnsFourStableGroups()
    {
        string packagePath = Path.Combine(
            FindRepoRoot(), "Authoring", "CharacterPackages", "character-000003");
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(
            packagePath,
            MxFramework.Authoring.Cli.Program.CreateJsonOptions());
        CharacterBodyPartDamageDocument document = package.Geometry.BodyPartDamage;
        Require(package.Geometry.BodyPartDamageDocumentState == CharacterBodyPartDamageDocumentState.Current
                && document != null && document.PartStates.Count == 4
                && document.PartStates[0].PartStateId == "partstate.000003.head"
                && document.PartStates[1].PartStateId == "partstate.000003.left_wing"
                && document.PartStates[2].PartStateId == "partstate.000003.right_wing"
                && document.PartStates[3].PartStateId == "partstate.000003.tail"
                && document.PartStates[3].MemberPartIds.Count == 3
                && document.PartStates[3].MemberPartIds.Contains("tail_base")
                && document.PartStates[3].MemberPartIds.Contains("tail_mid")
                && document.PartStates[3].MemberPartIds.Contains("tail_tip")
                && !document.PartStates[0].Severable
                && !document.PartStates[1].Severable
                && !document.PartStates[2].Severable
                && document.PartStates[3].Severable
                && document.PartStates[3].SeverRequiredTag == "action.000003.tail.sever"
                && document.PartStates[3].PresentationEventId == "event.000003.tail.severed"
                && document.PartStates.TrueForAll(definition =>
                    string.IsNullOrEmpty(definition.FocusConsumeTag)),
            "Dragon14 must author exactly head, left-wing, right-wing, and aggregate-tail groups with Focus consumption disabled.");
        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(
            package,
            new CharacterResourcePackageValidationOptions { PackageRootPath = packagePath });
        Require(!report.Issues.Exists(issue =>
                issue != null && issue.SourcePath == "geometry/body_part_damage.json"),
            "Dragon14 body-part-damage definitions must have no typed validation issue.");
    }

    private static CharacterBodyPartDamageDocument DamageDocument(
        params CharacterBodyPartDamageAuthoring[] definitions)
    {
        return new CharacterBodyPartDamageDocument
        {
            Revision = 7,
            PartStates = new List<CharacterBodyPartDamageAuthoring>(definitions)
        };
    }

    private static CharacterBodyPartDamageAuthoring DamageDefinition(
        string partStateId,
        string[] memberPartIds,
        string focusConsumeTag = "",
        bool severable = false,
        string severRequiredTag = "",
        string presentationEventId = "")
    {
        return new CharacterBodyPartDamageAuthoring
        {
            PartStateId = partStateId,
            MemberPartIds = new List<string>(memberPartIds),
            WoundCapacity = 300,
            OpenDamageScaleMilli = 1250,
            FocusConsumeTag = focusConsumeTag,
            MaxIntegrity = 500,
            IntegrityDamageScaleMilli = 1000,
            AuthoredPriorityDeltaOpen = 100,
            AuthoredPriorityDeltaBroken = 200,
            Severable = severable,
            SeverRequiredTag = severRequiredTag,
            PresentationEventId = presentationEventId
        };
    }

    private static CharacterBodyPartDamageLoadResult CurrentDamage(
        CharacterBodyPartDamageDocument document)
    {
        return new CharacterBodyPartDamageLoadResult
        {
            State = CharacterBodyPartDamageDocumentState.Current,
            Document = document
        };
    }
}
