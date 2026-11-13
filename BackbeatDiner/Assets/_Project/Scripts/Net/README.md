# Net — Netcode for GameObjects (beat + input sync)

Two-device localhost host/join for **single-role fullscreen** (裏 / 表).

| Piece | Role |
|-------|------|
| `NetSessionConfig` | Offline / Host / Client + `127.0.0.1:7777` |
| `NetcodeSessionBootstrap` | Runtime `NetworkManager` + `UnityTransport`, sync prefab register/spawn |
| `DaySessionNetSync` | Host clock (ServerTime → DSP), chart/gap, prep/sales; RPC for hits / serve / C1·C2·C3 |

## TitleMenu

1. Pick **裏** / **表** (1 / 2)
2. **ホスト作成** (H) or **参加 localhost** (J) → VerticalSlice
3. Offline shortcuts unchanged: **裏で始める** / **表で始める** (F8 role swap still works offline)

## What syncs (KG-53)

- **Beat / song**: host publishes `songStartServerTime` + BPM + beatIndex; client `RhythmClock.AlignToStartDsp`
- **Chart / gap**: `chartIndex` + `inGap` → client `DaySetlistRunner.ApplyNetworkChartIndex` (client does not StartSetlist)
- **Resources**: prep / sales / served (server NetworkVariables → client mirror)
- **Inputs (RPC)**: back hit feedback, serve open/resolve, Beat lexicon C3, Quest C1, Snapshot C2

## Test (2 instances / MPPM)

1. Instance A: TitleMenu → 裏 → **ホスト作成**
2. Instance B (or Multiplayer Play Mode virtual player): TitleMenu → 表 → **参加 localhost**
3. Console: Host/Client spawn + `Client clock aligned` / remote C1·C2·C3 / prep·sales updates
4. Offline regression: **裏で始める** then **F8** still swaps fullscreen view

## Notes

- Scene management **off** — each device loads VerticalSlice locally after connect.
- Prefab hash fixed: `0xB34C7E51` (host + client must match).
