using System;
using System.Collections.Generic;

namespace Arena
{
    /// World Tree (Healer Summoner's ultimate): a great tree that grows where you stand for 15 s (20 s mastered). Its zone is now
    /// small (100 px, was 170) and still heals everyone in it, slows and burns enemies in it. Every `WorldTreeWaveEvery` s it
    /// sends a wave of healing butterflies, one to each ally (the most hurt first, 0.16 s apart), instead of the old sprites.
    /// A butterfly flies in a fluttering curve to its ally and heals them when it arrives. Host logic only; the zone's timer is
    /// replicated so every screen works out the tree's growth and fade, and butterflies are ordinary replicated minions
    /// (`LinkTo` = the ally they fly to). Looks: Game/WorldTreeLook.cs. Mockup: Docs/Mockups/world-tree.html (look A, Heartwood).
    public partial class Game
    {
        public const float WorldTreeRadius = 100f, WorldTreeTime = 15f, WorldTreeTimeMastered = 20f;
        public const float WorldTreeGrow = 1.4f, WorldTreeFade = 1.2f;  // the tree grows in over this long, and fades over the last of its time
        public const float WorldTreeFirstWave = 0.9f, WorldTreeWaveEvery = 1.9f, WorldTreeWaveEveryMastered = 1.4f;
        public const float WorldTreeHeal = 14f, WorldTreeHealMastered = 18f;  // per butterfly on arrival (the zone's own 18 a second comes on top)
        public const float WorldTreeStagger = 0.16f;  // between the butterflies of one wave
        const float ButterflySpeed = 240f, ButterflyLife = 5f, ButterflyWobble = 85f;

        /// The tree's zone in this game is mastered when it was cast with the longer time.
        public static bool WorldTreeIsMastered(Zone z) => z.Duration > WorldTreeTime + 1f;

        /// Host, each tick for a World Tree zone: send a wave whenever one falls due.
        void WorldTreeWave(Zone z, float dt)
        {
            bool cap = WorldTreeIsMastered(z);
            float every = cap ? WorldTreeWaveEveryMastered : WorldTreeWaveEvery;
            float now = z.Duration - z.T, before = now - dt;
            float last = z.Duration - WorldTreeFade - 0.8f;  // nothing new leaves as the tree fades
            for (int k = 0; ; k++)
            {
                float at = WorldTreeFirstWave + every * k;
                if (at > now || at > last)
                    break;
                if (at <= before)
                    continue;
                var owner = PlayerOrNull(z.Owner);
                if (owner == null)
                    return;
                var allies = new List<Player>();
                foreach (var q in Players.Values)
                    if (q.Id != z.Owner && !q.Downed)
                        allies.Add(q);
                allies.Sort((a, b) => (a.Hp / Math.Max(1f, a.MaxHp)).CompareTo(b.Hp / Math.Max(1f, b.MaxHp)));
                for (int i = 0; i < allies.Count; i++)
                {
                    var m = SpawnMinion(MinionKind.Butterfly, owner, z.Pos + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(4f, 18f), ButterflyLife + i * WorldTreeStagger);
                    m.LinkTo = allies[i].Id;
                    m.Launch = i * WorldTreeStagger;
                    m.HealRate = (cap ? WorldTreeHealMastered : WorldTreeHeal) * z.HealMult;
                    m.Vel = V2.FromAngle(Rand(0f, MathX.Tau)) * 60f;
                    m.Aim = m.Vel.Angle;
                    m.Ang = Rand(0f, MathX.Tau);  // its own phase for the flutter
                }
            }
        }

        /// Host, each tick for a butterfly: wait its turn, then flutter after its ally and heal them on arrival. True once it has arrived
        /// (or its ally is gone).
        bool MoveButterfly(Minion m, Player owner, float dt)
        {
            if (m.Launch > 0f)
            {
                m.Launch -= dt;
                return false;
            }
            Player who;
            if (!Players.TryGetValue(m.LinkTo, out who) || who.Downed)
            {
                who = null;  // its ally fell or left: the most hurt one still up
                foreach (var q in Players.Values)
                    if (q.Id != m.Owner && !q.Downed && (who == null || q.Hp / Math.Max(1f, q.MaxHp) < who.Hp / Math.Max(1f, who.MaxHp)))
                        who = q;
                if (who == null)
                    return true;
                m.LinkTo = who.Id;
            }
            m.SentT += dt;
            V2 to = who.Pos - m.Pos;
            float dist = to.Length;
            if (dist < 16f + who.Def.BodyR * 0.5f)
            {
                Heal(who, m.HealRate, owner, "World Tree");
                BroadcastFx(FxKind.Ring, who.Pos, new Rgba(0.75f, 1f, 0.6f), 30f, 0.35f, Sfx.None);
                return true;
            }
            V2 dir = to / Math.Max(1f, dist);
            float wob = (float)Math.Sin(m.SentT * 5f + m.Ang) * ButterflyWobble * Math.Min(1f, dist / 90f);  // a sideways flutter that dies away near the ally
            V2 want = dir * ButterflySpeed + dir.Orthogonal() * wob;
            m.Vel += (want - m.Vel) * Math.Min(1f, 3.2f * dt);
            m.Pos += m.Vel * dt;
            if (m.Vel.Length > 1f)
                m.Aim = m.Vel.Angle;
            return false;
        }
    }
}
