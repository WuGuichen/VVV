/* Idempotently materialize the MxFramework product Bus hierarchy in FMOD Studio. */
(function () {
    function groups() {
        return studio.project.model.MixerGroup.findInstances();
    }

    function findGroup(name) {
        var items = groups();
        for (var index = 0; index < items.length; ++index) {
            if (items[index].name === name) {
                return items[index];
            }
        }
        return null;
    }

    function ensureGroup(name, output) {
        var group = findGroup(name);
        if (!group) {
            group = studio.project.create("MixerGroup");
            group.name = name;
        }
        group.output = output;
        return group;
    }

    function findSnapshot(name) {
        var items = studio.project.model.Snapshot.findInstances();
        for (var index = 0; index < items.length; ++index) {
            if (items[index].name === name) {
                return items[index];
            }
        }
        return null;
    }

    function clearSnapshotProperties(snapshot) {
        var properties = (snapshot.relationships.snapshotProperties.destinations || []).slice();
        for (var index = 0; index < properties.length; ++index) {
            studio.project.deleteObject(properties[index]);
        }
    }

    function scopeVolume(snapshot, bus, decibels) {
        var property = studio.project.create("SnapshotProperty");
        property.propertyName = "volume";
        property.value = decibels;
        property.snapshot = snapshot;
        property.automatableObject = bus;
    }

    function ensureSnapshot(name, bank, changes) {
        var snapshot = findSnapshot(name);
        if (!snapshot) {
            snapshot = studio.project.create("Snapshot");
            snapshot.name = name;
        }
        snapshot.mixer = studio.project.workspace.mixer;
        snapshot.behavior = 0;
        clearSnapshotProperties(snapshot);
        var currentBanks = (snapshot.relationships.banks.destinations || []).slice();
        for (var bankIndex = 0; bankIndex < currentBanks.length; ++bankIndex) {
            snapshot.relationships.banks.remove(currentBanks[bankIndex]);
        }
        snapshot.relationships.banks.add(bank);
        var modulators = (snapshot.automatableProperties.relationships.modulators.destinations || []).slice();
        for (var modulatorIndex = 0; modulatorIndex < modulators.length; ++modulatorIndex) {
            if (modulators[modulatorIndex].nameOfPropertyBeingModulated === "snapshotIntensity") {
                studio.project.deleteObject(modulators[modulatorIndex]);
            }
        }
        var envelope = snapshot.automatableProperties.addModulator("ADSRModulator", "snapshotIntensity");
        envelope.initialValue = 0;
        envelope.attackTime = 200;
        envelope.peakValue = 100;
        envelope.holdTime = 0;
        envelope.decayTime = 0;
        envelope.sustainValue = 100;
        envelope.releaseTime = 200;
        envelope.finalValue = 0;
        for (var index = 0; index < changes.length; ++index) {
            scopeVolume(snapshot, changes[index].bus, changes[index].decibels);
        }
        return snapshot;
    }

    function removeUnnamedSnapshots() {
        var snapshots = studio.project.model.Snapshot.findInstances().slice();
        for (var index = 0; index < snapshots.length; ++index) {
            if (!snapshots[index].name) {
                studio.project.deleteObject(snapshots[index]);
            }
        }
    }

    var master = studio.project.workspace.mixer.masterBus;
    var music = ensureGroup("Music", master);
    var sfx = ensureGroup("SFX", master);
    var ui = ensureGroup("UI", master);
    var ambience = ensureGroup("Ambience", master);
    var voice = ensureGroup("Voice", master);
    var combat = ensureGroup("Combat", sfx);
    var ability = ensureGroup("Ability", sfx);
    var physical = ensureGroup("Physical", sfx);

    var events = studio.project.model.Event.findInstances();
    for (var eventIndex = 0; eventIndex < events.length; ++eventIndex) {
        var event = events[eventIndex];
        if (event.name === "OneShot" || event.name === "OneShot3D" || event.name === "Loop") {
            event.mixerInput.output = sfx;
        } else {
            event.mixerInput.output = physical;
        }
    }

    var banks = studio.project.model.Bank.findInstances();
    var masterBank = null;
    for (var bankIndex = 0; bankIndex < banks.length; ++bankIndex) {
        if (banks[bankIndex].name === "Master") {
            masterBank = banks[bankIndex];
            break;
        }
    }
    if (!masterBank) {
        throw "Master Bank is required before configuring product snapshots";
    }

    removeUnnamedSnapshots();

    ensureSnapshot("MxPaused", masterBank, [
        { bus: music, decibels: -3.10 },
        { bus: sfx, decibels: -16.48 },
        { bus: ambience, decibels: -7.96 }
    ]);
    ensureSnapshot("MxMenu", masterBank, [
        { bus: sfx, decibels: -6.02 },
        { bus: ambience, decibels: -3.10 }
    ]);
    ensureSnapshot("MxCombat", masterBank, [
        { bus: ambience, decibels: -3.74 },
        { bus: voice, decibels: -0.92 }
    ]);
    ensureSnapshot("MxDialogue", masterBank, [
        { bus: music, decibels: -6.94 },
        { bus: sfx, decibels: -9.12 },
        { bus: ui, decibels: -3.10 },
        { bus: ambience, decibels: -10.46 }
    ]);

    studio.project.save();
    studio.system.print(
        "MxFramework product mix configured: groups=" + groups().length
        + " events=" + events.length
        + " snapshots=" + studio.project.model.Snapshot.findInstances().length
        + " reserved=" + [music.name, ui.name, ambience.name, voice.name].join(","));
}());
