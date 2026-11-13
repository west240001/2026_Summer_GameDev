# Rhythm（KG-2 / KG-4）

Week 1 vertical slice:

| Script | Role |
|--------|------|
| `ChartDefinition` | ScriptableObject chart: `bpm` / `lengthBeats` / notes (`timeBeats`+`lane`). Create menu **BACKBEAT DINER → Chart Definition**. Sample: `Resources/Charts/Chart_DemoA` |
| `RhythmClock` | BPM + `AudioSettings.dspTime` beat schedule / `OnBeat`; `ApplyChart` |
| `JudgeWindow` | Perfect ±40ms / Good ±80ms |
| `NoteHighway` | Chart-driven (or every-beat fallback) scrolling notes toward hit line |
| `HitInput` | Space / gamepad South → judge + HUD |
| `RhythmPrototypeBootstrap` | Loads `Chart_DemoA`, builds kitchen placeholders + wires systems |

Open `Assets/_Project/Scenes/RhythmPrototype.unity` (menu **BACKBEAT DINER → Build Rhythm Prototype Scene** if missing).
