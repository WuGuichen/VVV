using System;
using MxFramework.Settings;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Applies player preferences to an owned runtime copy, never to the authored settings asset.</summary>
    internal sealed class CharacterTestWaterSettingsApplier : IMxSettingsApplier, IDisposable
    {
        private readonly Func<CharacterTestShallowWaterConfiguration> _configuration;
        private CharacterTestShallowWaterConfiguration _bound;
        private CharacterTestShallowWaterSettings _source, _copy, _previous;
        private bool _disposed;
        public string ApplierId => "settings.graphics.characterTest.water";
        internal CharacterTestWaterSettingsApplier(Func<CharacterTestShallowWaterConfiguration> configuration)
        { _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)); }

        public MxSettingsApplyResult Apply(MxSettingsProfile profile)
        {
            if (_disposed) return MxSettingsApplyResult.Failed(new MxSettingsIssue(
                MxSettingsIssueCode.AdapterUnavailable, "$.graphics.waterQuality", "Water settings session has ended."));
            var quality = profile?.Graphics?.WaterQuality ?? MxGraphicsFeatureQuality.Default;
            var rippleQuality = profile?.Graphics?.RippleQuality ?? MxGraphicsFeatureQuality.Default;
            var configuration = _configuration();
            if ((quality == MxGraphicsFeatureQuality.Default && rippleQuality == MxGraphicsFeatureQuality.Default) || configuration?.Settings == null)
            { Restore(); return MxSettingsApplyResult.Succeeded(); }
            if (!ReferenceEquals(configuration, _bound) || !ReferenceEquals(configuration.Settings, _source))
            {
                Restore(); _bound = configuration; _source = configuration.Settings;
                _previous = configuration.RuntimeSettingsOverride;
                _copy = UnityEngine.Object.Instantiate(_source);
                _copy.name = _source.name + " (player water settings)";
                _copy.hideFlags = HideFlags.HideAndDontSave;
            }
            _copy.QualityTier = quality == MxGraphicsFeatureQuality.Default ? _source.QualityTier
                : quality == MxGraphicsFeatureQuality.Ultra ? CharacterTestWaterQuality.SsprHigh
                : quality == MxGraphicsFeatureQuality.High ? CharacterTestWaterQuality.Quality
                : quality == MxGraphicsFeatureQuality.Medium ? CharacterTestWaterQuality.Balanced : CharacterTestWaterQuality.Performance;
            _copy.RippleQuality = rippleQuality == MxGraphicsFeatureQuality.Default ? _source.RippleQuality : (CharacterTestRippleQuality)(int)rippleQuality;
            _bound.RuntimeSettingsOverride = _copy;
            return MxSettingsApplyResult.Succeeded();
        }
        private void Restore()
        {
            if (_bound != null && ReferenceEquals(_bound.RuntimeSettingsOverride, _copy)) _bound.RuntimeSettingsOverride = _previous;
            if (_copy != null)
            { if (Application.isPlaying) UnityEngine.Object.Destroy(_copy); else UnityEngine.Object.DestroyImmediate(_copy); }
            _bound = null; _source = _copy = _previous = null;
        }
        public void Dispose() { if (_disposed) return; Restore(); _disposed = true; }
    }
}
