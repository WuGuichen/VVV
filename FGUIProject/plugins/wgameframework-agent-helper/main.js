const FairyEditor = CS.FairyEditor;
const System = CS.System;

const PLUGIN_MENU = "wgameframework.agent";
const MENU_CREATE_SMOKE = "wgameframework.agent.createSmoke";
const MENU_PUBLISH_SMOKE = "wgameframework.agent.publishSmoke";
const MENU_CREATE_RUNTIME_HUD = "wgameframework.agent.createRuntimeHud";
const MENU_PUBLISH_RUNTIME_HUD = "wgameframework.agent.publishRuntimeHud";
const MENU_REFRESH = "wgameframework.agent.refresh";

const SMOKE_PACKAGE = {
    name: "MxFguiSmoke",
    id: "mxfgui0",
    componentName: "SmokePanel",
    componentId: "smkp0",
    files: [
        {
            path: "package.xml",
            content: `<?xml version="1.0" encoding="utf-8"?>
<packageDescription id="mxfgui0">
  <resources>
    <component id="smkp0" name="SmokePanel.xml" path="/" exported="true"/>
  </resources>
  <publish name=""/>
</packageDescription>
`
        },
        {
            path: "SmokePanel.xml",
            content: `<?xml version="1.0" encoding="utf-8"?>
<component size="320,160" opaque="false">
  <displayList>
    <text id="n0_smkp" name="txtTitle" xy="32,48" size="256,48" fontSize="28" align="center" autoSize="none" text="MxFguiSmoke"/>
  </displayList>
</component>
`
        }
    ]
};

const RUNTIME_HUD_PACKAGE = {
    name: "MxRuntimeHud",
    id: "mxrhud0",
    componentName: "RuntimeHudPanel",
    componentId: "rhud0",
    files: [
        {
            path: "package.xml",
            content: `<?xml version="1.0" encoding="utf-8"?>
<packageDescription id="mxrhud0">
  <resources>
    <component id="rhud0" name="RuntimeHudPanel.xml" path="/" exported="true"/>
    <component id="rhudb" name="RuntimeHudButton.xml" path="/Components/" exported="false"/>
  </resources>
  <publish name=""/>
</packageDescription>
`
        },
        {
            path: "RuntimeHudPanel.xml",
            content: `<?xml version="1.0" encoding="utf-8"?>
<component size="640,360" opaque="false">
  <displayList>
    <graph id="n0_rhud" name="panelBg" xy="0,0" size="640,360" type="rect" fillColor="#cc111820" corner="12,12,12,12"/>
    <graph id="n1_rhud" name="headerBg" xy="16,16" size="608,54" type="rect" fillColor="#ff1f3342" corner="8,8,8,8"/>
    <text id="n2_rhud" name="title" xy="32,24" size="360,30" fontSize="24" color="#ffffff" align="left" vAlign="middle" autoSize="none" singleLine="true" text="Runtime HUD"/>
    <text id="n3_rhud" name="mode" xy="432,25" size="176,28" fontSize="18" color="#88d8ff" align="right" vAlign="middle" autoSize="none" singleLine="true" text="Ability Slice"/>
    <graph id="n4_rhud" name="playerBg" xy="24,92" size="280,112" type="rect" fillColor="#dd183326" corner="8,8,8,8"/>
    <text id="n5_rhud" name="playerName" xy="40,106" size="248,26" fontSize="20" color="#ffffff" autoSize="none" singleLine="true" text="Player"/>
    <graph id="n6_rhud" name="playerHpTrack" xy="40,146" size="248,22" type="rect" fillColor="#ff243a34" corner="6,6,6,6"/>
    <graph id="n7_rhud" name="playerHpFill" xy="42,148" size="196,18" type="rect" fillColor="#ff43d17a" corner="5,5,5,5"/>
    <text id="n8_rhud" name="playerHp" xy="40,174" size="248,22" fontSize="16" color="#c9f7d8" align="right" autoSize="none" singleLine="true" text="HP 100/100"/>
    <graph id="n9_rhud" name="enemyBg" xy="336,92" size="280,112" type="rect" fillColor="#dd351d24" corner="8,8,8,8"/>
    <text id="n10_rhud" name="enemyName" xy="352,106" size="248,26" fontSize="20" color="#ffffff" autoSize="none" singleLine="true" text="Enemy"/>
    <graph id="n11_rhud" name="enemyHpTrack" xy="352,146" size="248,22" type="rect" fillColor="#ff462b32" corner="6,6,6,6"/>
    <graph id="n12_rhud" name="enemyHpFill" xy="354,148" size="150,18" type="rect" fillColor="#ffff667a" corner="5,5,5,5"/>
    <text id="n13_rhud" name="enemyHp" xy="352,174" size="248,22" fontSize="16" color="#ffd0d8" align="right" autoSize="none" singleLine="true" text="HP 75/100"/>
    <graph id="n14_rhud" name="actionBg" xy="24,228" size="592,48" type="rect" fillColor="#dd202a36" corner="8,8,8,8"/>
    <text id="n15_rhud" name="recentAction" xy="40,240" size="560,24" fontSize="17" color="#f5e9b8" autoSize="none" singleLine="true" text="Recent action: ready"/>
    <component id="n16_rhud" name="btnStrike" src="rhudb" fileName="Components/RuntimeHudButton.xml" xy="352,296">
      <Button title="Strike"/>
    </component>
    <component id="n17_rhud" name="btnReset" src="rhudb" fileName="Components/RuntimeHudButton.xml" xy="496,296">
      <Button title="Reset"/>
    </component>
  </displayList>
</component>
`
        },
        {
            path: "Components/RuntimeHudButton.xml",
            content: `<?xml version="1.0" encoding="utf-8"?>
<component size="120,40" extention="Button" initName="btn">
  <controller name="button" pages="0,up,1,down,2,over,3,selectedOver" selected="0"/>
  <displayList>
    <graph id="n0_rhdb" name="up" xy="0,0" size="120,40" type="rect" fillColor="#ff2f6f88" corner="8,8,8,8">
      <gearDisplay controller="button" pages="0"/>
      <relation target="" sidePair="width-width,height-height"/>
    </graph>
    <graph id="n1_rhdb" name="over" xy="0,0" size="120,40" type="rect" fillColor="#ff3f8da9" corner="8,8,8,8">
      <gearDisplay controller="button" pages="2"/>
      <relation target="" sidePair="width-width,height-height"/>
    </graph>
    <graph id="n2_rhdb" name="down" xy="0,0" size="120,40" type="rect" fillColor="#ff24576c" corner="8,8,8,8">
      <gearDisplay controller="button" pages="1,3"/>
      <relation target="" sidePair="width-width,height-height"/>
    </graph>
    <text id="n3_rhdb" name="title" xy="0,0" size="120,40" fontSize="18" color="#ffffff" align="center" vAlign="middle" autoSize="none" singleLine="true" bold="true" autoClearText="true" text="Button">
      <relation target="" sidePair="width-width,height-height"/>
    </text>
  </displayList>
  <Button/>
</component>
`
        }
    ]
};

function combinePath(left, right) {
    return System.IO.Path.Combine(left, right);
}

function writeText(path, content) {
    const dir = System.IO.Path.GetDirectoryName(path);
    if (!System.IO.Directory.Exists(dir))
        System.IO.Directory.CreateDirectory(dir);
    System.IO.File.WriteAllText(path, content, System.Text.Encoding.UTF8);
}

function ensurePackageFiles(descriptor) {
    const project = FairyEditor.App.project;
    if (!project || !project.opened)
        throw new Error("No FairyGUI project is open.");

    const packageDir = combinePath(project.assetsPath, descriptor.name);
    if (!System.IO.Directory.Exists(packageDir))
        System.IO.Directory.CreateDirectory(packageDir);

    descriptor.files.forEach(file => writeText(combinePath(packageDir, file.path), file.content));
}

function ensurePackage(descriptor) {
    ensurePackageFiles(descriptor);

    const project = FairyEditor.App.project;
    FairyEditor.App.RefreshProject();

    let pkg = project.GetPackageByName(descriptor.name);
    if (!pkg)
        pkg = project.AddPackage(combinePath(project.assetsPath, descriptor.name));

    if (pkg) {
        pkg.EnsureOpen();
        pkg.SetChanged();
        pkg.Save();
    }

    FairyEditor.App.RefreshProject();
    console.log(`[WGameFramework] Ensured ${descriptor.name}/${descriptor.componentName}.`);
    FairyEditor.App.Alert(`Ensured ${descriptor.name}/${descriptor.componentName}.`);
}

function ensureSmokePackage() {
    ensurePackage(SMOKE_PACKAGE);
}

function ensureRuntimeHudPackage() {
    ensurePackage(RUNTIME_HUD_PACKAGE);
}

async function publishPackage(descriptor) {
    const project = FairyEditor.App.project;
    if (!project || !project.opened)
        throw new Error("No FairyGUI project is open.");

    let pkg = project.GetPackageByName(descriptor.name);
    if (!pkg) {
        ensurePackageFiles(descriptor);
        FairyEditor.App.RefreshProject();
        pkg = project.GetPackageByName(descriptor.name) || project.AddPackage(combinePath(project.assetsPath, descriptor.name));
    }

    if (!pkg)
        throw new Error(`Package ${descriptor.name} is not available after refresh.`);

    pkg.EnsureOpen();
    pkg.Save();

    const branch = project.activeBranch || "";
    const handler = new FairyEditor.PublishHandler(pkg, branch);
    handler.genCode = false;
    await puerts.$promise(handler.Run());

    if (!handler.isSuccess)
        throw new Error(`Publish failed for ${descriptor.name}.`);

    console.log(`[WGameFramework] Published ${descriptor.name} to ${handler.exportPath}.`);
    FairyEditor.App.Alert(`Published ${descriptor.name}.`);
}

async function publishSmokePackage() {
    await publishPackage(SMOKE_PACKAGE);
}

async function publishRuntimeHudPackage() {
    await publishPackage(RUNTIME_HUD_PACKAGE);
}

async function runBatchCommand(done, arg) {
    const command = arg || "";
    try {
        if (command === "create-smoke") {
            ensureSmokePackage();
        } else if (command === "publish-smoke") {
            await publishSmokePackage();
        } else if (command === "create-runtime-hud") {
            ensureRuntimeHudPackage();
        } else if (command === "publish-runtime-hud") {
            await publishRuntimeHudPackage();
        } else if (command === "refresh") {
            FairyEditor.App.RefreshProject();
        } else {
            throw new Error(`Unknown WGameFramework batch command: ${command}`);
        }
    } finally {
        done();
    }
}

function runGuarded(action) {
    Promise.resolve()
        .then(action)
        .catch(err => {
            const message = err && err.stack ? err.stack : String(err);
            console.error(`[WGameFramework] ${message}`);
            FairyEditor.App.Alert(message);
        });
}

function installMenu() {
    const root = FairyEditor.App.menu;
    try {
        root.RemoveItem(PLUGIN_MENU);
    } catch (_) {
        // FairyGUI throws in some builds when the item is absent; reload should still be idempotent.
    }

    root.AddItem("WGameFramework", PLUGIN_MENU, -1, true, null);
    const menu = root.GetSubMenu(PLUGIN_MENU);
    menu.AddItem("Create/Repair Smoke Package", MENU_CREATE_SMOKE, () => runGuarded(ensureSmokePackage));
    menu.AddItem("Publish Smoke Package", MENU_PUBLISH_SMOKE, () => runGuarded(publishSmokePackage));
    menu.AddItem("Create/Repair Runtime HUD Package", MENU_CREATE_RUNTIME_HUD, () => runGuarded(ensureRuntimeHudPackage));
    menu.AddItem("Publish Runtime HUD Package", MENU_PUBLISH_RUNTIME_HUD, () => runGuarded(publishRuntimeHudPackage));
    menu.AddItem("Refresh Project", MENU_REFRESH, () => runGuarded(() => FairyEditor.App.RefreshProject()));
}

installMenu();

function onDestroy() {
    const root = FairyEditor.App.menu;
    root.RemoveItem(PLUGIN_MENU);
}

function onRunBatchModeScript(done, arg) {
    runBatchCommand(done, arg).catch(err => {
        const message = err && err.stack ? err.stack : String(err);
        console.error(`[WGameFramework] ${message}`);
        done();
    });
}

module.exports = {
    onDestroy,
    onRunBatchModeScript
};
