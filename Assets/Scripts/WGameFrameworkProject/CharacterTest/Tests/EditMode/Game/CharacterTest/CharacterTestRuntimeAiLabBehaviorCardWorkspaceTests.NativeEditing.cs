using System;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceTests
    {
        // The former graph-edit tests inspected writable v4/v5 projections after a v11 save.
        // Keep their identity, transaction and undo guarantees against the actual native source.
        private sealed class NativeEditingFixture : IDisposable
        {
            private readonly GameObject _host = new GameObject("Native Timeline Editing Fixture");
            internal CharacterTestRuntimeAiPlannerDevSceneController Controller { get; }
            internal const int Entry = 2;

            internal NativeEditingFixture()
            {
                Controller = PrepareRecipeController(_host);
                Assert.IsTrue(Controller.DraftWorkspace.TryGetGroupForSlot(CharacterTestArenaMatchConfig.Team1Id,
                    0, out var group));
                foreach (int i in new[] { Entry, 1 })
                {
                    var payload = BuildV6TimelinePayload();
                    CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
                    Controller.DraftWorkspace.Cards[group.Entries[i].CardDraftId].LogicPayload = payload;
                }
            }

            internal CharacterTestRuntimeAiLabBehaviorCardLogicPayload Payload(int entry = Entry) => CurrentDraftPayload(Controller, entry);
            internal CharacterTestRuntimeAiLabBehaviorRecipeEvent Event(int entry = Entry) => Payload(entry).Timelines[0].Events.Find(e => e.EventId == "event.exit");
            internal CharacterTestRuntimeAiLabStageEditIdentity Identity(int entry = Entry)
            {
                Assert.IsTrue(Controller.TryResolveStageEditIdentity(entry, out var identity, out string issue), issue);
                return identity;
            }
            internal void Edit(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue value, int entry = Entry)
            {
                Assert.IsTrue(Controller.TryApplyBehaviorRecipeTriggerEventField(entry, Identity(entry),
                    "approach", "event.exit", field, -1, value, out _, out string issue), issue);
            }
            internal void Undo()
            {
                Assert.IsTrue(Controller.TryUndoBehaviorCardEdit(Entry, Identity(), out _, out string issue), issue);
            }
            internal string AddEvent(int start = 7)
            {
                Assert.IsTrue(Controller.TryAddLogicBranchEvent(Entry, Identity(), "approach", start,
                    CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                    out _, out string eventId, out _, out string issue), issue);
                return eventId;
            }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(_host);
        }
    }
}
