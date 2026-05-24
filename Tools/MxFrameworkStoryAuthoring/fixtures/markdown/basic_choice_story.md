---
graph: 442001
entry: intro
source: basic_choice_story
---

# Basic Choice Story

## Beat intro
id: 442101
trigger: 442201
line: 442301 | WaitForCommand | A signal waits at the story boundary.
choice: 442401 | 442302 | end | effect 442501 | Stabilize signal

## Beat end
id: 442102
line: 442303 | NoWait | Signal stabilized through generated Story config.
set-fact: 442601 | Bool | true
