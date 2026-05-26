const FairyEditor = CS.FairyEditor;
const System = CS.System;

const PACKAGE_NAME = "MxFguiSmoke";
const PACKAGE_ID = "mxfgui0";
const COMPONENT_NAME = "SmokePanel";
const COMPONENT_ID = "smkp0";
const PLUGIN_MENU = "wgameframework.agent";
const MENU_CREATE_SMOKE = "wgameframework.agent.createSmoke";
const MENU_PUBLISH_SMOKE = "wgameframework.agent.publishSmoke";
const MENU_REFRESH = "wgameframework.agent.refresh";

function combinePath(left, right) {
    return System.IO.Path.Combine(left, right);
}

function writeText(path, content) {
    const dir = System.IO.Path.GetDirectoryName(path);
    if (!System.IO.Directory.Exists(dir))
        System.IO.Directory.CreateDirectory(dir);
    System.IO.File.WriteAllText(path, content, System.Text.Encoding.UTF8);
}

function ensureSmokePackageFiles() {
    const project = FairyEditor.App.project;
    if (!project || !project.opened)
        throw new Error("No FairyGUI project is open.");

    const packageDir = combinePath(project.assetsPath, PACKAGE_NAME);
    if (!System.IO.Directory.Exists(packageDir))
        System.IO.Directory.CreateDirectory(packageDir);

    writeText(
        combinePath(packageDir, "package.xml"),
        `<?xml version="1.0" encoding="utf-8"?>
<packageDescription id="${PACKAGE_ID}">
  <resources>
    <component id="${COMPONENT_ID}" name="${COMPONENT_NAME}.xml" path="/" exported="true"/>
  </resources>
  <publish name=""/>
</packageDescription>
`
    );

    writeText(
        combinePath(packageDir, `${COMPONENT_NAME}.xml`),
        `<?xml version="1.0" encoding="utf-8"?>
<component size="320,160" opaque="false">
  <displayList>
    <text id="n0_smkp" name="txtTitle" xy="32,48" size="256,48" fontSize="28" align="center" autoSize="none" text="MxFguiSmoke"/>
  </displayList>
</component>
`
    );
}

function ensureSmokePackage() {
    ensureSmokePackageFiles();

    const project = FairyEditor.App.project;
    FairyEditor.App.RefreshProject();

    let pkg = project.GetPackageByName(PACKAGE_NAME);
    if (!pkg)
        pkg = project.AddPackage(combinePath(project.assetsPath, PACKAGE_NAME));

    if (pkg) {
        pkg.EnsureOpen();
        pkg.SetChanged();
        pkg.Save();
    }

    FairyEditor.App.RefreshProject();
    console.log(`[WGameFramework] Ensured ${PACKAGE_NAME}/${COMPONENT_NAME}.`);
    FairyEditor.App.Alert(`Ensured ${PACKAGE_NAME}/${COMPONENT_NAME}.`);
}

async function publishSmokePackage() {
    const project = FairyEditor.App.project;
    if (!project || !project.opened)
        throw new Error("No FairyGUI project is open.");

    let pkg = project.GetPackageByName(PACKAGE_NAME);
    if (!pkg) {
        ensureSmokePackageFiles();
        FairyEditor.App.RefreshProject();
        pkg = project.GetPackageByName(PACKAGE_NAME) || project.AddPackage(combinePath(project.assetsPath, PACKAGE_NAME));
    }

    if (!pkg)
        throw new Error(`Package ${PACKAGE_NAME} is not available after refresh.`);

    pkg.EnsureOpen();
    pkg.Save();

    const branch = project.activeBranch || "";
    const handler = new FairyEditor.PublishHandler(pkg, branch);
    handler.genCode = false;
    await puerts.$promise(handler.Run());

    if (!handler.isSuccess)
        throw new Error(`Publish failed for ${PACKAGE_NAME}.`);

    console.log(`[WGameFramework] Published ${PACKAGE_NAME} to ${handler.exportPath}.`);
    FairyEditor.App.Alert(`Published ${PACKAGE_NAME}.`);
}

async function runBatchCommand(done, arg) {
    const command = arg || "";
    try {
        if (command === "create-smoke") {
            ensureSmokePackage();
        } else if (command === "publish-smoke") {
            await publishSmokePackage();
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
    root.AddItem("WGameFramework", PLUGIN_MENU, -1, true, null);
    const menu = root.GetSubMenu(PLUGIN_MENU);
    menu.AddItem("Create/Repair Smoke Package", MENU_CREATE_SMOKE, () => runGuarded(ensureSmokePackage));
    menu.AddItem("Publish Smoke Package", MENU_PUBLISH_SMOKE, () => runGuarded(publishSmokePackage));
    menu.AddItem("Refresh Project", MENU_REFRESH, () => runGuarded(() => FairyEditor.App.RefreshProject()));
}

installMenu();

export function onDestroy() {
    const root = FairyEditor.App.menu;
    root.RemoveItem(PLUGIN_MENU);
}

export function onRunBatchModeScript(done, arg) {
    runBatchCommand(done, arg).catch(err => {
        const message = err && err.stack ? err.stack : String(err);
        console.error(`[WGameFramework] ${message}`);
        done();
    });
}
