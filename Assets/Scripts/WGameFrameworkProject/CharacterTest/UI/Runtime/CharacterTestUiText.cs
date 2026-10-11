using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestUiTextProviderSignature :
        System.IEquatable<CharacterTestUiTextProviderSignature>
    {
        private readonly MxUiLocaleId _locale;
        private readonly long _revision;

        private CharacterTestUiTextProviderSignature(MxUiLocaleId locale, long revision)
        {
            _locale = locale;
            _revision = revision;
        }

        public static CharacterTestUiTextProviderSignature Create(IMxUiTextProvider provider)
        {
            if (provider == null)
                return default;

            return new CharacterTestUiTextProviderSignature(provider.CurrentLocale, provider.Revision);
        }

        public bool Equals(CharacterTestUiTextProviderSignature other)
        {
            return _locale == other._locale && _revision == other._revision;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestUiTextProviderSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_locale.GetHashCode() * 397) ^ _revision.GetHashCode();
            }
        }

        public static bool operator ==(
            CharacterTestUiTextProviderSignature left,
            CharacterTestUiTextProviderSignature right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            CharacterTestUiTextProviderSignature left,
            CharacterTestUiTextProviderSignature right)
        {
            return !left.Equals(right);
        }
    }

    internal static class CharacterTestUiText
    {
        public static string Resolve(IMxUiTextProvider provider, string key, string fallback)
        {
            provider = provider ?? MxUiNullTextProvider.Instance;
            return provider.TryGetText(
                new MxUiLocalizedTextRequest(new MxUiTextKey(key), fallback),
                out string text)
                ? text
                : (fallback ?? string.Empty);
        }

        public static string Signature(IMxUiTextProvider provider)
        {
            if (provider == null)
                return string.Empty;

            return provider.CurrentLocale.Value + "|" + provider.Revision;
        }
    }
}
