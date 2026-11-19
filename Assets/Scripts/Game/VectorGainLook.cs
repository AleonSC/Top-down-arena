using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Vectors coming in, made easy to see (no wire: each screen works it out from what it already gets):
    /// - in the world, a Stash's cache that was just taken bursts (its diamond pops, a ring goes out) and a stream of cyan shards flies
    ///   into whoever took it; a vector pickup that was just collected sends a smaller stream the same way (DrawVectorBursts);
    /// - on the HUD, when your own vectors go up: "+10 VECTORS" pops over you and rises, shards fly from you into the VECTORS chip,
    ///   and the chip glows, counts up and shows the "+10" beside it (DrawVectorGainHud, VectorChipFx).
    /// Preview: `VECGAIN=<s since> [VECGAINN=10]` (mode hud; with BREACHROOMS=1 STASHAT=1 the Stash's burst too).
    public partial class ArenaApp
    {
        class VecBurst { public V2 At; public int To; public float Age; public int N; public bool Stash; }

        const float VecBurstLife = 1.1f, VecShardFly = 0.5f;
        /// How big a show n vectors get: 0 for +1 (a blip), 1 for +12 or more (the whole thing); everything scales with it.
        static float VecWeight(int n) => Mathf.Clamp01((n - 1) / 11f);
        float VecGainSpan => 0.9f + 0.7f * VecWeight(vecGainN);
        static readonly Rgba VecCyan = new Rgba(0.3f, 0.88f, 1f);
        readonly List<VecBurst> vecBursts = new List<VecBurst>();
        readonly List<bool> stashSeen = new List<bool>();
        readonly Dictionary<int, V2> vecCoinsSeen = new Dictionary<int, V2>();
        readonly Dictionary<int, int> vecCoinValue = new Dictionary<int, int>();
        readonly List<int> vecScratch = new List<int>();
        int vecSeen = -1, vecGainN;
        bool stashPrimed;
        float vecGainAge = 99f, vecShown;
        V2 vecGainFrom;          // where you stood (world) when they came in: the shards start there
        Rect vecChipRect;        // the VECTORS chip as last drawn (DrawVectorChip), where the shards land

        /// Update: notices vectors coming in (your stock, a Stash taken, a vector pickup collected) and ages the effects.
        void TickVectorGain(float dt)
        {
            var me = game.Me;
            if (!game.ExpandingOn || me == null || !game.Running)
            {
                vecSeen = -1; stashPrimed = false; vecBursts.Clear(); stashSeen.Clear(); vecCoinsSeen.Clear(); vecCoinValue.Clear(); vecGainAge = 99f;
                return;
            }
            if (vecSeen >= 0 && me.Vectors > vecSeen)
            {
                int n = me.Vectors - vecSeen;
                vecGainN = vecGainAge < 0.7f ? vecGainN + n : n;  // (two close together add up)
                vecGainAge = 0f;
                vecGainFrom = me.Disp;
                vecShown = Mathf.Min(vecShown, vecSeen);
            }
            if (vecSeen < 0 || me.Vectors < vecShown)
                vecShown = me.Vectors;  // (spent, or a new run: no count-up)
            vecSeen = me.Vectors;
            vecGainAge += dt;
            if (vecGainAge > 0.35f)  // (counts up once the shards reach the chip)
                vecShown = Mathf.MoveTowards(vecShown, me.Vectors, Mathf.Max(10f, (me.Vectors - vecShown) * 4f) * dt);

            // A Stash's cache taken: Claimed flipped since the last frame.
            byte stash = (byte)(1 + (int)SurpriseShape.Stash);
            for (int i = 0; i < game.Expanded.Count; i++)
            {
                var r = game.Expanded[i];
                bool taken = r.Surprise == stash && r.Claimed;
                if (i < stashSeen.Count && stashPrimed && taken && !stashSeen[i])
                {
                    var at = new V2((r.X + r.X1) / 2f, (r.Y + r.Y1) / 2f);
                    vecBursts.Add(new VecBurst { At = at, To = NearestPlayer(at), N = Game.StashVectors, Stash = true });
                    sounds.Play(Sfx.PickupBig, 1f, Time.unscaledTime);
                }
                if (i < stashSeen.Count) stashSeen[i] = taken; else stashSeen.Add(taken);
            }
            if (stashSeen.Count > game.Expanded.Count)
                stashSeen.RemoveRange(game.Expanded.Count, stashSeen.Count - game.Expanded.Count);
            stashPrimed = true;  // (a cache already taken when we first look is not news)

            // A vector pickup collected: gone since the last frame (they never despawn, so gone means someone took it).
            vecScratch.Clear();
            foreach (var kv in vecCoinsSeen)
                if (!game.Coins.ContainsKey(kv.Key))
                    vecScratch.Add(kv.Key);
            foreach (int id in vecScratch)
            {
                V2 at = vecCoinsSeen[id];
                int to = NearestPlayer(at);
                if (to >= 0 && game.Players[to].Disp.CloserThan(at, 90f))  // (taken, not cleared away with the map)
                    vecBursts.Add(new VecBurst { At = at, To = to, N = vecCoinValue[id] });
                vecCoinsSeen.Remove(id); vecCoinValue.Remove(id);
            }
            foreach (var c in game.Coins.Values)
                if (c.Vec && game.MinePickup(c.Owner)) { vecCoinsSeen[c.Id] = c.Disp; vecCoinValue[c.Id] = c.Value; }

            for (int i = vecBursts.Count - 1; i >= 0; i--)
                if ((vecBursts[i].Age += dt) > VecBurstLife)
                    vecBursts.RemoveAt(i);
        }

        int NearestPlayer(V2 at)
        {
            int best = -1; float bd = float.MaxValue;
            foreach (var p in game.Players.Values)
            {
                float d = p.Disp.DistanceSq(at);
                if (!p.Downed && d < bd) { bd = d; best = p.Id; }
            }
            return best;
        }


        /// World, over the pickups: each burst's pop, ring and shards streaming into whoever took it.
        void DrawVectorBursts(float t)
        {
            foreach (var b in vecBursts)
            {
                Player to = null;
                if (b.To >= 0) game.Players.TryGetValue(b.To, out to);
                V2 dest = to != null ? to.Disp : b.At;
                float w = VecWeight(b.N), a = b.Age, scale = 0.3f + 0.7f * w;
                var ink = b.Stash ? DoorTeal : VecCyan;
                // The pop: a bright flash and the diamond (a Stash) or square (a pickup) blowing outward as it fades.
                float pk = Mathf.Clamp01(a / 0.45f), fade = 1f - pk;
                if (fade > 0f)
                {
                    Circle(b.At, (18f + 30f * pk) * scale, new Rgba(1f, 1f, 1f, 0.35f * fade * fade), 18);
                    float r = (28f + 46f * EaseOut(pk)) * scale;
                    V2 up = b.At + new V2(0f, -r), rt = b.At + new V2(r, 0f), dn = b.At + new V2(0f, r), lf = b.At + new V2(-r, 0f);
                    var col = ink.WithAlpha(fade);
                    float lw = (1.5f + 2f * w) * fade + 0.5f;
                    Line(up, rt, lw, col); Line(rt, dn, lw, col); Line(dn, lf, lw, col); Line(lf, up, lw, col);
                }
                // The ring going out.
                float rk = Mathf.Clamp01(a / 0.7f);
                if (rk < 1f && w > 0.25f)  // (not for a vector or two)
                {
                    float rr = (20f + 110f * EaseOut(rk)) * scale;
                    RingLine(b.At, rr, 10f * (1f - rk) + 2f, ink.WithAlpha(0.22f * (1f - rk)), 36);  // (a soft halo under it)
                    RingLine(b.At, rr, 3.5f * (1f - rk) + 1f, new Rgba(0.8f, 1f, 1f, 0.95f * (1f - rk)), 36);
                }
                // The shards: out a little, then homing into the player.
                int shards = 2 + Mathf.RoundToInt(12f * w);
                for (int i = 0; i < shards; i++)
                {
                    float start = 0.08f + i * (b.Stash ? 0.025f : 0.03f), k = (a - start) / VecShardFly;
                    if (k <= 0f || k >= 1f) continue;
                    float ang = i * 2.39996f + b.At.X * 0.01f;
                    V2 ctrl = b.At + V2.FromAngle(ang) * (70f + 30f * (i % 3)) * scale;
                    float e = k * k;  // (slow out, fast in)
                    V2 p = Bez(b.At, ctrl, dest, e), q = Bez(b.At, ctrl, dest, Mathf.Max(0f, e - 0.12f));
                    float sz = (2.5f + 2f * w) * (1f - 0.4f * k);
                    Line(q, p, sz * 0.8f, ink.WithAlpha(0.45f));
                    FillRect(p.X - sz, p.Y - sz, sz * 2f, sz * 2f, new Rgba(0.85f, 1f, 1f, 1f));
                }
                // The catch: a tight ring round the player as the shards land.
                float ck = (a - 0.45f) / 0.5f;
                if (to != null && ck > 0f && ck < 1f)
                    RingLine(dest, to.Def.BodyR + 6f + (6f + 16f * w) * EaseOut(ck), (1f + 2f * w) * (1f - ck) + 0.5f, ink.WithAlpha((0.5f + 0.5f * w) * (1f - ck)), 28);
            }
        }


        void RingLine(V2 c, float r, float w, Rgba col, int segs)
        {
            V2 prev = c + new V2(r, 0f);
            for (int i = 1; i <= segs; i++)
            {
                V2 nx = c + V2.FromAngle(i * MathX.Tau / segs) * r;
                Line(prev, nx, w, col);
                prev = nx;
            }
        }

        /// HUD: "+10 VECTORS" over you, and the shards flying from you into the VECTORS chip. Also "+10" over a teammate who took a Stash.
        void DrawVectorGainHud()
        {
            var me = game.Me;
            if (me == null || !game.ExpandingOn)
                return;
            foreach (var b in vecBursts)
            {
                if (!b.Stash || b.To == me.Id || !game.Players.TryGetValue(b.To, out var mate))
                    continue;
                float k = b.Age / VecBurstLife;
                V2 at = ToUi(mate.Disp) + new V2(0f, -54f - 26f * k);
                FloatText(at, "+" + b.N, Mathf.RoundToInt(14f + 4f * VecWeight(b.N)), 1f - k * k);
            }
            DrawVectorsFull(me);
            float a = vecGainAge;
            if (a >= VecGainSpan)
                return;
            bool full = me.Vectors >= me.VectorCap;
            float pop = a < 0.12f ? 1f + 0.5f * (1f - a / 0.12f) : 1f;
            float al = Mathf.Clamp01((VecGainSpan - a) / 0.45f);
            float gw = VecWeight(vecGainN);
            pop = 1f + (pop - 1f) * gw;  // (only a big gain pops)
            V2 at2 = ToUi(me.Disp) + new V2(0f, -70f - (14f + 20f * gw) - (14f + 20f * gw) * EaseOut(Mathf.Clamp01(a / VecGainSpan)));
            FloatText(at2, "+" + vecGainN + (vecGainN >= 3 ? " VECTORS" : vecGainN == 1 ? " VECTOR" : " VECTORS"), Mathf.RoundToInt((15f + 9f * gw) * pop), al);
            if (full)
                FloatText(at2 + new V2(0f, -24f), "STOCK FULL", 12, al * 0.85f);
            // Shards from you to the chip.
            if (vecChipRect.width <= 0f)
                return;
            V2 from = ToUi(vecGainFrom), dest = new V2(vecChipRect.x + 8f, vecChipRect.y + vecChipRect.height / 2f);
            V2 ctrl = new V2(from.X - 40f, Mathf.Min(from.Y, dest.Y) - 160f);
            int toChip = 1 + Mathf.RoundToInt(7f * gw);
            for (int i = 0; i < toChip; i++)
            {
                float k = (a - 0.15f - i * 0.035f) / 0.55f;
                if (k <= 0f || k >= 1f) continue;
                V2 c = ctrl + new V2(i * 14f - 50f, i * 6f);
                for (int j = 3; j >= 0; j--)  // a short fading trail behind each
                {
                    float kj = Mathf.Max(0f, k - j * 0.05f), e = kj * kj;
                    V2 p = Bez(from, c, dest, e);
                    float s = (5f + 5f * gw - (2f + 2f * gw) * k) * (1f - j * 0.2f);
                    var col = j == 0 ? new Color(0.85f, 1f, 1f, 1f) : new Color(0.3f, 0.88f, 1f, 0.55f - j * 0.12f);
                    if (j == 0)
                        Ui.Glow(new Rect(p.X - s / 2f, p.Y - s / 2f, s, s), new Color(0.3f, 0.88f, 1f), 0.8f);
                    Ui.Fill(new Rect(p.X - s / 2f, p.Y - s / 2f, s, s), col);
                }
            }
        }

        void FloatText(V2 at, string text, int size, float alpha)
        {
            if (alpha <= 0f)
                return;
            var r = new Rect(at.X - 150f, at.Y - 16f, 300f, 32f);
            Ui.Label(new Rect(r.x + 1.5f, r.y + 2f, r.width, r.height), text, size, new Color(0f, 0.05f, 0.1f, 0.85f * alpha), TextAnchor.MiddleCenter, true);
            Ui.Label(r, text, size, new Color(0.45f, 0.95f, 1f, alpha), TextAnchor.MiddleCenter, true);
        }

        /// Your stock is full and a vector pickup or an untaken Stash is near you (it waits for you to spend some).
        bool vecFullNear;

        /// DrawVectorGainHud: with a full stock, "FULL" over each vector pickup near you (they wait), and vecFullNear for the chip.
        void DrawVectorsFull(Player me)
        {
            vecFullNear = false;
            if (me.Vectors < me.VectorCap || me.Downed)
                return;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            foreach (var c in game.Coins.Values)
            {
                if (!c.Vec || !game.MinePickup(c.Owner) || !me.Disp.CloserThan(c.Disp, VecFullReach))
                    continue;
                vecFullNear = true;
                V2 at = ToUi(c.Disp) + new V2(0f, -c.Radius * 1.1f * WorldToUi - 16f);
                FullTag(at, pulse);
            }
            byte stash = (byte)(1 + (int)SurpriseShape.Stash);
            foreach (var r in game.Expanded)
                if (r.Surprise == stash && !r.Claimed && me.Pos.X >= r.X - 40f && me.Pos.X <= r.X1 + 40f && me.Pos.Y >= r.Y - 40f && me.Pos.Y <= r.Y1 + 40f)
                    vecFullNear = true;
        }

        const float VecFullReach = 160f;
        static readonly Color VecFullInk = new Color(1f, 0.72f, 0.3f);

        void FullTag(V2 at, float alpha)
        {
            var r = new Rect(at.X - 24f, at.Y - 9f, 48f, 18f);
            Ui.Pill(r, new Color(0f, 0f, 0f, 0.6f * alpha));
            Ui.Label(r, "FULL", 11, new Color(VecFullInk.r, VecFullInk.g, VecFullInk.b, alpha), TextAnchor.MiddleCenter, true);
        }

        /// DrawVectorChip: how lit the chip is (1 as the shards land, easing off), and the count it shows (counting up to your stock).
        float VectorChipFlash => (0.35f + 0.65f * VecWeight(vecGainN)) * (vecGainAge < 0.6f ? Mathf.Clamp01((vecGainAge - 0.15f) / 0.25f) : Mathf.Clamp01(1f - (vecGainAge - 0.6f) / 0.9f));
        int VectorChipCount(Player me) => vecGainAge < VecGainSpan ? Mathf.Min(me.Vectors, Mathf.RoundToInt(vecShown)) : me.Vectors;
    }
}
