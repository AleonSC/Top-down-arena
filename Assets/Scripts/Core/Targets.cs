using System;
using System.Collections.Generic;

namespace Arena
{
    /// Shared answers to "who or what can this go for?": the living players (first, nearest, at random, all of
    /// them), a boss's current target, and whether an enemy can be hit yet. Bosses and abilities use these instead
    /// of each writing its own loop.
    public partial class Game
    {
        /// For picking the most hurt ally: q has a lower health fraction than than (or there's no pick yet).
        static bool MoreHurt(Player q, Player than) => than == null || q.Hp / q.MaxHp < than.Hp / than.MaxHp;

        /// The first player still up (in join order), or null when everyone is down.
        Player FirstAlive()
        {
            foreach (var p in Players.Values)
                if (!p.Downed && !Jailed(p))  // (not someone locked in a Holding Cell: HoldingCell.cs)
                    return p;
            return null;
        }

        /// Every player still up, in join order (a new list each call).
        List<Player> AlivePlayersList()
        {
            var list = new List<Player>();
            foreach (var p in Players.Values)
                if (!p.Downed && !Jailed(p))
                    list.Add(p);
            return list;
        }

        /// The living player nearest `from` (the first in join order on a tie), or null.
        Player NearestPlayer(V2 from)
        {
            Player best = null;
            bool fromCell = HoldingCells.Count > 0 && InCellZone(from);  // (in a Holding Cell only its prisoner counts; outside, nobody locked in one)
            foreach (var p in Players.Values)
                if (!p.Downed && (HoldingCells.Count == 0 || InCellZone(p.Pos) == fromCell) && (best == null || p.Pos.DistanceSq(from) < best.Pos.DistanceSq(from)))
                    best = p;
            return best;
        }

        /// A living player picked with the game's RNG, or null (draws nothing from the RNG when everyone is down).
        Player RandomLivingPlayer()
        {
            var alive = AlivePlayersList();
            return alive.Count == 0 ? null : alive[(int)Rand(0f, alive.Count - 0.001f)];
        }

        /// The player a boss is going for: its TargetId while that player is up, otherwise the first one still up.
        Player BossTarget(Enemy e)
        {
            Player target;
            Players.TryGetValue(e.TargetId, out target);
            return target == null || target.Downed || Jailed(target) ? FirstAlive() : target;
        }

        /// Host: the nearest Targetable enemy to `at` within `range` (null: none). `by`: the player it's for (a summon's owner): with PvP
        /// on, the other players count too, as their stand-ins (Pvp.cs).
        Enemy NearestEnemy(V2 at, float range = float.MaxValue, int by = 0) => NearestEnemy(at, null, range, by);

        /// Host: the same, among the foes `ok` accepts (null: any), e.g. the enemies Force Pact can bind. The first in the
        /// enemy list wins a tie, as in a hand-written loop.
        Enemy NearestEnemy(V2 at, Func<Enemy, bool> ok, float range = float.MaxValue, int by = 0)
        {
            Enemy best = null;
            if (range <= 0f)
                return null;
            float bestD = range < float.MaxValue ? range * range : float.MaxValue;
            foreach (var e in AllFoes(by: by))
            {
                if (ok != null && !ok(e))
                    continue;
                float d = e.Pos.DistanceSq(at);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        /// Host: the nearest Targetable enemy to `to` that reaches within `leash` of `anchor` (its edge counts): a minion
        /// that goes for enemies near its owner but never strays far (guards, thralls, bots).
        Enemy NearestInLeash(V2 to, V2 anchor, float leash, int by = 0)
        {
            Enemy best = null;
            foreach (var e in AllFoes(by: by))
                if (e.Pos.CloserThan(anchor, leash + e.Radius) && (best == null || e.Pos.DistanceSq(to) < best.Pos.DistanceSq(to)))
                    best = e;
            return best;
        }

        /// Where a minion waits at its owner's heel: `dist` px behind their aim to the side (`turn` spreads several).
        static V2 BesideOwner(Player owner, float dist, float turn = 0f) => owner.Pos + V2.FromAngle(owner.Aim + MathX.Pi * 0.75f + turn) * dist;

        /// Host: an enemy that has finished spawning in and is still alive, so it can be hit or picked as a target.
        /// (A client can't use this: it doesn't get SpawnT; see IsSpawning.)
        static bool Targetable(Enemy e) => e.SpawnT <= 0f && e.Hp > 0f;

        /// A player an enemy attack can land on: up and not in their hit grace.
        static bool Hittable(Player p) => !p.Downed && p.InvulnT <= 0f && p.HorseId == 0 && !SacOn(p);  // (in a Trojan Horse: TrojanHorse.cs; Sacrificial Ritual: SacrificialRitual.cs)
    }
}
