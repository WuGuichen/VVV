(function () {
    var WAV_PATH = "../Audio/mxframework_demo_beep.wav";
    var BANK_NAME = "Master";
    var EVENT_FOLDER_PATH = "MxFramework/Demo";
    var EVENT_NAMES = ["OneShot", "Loop"];

    function ensureFolder(parent, name, entityName) {
        var existing = parent.getItem ? parent.getItem(name) : null;
        if (existing) {
            return existing;
        }

        var folder = studio.project.create(entityName);
        folder.name = name;
        folder.folder = parent;
        return folder;
    }

    function ensureEventFolderPath(path) {
        var parent = studio.project.workspace.masterEventFolder;
        var parts = path.split("/");
        for (var i = 0; i < parts.length; i++) {
            parent = ensureFolder(parent, parts[i], "EventFolder");
        }

        return parent;
    }

    function findOrCreateMasterBank() {
        var bank = studio.project.lookup("bank:/" + BANK_NAME);
        if (bank) {
            return bank;
        }

        var banks = studio.project.model.Bank.findInstances();
        for (var i = 0; i < banks.length; i++) {
            if (banks[i].isMasterBank || banks[i].name === BANK_NAME) {
                return banks[i];
            }
        }

        bank = studio.project.create("Bank");
        bank.name = BANK_NAME;
        bank.isMasterBank = true;
        bank.folder = studio.project.workspace.masterBankFolder;
        return bank;
    }

    function hasSingleSound(event) {
        var sounds = studio.project.model.SingleSound.findInstances({ searchContext: event });
        return sounds && sounds.length > 0;
    }

    function ensureEvent(name, folder, audioFile, bank) {
        var path = "event:/" + EVENT_FOLDER_PATH + "/" + name;
        var event = studio.project.lookup(path);
        if (!event) {
            event = studio.project.create("Event");
            event.name = name;
            event.folder = folder;
        }

        var length = audioFile.length && audioFile.length > 0 ? audioFile.length : 0.45;
        if (!hasSingleSound(event)) {
            var sound = event.masterTrack.addSound(event.timeline, "SingleSound", 0, length);
            sound.audioFile = audioFile;
        }

        event.relationships.banks.add(bank);
        return event;
    }

    var audioFile = studio.project.importAudioFile(WAV_PATH);
    if (!audioFile) {
        throw new Error("Failed to import wav: " + WAV_PATH);
    }

    var folder = ensureEventFolderPath(EVENT_FOLDER_PATH);
    var bank = findOrCreateMasterBank();
    for (var i = 0; i < EVENT_NAMES.length; i++) {
        ensureEvent(EVENT_NAMES[i], folder, audioFile, bank);
    }

    studio.project.save();
    var buildOk = studio.project.build({
        banks: BANK_NAME,
        platforms: "Desktop"
    });

    if (!buildOk) {
        throw new Error("FMOD build failed for bank: " + BANK_NAME);
    }
})();
