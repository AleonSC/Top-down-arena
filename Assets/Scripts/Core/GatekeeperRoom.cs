using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Gatekeeper room of the Expanding arena (Docs/FloatingRooms.md step 3): a floating room that opens once a built room touches it.
    /// While a regular wave is on, the first player to walk in starts the duel: The Gatekeeper wakes in the middle and the room locks
    /// behind whoever is inside (you can't leave until it falls). Beating it gives everyone the vault's prize pick and the room becomes
    /// an ordinary combat room. An unbeaten duel ends with the wave: the Gatekeeper goes, anyone inside is let out, and it waits for the
    /// next wave. The duel needs no extra sync: the Gatekeeper is an enemy everyone already sees, and the room's lock is read from it.
    public partial class Game
    {
        /// An unsealed Gatekeeper room of the Expanding map.
        bool IsDuelRoom(int area)
        {
            if (!ExpandingOn || !RoomsOn)
                return false;
            var areas = CurrentWorld.Areas;
            return area > 0 && area < areas.Length && areas[area].Kind == Arena.RoomKind.Gatekeeper && !areas[area].Sealed;
        }

        /// The Gatekeeper fighting in this room right now (null: no duel).
        Enemy DuelBoss(int area)
        {
            if (!IsDuelRoom(area))
                return null;
            var a = CurrentWorld.Areas[area];
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Gatekeeper && e.Hp > 0f && a.Contains(e.Disp, 40f))
                    return e;
            return null;
        }

        /// A foe that belongs to a duel (the wave ends without it, as it does without the Wings vault's).
        bool IsDuelFoe(Enemy e) => ExpandingOn && e.Type == EnemyType.Gatekeeper && IsDuelRoom(CurrentWorld.AreaAt(e.Disp));

        /// Host, every frame of a regular wave: wake the Gatekeeper when someone walks in, and settle a duel that was won.
        void TickDuelRooms()
        {
            if (!ExpandingOn || !RoomsOn || State != MatchState.Fighting)
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
                foreach (var p in Players.Values)
                    if (!p.Downed && a.Contains(p.Pos, -p.Def.BodyR - 30f))
                    {
                        var boss = SpawnEnemyAt(EnemyType.Gatekeeper, a.Mid, 1.2f, 0);
                        boss.Home = i;
                        boss.T2 = 2.5f;
                        duelBoss[i] = boss.Id;
                        DoLog("the Gatekeeper wakes: " + p.Name + " walked into " + a.Name);
                        break;
                    }
            }
        }

        readonly Dictionary<int, int> duelBoss = new Dictionary<int, int>();  // host: area -> its Gatekeeper's id

        /// The Gatekeeper is down: the prize pick for everyone, and the room is an ordinary one from now on.
        void WinDuel(int area)
        {
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
            DoLog("the Gatekeeper room is cleared");
        }

        /// Host, as a wave is cleared: an unbeaten Gatekeeper goes, anyone still in its room is put out of the door.
        void EndDuels()
        {
            if (duelBoss.Count == 0)
                return;
            var ids = new List<int>(duelBoss.Keys);
            duelBoss.Clear();
            var areas = CurrentWorld.Areas;
            foreach (var area in ids)
            {
                var a = areas[area];
                foreach (var e in new List<Enemy>(Enemies.Values))
                    if (e.Type == EnemyType.Gatekeeper && a.Contains(e.Disp, 40f))
                        Enemies.Remove(e.Id);
                V2 outside = a.Mid;
                foreach (var d in CurrentWorld.Doors)
                    if (d.Joins(area))
                    {
                        outside = d.Side(d.Other(area)) + (d.Side(d.Other(area)) - d.Pos).Normalized() * 40f;
                        break;
                    }
                foreach (var p in Players.Values)
                    if (a.Contains(p.Pos))
                        MovePlayer(p, outside);
            }
        }
    }
}
