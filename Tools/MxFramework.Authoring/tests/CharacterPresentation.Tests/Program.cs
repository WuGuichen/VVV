using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;
using MxFramework.CharacterApplication;

string root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
try
{
string source = Path.Combine(root, "Authoring/CharacterPackages/character-000001");
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
json.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
int count = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
CharacterResourcePackage Read() => CharacterPackageCommands.ReadPackage(source, json);
CharacterAuthoringCompileResult Compile(CharacterResourcePackage package)
{
    return CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest {
        Package = package, PackageRootPath = source,
        IdentityCatalog = CharacterContentIdentityCatalogSnapshot.LoadFromFile(Path.Combine(root, "Tools/RuntimeHash/character_definition_ids.json")),
        Options = new CharacterAuthoringCompileOptions { ValidateResourceFiles = true }
    });
}
bool Invalid(CharacterResourcePackage package) => CharacterResourcePackageValidator.Validate(package).Issues.Any(issue => issue.Code == "CHAR_PRESENTATION_IMAGE_INVALID");
var package = Read();
var images = package.ApplicationConfig.PresentationImages;
Check(images.PortraitResourceKey != images.StandingResourceKey, "Slots must remain independent.");
Check(!Invalid(package), "Prepared source must validate.");
var baseline = Compile(package);
Check(baseline.Status == CharacterAuthoringCompilerStatus.Ready, "Image source must compile.");
var presentation = baseline.GeneratedConfigPatch.Patch.Entries.Single(entry => entry.Source == "CharacterPresentationProfileConfig");
Check(presentation.Fields["PresentationImages"].Map["PortraitResourceKey"].Scalar == images.PortraitResourceKey, "Portrait key must be published.");
Check(presentation.Fields["PresentationImages"].Map["StandingResourceKey"].Scalar == images.StandingResourceKey, "Standing key must be published.");
images.Variants.Add(new CharacterPresentationImageVariant { VariantId = "appearance.test", PortraitResourceKey = images.PortraitResourceKey });
Check(images.ResolveResourceKey(true, "appearance.test") == images.StandingResourceKey, "Empty standing override inherits base.");
Check(images.ResolveResourceKey(false, "unknown") == images.PortraitResourceKey, "Unknown appearance uses base.");
var overridden = Compile(package);
Check(overridden.Hashes.SourcePackageHash != baseline.Hashes.SourcePackageHash && overridden.Hashes.GeneratedConfigHash != baseline.Hashes.GeneratedConfigHash, "Image edits must invalidate both publish hashes.");
var attributesBefore = JsonSerializer.Serialize(baseline.GeneratedConfigPatch.Patch.Entries.Single(entry => entry.Source == "CharacterAttributeProfileConfig"));
Check(JsonSerializer.Serialize(overridden.GeneratedConfigPatch.Patch.Entries.Single(entry => entry.Source == "CharacterAttributeProfileConfig")) == attributesBefore, "Image edits must not change tactical attributes.");
package.ApplicationConfig = JsonSerializer.Deserialize<CharacterApplicationAuthoringSummary>(JsonSerializer.Serialize(package.ApplicationConfig, json), json);
Check(Compile(package).Hashes.GeneratedConfigHash == overridden.Hashes.GeneratedConfigHash, "Save/reopen must preserve publish output.");
images = package.ApplicationConfig.PresentationImages;
package.ApplicationConfig.ResourceKeys = new List<string> { package.ResourceCatalog.Entries.First(entry => entry.Usage == "characterModel").ResourceKey };
var restricted = Compile(package).GeneratedConfigPatch.Patch.Entries.Single(entry => entry.Source == "CharacterPresentationProfileConfig");
Check(restricted.Fields["ResourceKeys"].List.Any(entry => entry.Map["Id"].Scalar == images.PortraitResourceKey && entry.Map["TypeId"].Scalar == "Texture2D"), "Images must remain referenced even with restricted model resource keys.");
var plan = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest {
    Package = package, PackageRootPath = source, AuthoringCompileResult = Compile(package), ValidateResourceFiles = true
});
Check(plan.RuntimeResourceCatalog.Entries.Any(entry => entry.Id == images.PortraitResourceKey && entry.Type == "Texture2D"), "Portrait must be loadable in the formal runtime catalog.");
Check(plan.RuntimeResourceCatalog.Entries.Any(entry => entry.Id == images.StandingResourceKey && entry.Type == "Texture2D"), "Standing must be loadable in the formal runtime catalog.");
Check(plan.RuntimeResourceCatalog.Entries.Where(entry => entry.Id == images.PortraitResourceKey || entry.Id == images.StandingResourceKey).All(entry => string.IsNullOrEmpty(entry.Variant)), "Simple image keys must load through the empty runtime variant.");
var portraitEntry = package.ResourceCatalog.Entries.Single(entry => entry.ResourceKey == images.PortraitResourceKey);
portraitEntry.Variant = "default"; Check(Invalid(package), "Qualified resource variants cannot silently fail simple image-key resolution."); portraitEntry.Variant = "";
images.SchemaVersion = "future"; Check(Invalid(package), "Unknown schema must block publication."); images.SchemaVersion = "1.0";
images.PortraitResourceKey = "missing.image"; Check(Invalid(package), "Missing key must block publication.");
images.PortraitResourceKey = package.ResourceCatalog.Entries.First(entry => entry.Usage == "previewThumbnail").ResourceKey;
Check(Invalid(package), "Model preview must not become a formal portrait.");
images.PortraitResourceKey = images.StandingResourceKey; Check(Invalid(package), "Slot usage mismatch must block publication.");
package = Read(); images = package.ApplicationConfig.PresentationImages;
images.Variants.Add(new() { VariantId = "appearance.same" }); images.Variants.Add(new() { VariantId = "appearance.same" });
Check(Invalid(package), "Duplicate appearance identities must block publication.");
images.Variants.Clear(); images.Variants.Add(new() { VariantId = "../invalid" }); Check(Invalid(package), "Variant identities cannot be filesystem paths.");
package.ApplicationConfig.PresentationImages = null;
Check(!Invalid(package), "Old packages must remain compatible with visible consumer fallback.");
// Exercise the actual Web save handler on an isolated package; never save the user's source during tests.
string fixtureRelative = ".agent/character-images/save-fixture-" + Guid.NewGuid().ToString("N");
string fixture = Path.Combine(root, fixtureRelative);
foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
{
    string target = Path.Combine(fixture, Path.GetRelativePath(source, file));
    Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
}
package = CharacterPackageCommands.ReadPackage(fixture, json);
package.ApplicationConfig.PresentationImages.Variants.Add(new() { VariantId = "appearance.web", StandingResourceKey = "" });
EditorServer.SaveCharacterStudioPackage(root, fixtureRelative, package, json);
var reopened = CharacterPackageCommands.ReadPackage(fixture, json);
Check(reopened.ApplicationConfig.PresentationImages.ResolveResourceKey(true, "appearance.web") == images.StandingResourceKey
    && reopened.ApplicationConfig.PresentationImages.Variants.Single().VariantId == "appearance.web", "Actual Web save handler must persist image keys and variant inheritance.");
string savedConfig = File.ReadAllText(Path.Combine(fixture, "config/character_application.json"));
reopened.ApplicationConfig.PresentationImages.PortraitResourceKey = "missing.image";
bool rejected = false;
try { EditorServer.SaveCharacterStudioPackage(root, fixtureRelative, reopened, json); }
catch (EditorServer.CharacterPackageValidationException) { rejected = true; }
Check(rejected && File.ReadAllText(Path.Combine(fixture, "config/character_application.json")) == savedConfig, "Web save must reject invalid image references without changing the persisted config.");
Console.WriteLine($"Character presentation contract: {count} assertions passed.");
}
catch (Exception error)
{
    Console.Error.WriteLine(error.ToString());
    Environment.ExitCode = 1;
}
