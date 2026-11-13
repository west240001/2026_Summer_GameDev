# Rhythm（KG-2 / KG-4）

Week 1 vertical slice:

| Script | Role |
|--------|------|
| `ChartDefinition` | ScriptableObject chart: `displayName` / `bpm` / `lengthBeats` / optional `songClip` / notes (`timeBeats`+`lane`). Create menu **BACKBEAT DINER → Chart Definition**. Samples: `Resources/Charts/Chart_DemoA` (wired to `Audio/DemoSong_120BPM.wav`), `Chart_DemoB` |
| `SetlistDefinition` | Day setlist SO: ordered charts + `gapSeconds`. Sample: `Resources/Setlists/Setlist_DemoDay` (A→gap→B) |
| `DaySetlistRunner` | VerticalSlice multi-chart flow; Japanese HUD `チャート名`; gap opens `ChartGapCoopWindow`（スナップ推奨 / Beat-B3）; `ApplyChart`+`StartClock` plays chart song when assigned |
| `RhythmClock` | BPM + `AudioSettings.dspTime` beat schedule / `OnBeat`; `ApplyChart`; `PlayScheduled` song bed at chart start dspTime |
| `JudgeWindow` | Perfect ±40ms / Good ±80ms |
| `NoteHighway` | Chart-driven (or every-beat fallback) scrolling notes toward hit line |
| `HitInput` | Space / gamepad South → judge + HUD |
| `RhythmPrototypeBootstrap` | Loads `Chart_DemoA`, builds kitchen placeholders + wires systems |

Open `Assets/_Project/Scenes/RhythmPrototype.unity` (menu **BACKBEAT DINER → Build Rhythm Prototype Scene** if missing).
