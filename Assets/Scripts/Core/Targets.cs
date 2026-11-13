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
                if (!p.Downed)
                    return p;
            return null;
        }

        /// Every player still up, in join order (a new list each call).
        List<Player> AlivePlayersList()
        {
            var list = new List<Player>();
            foreach (var p in Players.Values)
                if (!p.Downed)
                    list.Add(p);
            return list;
        }

        /// The living player nearest `from` (the first in join order on a tie), or null.
        Player NearestPlayer(V2 from)
        {
            Player best = null;
            foreach (var p in Players.Values)
                if (!p.Downed && (best == null || p.Pos.DistanceSq(from) < best.Pos.DistanceSq(from)))
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
            return target == null || target.Downed ? FirstAlive() : target;
        }

        /// Host: an enemy that has finished spawning in and is still alive, so it can be hit or picked as a target.
        /// (A client can't use this: it doesn't get SpawnT; see IsSpawning.)
        static bool Targetable(Enemy e) => e.SpawnT <= 0f && e.Hp > 0f;
    }
}
