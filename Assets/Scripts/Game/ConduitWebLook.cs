using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Conduit Web's look (Core/ConduitWeb.cs; from its mockup, claude.ai artifact "Conduit Web"), drawn with Static Shock's
    /// lightning (StaticShockLook.cs: ShkDrawBolt, its colours and its 18-a-second re-strike). Drawing only, from the replicated
    /// CwT, CwLen and CwR; the circuit is worked out on this screen from where the enemies are drawn (Game.CwCircuit, the
    /// host's own rule).
    ///   - the field: pale static round you, bright at your hand and gone at its reach, a faint rim that flashes as it bursts
    ///     out, and nine crackling filaments from your hand across it;
    ///   - the circuit: a bolt along each link (soft halo, blue glow, white core), the three from your hand thicker; each
    ///     thickens and runs whiter the longer its enemy is wired; wired enemies glow, flicker and throw sparks;
    ///   - gold bolts from your hand to the teammates in the field, a flare on your hand and a time ring round you.
    /// With Visual effects off: the rim, straight links and gold lines.
    public partial class ArenaApp
    {
        readonly List<Enemy> cwShown = new List<Enemy>();
        readonly List<int> cwShownFrom = new List<int>();
        readonly Dictionary<int, float> cwHeat = new Dictionary<int, float>(), cwHeatSeen = new Dictionary<int, float>();
        readonly List<int> cwHeatGone = new List<int>();
        float cwLookClock;

        static readonly Rgba CwTimeRing = new Rgba(0.63f, 0.86f, 1f);

        /// The web of player p on this screen (into cwShown / cwShownFrom).
        void CwShownCircuit(Player p) =>
            Game.CwCircuit(Game.StaticHand(p, p.Disp), p.Disp, Game.CwReach(p), game.Enemies.Values, e => e.Disp, ShkShowable, Game.CwArms, cwShown, cwShownFrom);

        static bool CwShows(Player p) => Game.CwOn(p) && p.CwR > 0f;

        /// 0 to 1: how far along its ramp the shock on this enemy is (as this screen has seen it wired).
        float CwHeatOf(int id)
        {
            float h;
            return cwHeat.TryGetValue(id, out h) ? Mathf.Clamp01(h / Game.CwRamp) : 0f;
        }

        /// It fades out over its last 0.3 s.
        static float CwFade(Player p) => Mathf.Clamp01(p.CwT / 0.3f);

        /// Juice.UpdateJuice (Visual effects on): each wired enemy's heat, and sparks off them.
        void UpdateConduitWebLook(float dt)
        {
            if (dt <= 0f)
                return;
            cwLookClock += dt;
            foreach (var p in game.Players.Values)
            {
                if (!CwShows(p))
                    continue;
                CwShownCircuit(p);
                foreach (var e in cwShown)
                {
                    float h;
                    cwHeat.TryGetValue(e.Id, out h);
                    cwHeat[e.Id] = h + dt;
                    cwHeatSeen[e.Id] = cwLookClock;
                    if (Rnd(0f, 1f) < dt * (14f + 20f * CwHeatOf(e.Id)))
                        ShkSpark(e.Disp + new V2(Rnd(-e.Radius, e.Radius), Rnd(-e.Radius, e.Radius)), Rnd(0f, MathX.Tau), Rnd(40f, 140f), ShkSparkCol);
                }
            }
            cwHeatGone.Clear();
            foreach (var kv in cwHeatSeen)
                if (cwLookClock - kv.Value > Game.CwForget)
                    cwHeatGone.Add(kv.Key);
            foreach (int k in cwHeatGone)
            {
                cwHeat.Remove(k);
                cwHeatSeen.Remove(k);
            }
        }

        /// WorldRenderer.DrawPlayerAuras: the time ring round the caster; with Visual effects off, the whole web plainly.
        void DrawConduitWeb(Player p, float t)
        {
            if (!CwShows(p) || LaunchHides(p))
                return;
            float fade = CwFade(p), left = Mathf.Clamp01(p.CwT / Mathf.Max(0.1f, p.CwLen)), R = p.Def.BodyR + 9f;
            Arc(p.Disp, R, 2.5f, CwTimeRing.WithAlpha(0.18f * fade), 32);
            Arc(p.Disp, R, 2.5f, CwTimeRing.WithAlpha(0.9f * fade), 32, -MathX.Pi / 2f, MathX.Tau * left);
            if (fxOn)
                return;
            V2 h = Game.StaticHand(p, p.Disp);
            Arc(p.Disp, Game.CwReach(p), 1.5f, Game.StaticBlue.WithAlpha(0.5f * fade), 64);
            CwShownCircuit(p);
            for (int i = 0; i < cwShown.Count; i++)
                Line(cwShownFrom[i] < 0 ? h : cwShown[cwShownFrom[i]].Disp, cwShown[i].Disp, cwShownFrom[i] < 0 ? 2.5f : 2f, Game.StaticBlue.WithAlpha(fade));
            float reach = Game.CwReach(p);
            foreach (var q in game.Players.Values)
                if (q != p && !q.Downed && q.Disp.CloserThan(p.Disp, reach + q.Def.BodyR))
                    Line(h, q.Disp, 2f, Game.OverchargeGold.WithAlpha(fade));
        }

        /// Juice.DrawGlowPass: the field, its filaments, the circuit, the gold bolts and the hand's flare.
        void DrawConduitWebGlow()
        {
            float t = game.AnimT;
            int fr = ShkFrame(t);
            foreach (var p in game.Players.Values)
            {
                if (!CwShows(p) || LaunchHides(p))
                    continue;
                V2 c = p.Disp, h = Game.StaticHand(p, c);
                float R = Game.CwReach(p), fade = CwFade(p), grow = Mathf.Clamp01((p.CwLen - p.CwT) / Game.CwGrow);
                uint fs = ShkSeed(fr, p.Id, 7);
                float fl = 0.75f + 0.25f * Rand01(ref fs);
                // the field: pale static round you, bright at your hand, gone at its reach; a rim that flashes as it bursts out
                GlowDisc(c, R, ShkFanMid, 0.16f * fl * fade);
                GlowDisc(c, R * 0.5f, ShkFanIn, 0.22f * fl * fade);
                Arc(c, R, 1f + 3f * (1f - grow), ShkRim.WithAlpha((0.22f + 0.55f * (1f - grow)) * fl * fade), 72);
                // nine crackling filaments from your hand across it
                for (int i = 0; i < 9; i++)
                {
                    uint s = ShkSeed(fr, p.Id * 16 + i, 11);
                    float a = Rand01(ref s) * MathX.Tau, L = (0.25f + 0.55f * Rand01(ref s)) * R;
                    ShkDrawBolt(h, c + V2.FromAngle(a) * L, L * 0.12f, s, ShkCrackGlow, ShkCrackCore, 1f, 0.5f * fade);
                }
                // the circuit: three conduits from your hand, branching enemy to enemy
                CwShownCircuit(p);
                for (int i = 0; i < cwShown.Count; i++)
                {
                    var e = cwShown[i];
                    bool root = cwShownFrom[i] < 0;
                    V2 from = root ? h : cwShown[cwShownFrom[i]].Disp, to = e.Disp;
                    float heat = CwHeatOf(e.Id);
                    ShkDrawBolt(from, to, Mathf.Min(28f, (to - from).Length * 0.18f), ShkSeed(fr, e.Id, 3), ShkHotGlow(heat), Rgba.White, (root ? 2.4f : 1.8f) + heat, fade);
                    uint es = ShkSeed(fr, e.Id, 5);
                    GlowDisc(to, e.Radius * (2.4f + 1.2f * heat), ShkEnemyGlow, (0.4f + 0.3f * heat) * (0.6f + 0.4f * Rand01(ref es)) * fade);
                    if (Rand01(ref es) < 0.5f)
                        Arc(to, e.Radius, 2f, new Rgba(0.74f, 0.94f, 1f, 0.8f * fade), 20);
                }
                // gold bolts to the teammates in it
                foreach (var q in game.Players.Values)
                    if (q != p && !q.Downed && q.Disp.CloserThan(c, R + q.Def.BodyR))
                    {
                        ShkDrawBolt(h, q.Disp, 14f, ShkSeed(fr, q.Id, 9), OcLinkGlow, OcCore, 1.8f, fade);
                        GlowDisc(q.Disp, q.Def.BodyR * 3f, OcGlow, 0.35f * fade);
                    }
                GlowDisc(h, 30f, ShkHand, 0.95f * fl * fade);
            }
        }
    }
}
