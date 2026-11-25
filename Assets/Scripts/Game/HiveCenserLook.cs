using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Hive Censer's look (Core/HiveCenser.cs), ported from the owner's pick, the "Hive Censer" mockup artifact, with its
    /// numbers. Drawing only: every screen flies its own insects from what the host sends (the swarms, each pourer's pour
    /// point), so they match the swarms the host hurts and shells with.
    ///   - the pour: insects puff out of the pour point like powder (pale gold, small), carried on by the flick of the cursor
    ///     (a quarter to 60% of its speed plus a scatter of 10-70 px/s), slowed by drag and fluttering, then after
    ///     0.5-0.75 s each flies to its owner's nearest swarm within 140 px (waiting up to 1.5 s more for the host's new
    ///     swarm to show up, then the nearest);
    ///   - a swarm: each insect circles its middle on its own ring (phase, 2-4.5 turns a second either way, 20-110% of the
    ///     swarm's radius) on a soft spring with jitter, so the cloud seethes; wrapping a body it hugs it (the body's
    ///     edge + 9 px), tighter and quicker; gold with short tails, drawn in the glow pass (additive) with Visual effects on;
    ///   - insects fade in over 1/6 s and out over their last 1.2 s; this screen keeps as many on a swarm as the host
    ///     counts (half with Reduced effects), adding or retiring the difference;
    ///   - a faint gold ring (Game.ChitinR) round each swarm: where chitin builds; a faint dashed ring of your reach;
    ///   - a wrapped enemy darkens under its insects and "SLOWED" shows under it;
    ///   - chitin (Player.ChitinHp): twelve gold plates round the body (lit as it fills) turning slowly, crawling specks and
    ///     a faint gold disc, and a thin gold bar under the player;
    ///   - Hive Call (FxKind.HiveCall): a gold ring easing out at the spot.
    public partial class ArenaApp
    {
        static readonly Rgba HvPowder = new Rgba(1f, 0.91f, 0.667f), HvSwarm = new Rgba(1f, 0.8f, 0.35f), HvShell = new Rgba(1f, 0.863f, 0.51f);
        static readonly Rgba HvDark = new Rgba(0.157f, 0.118f, 0.04f), HvSlowInk = new Rgba(0.4f, 0.64f, 1f);
        const int HvMaxInsects = 2400;
        const float HvJoinR = 140f, HvStray = 1.5f, HvFadeIn = 6f, HvFadeOut = 1.2f;

        class HvInsect
        {
            public int Owner, Swarm;  // Swarm: the swarm's id, 0 while still powder
            public V2 Pos, Prev, Vel;
            public float Age, Settle, Ph, W, Rf, Jit, Bright, Dying = -1f;
        }

        readonly List<HvInsect> hvBugs = new List<HvInsect>();
        readonly Dictionary<int, HiveSwarm> hvSwarmById = new Dictionary<int, HiveSwarm>();
        readonly Dictionary<int, int> hvOn = new Dictionary<int, int>();  // swarm id -> insects on it this frame
        readonly Dictionary<int, float> hvAcc = new Dictionary<int, float>();  // player id -> insects owed to the pour
        readonly Dictionary<int, V2> hvLastPour = new Dictionary<int, V2>(), hvFlick = new Dictionary<int, V2>();
        readonly System.Random hvRng = new System.Random(4242);
        Game hvGame;
        float hvLastT = -1f, hvClock;

        float HvRnd(float a, float b) => a + (float)hvRng.NextDouble() * (b - a);
        float HvDensity => fxReduced ? 0.5f : 1f;

        /// Where a swarm's insects circle: the body it wraps, or its own middle.
        V2 HvCentre(HiveSwarm s)
        {
            if (s.Wrapping)
            {
                Enemy e;
                Player q;
                if (s.Kind == Game.HiveFoe && game.Enemies.TryGetValue(s.TargetId, out e))
                    return e.Disp;
                if (s.Kind == Game.HiveAlly && game.Players.TryGetValue(s.TargetId, out q))
                    return q.Disp;
            }
            return s.Disp;
        }

        HvInsect HvNew(int owner, V2 at)
        {
            return new HvInsect
            {
                Owner = owner, Pos = at, Prev = at, Settle = Game.HiveSettle + HvRnd(0f, Game.HiveSettleJitter),
                Ph = HvRnd(0f, MathX.Tau), W = HvRnd(2f, 4.5f) * (hvRng.NextDouble() < 0.5 ? -1f : 1f),
                Rf = Mathf.Sqrt((float)hvRng.NextDouble()) * 0.9f + 0.2f, Jit = HvRnd(0f, 100f), Bright = HvRnd(0.55f, 1f),
            };
        }

        /// Once a frame (DrawHiveFloor): pour, settle, fly and age this screen's insects.
        void UpdateHiveLook()
        {
            if (hvGame != game)
            {
                hvBugs.Clear(); hvAcc.Clear(); hvLastPour.Clear(); hvFlick.Clear();
                hvGame = game;
                hvLastT = game.AnimT;
            }
            float dt = Mathf.Clamp(game.AnimT - hvLastT, 0f, 0.05f);
            hvLastT = game.AnimT;
            if (dt <= 0f)
                return;
            hvClock += dt;
            float t = hvClock;
            hvSwarmById.Clear();
            foreach (var s in game.Swarms)
                hvSwarmById[s.Id] = s;
            // The pour: powder out of each pourer's point, carried on by the flick of the cursor.
            foreach (var p in game.Players.Values)
            {
                V2 at = p.HivePourDisp, last;
                if (!p.HiveOn || p.Downed || p.Weapon.Id != WeaponId.HiveCenser)
                {
                    hvLastPour.Remove(p.Id);
                    hvFlick.Remove(p.Id);
                    continue;
                }
                if (!hvLastPour.TryGetValue(p.Id, out last))
                    last = at;
                V2 flick, now = (at - last) / dt;
                hvFlick.TryGetValue(p.Id, out flick);
                flick = flick * 0.7f + now * 0.3f;
                hvFlick[p.Id] = flick;
                hvLastPour[p.Id] = at;
                float acc;
                hvAcc.TryGetValue(p.Id, out acc);
                acc += game.HivePourOf(p) * dt * HvDensity;
                while (acc >= 1f && hvBugs.Count < HvMaxInsects)
                {
                    acc -= 1f;
                    var b = HvNew(p.Id, at + new V2(HvRnd(-4f, 4f), HvRnd(-4f, 4f)));
                    float a = HvRnd(0f, MathX.Tau), sp = HvRnd(10f, 70f);
                    b.Vel = flick * HvRnd(0.25f, 0.6f) + V2.FromAngle(a) * sp;
                    hvBugs.Add(b);
                }
                hvAcc[p.Id] = Mathf.Min(acc, 4f);
            }
            // Fly and age.
            hvOn.Clear();
            float drag = Mathf.Exp(-dt * Game.HiveDrag);
            for (int i = 0; i < hvBugs.Count; i++)
            {
                var b = hvBugs[i];
                b.Age += dt;
                b.Prev = b.Pos;
                if (b.Dying >= 0f)
                {
                    b.Dying += dt;
                    if (b.Dying > 0.4f) { hvBugs.RemoveAt(i--); continue; }
                }
                if (b.Age >= Game.HiveLife) { hvBugs.RemoveAt(i--); continue; }
                HiveSwarm s = null;
                if (b.Swarm != 0 && !hvSwarmById.TryGetValue(b.Swarm, out s))
                {
                    b.Swarm = 0;  // its swarm is gone: it fades as it drifts
                    if (b.Dying < 0f)
                        b.Dying = 0f;
                }
                if (s == null)
                {
                    b.Vel = b.Vel * drag + new V2(Mathf.Sin(b.Jit + t * 9f), Mathf.Cos(b.Jit * 1.7f + t * 8f)) * (30f * dt);
                    if (b.Age > b.Settle && b.Dying < 0f)
                    {
                        HiveSwarm best = null;
                        float bd = HvJoinR * HvJoinR, any = float.MaxValue;
                        HiveSwarm nearest = null;
                        foreach (var x in game.Swarms)
                        {
                            if (x.Owner != b.Owner)
                                continue;
                            float d = x.Disp.DistanceSq(b.Pos);
                            if (d < bd) { bd = d; best = x; }
                            if (d < any) { any = d; nearest = x; }
                        }
                        if (best == null && b.Age > b.Settle + HvStray)
                            best = nearest;  // (none came of it near here: the nearest, as the host does when it can't start one)
                        if (best != null)
                            b.Swarm = best.Id;
                        else if (b.Age > b.Settle + HvStray)
                            b.Dying = 0f;  // no swarm came of it on this screen
                    }
                }
                else
                {
                    V2 c = HvCentre(s);
                    float ph = b.Ph + t * b.W;
                    float r = s.WrapR * b.Rf * (s.Wrapping ? 0.85f + 0.3f * Mathf.Abs(Mathf.Sin(ph * 0.5f + b.Jit)) : 1f);
                    V2 goal = c + new V2(Mathf.Cos(ph) * r, Mathf.Sin(ph) * r * (s.Wrapping ? 1f : 0.8f));
                    float k = s.Wrapping ? 34f : 14f;
                    b.Vel += ((goal - b.Pos) * k - b.Vel * 4.5f) * dt + new V2(Mathf.Sin(b.Jit + t * 13f), Mathf.Cos(b.Jit + t * 11f)) * (120f * dt);
                    if (b.Dying < 0f)
                    {
                        int n;
                        hvOn.TryGetValue(s.Id, out n);
                        hvOn[s.Id] = n + 1;
                    }
                }
                b.Pos += b.Vel * dt;
            }
            // Keep as many on each swarm as the host counts: add the missing (a screen that joined late, or missed the
            // pour), retire the oldest extra.
            foreach (var s in game.Swarms)
            {
                int on;
                hvOn.TryGetValue(s.Id, out on);
                int want = Mathf.RoundToInt(s.N * HvDensity);
                if (on < want - 8)
                    for (int k = on; k < want && hvBugs.Count < HvMaxInsects; k++)
                    {
                        var b = HvNew(s.Owner, s.Disp + V2.FromAngle(HvRnd(0f, MathX.Tau)) * HvRnd(0f, s.WrapR));
                        b.Swarm = s.Id;
                        b.Age = HvRnd(0.2f, 3f);
                        hvBugs.Add(b);
                    }
                else if (on > want + 8)
                {
                    int drop = on - want;
                    for (int i = 0; i < hvBugs.Count && drop > 0; i++)
                        if (hvBugs[i].Swarm == s.Id && hvBugs[i].Dying < 0f)
                        {
                            hvBugs[i].Dying = 0f;
                            drop--;
                        }
                }
            }
        }

        float HvAlpha(HvInsect b) =>
            Mathf.Min(1f, b.Age * HvFadeIn) * Mathf.Clamp01((Game.HiveLife - b.Age) / HvFadeOut) * b.Bright * (b.Dying >= 0f ? 1f - b.Dying / 0.4f : 1f);

        Rgba HvColour(HvInsect b, out bool powder)
        {
            HiveSwarm s = null;
            powder = b.Swarm == 0 || !hvSwarmById.TryGetValue(b.Swarm, out s);
            if (powder)
                return HvPowder;
            return s.Kind == Game.HiveAlly && s.Wrapping ? HvShell : HvSwarm;
        }

        // ---- the floor: the refill rings, your reach (and the insects' update)

        void DrawHiveFloor(float t)
        {
            UpdateHiveLook();
            foreach (var s in game.Swarms)
                Arc(s.Disp, Game.ChitinR, 1.2f, HvSwarm.WithAlpha(0.05f + 0.05f * Mathf.Min(1f, s.N / Game.HiveFull)), 40);
            var me = game.Me;
            if (me != null && !me.Downed && me.Weapon.Id == WeaponId.HiveCenser)
            {
                const int dashes = 64;
                for (int i = 0; i < dashes; i++)  // the mockup's 4-on, 8-off dashes
                    Arc(me.Disp, Game.HiveReach, 1f, Game.HiveGold.WithAlpha(0.08f), 2, MathX.Tau * i / dashes, MathX.Tau / dashes / 3f);
            }
        }

        // ---- over the enemies: wrapped ones darken; the plain insects with Visual effects off

        void DrawHiveOver(float t)
        {
            foreach (var s in game.Swarms)
            {
                Enemy e;
                if (!s.Wrapping || s.Kind != Game.HiveFoe || !game.Enemies.TryGetValue(s.TargetId, out e))
                    continue;
                Circle(e.Disp, e.Radius, HvDark.WithAlpha(0.45f * Mathf.Min(1f, s.N / 40f)), 24);
            }
            if (fxOn)
                return;
            foreach (var b in hvBugs)
            {
                bool powder;
                Rgba col = HvColour(b, out powder);
                float a = HvAlpha(b), sz = powder ? 1.3f : 1.8f;
                FillRect(b.Pos.X - sz / 2f, b.Pos.Y - sz / 2f, sz, sz, col.WithAlpha(a * (powder ? 0.6f : 0.95f)));
            }
        }

        // ---- the glow pass: insects with short tails, adding light

        void DrawHiveGlow()
        {
            foreach (var b in hvBugs)
            {
                bool powder;
                Rgba col = HvColour(b, out powder);
                float a = HvAlpha(b);
                if (a <= 0.01f)
                    continue;
                V2 step = b.Pos - b.Prev;
                if (step.Length > 4f)
                    step = step * (4f / step.Length);  // (a short tail even on a fast insect)
                V2 tail = b.Pos - step * 2.5f;
                if ((b.Pos - tail).Length > 0.3f)
                    Line(tail, b.Pos, 1.2f, col.WithAlpha(a * (powder ? 0.35f : 0.55f)));
                float sz = powder ? 1.3f : 1.8f;
                FillRect(b.Pos.X - sz / 2f, b.Pos.Y - sz / 2f, sz, sz, col.WithAlpha(a * (powder ? 0.6f : 0.95f)));
            }
        }

        // ---- chitin, under each player

        void DrawChitin(Player p, float t)
        {
            float f = p.ChitinHp / Game.ChitinMax;
            if (f <= 0.01f || p.Downed || LaunchHides(p))
                return;
            float R = p.Def.BodyR + 7f;
            Circle(p.Disp, R + 2f, Game.ChitinGold.WithAlpha(0.08f + 0.1f * f), 32);
            int plates = Mathf.CeilToInt(f * 12f);
            for (int k = 0; k < plates; k++)
            {
                float a0 = k / 12f * MathX.Tau + t * 0.4f;
                Arc(p.Disp, R, 3.2f, Game.ChitinGold.WithAlpha(0.35f + 0.55f * f), 6, a0, MathX.Tau / 12f * 0.78f);
            }
            int specks = Mathf.FloorToInt(f * 18f);
            for (int k = 0; k < specks; k++)  // crawling on the shell
            {
                float ang = k * 2.39f + t * (k % 2 == 1 ? 1.6f : -1.2f), rr = R + Mathf.Sin(t * 5f + k) * 2.5f;
                V2 at = p.Disp + V2.FromAngle(ang) * rr;
                FillRect(at.X - 1f, at.Y - 1f, 2f, 2f, Game.HiveGold.WithAlpha(0.9f));
            }
            const float bw = 34f;
            V2 bar = p.Disp + new V2(-bw / 2f, p.Def.BodyR + 12f);
            FillRect(bar.X, bar.Y, bw, 3f, new Rgba(0f, 0f, 0f, 0.6f));
            FillRect(bar.X, bar.Y, bw * f, 3f, Game.HiveGold);
        }

        // ---- Hive Call (WorldEffects)

        void DrawHiveCallFx(Effect fx)
        {
            float k = Mathf.Clamp01(fx.T / Mathf.Max(0.05f, fx.Dur));
            float left = 1f - k;
            if (left <= 0f)
                return;
            Arc(fx.Pos, fx.Radius * (0.2f + k), 1.5f, Game.HiveGold.WithAlpha(left), 40);
        }

        // ---- HUD: "SLOWED" under each wrapped enemy

        void DrawHiveHud()
        {
            foreach (var s in game.Swarms)
            {
                Enemy e;
                if (!s.Wrapping || s.Kind != Game.HiveFoe || !game.Enemies.TryGetValue(s.TargetId, out e))
                    continue;
                V2 at = ToUi(e.Disp + new V2(0f, e.Radius + 10f));
                var r = new Rect(at.X - 60f, at.Y - 8f, 120f, 16f);
                Ui.Label(r, "SLOWED", 11, HvSlowInk.ToColor().WithAlpha(0.9f), TextAnchor.MiddleCenter, true);
            }
        }
    }
}
