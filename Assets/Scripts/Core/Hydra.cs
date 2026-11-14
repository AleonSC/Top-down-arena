using System.Collections.Generic;

namespace Arena
{
    /// Hydra (DPS Summoner, replaces Sacrifice; Docs/Mockups/summoner-sacrifice-rework.html concept B): a three-headed
    /// hydra rises where your cursor is and stays 8 s. The heads take turns to spit a venom bolt, each at a different
    /// enemy (the nearest, the second nearest, ...), so one summon covers three targets. Mastered: a fourth head.
    public partial class Game
    {
        const float HydraTime = 8f, HydraBoltSpeed = 560f;

        /// The head count rides in LinkTo (replicated, and otherwise only a Soul Lantern's), so clients can draw them.
        public static int HydraHeads(Minion m) => m.LinkTo > 0 ? m.LinkTo : 3;

        void CastHydra(Player p, V2 target, float power)
        {
            foreach (var old in Minions.Values)
                if (old.Owner == p.Id && old.Kind == MinionKind.Hydra)
                    old.T = 0.01f;  // a new hydra replaces the last
            var m = SpawnMinion(MinionKind.Hydra, p, target, HydraTime * power + p.Stats.HydraBonusTime);  // Venom Glands: it stays longer
            m.LinkTo = castMastered ? 4 : 3;
            BroadcastFx(FxKind.Ring, m.Pos, m.Def.Color, 90f, 0.5f, Sfx.Summon);
        }

        /// Host, each tick. m.Cd has already counted down (faster with fire rate and frenzy). A head fires every 0.6 s,
        /// so the hydra fires one bolt every 0.6 / heads s, passing from head to head.
        void TickHydra(Minion m, float dmg)
        {
            if (m.Cd > 0f || !Battle || m.T < 0f || m.T > m.Duration - 0.5f)  // not while it is still rising
                return;
            var d = m.Def;
            var near = new List<Enemy>();
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Pos.DistanceTo(m.Pos) < d.Range)
                    near.Add(e);
            if (near.Count == 0)
                return;
            near.Sort((a, b) => a.Pos.DistanceSq(m.Pos).CompareTo(b.Pos.DistanceSq(m.Pos)));
            int heads = HydraHeads(m);
            m.Head = (m.Head + 1) % heads;
            Enemy tg = near[m.Head % near.Count];  // head 0 the nearest, head 1 the next, ...
            m.Cd = d.Interval / heads;
            V2 dir = (tg.Pos - m.Pos).Normalized();
            m.Aim = dir.Angle;
            var bolt = SpawnBullet(Defs.MinionShot, m.Pos + dir * (d.Radius + 18f), dir * HydraBoltSpeed, 0f, d.Range / HydraBoltSpeed, dmg, m.Owner);
            Player owner;
            if (bolt != null && Players.TryGetValue(m.Owner, out owner))
                bolt.SlowAmt = owner.Stats.HydraSlow;  // Venom Glands: the venom slows what it hits
        }

        /// What a client needs to draw the heads: the enemy each head faces, worked out the same way from the snapshot.
        public static Enemy HydraTarget(IEnumerable<Enemy> enemies, Minion m, int head)
        {
            var near = new List<Enemy>();
            foreach (var e in enemies)
                if (e.SpawnT <= 0f && e.Pos.DistanceTo(m.Pos) < m.Def.Range)
                    near.Add(e);
            if (near.Count == 0)
                return null;
            near.Sort((a, b) => a.Pos.DistanceSq(m.Pos).CompareTo(b.Pos.DistanceSq(m.Pos)));
            return near[head % near.Count];
        }
    }
}
