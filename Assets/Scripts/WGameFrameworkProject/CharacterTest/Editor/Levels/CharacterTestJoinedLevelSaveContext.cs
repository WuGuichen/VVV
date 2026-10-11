using System;
using MxFramework.Config.Levels;
namespace MxFramework.Game.CharacterTest.Editor
{
    // Project-only synchronous save scope. Every participant receives a deep copy of the
    // same complete candidate; standalone module saves retain their existing merge behavior.
    internal sealed class CharacterTestJoinedLevelSaveContext : IDisposable
    {
        [ThreadStatic] private static CharacterTestJoinedLevelSaveContext _current;
        private readonly CharacterTestJoinedLevelSaveContext _previous;
        private readonly string _path, _json;
        internal CharacterTestJoinedLevelSaveContext(string path, MxLevelAuthoringDocument document)
        {
            _path = path; _json = MxLevelAuthoringJson.SaveToJson(document);
            _previous = _current; _current = this;
        }
        internal static bool TryGet(string path, out MxLevelAuthoringDocument document)
        {
            document = null;
            if (_current == null || !string.Equals(path, _current._path, StringComparison.Ordinal)) return false;
            document = MxLevelAuthoringJson.LoadFromJson(_current._json); return true;
        }
        public void Dispose()
        {
            if (!ReferenceEquals(_current, this)) throw new InvalidOperationException("Joined save scopes must close in order.");
            _current = _previous;
        }
    }
}
