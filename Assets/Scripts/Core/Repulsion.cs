using System;

namespace Arena
{
    /// The Repulsion Cannon (the Soldier Healer's first weapon, WeaponId.LightBolt): a cone blast that shoves enemies
    /// away, does a little damage, and knocks tiny health orbs loose from what it hits.
    /// Alt (the weapon-action key), the Undertow (Docs/Mockups/repulsion-pull.html, option B): every blast fills the Undertow
    /// meter by RepelGain(enemies hit), more per enemy the more it hits at once. Hold Alt (at least RepelPullMin in the meter)
    /// and a current fills a narrower, longer cone, dragging enemies toward you at RepelPullSpeed (stopping RepelPullStop in
    /// front of you), slowing them and stinging a little, while the meter drains at RepelDrain a second. You can't fire while
    /// pulling: let go (or run dry) and the next blast lands on the crowd you pulled in, which fills the meter back up. Your game
    /// sends Msg.WeaponDash (DashPullOn when you press, DashPullOff when you let go); the host does the rest (TickRepulsion).
    public partial class Game
    {
        public const float RepulseFxTime = 0.6f;  // how long the blast's effect lives (the smoke); the cone itself is instant
        const float RepelHalfArc = 0.61f;      // 70 degrees across...
        const float RepelArcPerExtra = 0.175f; // ...20 degrees wider per extra projectile
        const float RepelPush = 90f, RepelPushPerExtra = 30f, RepelDmgPerExtra = 2f;
        public const float RepelMeterMax = 100f, RepelGainBase = 5f, RepelGainPer = 1.5f;  // the meter; each enemy a blast hits adds base + per x (the others it hit)
        public const int RepelGainCap = 8;                                                    // (enemies past this many add no more)
        public const float RepelPullMin = 10f, RepelDrain = 35f;                              // to start a pull; drained a second while pulling
        public const float RepelPullReach = 300f, RepelPullHalfArc = 0.52f;                   // the current's cone: 60 degrees across
        public const float RepelPullSpeed = 140f, RepelPullPerExtra = 30f, RepelPullStop = 44f;  // px a second toward you (more per extra projectile); stops this far in front
        public const float RepelPullSlow = 0.6f, RepelPullDps = 4f, RepelPullTick = 0.25f;    // caught enemies move at 60% and take 4 a second
        public const float RepelPullSelfSlow = 0.75f;
        public const float RepelHumEvery = 0.7f;                                              // the hum (Sfx.SpellPullHum) repeats this often while pulling                                         // your speed while pulling

        /// How much a blast that hits `n` enemies adds to the Undertow meter: 5, 13, 24, 38, 55, 75...
        public static float RepelGain(int n)
        {
            n = Math.Min(n, RepelGainCap);
            return n <= 0 ? 0f : n * (RepelGainBase + RepelGainPer * (n - 1));
        }

        const float ShedChance = 0.3f, ShedEvery = 1.5f;  // a hit knocks a tiny orb loose 30% of the time, at most every 1.5 s per enemy

        void FireRepulsion(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            // Homing Rounds condense the blast: narrower, and harder hitting and pushing (Combat.cs, ConeNarrow / ConeFocus).
            float reach = w.Reach * AreaMult(p) * PierceReach(p), half = (RepelHalfArc + RepelArcPerExtra * extra) * ConeNarrow(p);
            float hit = (w.Damage + RepelDmgPerExtra * extra) * dmg * ConeFocus(p), push = (RepelPush + RepelPushPerExtra * extra) * ConeFocus(p);
            V2 origin = p.Pos;
            BroadcastFx(FxKind.Repulse, origin, origin + V2.FromAngle(aim) * (reach + p.Def.BodyR), new Rgba(0.55f, 0.95f, 1f), half, RepulseFxTime, Sfx.ShotRepulse);
            int hits = 0;
            foreach (var e in new System.Collections.Generic.List<Enemy>(Enemies.Values))
            {
                if (!Targetable(e))
                    continue;
                V2 rel = e.Pos - origin;
                float d = rel.Length;
                if (d - e.Radius > reach + p.Def.BodyR)
                    continue;
                float slack = BodySlack(d, e.Radius);
                float off = Math.Abs(MathX.Fmod(rel.Angle - aim + MathX.Pi, MathX.Tau) - MathX.Pi);
                if (off > half + slack)
                    continue;
                if (BlocksShot(e, origin))
                {
                    WearShield(e, hit);  // a shield takes the blast (and wears)
                    continue;
                }
                hits++;
                HurtEnemy(e, hit, p.Id);
                ModHit(e, p.Id, hit, Defs.HolyShot, V2.FromAngle(aim));  // weapon mods ride the blast
                if (!Heavy(e))
                {
                    V2 away = d > 1f ? rel / d : V2.FromAngle(aim);
                    KnockEnemy(e, away * push);  // slides (Knock.cs)
                }
                // Knock a tiny health orb loose now and then.
                if (e.T >= e.ShedT && rng.NextDouble() < ShedChance)
                {
                    e.ShedT = e.T + ShedEvery;
                    SpawnOrb(e.Pos, false, true);
                }
            }
            if (hits > 0)
            {
                p.Run.ShotsHit++;
                SetRepelMeter(p, p.RepelMeterF + RepelGain(hits));
            }
        }

        static void SetRepelMeter(Player p, float v)
        {
            p.RepelMeterF = Math.Max(0f, Math.Min(RepelMeterMax, v));
            p.RepelMeter = (byte)Math.Ceiling(p.RepelMeterF);
        }

        /// Host: a player pressed (on = true) or let go of the weapon-action key with the Repulsion Cannon.
        void HostRepelPull(Player p, bool on)
        {
            if (p.Weapon.Id != WeaponId.LightBolt || State == MatchState.GameOver)
                return;
            if (!on)
            {
                EndRepelPull(p);
                return;
            }
            if (p.RepelPull || p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.ParryActive || p.RepelMeterF < RepelPullMin)
                return;
            p.RepelPull = true;
            p.RepelTickT = 0f;
            p.RepelHumT = RepelHumEvery * 0.6f;  // (the inhale carries it into the first hum)
            BroadcastSound(Sfx.SpellPullStart);
        }

        /// The pull stops (let go, ran dry, or taken out of it): the release pop.
        void EndRepelPull(Player p)
        {
            if (!p.RepelPull)
                return;
            p.RepelPull = false;
            BroadcastSound(Sfx.SpellPullStop);
        }

        /// Host, every tick: the current drags what's in its cone toward you and drains the meter; the meter empties when
        /// the cannon is put away.
        void TickRepulsion(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.Weapon.Id != WeaponId.LightBolt)
                {
                    p.RepelPull = false;
                    SetRepelMeter(p, 0f);
                    continue;
                }
                if (!p.RepelPull)
                    continue;
                if (p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.ParryActive || State == MatchState.GameOver || p.RepelMeterF <= 0f)
                {
                    EndRepelPull(p);
                    continue;
                }
                SetRepelMeter(p, p.RepelMeterF - RepelDrain * dt);
                RepelPullStep(p, dt);
                if ((p.RepelHumT -= dt) <= 0f)
                {
                    p.RepelHumT += RepelHumEvery;
                    BroadcastSound(Sfx.SpellPullHum);
                }
                if (p.RepelMeterF <= 0f)
                    EndRepelPull(p);  // ran dry
            }
        }

        /// The Undertow's cone for `p`: how far it reaches from your middle, and half its width (the screen draws the same).
        public float RepelPullReachOf(Player p) => RepelPullReach * AreaMult(p) * PierceReach(p) + p.Def.BodyR;
        public float RepelPullHalfOf(Player p) => (RepelPullHalfArc + RepelArcPerExtra * ExtraShots(p)) * ConeNarrow(p);

        void RepelPullStep(Player p, float dt)
        {
            int extra = ExtraShots(p);
            float reach = RepelPullReachOf(p), half = RepelPullHalfOf(p);
            float speed = (RepelPullSpeed + RepelPullPerExtra * extra) * ConeFocus(p);
            p.RepelTickT -= dt;
            bool sting = p.RepelTickT <= 0f;
            if (sting)
                p.RepelTickT += RepelPullTick;
            float dmg = RepelPullDps * RepelPullTick * p.Stats.DmgMult;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                V2 rel = e.Pos - p.Pos;
                float d = rel.Length;
                if (d < 1f || d - e.Radius > reach)
                    continue;
                float slack = (float)Math.Asin(Math.Min(1f, e.Radius / d));
                if (AngleTo(p.Pos, e.Pos, p.Aim) > half + slack)
                    continue;
                if (!Heavy(e))
                {
                    float stop = e.Radius + p.Def.BodyR + RepelPullStop;
                    if (d > stop)
                        e.Pos = ClampToArena(e.Pos - rel / d * Math.Min(d - stop, speed * dt), e.Radius);
                    Slow(e, 0.2f, RepelPullSlow);
                }
                if (!sting)
                    continue;
                if (BlocksShot(e, p.Pos))
                    WearShield(e, dmg);
                else
                    HurtEnemy(e, dmg, p.Id);
            }
        }
    }
}
