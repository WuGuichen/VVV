"""Compile the existing production CPU barrier verbatim; never maintain a test implementation."""
from pathlib import Path
import hashlib

base = Path(__file__).resolve().parent
source = base.parents[2] / 'Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/CharacterTacticalServices.cs'
text = source.read_text(encoding='utf-8-sig')
start = text.index('    internal enum TacticalCheckpointTransition')
end = text.index('    // End checkpoint barriers.', start)
section = text[start:end]
(base / 'ProductionBarrier.g.cs').write_text('using System;\nusing System.Threading.Tasks;\nnamespace MxFramework.Game.CharacterTest.TacticalCard\n{\n' + section + '}\n', encoding='utf-8')
print('Production checkpoint barrier SHA256: ' + hashlib.sha256(section.encode()).hexdigest())
