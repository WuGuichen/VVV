using System.Collections.Generic;
using MxFramework.Buffs;

namespace MxFramework.Preview
{
    /// <summary>
    /// Abstraction over the runtime world that the preview server drives.
    /// All external dependencies are expected to be injected (see AGENTS.md red lines).
    /// </summary>
    public interface IPreviewWorld
    {
        IBuffTarget GetOrCreateTarget(string targetId);
        IBuffTarget GetOrCreateCaster(string casterId);
        void Reset(bool reloadBase);
        void Tick(int frames);
        IReadOnlyList<BuffSnapshot> SnapshotBuffs(string targetId);
        IReadOnlyList<AttributeChange> SnapshotAttributeChanges(string targetId);
        IReadOnlyList<DamageTick> DrainDamageTicks();
        IReadOnlyList<StatusChange> DrainStatusChanges();
        // Apply a buff via the injected pipeline. Returns true on success.
        // Caster / stack / durationOverrideMs are advisory; framework-level IBuff has no caster
        // concept yet, so DummyPreviewWorld may ignore them and emit a TODO log.
        bool ApplyBuff(string buffId, string casterId, string targetId, int stack, long? durationOverrideMs);
        /// <summary>
        /// Load a preview patch (Runtime Patch v1 format) into the world.
        /// Used by ScenePreviewWorld to set up ConfigBuffFactory / ConfigModifierFactory.
        /// </summary>
        void LoadPreviewPatch(string sourceJson);
    }
}
