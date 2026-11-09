# Lag chart

A toggleable overlay for finding out why co-op feels laggy (owner's request, Nov 2026, protocol 231). Player-facing
reference: README → *Playing with a friend* → *Lag chart (F5)*.

## What it shows

| Feature | Where it comes from |
| --- | --- |
| Ping graph (30 s, a column every 0.25 s) | `Msg.Ping`/`Pong`, now 4 a second (`Game.PingEvery`); `LagStats.AddPing` |
| Snapshot gaps and stalls (> 150 ms) | `Game.NoteSnapshotLag` on every `Msg.Snapshot`; a gap still open counts toward its column, so a stall shows while it happens. Counted only while fighting and not paused |
| Your frame time and FPS, 1% low | `ArenaApp.LagChartUpdate` → `Game.LagFrame` (unscaled frame time); a 1 ms histogram for the 1% low |
| Good / OK / Poor pill | `LagStats.Quality`: ping over the last 3 s (80 / 150 ms), a stall in the last 5 s (any / 300 ms), FPS (40 / 20) |
| Bandwidth in / out, snapshot size | `NetPeer.BytesIn/BytesOut` (every byte, length prefixes included) |
| Every player's ping | The client's `Msg.Ping` carries its last round trip (ushort ms); the host keeps them (`Session.PeerPingMs`) and broadcasts `Msg.NetPings` once a second. The host plots the worst guest's ping as its own line |
| Event ticks | `Game.MarkLagEvents`, every 6 steps: wave start, a boss arriving, a bullet storm (450+ enemy bullets; it ends below 300) |
| Freeze (Shift+F5) | `LagStats.Frozen`: the strip stops, totals keep counting |
| Copy summary (Ctrl+F5) | `LagStats.Summary` → `GUIUtility.systemCopyBuffer` |
| CSV log | `LagStats.LogPath`, one file per run in `<profile>/lag/lag-<date>.csv`, flushed every 5 s and when the run ends |

## Controls and settings

F5 cycles off → pill → chart. Settings → Gameplay → **Lag chart** (Off / Pill / Chart) and **Log lag to a file**,
saved in PlayerPrefs (`lag_chart`, `lag_log`), so per machine, not per profile. F5 can't be given to an action in
Keybinds.

## Checking it

- `Tools/SimTest/run.sh 1 1 lag` (also last in the feature suite).
- `LAG=pill|chart|guest [LAGFROZEN=1] Tools/Preview/shot.sh lag hud 6 12.2 3`: the overlay with made-up numbers
  (`Tools/Preview/ShotLag.cs`).

## Ideas not built

- A per-player ping history (only the host's worst-guest line is kept).
- Bandwidth broken down by message type.
