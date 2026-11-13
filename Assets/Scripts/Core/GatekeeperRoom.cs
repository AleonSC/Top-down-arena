using System;
using System.Collections.Generic;

namespace Arena
{
    /// The mini-boss room of the Expanding arena (Docs/FloatingRooms.md step 3; RoomKind.Gatekeeper, kept for saves): a floating room that opens once a built room
    /// touches it. Its foe is its faction's mini-boss (ExpansionRoom.Mini, FactionMinis.cs; the Gatekeeper is the Data Gates' only).
    /// Once it is open, the first player to walk in starts the duel, during a regular wave or between waves (not just before a boss,
    /// whose beam would pull you out): the mini-boss wakes in the middle and the room locks behind whoever is inside (you can't leave
    /// until it falls). A duel begun between waves holds the countdown till it is settled. Beating it gives everyone the vault's prize pick and the room becomes
    /// an ordinary combat room. An unbeaten duel ends with the wave: the mini-boss goes, anyone inside is let out, and it waits for the
    /// next wave. The duel needs no extra sync: the mini-boss is an enemy everyone already sees, and the room's lock is read from it.
    public partial class Game
    {
        /// An unsealed mini-boss room of the Expanding map.
        bool IsDuelRoom(int area)
        {
            if (!ExpandingOn || !RoomsOn)
                return false;
            var areas = CurrentWorld.Areas;
            return area > 0 && area < areas.Length && areas[area].Kind == Arena.RoomKind.Gatekeeper && !areas[area].Sealed;
        }

        /// The mini-boss fighting in this room right now (null: no duel).
        Enemy DuelBoss(int area)
        {
            if (!IsDuelRoom(area))
                return null;
            var a = CurrentWorld.Areas[area];
            foreach (var e in Enemies.Values)
                if (FactionMinis.Duels(e.Type) && e.Hp > 0f && a.Contains(e.Disp, 40f))
                    return e;
            return null;
        }

        /// A foe that belongs to a duel (the wave ends without it, as it does without the Wings vault's).
        bool IsDuelFoe(Enemy e) => ExpandingOn && (FactionMinis.Duels(e.Type) || IsDuelAdd(e)) && IsDuelRoom(CurrentWorld.AreaAt(e.Disp));

        /// One a duel's mini-boss called in (the Riot Captain's Taser Guards): it fights in the locked room with it.
        bool IsDuelAdd(Enemy e)
        {
            if (e.ParentId == 0)
                return false;
            foreach (var id in duelBoss.Values)
                if (id == e.ParentId)
                    return true;
            return false;
        }

        /// The mini-boss a room of the Expanding map holds: its own, or (an old save's room) the next boss's faction's.
        EnemyType DuelFoeOf(int area)
        {
            int i = area - 1;
            var t = i >= 0 && i < Expanded.Count ? (EnemyType)Expanded[i].Mini : default(EnemyType);
            return FactionMinis.Is(t) ? t : MiniBossNow();
        }

        /// Between waves, may a mini-boss wake now? Not before a boss (everyone is beamed into the hub for it, BossBeam.cs).
        bool DuelsBetweenWaves => State == MatchState.Intermission && !BossNext && !BossStartsNext && Practice == null && Hunt == null;

        /// A duel begun between waves is on: the countdown to the next wave waits for it (Waves.cs).
        bool DuelHoldsWave => State == MatchState.Intermission && duelBoss.Count > 0;

        /// Host, every frame of a regular wave and between waves: wake the mini-boss when someone walks in, and settle a duel that was won.
        void TickDuelRooms()
        {
            if (!ExpandingOn || !RoomsOn || !(State == MatchState.Fighting || State == MatchState.Intermission))
                return;
            var areas = CurrentWorld.Areas;
            for (int i = 1; i < areas.Length; i++)
            {
                if (!IsDuelRoom(i))
                    continue;
                var a = areas[i];
                if (duelBoss.TryGetValue(i, out int id))
                {
                    Enemy boss;
                    if (!Enemies.TryGetValue(id, out boss) || boss.Hp <= 0f)
                        WinDuel(i);
                    continue;
                }
                if (State == MatchState.Intermission && !DuelsBetweenWaves)
                    continue;
                foreach (var p in Players.Values)
                    if (!p.Downed && a.Contains(p.Pos, -p.Def.BodyR - 30f))
                    {
                        var boss = SpawnEnemyAt(DuelFoeOf(i), a.Mid, 1.2f, 0);
                        boss.Home = i;
                        boss.T2 = 2.5f;
                        duelBoss[i] = boss.Id;
                        DoLog(boss.Def.Name + " wakes: " + p.Name + " walked into " + a.Name);
                        break;
                    }
            }
        }

        readonly Dictionary<int, int> duelBoss = new Dictionary<int, int>();  // host: area -> its mini-boss's id

        /// The mini-boss is down: the prize pick for everyone, and the room is an ordinary one from now on.
        void WinDuel(int area)
        {
            int was;
            if (duelBoss.TryGetValue(area, out was))
                ClearCaptainAdds(was);  // its men go with it (RiotCaptain.cs)
            duelBoss.Remove(area);
            GrantVaultPrize(CurrentWorld.Areas[area].Mid);
            int i = area - 1;  // (area 0 is the hub; the rooms follow in order)
            if (i >= 0 && i < Expanded.Count)
            {
                var r = Expanded[i];
                r.Kind = Arena.RoomKind.Combat;
                Expanded[i] = r;
                expWorld = null;
                BroadcastExpansion();
            }
            DoLog("the mini-boss room is cleared");
        }

        /// A spot just past the room's door, in the room beyond.
        V2 DuelDoorOutside(int area)
        {
            var areas = CurrentWorld.Areas;
            foreach (var d in CurrentWorld.Doors)
                if (d.Joins(area))
                {
                    var other = areas[d.Other(area)];
                    V2 toward = other.Contains(d.Pos + d.Across * Rooms.DoorPad) ? d.Across : new V2(-d.Across.X, -d.Across.Y);
                    return d.Pos + toward * (Rooms.DoorPad + 40f);  // (just past the door, in the room beyond)
                }
            return areas[area].Mid;
        }

        /// Host, every enemy move: while a duel has its room locked, no other foe may be in it (you can't leave, so they'd trap you with them).
        void KeepOutOfDuel(Enemy e)
        {
            if (duelBoss.Count == 0 || FactionMinis.Duels(e.Type) || !IsHost || IsDuelAdd(e))
                return;
            var areas = CurrentWorld.Areas;
            foreach (var area in duelBoss.Keys)
                if (area < areas.Length && areas[area].Contains(e.Pos))
                {
                    e.Pos = DuelDoorOutside(area);
                    return;
                }
        }

        /// Host, as a wave is cleared: an unbeaten mini-boss (and its men) goes, anyone still in its room is put out of the door.
        void EndDuels()
        {
            if (duelBoss.Count == 0)
                return;
            var ids = new List<int>(duelBoss.Keys);
            foreach (var id in duelBoss.Values)
                ClearCaptainAdds(id);
            duelBoss.Clear();
            var areas = CurrentWorld.Areas;
            foreach (var area in ids)
            {
                if (area >= areas.Length)
                    continue;  // (the map changed under the duel)
                var a = areas[area];
                foreach (var e in new List<Enemy>(Enemies.Values))
                    if (FactionMinis.Duels(e.Type) && a.Contains(e.Disp, 40f))
                        Enemies.Remove(e.Id);
                V2 outside = DuelDoorOutside(area);
                foreach (var p in Players.Values)
                    if (a.Contains(p.Pos))
                        MovePlayer(p, outside);
            }
        }
    }
}
