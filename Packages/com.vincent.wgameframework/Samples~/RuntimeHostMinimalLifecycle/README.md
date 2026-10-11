# Runtime Host Minimal Lifecycle

This sample demonstrates the smallest observable `MxFramework.Runtime.RuntimeHost` lifecycle without project configuration or optional packages.

## Run

1. Import **Runtime Host Minimal Lifecycle** from Package Manager.
2. Open `RuntimeHostMinimalLifecycle.unity` from the imported sample folder.
3. Enter Play Mode.
4. Confirm the Game view reports `RuntimeHost lifecycle: Passed`.

The sample registers one probe module, calls `Initialize`, `Start`, three `Tick` frames, `Stop`, and `Dispose`, then displays the exact callback sequence. Leaving Play Mode disposes any still-running host.

This sample does not replace the repository `RuntimeVerticalSlice` showcase and does not cover Gameplay, Ability, resources, UI, VFX, audio or optional adapters.
