using System;
using MxFramework.Editor.CharacterAction;
using NUnit.Framework;
using UnityEditor;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionRecentEditHistoryTests
    {
        [Test]
        public void History_PersistsMostRecentUniqueTenAndRecoversFromMalformedJson()
        {
            string key = "MxFramework.Tests.CharacterAction.RecentEdits." + Guid.NewGuid();
            try
            {
                var history = new CharacterActionRecentEditHistory(key);
                for (int i = 0; i < 12; i++)
                    history.Touch("test.library", 100 + i);

                Assert.AreEqual(CharacterActionRecentEditHistory.Capacity, history.Entries.Count);
                Assert.AreEqual(111, history.Entries[0].ActionId);
                Assert.AreEqual(102, history.Entries[9].ActionId);

                history.Touch("test.library", 105);

                Assert.AreEqual(CharacterActionRecentEditHistory.Capacity, history.Entries.Count);
                Assert.AreEqual(105, history.Entries[0].ActionId);
                Assert.AreEqual(
                    105,
                    new CharacterActionRecentEditHistory(key).Entries[0].ActionId);

                EditorPrefs.SetString(key, "{invalid");

                Assert.AreEqual(0, new CharacterActionRecentEditHistory(key).Entries.Count);
            }
            finally
            {
                EditorPrefs.DeleteKey(key);
            }
        }

        [Test]
        public void History_MigratesRenamedIdentityPrunesMissingEntriesAndScopesByProject()
        {
            string key = "MxFramework.Tests.CharacterAction.RecentEdits." + Guid.NewGuid();
            try
            {
                var history = new CharacterActionRecentEditHistory(key);
                history.Touch("library.a", 100);
                history.Touch(
                    "library.a",
                    101,
                    "library.a",
                    100);
                history.Touch("library.b", 200);

                Assert.AreEqual(-1, history.IndexOf("library.a", 100));
                Assert.AreEqual(1, history.IndexOf("library.a", 101));

                history.Prune(entry => string.Equals(
                    entry.LibraryId,
                    "library.a",
                    StringComparison.Ordinal));

                Assert.AreEqual(1, history.Entries.Count);
                Assert.AreEqual(101, history.Entries[0].ActionId);
                Assert.AreNotEqual(
                    CharacterActionRecentEditHistory.PreferenceKey("C:/ProjectA/Assets"),
                    CharacterActionRecentEditHistory.PreferenceKey("C:/ProjectB/Assets"));
                Assert.AreEqual(
                    CharacterActionRecentEditHistory.PreferenceKey("C:\\ProjectA\\Assets\\"),
                    CharacterActionRecentEditHistory.PreferenceKey("c:/projecta/assets"));
            }
            finally
            {
                EditorPrefs.DeleteKey(key);
            }
        }
    }
}
