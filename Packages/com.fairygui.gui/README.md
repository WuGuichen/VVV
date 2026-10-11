# FairyGUI Embedded Package

This package is an embedded local import of the official FairyGUI Unity runtime and editor code for WGameFramework dependency validation.

## Source

- Source URL: https://github.com/fairygui/FairyGUI-unity
- Source tag: 5.2.0
- Source commit: 7f8555dd163bd17315f77b64907e07e735cf0ed0
- Source commit date: 2025-05-11 22:37:03 +0800
- Import date: 2026-05-26
- License: MIT, preserved in `LICENSE`

## Layout Normalization

The upstream repository is a Unity project, not a UPM package root. This embedded package keeps the runtime/editor source unchanged and normalizes only the containing folders:

- Upstream `Assets/Scripts/` -> package `Runtime/`
- Upstream `Assets/Editor/` -> package `Editor/`
- Upstream `Assets/Resources/Shaders/` -> package `Resources/Shaders/`
- Upstream `LICENSE`, `README.md`, and `README_zh-CN.md` are preserved as package documentation files.

The upstream examples, `UIProject`, and Lua support folders were not imported because this dependency/import PR only adds the reusable FairyGUI runtime/editor package. No WGame patched `FairyGUI_Dynamic` files are included.

## Scope

This package import intentionally does not add an MxFramework adapter, generated FairyGUI UI code, YooAsset integration, Wwise/Steam/Entitas/Luban coupling, or UI Toolkit migration code.
