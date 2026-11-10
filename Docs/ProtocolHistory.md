# Protocol version history

One line per bump, newest first. When you bump `Session.ProtocolVersion`, add your line at the top
(`NNN: what changed on the wire`). The code keeps only the number, so parallel sessions don't conflict on a long comment.

- 248: Game.BossSlot in the snapshot (the boss between waves, Hunts.cs); Msg.Start's start wave may be negative (the boss after that wave)
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
