using System;

namespace Arena
{
    /// Water Ward, the Healer Elementalist's 4th choice (`AbilityId.WaterWard`, `AbilityDef.Fourth`: the lobby's 4th choice),
    /// from Docs/Mockups/water-ward.html (look C, Bubbles). Each cast calls WwPerCast drops of water down from above (WwFallGap s apart, each
    /// falling WwFall s); they land on a ring WwOrbit px round you and circle you, up to WwMax. The cast is refused while you hold WwMax.
    ///   splash: a landed drop touching an enemy bursts: WwSplashDamage to every enemy within WwSplashR of it, and each is soaked **wet**
    ///     (Tsunami.cs: slowed 30%, lightning arcs to it) for WwWet s;
    ///   burst: a landed drop meeting a blockable enemy shot eats it and bursts into a mist that soaks everything within WwBurstR (no damage).
    /// The drops keep spreading out to even spacing round you (host, in TickWaterWards) and last until used, or until you go down.
    /// Mastered: splashes and bursts reach WwMasteredReach times as far. Its Rare card Deep Current adds Stats.WwDmgMult and WwWetBonus.
    /// Replicated on the Player: WwAng (the ring's turn) and each drop's place on it and fall left (WwOff, WwFall: a byte each), so every
    /// screen draws the drops where the host has them. FxKind.WwSplash / WwBurst are the two bursts (Game/WaterWardLook.cs).
    public partial class Game
    {
        public const float WwCooldown = 3f, WwOrbit = 54f, WwSpin = MathX.Tau / 2.4f, WwDropR = 8f, WwFall = 0.45f, WwFallGap = 0.12f;
        public const float WwSplashR = 52f, WwSplashDamage = 12f, WwBurstR = 72f, WwWet = 4f, WwMasteredReach = 1.4f, WwSpread = 3.2f;
        public const float WwSplashT = 1.4f, WwBurstT = 1.2f;  // (the splash's puddle outlives its rings)
        public const int WwPerCast = 2, WwMax = 10;
        public static readonly Rgba WwColor = new Rgba(0.31f, 0.82f, 0.91f);  // Tsunami's wet cyan

        /// Where drop i is now: on the ring round its owner (falling drops too: the look lifts them).
        public static V2 WwDrop(Player p, int i) => WwDropAt(p.Pos, p, i);

        public static V2 WwDropAt(V2 centre, Player p, int i) => centre + V2.FromAngle(p.WwAng + p.WwOff[i]) * WwOrbit;

        /// A drop still falling can't splash or block yet.
        public static bool WwLanded(Player p, int i) => p.WwFall[i] <= 0f;

        /// Pressing Water Ward with every drop out does nothing (the cooldown isn't spent).
        public static bool WwFull(Player p) => p.WwOff.Count >= WwMax;

        void CastWaterWard(Player p, bool cap)
        {
            p.WwWide = cap;
            int n = Math.Min(WwPerCast, WwMax - p.WwOff.Count);
            for (int k = 0; k < n; k++)
            {
                p.WwOff.Add(WwGap(p));  // the middle of the widest gap, so the ring spreads out least
                p.WwFall.Add(WwFall + k * WwFallGap);
            }
            BroadcastFx(FxKind.Ring, p.Pos, WwColor, WwOrbit + 14f, 0.5f, Sfx.WwCast);  // (its sound lands each drop in time: WaterWardSoundBank.cs)
        }

        /// The middle of the widest gap between a player's drops (any angle when there are none).
        static float WwGap(Player p)
        {
            int n = p.WwOff.Count;
            if (n == 0)
                return p.Aim + MathX.Pi * 0.5f - p.WwAng;
            if (n == 1)
                return p.WwOff[0] + MathX.Pi;
            float best = -1f, at = 0f;
            for (int i = 0; i < n; i++)
            {
                float a = MathX.Fmod(p.WwOff[i], MathX.Tau), next = float.MaxValue;
                for (int j = 0; j < n; j++)  // the nearest drop ahead of it round the ring
                {
                    if (j == i)
                        continue;
                    float d = MathX.Fmod(p.WwOff[j] - a, MathX.Tau);
                    if (d <= 0f)
                        d += MathX.Tau;
                    next = Math.Min(next, d);
                }
                if (next > best)
                {
                    best = next;
                    at = a + next * 0.5f;
                }
            }
            return at;
        }

        /// Host, every tick: the ring turns, drops land and spread out evenly, and landed drops splash on the enemies they touch.
        void TickWaterWards(float dt)
        {
            foreach (var p in Players.Values)
            {
                int n = p.WwOff.Count;
                if (n == 0)
                    continue;
                if (p.Downed)
                {
                    ClearWaterWard(p);
                    continue;
                }
                p.WwAng = MathX.Fmod(p.WwAng + WwSpin * dt, MathX.Tau);
                for (int i = 0; i < n; i++)
                    p.WwFall[i] = Math.Max(0f, p.WwFall[i] - dt);
                WwSpace(p, dt);
                for (int i = p.WwOff.Count - 1; i >= 0; i--)
                {
                    if (!WwLanded(p, i))
                        continue;
                    V2 d = WwDrop(p, i);
                    foreach (var e in Enemies.Values)
                    {
                        if (!Targetable(e) || d.FartherThan(e.Pos, e.Radius + WwDropR))
                            continue;
                        WwSplash(p, d);
                        WwRemove(p, i);
                        break;
                    }
                }
            }
        }

        /// Ease the drops toward even spacing: keep their order round the ring, and the even ring that sits nearest them on average.
        static void WwSpace(Player p, float dt)
        {
            int n = p.WwOff.Count;
            if (n < 2)
                return;
            for (int i = 0; i < n; i++)
                p.WwOff[i] = MathX.Fmod(p.WwOff[i], MathX.Tau);
            for (int i = 1; i < n; i++)  // (insertion sort, both lists together: ten at most)
                for (int j = i; j > 0 && p.WwOff[j] < p.WwOff[j - 1]; j--)
                {
                    float o = p.WwOff[j]; p.WwOff[j] = p.WwOff[j - 1]; p.WwOff[j - 1] = o;
                    float f = p.WwFall[j]; p.WwFall[j] = p.WwFall[j - 1]; p.WwFall[j - 1] = f;
                }
            float step = MathX.Tau / n, sx = 0f, sy = 0f;
            for (int i = 0; i < n; i++)
            {
                float a = p.WwOff[i] - i * step;
                sx += (float)Math.Cos(a);
                sy += (float)Math.Sin(a);
            }
            float b = (float)Math.Atan2(sy, sx), k = Math.Min(1f, dt * WwSpread);
            for (int i = 0; i < n; i++)
                p.WwOff[i] += (MathX.WrapPi(b + i * step - p.WwOff[i])) * k;
        }

        static void WwRemove(Player p, int i)
        {
            p.WwOff.RemoveAt(i);
            p.WwFall.RemoveAt(i);
        }

        static void ClearWaterWard(Player p)
        {
            p.WwOff.Clear();
            p.WwFall.Clear();
        }

        float WwReach(Player p, float r) => p.WwWide ? r * WwMasteredReach : r;

        /// A drop touched an enemy: hurt and soak everything round it.
        void WwSplash(Player p, V2 at)
        {
            var s = p.Stats;
            float r = WwReach(p, WwSplashR), dmg = WwSplashDamage * s.DmgMult * s.WwDmgMult, wet = WwWet + s.WwWetBonus;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (at.FartherThan(e.Pos, r + e.Radius * 0.5f))
                    continue;
                HurtEnemy(e, dmg, p.Id);
                e.WetT = Math.Max(e.WetT, wet);
            }
            BroadcastFx(FxKind.WwSplash, at, p.Pos, WwColor, r, WwSplashT, Sfx.WwSplash);
        }

        /// Host, from the enemy-bullet loop in Combat.cs: does a landed Water Ward drop eat this shot? It bursts into a soaking mist.
        bool WardStops(Bullet b)
        {
            if (Defs.Unblockable(b.Kind))
                return false;
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    continue;
                for (int i = 0; i < p.WwOff.Count; i++)
                {
                    if (!WwLanded(p, i))
                        continue;
                    V2 d = WwDrop(p, i);
                    if (d.FartherThan(b.Pos, WwDropR + b.Radius))
                        continue;
                    float r = WwReach(p, WwBurstR), wet = WwWet + p.Stats.WwWetBonus;
                    foreach (var e in Enemies.Values)
                        if (Targetable(e) && d.CloserThan(e.Pos, r + e.Radius * 0.5f))
                            e.WetT = Math.Max(e.WetT, wet);
                    BroadcastFx(FxKind.WwBurst, d, b.Pos, WwColor, r, WwBurstT, Sfx.WwBurst);
                    WwRemove(p, i);
                    return true;
                }
            }
            return false;
        }

        // --- Network: the player's Water Ward fields (SyncPlayer) ---

        void SyncWaterWard(NetSync s, Player p)
        {
            int drops = Math.Min(Game.WwMax, p.WwOff.Count);  // Water Ward
            s.Byte(ref drops);
            if (drops > 0)
                s.Float(ref p.WwAng);
            if (s.Reading)
            {
                p.WwOff.Clear();
                p.WwFall.Clear();
            }
            for (int k = 0; k < drops; k++)
            {
                int off = s.Reading ? 0 : (int)Math.Round(MathX.Fmod(p.WwOff[k], MathX.Tau) / MathX.Tau * 256f) & 255;
                int fall = s.Reading ? 0 : Math.Min(255, (int)Math.Ceiling(p.WwFall[k] * 100f));
                s.Byte(ref off);
                s.Byte(ref fall);
                if (s.Reading)
                {
                    p.WwOff.Add(off / 256f * MathX.Tau);
                    p.WwFall.Add(fall / 100f);
                }
            }
        }
    }
}
