# Compact shared UI fonts

Current runtime payload: **10 OTF files, 19,915,556 bytes (18.99 MiB)**, down from
110,521,320 bytes (105.40 MiB). A focused test enforces a 20 MiB ceiling and rejects
retired font files left in Resources.

## Typography and sharing

- `MxUI Sans SC`: body, buttons, labels and numbers share the Regular face.
- `MxUI Serif SC`: card names and page headings retain the original SemiBold outlines.
- TW / JP / KR files contain only regional glyphs whose outlines or advance metrics
  differ from the SC shared file, plus characters absent from SC.
- Two shared Western fallbacks: Source Sans 3 Regular and Source Serif 4 Semibold.
- Embedded reference order: regional -> SC -> Western; SC -> Western. Western
  references no other font. Native font names and explicit asset references are
  both configured, and every file embeds its data. Player needs no installed font.
- Locale changes retain existing FairyGUI font handles and rebuild their text.

The 11-language list and samples remain in `font-locales.json`. Font support does
not create translated game strings. English and the other European languages
reuse the shared Latin/Cyrillic glyphs instead of receiving separate full fonts.

## Coverage policy

SC retains GB2312; TW retains Big5; JP retains Shift-JIS; KR retains all 11,172
modern Hangul syllables and the Jamo blocks. Common Latin/Cyrillic, punctuation,
current UI/localization source text and authored test-card text are added. Regional
coverage is the union of its own glyphs and its explicit fallback chain.

These are horizontal game-UI fonts, not full Unicode text-editor fonts. Rare
characters outside these sets are not promised. When new game text or card names
need additional characters, rebuild the fonts; missing required characters must
be resolved before shipping that text. This is not a sample-only character cut.

## Rebuild and provenance

Install `fonttools==4.62.1` into a local tool environment and run:

```text
python Tools/MxFramework.FairyGuiUiComposer/scripts/build_compact_ui_fonts.py
```

The script verifies pinned upstream hashes, uses an ignored cache outside Assets,
compares actual glyph outlines and metrics, and writes candidates under
`.agent/font-optimization/build`. It retains body hinting and removes size-specific
hint programs from the larger heading faces. Copyright and OFL notices remain;
derivative family names use `MxUI`, not the upstream reserved name `Source`.

`ArtSource/UIKits/MxUiFoundation/fonts/upstream-fonts.json` records original sources.
The local `font-sources.json` records generated hashes, sizes and shared-character
counts. Copy approved outputs to this directory, import/configure references via
Unity, and rerun `CharacterTestFairyGuiFontTests`. Do not copy the cached original
fonts into Assets. The two unchanged Western fallback files retain their original
names. All font families remain under SIL Open Font License 1.1; ship the adjacent
license files with them.

For FairyGUI Editor, install the current files with
`Tools/MxFramework.FairyGuiUiComposer/scripts/install_ui_fonts.ps1`.
Composer uses the same files through `@font-face`; its fallback order matches Unity.
