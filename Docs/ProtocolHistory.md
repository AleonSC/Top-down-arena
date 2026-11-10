# Protocol version history

One line per bump, newest first. When you bump `Session.ProtocolVersion`, add your line at the top
(`NNN: what changed on the wire`). The code keeps only the number, so parallel sessions don't conflict on a long comment.
Before bumping, `git fetch origin main` and take main's number + 1, so two sessions don't ship the same number.

- 263: the Fuma Shuriken's Orbit stance: snapshots carry `Player.FumaStance`, then (in the Orbit stance) its move, step, stars, stolen stars, clock, anchor, start radius, move start, lash aim and steal time; `Msg.WeaponDash` kind 24 switches stance
- 262: `ShopAction.Vectors` (the vendor's vector pack, +10 vectors for 50 gold) in Msg.Shop
- 261: portal links: the Expanding arena's bought portal pairs (PortalLink list after the openings in Msg.ExpandState), Msg.ExpandLink, RoomKind.Link proposals (ExpansionGame.cs)
- 260: snapshots carry each player's Double Barrel shells and charge (`ScatShells`, `ScatCharge`); new `Msg.WeaponDash` kinds 21-23, `ZoneKind.FlakMine`/`TearGas`, `FxKind.DoubleBarrel`
- 259: the Repulsion Cannon's Undertow: `Player.RepelMeter` (byte) and `Player.RepelPull` (bool) after `TwinCharge` in each player's snapshot; `Msg.WeaponDash` kinds 19/20 (pull on/off)
- 258: shop shelf grows with Add stock: `Player.ShelfSold` is an Int (was a Byte) and only `Game.ShelfCount` shelf slots are sent (6 per stock, up to 4 stocks)
- 257: snapshots carry the gold coins on the ground (`Game.Coins`: count, then id, value, position each)
- 257 (second, shipped in parallel with the coins; 258 on carries both): boss wave beam-in: `FxKind.BossBeam` (last in the enum) and `Player.WarpT` (a float after `SwallowT` in each player's snapshot).
- 256: opened walls: the Expanding arena's openings (WallCut list after the rooms in Msg.ExpandState), Msg.ExpandCut, RoomKind.Opening proposals (ExpansionGame.cs)
- 255: Msg.ExpandDraw carries the drawn room's theme (a PathId byte; themed rooms, ExpansionGame.cs)
- 254: room kits (RoomToken list after the rooms in Msg.ExpandState), Msg.ExpandApply, ShopAction.RoomKit (the vendor sells one) (ExpansionGame.cs, Shop.cs)
- 253: the Rapier's Riposte stance: Player.RapierGuard (bool) and RapierCharge (byte) in the player snapshot; Msg.WeaponDash kinds 17 and 18 (stance on, lunge on release)
- 252: ExpansionRoom.Floating in Msg.ExpandState's room list; RoomKind.Vendor and Gatekeeper at the end (the floating rooms, ExpansionGame.cs)
- 251: no wire change: main had two different changes both numbered 250 (Game.BossSlot, and the vector cap cards at the end of Upgrades.All), so builds with only one of them could join each other; everyone on 251 has both
- 250: Game.BossSlot in the snapshot (the boss between waves, Hunts.cs); Msg.Start's start wave may be negative (the boss after that wave)
- 250: two new upgrades at the end of Upgrades.All (Deep Inkwell, Architect: the Expanding arena's vector cap cards, Core/Upgrades.cs)
- 249: the Expanding arena's building rounds: Msg.ExpandState (rooms with owners, open flag, proposals, votes), Msg.ExpandVote carries a proposer id, the purse is gone from the snapshot (ExpansionGame.cs)
- 248: Player.Vectors and VectorCap in the player snapshot (the Expanding arena's drawing budget, ExpansionGame.cs)
- 247: Msg.ExpandState carries whether the offer is optional (ExpansionGame.cs)
- 246: a Wildcard's LobbyEntry.Ult / Msg.Start ult byte is now class * 4 + key (any class's DPS ultimate), no longer always 0
- 245: the Expanding arena's drawn rooms (Msg.ExpandDraw, a custom card in Msg.ExpandState; ExpansionGame.cs)
- 244: the Expanding arena (DiffKnob.ArenaExpanding, Msg.ExpandState/ExpandVote, the team purse in the snapshot; ExpansionGame.cs)
- 243: EnemyType.SecurityGuard and bullet kind TracerRound (SecurityGuard.cs, Game/SecurityLook.cs)
- 242: EnemyType.TaserGuard, bullet kind TaserBolt and FxKind.TaserHit (WardenFaction.cs, Game/TaserLook.cs)
- 241: Enemy.Cells (a bubble's twelve cells in the enemy snapshot, Variety.cs)
- 240: the Katana's stance combos (Player.KatanaHay/Sen, Enemy.KatanaShocked, Defs.Caltrop, four FxKinds, three Sfx)
- 239: LobbyEntry.XpPct/BestWave/Kills/BossKills (the inspect card's records, in Msg.Cosmetics and Msg.Lobby)
- 238: Msg.Respawn (the Practice Range's respawn and restart wave, PracticeRespawn.cs)
- 237: Player.Wound in the snapshot (Combat.cs)
- 236: main (235) merged with the Rooms arena (233-234: DiffKnob.ArenaRooms, the layout byte's top bit, SyncRooms)
