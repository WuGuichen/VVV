param(
    [Parameter(Mandatory = $false)]
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required migration input is missing: $Path"
    }
    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Write-JsonAtomic([string]$Path, [object]$Value) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Migration target escapes project root: $fullPath"
    }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($fullPath)) | Out-Null
    $tempPath = $fullPath + '.tmp.' + [Guid]::NewGuid().ToString('N')
    try {
        $json = ($Value | ConvertTo-Json -Depth 100) + "`n"
        [IO.File]::WriteAllText($tempPath, $json, $utf8NoBom)
        Move-Item -LiteralPath $tempPath -Destination $fullPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $tempPath) {
            Remove-Item -LiteralPath $tempPath -Force
        }
    }
}

function New-ActionRef([string]$Role, [string]$ActionId) {
    return [ordered]@{ actionRole = $Role; actionId = $ActionId }
}

function New-CommandSequence([string]$IntentId, [string[]]$Roles, [bool]$WrapAfterLast) {
    return [ordered]@{
        intentId = $IntentId
        actionRoles = @($Roles)
        wrapAfterLast = $WrapAfterLast
        priority = 10
        allowQueue = $false
        queueWindowFrames = 0
    }
}

function Normalize-JsonSemanticValue([object]$Value) {
    if ($null -eq $Value) { return $null }
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $result = [ordered]@{}
        foreach ($property in @($Value.PSObject.Properties | Sort-Object Name)) {
            $result[$property.Name] = Normalize-JsonSemanticValue $property.Value
        }
        return $result
    }
    if ($Value -is [System.Collections.IDictionary]) {
        $result = [ordered]@{}
        foreach ($key in @($Value.Keys | Sort-Object)) {
            $result[[string]$key] = Normalize-JsonSemanticValue $Value[$key]
        }
        return $result
    }
    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        return @($Value | ForEach-Object { Normalize-JsonSemanticValue $_ })
    }
    if ($Value -is [byte] -or $Value -is [sbyte] -or
        $Value -is [int16] -or $Value -is [uint16] -or
        $Value -is [int32] -or $Value -is [uint32] -or
        $Value -is [int64] -or $Value -is [uint64] -or
        $Value -is [single] -or $Value -is [double] -or $Value -is [decimal]) {
        return [double]$Value
    }
    return $Value
}

function Get-ActionSemanticHash([object]$Action, [string[]]$IgnoredTags = @()) {
    $clone = $Action | ConvertTo-Json -Depth 100 | ConvertFrom-Json
    if ($IgnoredTags.Count -gt 0 -and $clone.PSObject.Properties['tags']) {
        $clone.tags = @($clone.tags | Where-Object { $_ -notin $IgnoredTags })
    }
    $canonical = (Normalize-JsonSemanticValue $clone) | ConvertTo-Json -Depth 100 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($canonical)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return 'sha256:' + ([BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant())
    }
    finally {
        $sha.Dispose()
    }
}

function Get-JsonSemanticHash([object]$Value) {
    $canonical = (Normalize-JsonSemanticValue $Value) | ConvertTo-Json -Depth 100 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($canonical)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return 'sha256:' + ([BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant())
    }
    finally {
        $sha.Dispose()
    }
}

function Assert-ActionMigration([object[]]$OldActions, [object[]]$NewActions, [string[]]$ExpectedAnimationOnlyIds = @()) {
    $newById = @{}
    foreach ($action in @($NewActions)) { $newById[[string]$action.stableId] = $action }
    foreach ($oldAction in @($OldActions)) {
        $id = [string]$oldAction.stableId
        if (-not $newById.ContainsKey($id)) { throw "Migrated action is missing: $id" }
        $ignoredTags = if ($id -in $ExpectedAnimationOnlyIds) { @('animation_only') } else { @() }
        $oldHash = Get-ActionSemanticHash $oldAction $ignoredTags
        $newHash = Get-ActionSemanticHash $newById[$id] $ignoredTags
        if ($oldHash -ne $newHash) {
            throw "Migrated action semantic hash changed unexpectedly: $id old=$oldHash new=$newHash"
        }
        if ($id -in $ExpectedAnimationOnlyIds -and 'animation_only' -notin @($newById[$id].tags)) {
            throw "Animation-only migration marker is missing: $id"
        }
    }
}

$commonPath = Join-Path $ProjectRoot 'CharacterActionSource\Libraries\Common\character_actions.json'
$legacyCodexPath = Join-Path $ProjectRoot 'Assets\MxFrameworkGenerated\CharacterActions\Libraries\codex_flow_test\character_actions.json'
$legacyCharacterPath = Join-Path $ProjectRoot 'Assets\MxFrameworkGenerated\CharacterActions\Libraries\combat_girl_sword\character_actions.json'
$characterPath = Join-Path $ProjectRoot 'Authoring\CharacterPackages\character-combat-girl-sword\config\character_actions.json'
$weaponPath = Join-Path $ProjectRoot 'WeaponSource\global_weapon_library.json'
$applicationPath = Join-Path $ProjectRoot 'Authoring\CharacterPackages\character-combat-girl-sword\config\character_application.json'
$manifestPath = Join-Path $ProjectRoot 'Authoring\CharacterPackages\character-combat-girl-sword\manifest.json'
$codexPackagePath = Join-Path $ProjectRoot 'Tools\MxFramework.Authoring\samples\character-codex-flow-test'
$codexApplicationPath = Join-Path $codexPackagePath 'config\character_application.json'
$codexCharacterActionsPath = Join-Path $codexPackagePath 'config\character_actions.json'
$codexManifestPath = Join-Path $codexPackagePath 'manifest.json'

$common = Read-Json $commonPath
$legacyCodex = Read-Json $legacyCodexPath
$knownActionIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($action in @($common.actions)) {
    if (-not $knownActionIds.Add([string]$action.stableId)) {
        throw "Common action stableId is duplicated: $($action.stableId)"
    }
}
foreach ($action in @($legacyCodex.actions)) {
    if ($knownActionIds.Add([string]$action.stableId)) {
        $common.actions += $action
    }
}
$common.schemaVersion = '0.2.0'
Assert-ActionMigration @((Read-Json $commonPath).actions) @($common.actions)
Write-JsonAtomic $commonPath $common

$character = Read-Json $characterPath
$characterMigrationSourceActions = @($character.actions)
if ($characterMigrationSourceActions.Count -eq 0) {
    $legacyCharacter = Read-Json $legacyCharacterPath
    $characterMigrationSourceActions = @($legacyCharacter.actions)
    $character.actions = @($characterMigrationSourceActions)
}
if (@($character.animationSources).Count -eq 0) {
    $character.animationSources = @(
        [ordered]@{
            resourceKeyPrefix = 'art.character.combat_girl_sword.animation.'
            rigType = 'Humanoid'
            skeletonProfileId = 'skeleton.combat_girl_sword.humanoid'
            avatarProfileId = 'avatar.combat_girl_sword.humanoid'
            allowRetargeting = $false
        }
    )
}
foreach ($action in @($character.actions)) {
    if ([string]$action.stableId -in @(
        'combat_girl_sword.sword_empty.primary',
        'combat_girl_sword.sword_empty.primary.combo2',
        'combat_girl_sword.sword_empty.heavy')) {
        $tags = @($action.tags | Where-Object { $_ -ne 'animation_only' })
        $action.tags = @($tags + 'animation_only')
    }
}
$character.schemaVersion = '0.2.0'
Assert-ActionMigration $characterMigrationSourceActions @($character.actions) @(
    'combat_girl_sword.sword_empty.primary',
    'combat_girl_sword.sword_empty.primary.combo2',
    'combat_girl_sword.sword_empty.heavy'
)
Write-JsonAtomic $characterPath $character

$weapon = Read-Json $weaponPath
$weapon.schemaVersion = '6.0'
foreach ($combination in @($weapon.combinedWeaponConfigurations)) {
    $configuration = $combination.actionConfiguration
    $refs = @()
    foreach ($entry in @($configuration.actions)) {
        $refs += New-ActionRef ([string]$entry.actionRole) ([string]$entry.actionId)
    }
    $configuration.PSObject.Properties.Remove('actionSetId')
    $configuration.PSObject.Properties.Remove('actionSet')
    $configuration.actions = @($refs)
    if ($configuration.PSObject.Properties['commandSequences']) {
        $configuration.commandSequences = @()
    }
    else {
        $configuration | Add-Member -NotePropertyName commandSequences -NotePropertyValue @()
    }

    if ($combination.combinedWeaponConfigurationId -eq 'combat_girl_sword.sword_empty') {
        $combination.reactionConfigurationId = 'reaction.combat_girl_sword.default'
        $configuration.actions = @(
            (New-ActionRef 'primary' 'light_attack'),
            (New-ActionRef 'primary.combo2' 'light_attack_2'),
            (New-ActionRef 'primary.combo3' 'light_attack_3'),
            (New-ActionRef 'reaction.death' 'combat_girl_sword_death_react'),
            (New-ActionRef 'reaction.stun' 'combat_girl_sword_cohesion_break_react'),
            (New-ActionRef 'reaction.hit_right' 'combat_girl_sword_hit_normal_high_react'),
            (New-ActionRef 'reaction.hit_default' 'combat_girl_sword_hit_normal_mid_react')
        )
        $configuration.commandSequences = @(
            (New-CommandSequence 'LightAttack' @('primary', 'primary.combo2', 'primary.combo3') $true)
        )
        $configuration.abilityActionBindings = @()
        $configuration.reactionActionBindings = @(
            [ordered]@{ reactionSlotId = 'death'; actionRole = 'reaction.death'; actionId = 'combat_girl_sword_death_react' },
            [ordered]@{ reactionSlotId = 'stun'; actionRole = 'reaction.stun'; actionId = 'combat_girl_sword_cohesion_break_react' },
            [ordered]@{ reactionSlotId = 'hit_right'; actionRole = 'reaction.hit_right'; actionId = 'combat_girl_sword_hit_normal_high_react' },
            [ordered]@{ reactionSlotId = 'hit_default'; actionRole = 'reaction.hit_default'; actionId = 'combat_girl_sword_hit_normal_mid_react' }
        )
    }
    elseif ($combination.combinedWeaponConfigurationId -in @(
        'codex_flow_test.sword_empty',
        'codex_flow_test.empty_empty')) {
        $existingActions = @($configuration.actions)
        $nonPrimaryActions = @($existingActions | Where-Object {
            [string]$_.actionRole -notin @('primary', 'primary.combo2', 'primary.combo3')
        })
        $configuration.actions = @(
            (New-ActionRef 'primary' 'light_attack'),
            (New-ActionRef 'primary.combo2' 'light_attack_2'),
            (New-ActionRef 'primary.combo3' 'light_attack_3')
        ) + $nonPrimaryActions
        $configuration.commandSequences = @(
            (New-CommandSequence 'LightAttack' @('primary', 'primary.combo2', 'primary.combo3') $true)
        )
    }
}
Write-JsonAtomic $weaponPath $weapon

$application = Read-Json $applicationPath
$application.schemaVersion = '8.0'
$binding = @($application.combinedWeaponConfigurationBindings | Where-Object {
    $_.combinedWeaponConfigurationId -eq 'combat_girl_sword.sword_empty'
})[0]
if ($null -eq $binding) {
    throw 'Combat Girl default combined weapon binding is missing.'
}
$binding.overrides.animationConfigurationId = ''
$binding.overrides.abilityConfigurationId = ''
$binding.overrides.reactionConfigurationId = ''
$binding.overrides.actionConfiguration = [ordered]@{
    actions = @(
        (New-ActionRef 'primary' 'light_attack'),
        (New-ActionRef 'primary.combo2' 'light_attack_2'),
        (New-ActionRef 'primary.combo3' 'light_attack_3'),
        (New-ActionRef 'heavy' 'combat_girl_sword.sword_empty.heavy'),
        (New-ActionRef 'evade' 'combat_girl_sword.sword_empty.evade'),
        (New-ActionRef 'reaction.death' 'combat_girl_sword.sword_empty.reaction.death'),
        (New-ActionRef 'reaction.stun' 'combat_girl_sword.sword_empty.reaction.stun'),
        (New-ActionRef 'reaction.hit_right' 'combat_girl_sword.sword_empty.reaction.hit_right'),
        (New-ActionRef 'reaction.hit_default' 'combat_girl_sword.sword_empty.reaction.hit_default')
    )
    commandSequences = @(
        (New-CommandSequence 'LightAttack' @('primary', 'primary.combo2', 'primary.combo3') $true)
    )
    abilityActionBindings = @()
    reactionActionBindings = @(
        [ordered]@{ reactionSlotId = 'death'; actionRole = 'reaction.death'; actionId = 'combat_girl_sword.sword_empty.reaction.death' },
        [ordered]@{ reactionSlotId = 'stun'; actionRole = 'reaction.stun'; actionId = 'combat_girl_sword.sword_empty.reaction.stun' },
        [ordered]@{ reactionSlotId = 'hit_right'; actionRole = 'reaction.hit_right'; actionId = 'combat_girl_sword.sword_empty.reaction.hit_right' },
        [ordered]@{ reactionSlotId = 'hit_default'; actionRole = 'reaction.hit_default'; actionId = 'combat_girl_sword.sword_empty.reaction.hit_default' }
    )
}
Write-JsonAtomic $applicationPath $application

$manifest = Read-Json $manifestPath
$manifest.packageSchemaVersion = '8.0'
$manifest.sourceSchemaVersion = '8.0'
$manifest.authoringSchemaVersion = '8.0'
if ('config/character_actions.json' -notin @($manifest.configFiles)) {
    $manifest.configFiles += 'config/character_actions.json'
}
Write-JsonAtomic $manifestPath $manifest

$codexApplication = Read-Json $codexApplicationPath
$codexApplication.schemaVersion = '8.0'
Write-JsonAtomic $codexApplicationPath $codexApplication

if (-not (Test-Path -LiteralPath $codexCharacterActionsPath)) {
    Write-JsonAtomic $codexCharacterActionsPath ([ordered]@{
        schemaVersion = '0.2.0'
        actions = @()
    })
}

$codexManifest = Read-Json $codexManifestPath
$codexManifest.packageSchemaVersion = '8.0'
$codexManifest.sourceSchemaVersion = '8.0'
$codexManifest.authoringSchemaVersion = '8.0'
if ('config/character_actions.json' -notin @($codexManifest.configFiles)) {
    $codexManifest.configFiles += 'config/character_actions.json'
}
Write-JsonAtomic $codexManifestPath $codexManifest

foreach ($packageId in @('combat_girl_sword', 'codex_flow_test')) {
    $profilePath = Join-Path $ProjectRoot "Assets\MxFrameworkGenerated\CharacterActions\Profiles\$packageId\character_action_profile.json"
    $actionSetPath = Join-Path $ProjectRoot "Assets\MxFrameworkGenerated\CharacterActions\ActionSets\$packageId\character_action_sets.json"
    if (Test-Path -LiteralPath $profilePath) {
        Write-Output "$packageId Profile semanticHash=$(Get-JsonSemanticHash (Read-Json $profilePath))"
    }
    if (Test-Path -LiteralPath $actionSetPath) {
        Write-Output "$packageId ActionSet semanticHash=$(Get-JsonSemanticHash (Read-Json $actionSetPath))"
    }
}

Write-Output 'CharacterAction source migration to Global Weapon 6.0 / Character Application 8.0 completed.'
