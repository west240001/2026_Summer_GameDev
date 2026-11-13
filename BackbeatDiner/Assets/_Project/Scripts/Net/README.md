# Net — Netcode for GameObjects scaffold

Two-device localhost host/join for **single-role fullscreen** (裏 / 表).

| Piece | Role |
|-------|------|
| `NetSessionConfig` | Offline / Host / Client + `127.0.0.1:7777` |
| `NetcodeSessionBootstrap` | Runtime `NetworkManager` + `UnityTransport`, sync prefab register/spawn |
| `DaySessionNetSync` | `NetworkVariable` **salesScore** + **chartIndex** (server write) |

## TitleMenu

1. Pick **裏** / **表** (1 / 2)
2. **ホスト作成** (H) or **参加 localhost** (J) → VerticalSlice
3. Offline shortcuts unchanged: **裏で始める** / **表で始める**

## Notes

- Scene management **off** — each device loads VerticalSlice locally after connect.
- Prefab hash fixed: `0xB34C7E51` (host + client must match).
- Full KG-3 SoT / beat sync is later; this is the connection + minimal session sync scaffold.
