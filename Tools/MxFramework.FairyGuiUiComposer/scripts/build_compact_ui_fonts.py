"""Build shared UI fonts with fonttools==4.62.1; originals stay outside Assets.

Keeps GB2312, Big5, Shift-JIS and modern Hangul plus project text. Regional
fonts retain differing outlines/metrics only; identical glyphs use SC fallback.
No Unity serialized asset or .meta is written by this script.
"""
import argparse
import hashlib
import json
import unicodedata
from pathlib import Path
from urllib.request import urlopen

from fontTools import subset
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[3]
SOURCE_CATALOG = ROOT / "ArtSource/UIKits/MxUiFoundation/fonts/upstream-fonts.json"


def encoded_chars(codec):
    result = set()
    for value in range(65536):
        try:
            result.update(map(ord, value.to_bytes(2, "big").decode(codec)))
        except UnicodeError:
            pass
    return result


def project_characters():
    result = set()
    patterns = ["Assets/StreamingAssets/MxFramework/Localization/*.tsv",
                "ArtSource/UIKits/MxUiFoundation/**/*.json",
                "Assets/Scripts/WGameFrameworkProject/CharacterTest/**/*.cs",
                "Assets/Resources/Fonts/font-locales.json", "Authoring/RuntimeAiLab/TestCards/**/*.json"]
    for pattern in patterns:
        for path in sorted(ROOT.glob(pattern)):
            result.update(map(ord, unicodedata.normalize("NFC", path.read_text(encoding="utf-8-sig"))))
    return result


def signature(font, glyph, glyph_set):
    pen = DecomposingRecordingPen(glyph_set)
    glyph_set[glyph].draw(pen)
    return (font["hmtx"].metrics[glyph], pen.value)


def rename(font, family, ps_name):
    # OFL derivative names must not retain the upstream Reserved Font Name.
    names = {1: family, 2: "Regular", 3: ps_name + ";1.0", 4: family,
             6: ps_name, 16: family, 17: "Regular"}
    for entry in list(font["name"].names):
        if entry.nameID in (18, 21, 22, 25):
            font["name"].names.remove(entry)
        elif entry.nameID in names:
            entry.string = names[entry.nameID].encode(entry.getEncoding())
    for key, value in names.items():
        font["name"].setName(value, key, 3, 1, 0x409)
    cff = font["CFF "].cff
    cff.fontNames = [ps_name]
    top = cff.topDictIndex[0]
    top.FamilyName = family
    top.FullName = family
    if hasattr(top, "FDArray"):
        for i, fd in enumerate(top.FDArray):
            if hasattr(fd, "FontName"):
                fd.FontName = ps_name + "-" + str(i)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache", type=Path, default=ROOT / ".agent/font-optimization/upstream")
    parser.add_argument("--output", type=Path, default=ROOT / ".agent/font-optimization/build")
    args = parser.parse_args()
    args.cache.mkdir(parents=True, exist_ok=True)
    args.output.mkdir(parents=True, exist_ok=True)
    sources = {entry["file"]: entry for entry in json.loads(SOURCE_CATALOG.read_text(encoding="utf-8"))["files"]}

    def source(file):
        path = args.cache / file
        entry = sources[file]
        if not path.exists():
            with urlopen(entry["source"], timeout=90) as response:
                path.write_bytes(response.read())
        if hashlib.sha256(path.read_bytes()).hexdigest() != entry["sha256"]:
            raise RuntimeError("Upstream hash mismatch: " + file)
        return path

    common = set(range(0x20, 0x530)) | set(range(0x1e00, 0x1f00))
    common |= set(range(0x2000, 0x2070)) | set(range(0x3000, 0x3040)) | set(range(0xff00, 0xfff0))
    corpus = project_characters()
    charsets = {"SC": encoded_chars("gb2312"), "TW": encoded_chars("big5"),
                "JP": encoded_chars("shift_jis"),
                "KR": set(range(0xac00, 0xd7a4)) | set(range(0x1100, 0x1200)) | set(range(0x3130, 0x3190))}
    output = []
    for kind, weight in (("Sans", "Regular"), ("Serif", "SemiBold")):
        shared = None
        shared_signatures = {}
        for region in ("SC", "TW", "JP", "KR"):
            original = "SourceHan" + kind + region + "-" + weight + ".otf"
            font = TTFont(source(original), recalcTimestamp=False)
            cmap = font.getBestCmap()
            desired = (charsets[region] | common | corpus) & set(cmap)
            keep = set(desired)
            if shared is not None:
                shared_map = shared.getBestCmap()
                shared_set = shared.getGlyphSet()
                own_set = font.getGlyphSet()
                for code in sorted(keep & set(shared_map)):
                    if code not in shared_signatures:
                        shared_signatures[code] = signature(shared, shared_map[code], shared_set)
                    if signature(font, cmap[code], own_set) == shared_signatures[code]:
                        keep.remove(code)
            options = subset.Options()
            options.layout_features = ["kern", "liga", "mark", "mkmk", "ccmp"]
            # Keep small body text hinting; headings retain outlines and metrics
            # but omit size-specific hint programs at their larger UI sizes.
            options.hinting = kind == "Sans"
            options.notdef_outline = True
            options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14, 16, 17]
            sub = subset.Subsetter(options)
            sub.populate(unicodes=keep)
            sub.subset(font)
            if region == "SC":
                shared = font
            filename = "MxUi" + kind + region + ".otf"
            family = "MxUI " + kind + " " + region
            rename(font, family, "MxUI" + kind + region)
            target = args.output / filename
            font.save(target)
            rebuilt = TTFont(target)
            actual = set(rebuilt.getBestCmap())
            coverage = actual | (set(shared.getBestCmap()) if region != "SC" else set())
            if desired - coverage:
                raise RuntimeError("Lost required coverage in " + filename)
            result = dict(file=filename, family=family, derived=True, upstreamFile=original,
                          source=sources[original]["source"], upstreamSha256=sources[original]["sha256"],
                          sha256=hashlib.sha256(target.read_bytes()).hexdigest(), bytes=target.stat().st_size,
                          requiredCharacters=len(desired), ownCharacters=len(actual), sharedCharacters=len(desired-actual))
            output.append(result)
            print(json.dumps(result), flush=True)
    for file in ("SourceSans3-Regular.otf", "SourceSerif4-Semibold.otf"):
        target = args.output / file
        target.write_bytes(source(file).read_bytes())
        output.append(dict(sources[file], derived=False))
    total = sum(row["bytes"] for row in output)
    if total > 20 * 1024 * 1024:
        raise RuntimeError("UI font budget exceeded: " + str(total))
    manifest = dict(license="SIL Open Font License 1.1", unmodified=False, generator="fonttools 4.62.1",
                    policy="Common encoding repertoires + NFC project corpus + exact regional glyph sharing",
                    totalBytes=total, budgetBytes=20*1024*1024, files=output)
    (args.output / "font-sources.json").write_text(json.dumps(manifest, indent=2)+"\n", encoding="utf-8")
    print("TOTAL_BYTES="+str(total), flush=True)


if __name__ == "__main__":
    main()
