#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";

const projectDirectory = process.argv[2];
const bankGuid = process.argv[3];
const eventNames = process.argv.slice(4);

if (!projectDirectory || !/^\{[0-9a-f-]{36}\}$/i.test(bankGuid) || eventNames.length === 0) {
    console.error("Usage: AssignEventBanksOffline.mjs <project-dir> <bank-guid> <event-name>...");
    process.exit(2);
}

const eventDirectory = path.join(projectDirectory, "Metadata", "Event");
const files = fs.readdirSync(eventDirectory)
    .filter((file) => file.endsWith(".xml"))
    .sort();

function xmlValue(name) {
    return `<property name="name">\\s*<value>${name.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}</value>`;
}

for (const eventName of eventNames) {
    const matches = files.filter((file) => {
        const xml = fs.readFileSync(path.join(eventDirectory, file), "utf8");
        return new RegExp(xmlValue(eventName)).test(xml);
    });

    if (matches.length !== 1) {
        throw new Error(`Expected exactly one Event named ${eventName}, found ${matches.length}`);
    }

    const eventPath = path.join(eventDirectory, matches[0]);
    const original = fs.readFileSync(eventPath, "utf8");
    const bankRelationship = /(\t\t<relationship name="banks">\n)([\s\S]*?)(\t\t<\/relationship>)/;
    let updated;

    if (bankRelationship.test(original)) {
        updated = original.replace(bankRelationship, (match, open, destinations, close) => {
            if (destinations.includes(bankGuid)) return match;
            return `${open}${destinations}\t\t\t<destination>${bankGuid}</destination>\n${close}`;
        });
    } else {
        const eventClose = "\t</object>";
        const closeIndex = original.indexOf(eventClose);
        if (closeIndex < 0) throw new Error(`Could not locate Event object close in ${matches[0]}`);
        const relationship =
            `\t\t<relationship name="banks">\n` +
            `\t\t\t<destination>${bankGuid}</destination>\n` +
            `\t\t</relationship>\n`;
        updated = original.slice(0, closeIndex) + relationship + original.slice(closeIndex);
    }

    if (updated !== original) {
        fs.writeFileSync(eventPath, updated, "utf8");
        console.log(`Assigned ${eventName} to ${bankGuid} in ${matches[0]}`);
    } else {
        console.log(`${eventName} already assigned to ${bankGuid}`);
    }
}
