using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestTacticalWholeCardTimeline
    {
        private void AggregateBlocks(CharacterTestTacticalDslSource document)
        {
            foreach (var node in CharacterTestTacticalCompositionEdits.Nodes(document.Root).Where(n => n.Kind == "Reference"))
            {
                var definition = document.Definitions.FirstOrDefault(d => d.DefinitionId == node.DefinitionId);
                var display = DisplayPayload.Timelines.FirstOrDefault(t => t.TimelineId == node.NodeId);
                if (definition == null || display == null) continue;
                CharacterTestTacticalAuthoringCompilation compiled = null;
                if (Program.TacticalSource?.SharedCompilations != null && !Program.TacticalSource.SharedCompilations.TryGetValue(node.NodeId, out compiled))
                    Program.TacticalSource.SharedCompilations.TryGetValue(node.DefinitionId, out compiled);
                foreach (var block in definition.Source.Timelines.SelectMany(t => t.EventBlocks))
                {
                    var members = display.Events.Where(e => Location(e.EventId)?.BlockId == block.BlockId).ToArray();
                    int start = members.Length == 0 ? 0 : members.Min(e => e.StartFrame);
                    int end = members.Length == 0 ? start : members.Max(e => e.StartFrame + e.DurationFrames);
                    if (compiled != null) compiled.TryBlockFrames(block.BlockId, out start, out end);
                    foreach (var member in members) display.Events.Remove(member);
                    string id = CharacterTestBehaviorExecutionBuilder.CompositionId(node.NodeId, "block:" + block.BlockId);
                    _locations[id] = new CharacterTestTacticalSourceLocation(document.CardId, node.NodeId, "eventBlock",
                        definition.DefinitionId, definition.Revision, block.BlockId, block: block.BlockId);
                    display.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = id, TimelineId = node.NodeId,
                        Title = block.Title, StartFrame = start, DurationFrames = System.Math.Max(1, end - start),
                        TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                        Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation() });
                }
            }
        }
    }
}
