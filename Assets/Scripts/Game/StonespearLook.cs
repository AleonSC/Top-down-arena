using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Elementalist DPS's Stonespear, Basalt look (the Stonespear mockup). Drawing only: the slam, throws and volley are
    /// Core/Stonespear.cs.
    ///   Hovering spears (Player.SsMask): each eases toward its slot (Game.SlotOffset) at SsFollow a second, so they trail behind you
    ///     when you move, and turns to point at the cursor; a shadow under each sells the float. Rising ones burst out of the ground
    ///     at the slam (FxKind.StoneSlam's impact, by its age) and lift to their slots.
    ///   Thrown spears (Defs.StoneSpear): the spear, its shadow and an amber trail of where it has been; on a hit it crumbles in dust,
    ///     run out it sticks in the ground and fades. A volley's target wears a lock ring (Player.SsLock).
    ///   The slam: a dashed ring closing in over the wind-up, then a ring rolling out, nine cracks glowing amber and dust.
    public partial class ArenaApp
    {
        static readonly Rgba SsHi = Hx(0x7d746a), SsLo = Hx(0x3b3632), SsEdge = Hx(0xb3a796), SsGlow = Hx(0xffb04a), SsDust = Hx(0x8c7f6e), SsCrack = Hx(0xffa23a);
        const float SsLen = 46f, SsWidth = 11f, SsFollow = 14f, SsTurn = 12f, SsStuckTime = 0.8f, SsCrumbleTime = 0.35f;

        class SsSpear { public V2 Pos; public float Ang; public bool Hovering; }

        class SsState
        {
            public readonly Dictionary<int, SsSpear> Slots = new Dictionary<int, SsSpear>();
            public float Last = -1f;
        }

        class SsShot { public V2 Pos; public float Ang, Life; public int Owner; public readonly List<V2> Trail = new List<V2>(); public bool Seen; }
        class SsGhost { public V2 Pos; public float Ang, Age; public bool Stuck; }
        class SsMote { public V2 Pos, Vel; public float Age, Life, Size; }

        readonly Dictionary<int, SsState> ssPlayers = new Dictionary<int, SsState>();
        readonly Dictionary<int, SsShot> ssShots = new Dictionary<int, SsShot>();
        readonly List<SsGhost> ssGhosts = new List<SsGhost>();
        readonly List<SsMote> ssMotes = new List<SsMote>();
        readonly HashSet<Effect> ssSlamsSeen = new HashSet<Effect>();
        readonly List<int> ssGone = new List<int>();
        Game ssGame;
        float ssClock = -1f;
        int ssFrame = -1;

        /// The latest slam impact of player `id` (FxKind.StoneSlam, Pos2.X >= 1), or null.
        Effect SsImpactOf(int id)
        {
            Effect best = null;
            foreach (var fx in FxOf(FxKind.StoneSlam))
                if (fx.Kind == FxKind.StoneSlam && fx.Pos2.X >= 1f && (int)fx.Pos2.Y == id && (best == null || fx.T < best.T))
                    best = fx;
            return best;
        }

        /// Where a player's spears point: your own cursor, or a point along another player's aim.
        V2 SsAimAt(Player p) => p.Id == game.MyId && !p.AimAt.IsZero ? p.AimAt : p.Disp + V2.FromAngle(p.Aim) * 220f;

        /// Once a frame (from the first player drawn under): the clock, the spears easing to their slots, the thrown spears' trails,
        /// the ghosts and the dust.
        void SsUpdate()
        {
            if (ssFrame == Time.frameCount && ssGame == game && ssClock == game.AnimT)
                return;
            ssFrame = Time.frameCount;
            if (ssGame != game)
            {
                ssGame = game;
                ssPlayers.Clear(); ssShots.Clear(); ssGhosts.Clear(); ssMotes.Clear(); ssSlamsSeen.Clear();
                ssClock = -1f;
            }
            float now = game.AnimT, dt = ssClock < 0f ? 0f : Mathf.Clamp(now - ssClock, 0f, 0.1f);
            ssClock = now;
            // the slams' dust, once each
            foreach (var fx in FxOf(FxKind.StoneSlam))
                if (fx.Kind == FxKind.StoneSlam && ssSlamsSeen.Add(fx))
                {
                    bool impact = fx.Pos2.X >= 1f;
                    if (impact)
                        SsBurst(fx.Pos, 26, 260f);
                    SsSound(impact ? Sfx.StoneSlam : Sfx.StoneWind, (int)fx.Pos2.Y, fx.Pos);
                }
            ssSlamsSeen.RemoveWhere(fx => !game.Effects.Contains(fx));
            // the thrown spears: trails, and what happened to the ones gone
            foreach (var b in game.Bullets.Values)
            {
                if (b.Kind != Defs.StoneSpear)
                    continue;
                SsShot s;
                if (!ssShots.TryGetValue(b.Id, out s))
                {
                    ssShots[b.Id] = s = new SsShot();
                    SsBurst(b.Pos, 6, 140f);
                    SsSound(Sfx.StoneThrow, b.Owner, b.Pos);
                }
                s.Pos = b.Pos;
                s.Ang = b.Vel.IsZero ? s.Ang : b.Vel.Angle;
                s.Life = b.Life;
                s.Owner = b.Owner;
                s.Seen = true;
                if (dt > 0f || s.Trail.Count == 0)
                {
                    s.Trail.Add(b.Pos);
                    if (s.Trail.Count > 9)
                        s.Trail.RemoveAt(0);
                }
            }
            ssGone.Clear();
            foreach (var kv in ssShots)
            {
                if (kv.Value.Seen)
                {
                    kv.Value.Seen = false;
                    continue;
                }
                ssGone.Add(kv.Key);
                var s = kv.Value;
                bool stuck = s.Life <= 0.05f;
                ssGhosts.Add(new SsGhost { Pos = s.Pos, Ang = s.Ang, Stuck = stuck });
                SsBurst(s.Pos, stuck ? 6 : 10, stuck ? 90f : 200f);
                SsSound(stuck ? Sfx.StoneStick : Sfx.StoneHit, s.Owner, s.Pos);
            }
            foreach (int id in ssGone)
                ssShots.Remove(id);
            for (int i = ssGhosts.Count - 1; i >= 0; i--)
                if ((ssGhosts[i].Age += dt) > (ssGhosts[i].Stuck ? SsStuckTime : SsCrumbleTime))
                    ssGhosts.RemoveAt(i);
            float drag = Mathf.Pow(0.05f, dt);
            for (int i = ssMotes.Count - 1; i >= 0; i--)
            {
                var m = ssMotes[i];
                m.Pos += m.Vel * dt;
                m.Vel *= drag;
                if ((m.Age += dt) > m.Life)
                    ssMotes.RemoveAt(i);
            }
            // the hovering spears
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon.Id != WeaponId.Stonespear || p.SsMask == 0)
                {
                    ssPlayers.Remove(p.Id);
                    continue;
                }
                SsState st;
                if (!ssPlayers.TryGetValue(p.Id, out st))
                    ssPlayers[p.Id] = st = new SsState();
                var impact = SsImpactOf(p.Id);
                V2 aimAt = SsAimAt(p);
                for (int k = 0; k < 2 * p.SsSide; k++)
                {
                    if ((p.SsMask & (1 << k)) == 0)
                    {
                        st.Slots.Remove(k);
                        continue;
                    }
                    V2 slot = p.Disp + Game.SlotOffset(p.Aim, k, p.SsSide, now);
                    SsSpear sp;
                    bool rising = impact != null && impact.T < Game.SsRisen(k);
                    if (!st.Slots.TryGetValue(k, out sp))
                    {
                        st.Slots[k] = sp = new SsSpear { Pos = slot, Ang = (aimAt - slot).Angle };
                        if (rising)
                            SsBurst(SsGround(impact, p, k), 7, 120f);  // it breaks out of the ground
                    }
                    if (rising)
                    {
                        sp.Hovering = false;
                        sp.Pos = slot;
                        sp.Ang = (aimAt - slot).Angle;
                        continue;
                    }
                    if (!sp.Hovering)
                    {
                        sp.Hovering = true;
                        sp.Pos = slot;
                    }
                    sp.Pos += (slot - sp.Pos) * Mathf.Min(1f, dt * SsFollow);  // eased: they trail behind you when you move
                    float want = (aimAt - sp.Pos).Angle;
                    sp.Ang += SsAngDiff(sp.Ang, want) * Mathf.Min(1f, dt * SsTurn);
                }
            }
        }

        /// Where slot k's spear breaks out of the ground: out past its slot, as the slam's cracks run.
        V2 SsGround(Effect impact, Player p, int k)
        {
            int n = Mathf.Max(1, p.SsSide), i = k % n;
            V2 off = Game.SlotOffset(p.Aim, k, n, 0f);
            return impact.Pos + off.Normalized() * (70f + i * 14f * (n > 1 ? 2f / (n - 1) : 0f));
        }

        readonly System.Random ssRng = new System.Random(7);
        float SsRnd(float a, float b) => a + (b - a) * (float)ssRng.NextDouble();
        static float SsAngDiff(float from, float to) => MathX.WrapPi(to - from);

        /// A Stonespear sound (StonespearSoundBank.cs): quieter for someone else's spear and with distance.
        void SsSound(Sfx cue, int owner, V2 at)
        {
            bool mine = owner == game.MyId;
            sounds?.Play(new SoundCue { Sfx = cue, Ally = !mine, Quiet = mine ? 0f : game.QuietAt(at) }, Time.unscaledTime);
        }

        void SsBurst(V2 at, int n, float speed)
        {
            if (fxReduced)
                n = (n + 2) / 3;
            for (int i = 0; i < n; i++)
            {
                float a = SsRnd(0f, MathX.Tau), v = speed * SsRnd(0.3f, 1f);
                ssMotes.Add(new SsMote { Pos = at, Vel = V2.FromAngle(a) * v, Life = SsRnd(0.35f, 0.75f), Size = SsRnd(1.5f, 4f) });
            }
        }

        /// Under a player (DrawPlayerAuras): the hovering and rising spears' shadows and the dust where they break out.
        void DrawStonespearUnder(Player p, float t)
        {
            SsUpdate();
            SsState st;
            if (p.Weapon.Id != WeaponId.Stonespear || !ssPlayers.TryGetValue(p.Id, out st))
                return;
            var impact = SsImpactOf(p.Id);
            foreach (var kv in st.Slots)
            {
                float g, lift;
                V2 at;
                float ang;
                SsPose(p, kv.Key, kv.Value, impact, out at, out ang, out lift, out g);
                if (g <= 0f)
                    continue;
                SsShadow(at, ang, lift, g, 0.35f);
                if (g < 1f && impact != null)
                {
                    V2 ground = SsGround(impact, p, kv.Key);
                    EllipseLine(ground, 14f * g + 4f, 6f * g + 2f, 2f, SsDust.WithAlpha(0.6f * (1f - g)), 16);
                }
            }
        }

        /// Over a player (DrawPlayerWrapped): the hovering and rising spears, and a volley's lock ring.
        void DrawStonespears(Player p, float t)
        {
            SsState st;
            if (p.Weapon.Id != WeaponId.Stonespear)
                return;
            Enemy lockOn;
            if (p.SsLock != 0 && game.Enemies.TryGetValue(p.SsLock, out lockOn) && lockOn.Hp > 0f)
            {
                float r = 26f + 6f * Mathf.Sin(t * 10f);
                for (int i = 0; i < 4; i++)
                    Arc(lockOn.Disp, r, 2f, SsGlow.WithAlpha(0.9f), 8, i * MathX.Pi / 2f + t * 2f, 0.9f);
            }
            if (!ssPlayers.TryGetValue(p.Id, out st))
                return;
            var impact = SsImpactOf(p.Id);
            foreach (var kv in st.Slots)
            {
                float g, lift, ang;
                V2 at;
                SsPose(p, kv.Key, kv.Value, impact, out at, out ang, out lift, out g);
                if (g > 0f)
                    SsSpearShape(at, ang, SsLen, g, 1f, kv.Key, t, false);
            }
        }

        /// A spear's pose: where (at), which way, how high (lift) and how far it has grown out of the ground (g, 0..1).
        void SsPose(Player p, int k, SsSpear sp, Effect impact, out V2 at, out float ang, out float lift, out float g)
        {
            Game.SlotOffset(p.Aim, k, p.SsSide, game.AnimT, out lift);
            at = sp.Pos;
            ang = sp.Ang;
            g = 1f;
            if (sp.Hovering || impact == null)
                return;
            float age = impact.T - Game.SsRiseGap * k;
            if (age < 0f)
            {
                g = 0f;
                return;
            }
            float u = Mathf.Min(1f, age / Game.SsRise);
            V2 ground = SsGround(impact, p, k);
            float radial = (ground - impact.Pos).Angle;
            if (u < 0.45f)
            {
                g = u / 0.45f;  // bursting out of the ground
                at = ground;
                ang = radial;
                lift = 0f;
                return;
            }
            float q = (u - 0.45f) / 0.55f, e = 1f - (1f - q) * (1f - q) * (1f - q);  // then lifting to its slot
            at = V2.Lerp(ground, sp.Pos, e);
            lift *= e;
            ang = radial + SsAngDiff(radial, sp.Ang) * e;
        }

        void SsShadow(V2 at, float ang, float lift, float scale, float alpha)
        {
            Ellipse(at + new V2(lift * 0.55f, lift * 0.9f), 22f * scale, 4.5f * scale, ang, new Rgba(0f, 0f, 0f, alpha), 14);
        }

        /// One earth spear, point along `ang`: a light and a dark facet, a pale edge, an amber seam pulsing and a chunk of rubble at
        /// its base. A flying one glows.
        void SsSpearShape(V2 at, float ang, float len, float scale, float alpha, int idx, float t, bool fly)
        {
            V2 d = V2.FromAngle(ang) * scale, s = new V2(-d.Y, d.X);
            float h = len / 2f, w = SsWidth;
            System.Func<float, float, V2> P = (x, y) => at + d * x + s * y;
            if (fly)
                for (int g = 0; g < 2; g++)  // the glow
                {
                    float k = g == 0 ? 1.7f : 1.35f;
                    var gc = SsGlow.WithAlpha((g == 0 ? 0.08f : 0.18f) * alpha);
                    SetColor(gc);
                    Tri(P(h * k, 0f), P(0.2f * h, -w / 2f * k), P(-h * k, 0f));
                    Tri(P(h * k, 0f), P(0.2f * h, w / 2f * k), P(-h * k, 0f));
                }
            SetColor(SsHi.WithAlpha(alpha));
            Tri(P(h, 0f), P(0.2f * h, -w / 2f), P(-h + 5f, 0f));
            Tri(P(0.2f * h, -w / 2f), P(-h, -0.32f * w), P(-h + 5f, 0f));
            SetColor(SsLo.WithAlpha(alpha));
            Tri(P(h, 0f), P(0.2f * h, w / 2f), P(-h + 5f, 0f));
            Tri(P(0.2f * h, w / 2f), P(-h, 0.32f * w), P(-h + 5f, 0f));
            Line(P(h, 0f), P(0.2f * h, -w / 2f), 1f, SsEdge.WithAlpha(alpha));
            Line(P(0.2f * h, -w / 2f), P(-h, -0.32f * w), 1f, SsEdge.WithAlpha(alpha));
            float pulse = 0.55f + 0.45f * Mathf.Sin(t * 5f + idx);
            var seam = SsGlow.WithAlpha(0.9f * pulse * alpha);
            Line(P(0.7f * h, 0f), P(0.1f * h, -1f), 1.3f, seam);
            Line(P(0.1f * h, -1f), P(-0.3f * h, 1.5f), 1.3f, seam);
            Line(P(-0.3f * h, 1.5f), P(-h + 6f, 0f), 1.3f, seam);
            SetColor(SsLo.WithAlpha(alpha));
            Tri(P(-h + 2f, -4f), P(-h - 5f, -2f), P(-h - 4f, 3f));
            Tri(P(-h + 2f, -4f), P(-h - 4f, 3f), P(-h + 3f, 4f));
        }

        /// A thrown spear (Defs.StoneSpear, from DrawSmallShotLook): its trail, its shadow and the spear, glowing.
        void DrawStoneSpearShot(Bullet b, float t)
        {
            SsUpdate();
            SsShot s;
            float ang = b.Vel.IsZero ? 0f : b.Vel.Angle;
            if (ssShots.TryGetValue(b.Id, out s) && s.Trail.Count > 1)
                for (int i = 1; i < s.Trail.Count; i++)
                {
                    float k = (float)i / s.Trail.Count;
                    Line(s.Trail[i - 1], s.Trail[i], 5f * k, SsGlow.WithAlpha(0.35f * k));
                }
            float lift = Mathf.Max(4f, 18f - (b.Life0 - b.Life) * 30f);
            SsShadow(b.Pos, ang, lift, 1f, 0.35f);
            SsSpearShape(b.Pos, ang, SsLen, 1f, 1f, b.Id, t, true);
        }

        /// The spears that ended (stuck in the ground, or crumbling where they hit) and the dust: drawn with the slam effects.
        void DrawStonespearLeftovers(float t)
        {
            SsUpdate();
            foreach (var gh in ssGhosts)
            {
                float a = gh.Stuck ? 1f - gh.Age / SsStuckTime : 1f - gh.Age / SsCrumbleTime;
                float len = gh.Stuck ? SsLen * (1f - gh.Age * 0.4f) : SsLen * Mathf.Max(0.05f, 1f - gh.Age * 1.5f);
                if (gh.Stuck)
                    SsShadow(gh.Pos, gh.Ang, 0f, 1f, 0.25f * a);
                SsSpearShape(gh.Pos, gh.Ang, len, 1f, Mathf.Max(0f, a), 0, t, false);
                if (!gh.Stuck && gh.Age < 0.2f)
                {
                    float k = gh.Age / 0.2f;
                    Circle(gh.Pos, 14f * k + 3f, Rgba.White.WithAlpha(0.8f * (1f - k)), 12);  // the hit's flash
                }
            }
            foreach (var m in ssMotes)
            {
                float a = 1f - m.Age / m.Life, hs = m.Size / 2f;
                FillRect(m.Pos.X - hs, m.Pos.Y - hs, m.Size, m.Size, SsDust.WithAlpha(a));
            }
        }

        /// On the floor, under everyone (DrawArenaContents): each slam's nine glowing cracks, fading over the impact's life.
        void DrawStoneCracks()
        {
            foreach (var fx in FxOf(FxKind.StoneSlam))
            {
                if (fx.Kind != FxKind.StoneSlam || fx.Pos2.X < 1f)
                    continue;
                float k = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur)), R = fx.Radius;
                float a = 1f - k;
                // the cracks: jagged lines from the centre, the same every frame (seeded by the effect)
                var rng = new System.Random((int)((fx.Pos2.X - 1f) * 100000f) + (int)fx.Pos2.Y * 7919);
                for (int c = 0; c < 9; c++)
                {
                    float ang = c / 9f * MathX.Tau + (float)rng.NextDouble() * 0.3f, len = (50f + (float)rng.NextDouble() * 60f) * R / Game.SsSlamRadius;
                    V2 at = fx.Pos;
                    for (int j = 0; j < 5; j++)
                    {
                        ang += ((float)rng.NextDouble() - 0.5f) * 0.7f;
                        V2 next = at + V2.FromAngle(ang) * (len / 5f);
                        Line(at, next, 3f, SsCrack.WithAlpha(0.55f * a));
                        Line(at, next, 1.4f, new Rgba(0f, 0f, 0f, 0.6f * a));
                        at = next;
                    }
                }
            }
        }

        /// FxKind.StoneSlam: the wind-up's dashed ring closing in round the slammer (Pos2.X 0), or the impact's ring rolling out
        /// (Pos2.X >= 1; its cracks are DrawStoneCracks).
        void DrawStoneSlamFx(Effect fx)
        {
            float k = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur)), R = fx.Radius;
            if (fx.Pos2.X < 1f)
            {
                Player p;
                V2 at = game.Players.TryGetValue((int)fx.Pos2.Y, out p) ? p.Disp : fx.Pos;
                float r = R * (0.4f + 0.6f * k), dash = 6f / Mathf.Max(1f, r);
                int n = Mathf.Max(6, (int)(MathX.Tau * r / 12f));
                for (int i = 0; i < n; i++)
                    Arc(at, r, 2f, SsGlow.WithAlpha(0.25f + 0.5f * k), 3, MathX.Tau * i / n, dash);
                return;
            }
            float ringT = fx.T / 0.45f;
            if (ringT < 1f)
                Arc(fx.Pos, R * (0.3f + 0.7f * Mathf.Sqrt(ringT)), 10f * (1f - ringT) + 1f, SsGlow.WithAlpha(0.7f * (1f - ringT)), 48);
        }
    }
}
