# Coop（KG-3）

| Script | Role |
|--------|------|
| `BeatLexicon` | Static defs **Beat-B1..B4 / Beat-F1..F4**（日本語ラベル・SE 周波数）。CD 定数 **8s** any / **20s** same-motif |
| `BeatLexiconDemo` | Pad keys via `BeatPadKeyScheme`：solo **1–4**、縦スライス裏 **A/S/D/F**、表 **←↓→↑**。日本語パッド UI。**表のみ** `FrontCommsSendLocked` でブロック。`MotifSent` / `BeatSent` |
| `BeatReceivePulse` | Edge pulse + optional waveform strip on motif / simulated back beat (**B** / auto); Japanese toast tutorial-only |

## Pad labels（v1）

| Motif | 裏 (Back) | 表 (Front) |
|-------|-----------|------------|
| 1 | プッシュ (B1) — 縦スライス **A** / solo **1** | ビジー (F1) — 縦スライス **←** / solo **1** |
| 2 | クリア (B2) — **S** / **2** | ビッグオーダー (F2) — **↓** / **2** |
| 3 | スナップ前 (B3) — **D** / **3** | 在庫赤 (F3) — **→** / **3** |
| 4 | あとで (B4) — **F** / **4** | ラスト客 (F4) — **↑** / **4** |
