(function () {
    var ALLOW_BANK_RELATIONSHIP_WRITES = false;
    var BANK_NAME = "Master";
    var EVENT_FOLDER_PATH = "MxFramework/CharacterTest/PhysicalAudio";
    var DEMO_BEEP_PATH = "FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Assets/mxframework_demo_beep.wav";
    var DEMO_EVENT_PATHS = [
        "event:/MxFramework/Demo/OneShot",
        "event:/MxFramework/Demo/OneShot3D",
        "event:/MxFramework/Demo/Loop"
    ];
    var EVENTS = [
        {
            name: "SoleContactDefault",
            audioPath: "Assets/Audio/MxFramework/CharacterTest/PhysicalAudio/SoleContacts/sole_contact_default_01.wav",
            minDistance: 1,
            maxDistance: 18
        },
        {
            name: "SoleContactRough",
            audioPath: "Assets/Audio/MxFramework/CharacterTest/PhysicalAudio/SoleContacts/sole_contact_rough_01.wav",
            minDistance: 1,
            maxDistance: 18
        },
        {
            name: "SoleContactHardSlick",
            audioPath: "Assets/Audio/MxFramework/CharacterTest/PhysicalAudio/SoleContacts/sole_contact_hard_slick_01.wav",
            minDistance: 1,
            maxDistance: 18
        },
        {
            name: "SoleContactElastic",
            audioPath: "Assets/Audio/MxFramework/CharacterTest/PhysicalAudio/SoleContacts/sole_contact_elastic_01.wav",
            minDistance: 1,
            maxDistance: 20
        },
        {
            name: "ObjectCollisionDefault",
            audioPath: "Assets/Audio/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisions/object_collision_default_01.wav",
            minDistance: 1,
            maxDistance: 24
        },
        {
            name: "ObjectCollisionRough",
            audioPath: "Assets/Audio/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisions/object_collision_rough_01.wav",
            minDistance: 1,
            maxDistance: 24
        },
        {
            name: "ObjectCollisionHardSlick",
            audioPath: "Assets/Audio/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisions/object_collision_hard_slick_01.wav",
            minDistance: 1,
            maxDistance: 24
        },
        {
            name: "ObjectCollisionElastic",
            audioPath: "Assets/Audio/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisions/object_collision_elastic_01.wav",
            minDistance: 1,
            maxDistance: 26
        }
    ];

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

    function importAudioFile(path) {
        var audioFile = studio.project.importAudioFile(path);
        if (!audioFile) {
            throw new Error("Failed to import wav: " + path);
        }

        return audioFile;
    }

    function setEventSingleSound(event, audioFile) {
        if (!event) {
            return;
        }

        var length = audioFile.length && audioFile.length > 0 ? audioFile.length : 0.25;
        var sounds = studio.project.model.SingleSound.findInstances({ searchContext: event });
        if (sounds && sounds.length > 0) {
            sounds[0].audioFile = audioFile;
            sounds[0].length = length;
        } else {
            var sound = event.masterTrack.addSound(event.timeline, "SingleSound", 0, length);
            sound.audioFile = audioFile;
        }
    }

    function hasSpatializer(event) {
        var spatializers = studio.project.model.SpatialiserEffect.findInstances({ searchContext: event });
        return spatializers && spatializers.length > 0;
    }

    function ensureEventSpatializer(event) {
        if (!event || hasSpatializer(event)) {
            return;
        }

        event.masterTrack.mixerGroup.effectChain.addEffect("SpatialiserEffect");
    }

    function setEventDistanceRange(event, definition) {
        var minDistance = definition.minDistance && definition.minDistance > 0
            ? definition.minDistance
            : 1;
        var maxDistance = definition.maxDistance && definition.maxDistance > minDistance
            ? definition.maxDistance
            : 20;
        event.automatableProperties.minimumDistance = minDistance;
        event.automatableProperties.maximumDistance = maxDistance;
    }

    function ensureBankRelationship(event, bank) {
        var banks = event.relationships.banks.destinations || [];
        for (var i = 0; i < banks.length; i++) {
            if (String(banks[i].id) === String(bank.id)) {
                return;
            }
        }

        if (!ALLOW_BANK_RELATIONSHIP_WRITES) {
            throw new Error(
                "FMOD Studio 2.03.13-2.03.14 Bank relationship writes are disabled. "
                + "Close FMOD Studio and use Tools/FMOD/AssignEventBanksOffline.mjs."
            );
        }

        event.relationships.banks.add(bank);
    }

    function repairDemoEvents(bank) {
        var audioFile = importAudioFile(DEMO_BEEP_PATH);
        for (var i = 0; i < DEMO_EVENT_PATHS.length; i++) {
            var event = studio.project.lookup(DEMO_EVENT_PATHS[i]);
            if (!event) {
                continue;
            }

            setEventSingleSound(event, audioFile);
            ensureBankRelationship(event, bank);
        }
    }

    function ensureEvent(definition, folder, bank) {
        var audioFile = importAudioFile(definition.audioPath);
        var eventPath = "event:/" + EVENT_FOLDER_PATH + "/" + definition.name;
        var event = studio.project.lookup(eventPath);
        if (!event) {
            event = studio.project.create("Event");
            event.name = definition.name;
            event.folder = folder;
        }

        event.outputFormat = 2;
        setEventDistanceRange(event, definition);
        ensureEventSpatializer(event);
        setEventSingleSound(event, audioFile);
        ensureBankRelationship(event, bank);
        return event;
    }

    var folder = ensureEventFolderPath(EVENT_FOLDER_PATH);
    var bank = findOrCreateMasterBank();
    repairDemoEvents(bank);
    for (var i = 0; i < EVENTS.length; i++) {
        ensureEvent(EVENTS[i], folder, bank);
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
