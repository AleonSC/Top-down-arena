using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Tank Soldier's Grenade Launcher with Visual effects on (Docs/Mockups/grenade-launcher.html). Drawing only: the drums,
    /// rounds and clouds are Core/GrenadeLauncher.cs. With the effects off a plain model draws and nothing else here runs.
    ///   The model: stock, receiver, a slotted barrel that glows after shots, and the revolving drum on top (its colour band amber
    ///   for HE, teal for smoke; each loaded chamber shows its round). A shot kicks it back, flashes and puffs smoke at the muzzle
    ///   and indexes the drum one chamber. Alt (Player.GlSwap) spins the drum out sideways and slides the other in with a clack and a
    ///   label over you; the reload (Player.GlReload) swings the drum out, spills the hulls and clicks rounds back in one by one.
    ///   HE grenades fly on an arc over their shadow, spinning, with a smoke trail, hop once and burst (a flash, two shockwave rings,
    ///   fireballs, smoke, sparks, debris, embers and a scorch). Smoke canisters arc in, bounce twice, vent from both ends and open
    ///   into a churning teal-grey cloud with a dashed edge; regular enemies in it are greyed with a jammed-gun icon and coughs, and
    ///   bosses get a SLOWED tag. Behind your own player: the drum's rounds and the smoke canisters, the next one ringed as it refills.
    public partial class ArenaApp
    {
        const byte PGlFire = 18, PGlPuff = 19;  // particle kinds (Juice.cs draws them through DrawGlPart)
        const float GlU = 1f, GlGripX = 6f, GlGripY = 4f;  // the mockup's gun units into the weapon frame; where the rear hand holds it

        static readonly Rgba GlStock = Hx(0x3b4234), GlPad = Hx(0x1d2119), GlRecv = Hx(0x4a5240), GlGroove = Hx(0x2a2f25),
            GlBarLight = Hx(0x86907c), GlBarMid = Hx(0x4b5446), GlBarDark = Hx(0x262b22), GlSlot = new Rgba(0.06f, 0.07f, 0.05f, 0.85f),
            GlRingCol = Hx(0x22271e), GlGrip = Hx(0x2e3428), GlAmber = Hx(0xe2a23c), GlTeal = Hx(0x4fd0b8), GlDrumCol = Hx(0x565f4b),
            GlHole = Hx(0x0b0d0a), GlHeBody = Hx(0x5d6a3a), GlHeTip = Hx(0xe0562f), GlSmBody = Hx(0x7b888c), GlBrass = Hx(0xc9a24a),
            GlBrassD = Hx(0x3a2c10), GlHeLabel = Hx(0xf0a640), GlSmLabel = Hx(0x5fd3bd), GlCanLight = Hx(0xa8b4b7), GlCanMid = Hx(0x6d7a7e),
            GlCanDark = Hx(0x3e4749), GlCanEnd = Hx(0x2b3133), GlShadow = new Rgba(0f, 0f, 0f, 0.42f);

        class GlSeen
        {
            public int Shot = -1;
            public float ShotAt = -99f, Heat, Drum, DrumTo, Swap = -1f, Reload = -1f;
            public V2 Muzzle, DrumAt;
        }

        readonly Dictionary<int, GlSeen> glSeen = new Dictionary<int, GlSeen>();
        readonly Dictionary<int, int> glPhase = new Dictionary<int, int>();  // a grenade's or canister's flight phase last seen (for the landing puffs)
        readonly List<int> glGone = new List<int>();

        struct GlText { public V2 Pos; public string Text; public Rgba Col; public float T, Life; public bool Big; }
        readonly List<GlText> glTexts = new List<GlText>();

        void ClearGrenadeLauncherLooks() { glSeen.Clear(); glPhase.Clear(); glTexts.Clear(); }

        GlSeen GlOf(int id)
        {
            GlSeen s;
            if (!glSeen.TryGetValue(id, out s))
                glSeen[id] = s = new GlSeen();
            return s;
        }

        // --- the pose ---------------------------------------------------------------------------------------------------

        // what the model reads while it draws (GlPose)
        float mGlKick, mGlHeat, mGlDrum, mGlDy, mGlSpin, mGlA = 1f, mGlTilt;
        int mGlMode, mGlLoaded, mGlChambers, mGlFront;

        static float GlSmooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float GlEaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }

        /// Where along the swap (0..1) and reload (0..1) a launcher is; -1 when not.
        static float GlSwapAt(Player p) => p.GlSwap > 0f ? 1f - p.GlSwap / Game.GlSwapTime : -1f;
        static float GlReloadAt(Player p) => p.GlReload > 0f ? Mathf.Clamp01(1f - p.GlReload / Game.GlReloadTime) : -1f;

        /// The drum being drawn: the old one until the swap is GlSwapFlip through.
        static int GlDrawnMode(Player p) { float s = GlSwapAt(p); return s >= 0f && s < Game.GlSwapFlip ? 1 - p.GlMode : p.GlMode; }

        static int GlChambersOf(Player p, int mode) => mode == 0 ? Game.GlDrumOf(p) : 6;

        /// The model's pose for the player being drawn (the mockup's drumPose): the drum sliding out and in, spinning, the gun tilting.
        void GlPose(Player p)
        {
            var s = GlOf(p.Id);
            float age = juiceClock - s.ShotAt;
            mGlKick = age >= 0f ? Mathf.Exp(-11f * age) : 0f;
            mGlHeat = s.Heat;
            mGlMode = GlDrawnMode(p);
            mGlChambers = GlChambersOf(p, mGlMode);
            float sw = GlSwapAt(p), rl = mGlMode == 0 && sw < 0f ? GlReloadAt(p) : -1f;
            mGlDy = mGlSpin = mGlTilt = 0f; mGlA = 1f;
            if (sw >= 0f)
            {
                if (sw < Game.GlSwapFlip) { float k = GlSmooth(sw / Game.GlSwapFlip); mGlDy = k * 24f; mGlSpin = k * MathX.Tau * 0.9f; mGlA = 1f - k * 0.7f; mGlTilt = -k * 0.18f; }
                else if (sw < 0.8f) { float k = GlEaseOut((sw - Game.GlSwapFlip) / (0.8f - Game.GlSwapFlip)); mGlDy = -24f * (1f - k); mGlSpin = -(1f - k) * MathX.Tau * 0.7f; mGlA = 0.3f + 0.7f * k; mGlTilt = -0.18f * (1f - k); }
                else { float k = (sw - 0.8f) / 0.2f; mGlDy = -Mathf.Sin(k * MathX.Pi) * 2.5f; mGlTilt = Mathf.Sin(k * MathX.Pi) * 0.04f; }
            }
            else if (rl >= 0f)
            {
                float o = rl < 0.88f ? GlSmooth(rl / 0.18f) : 1f - GlEaseOut((rl - 0.88f) / 0.12f);
                mGlDy = o * 14f; mGlTilt = o * 0.22f;
                if (rl > 0.18f && rl < 0.42f) mGlSpin = GlSmooth((rl - 0.18f) / 0.24f) * MathX.Tau * 1.5f;
            }
            mGlLoaded = rl >= 0f ? Mathf.FloorToInt(Mathf.Clamp01((rl - 0.45f) / 0.4f) * mGlChambers)
                : mGlMode == 0 ? Mathf.Max(0, Game.GlDrumOf(p) - p.GlUsed) : Mathf.Max(0, Game.GlSmokeMax - p.GlSmokeUsed);
            float step = MathX.Tau / mGlChambers;
            mGlFront = ((Mathf.RoundToInt(-s.DrumTo / step) % mGlChambers) + mGlChambers) % mGlChambers;
            mGlDrum = s.Drum;
        }

        /// The model's kick (CharacterModels.cs slides both hands back by it).
        float GlKick(Player p) => Juicy ? 7f * GlU * Mathf.Exp(-11f * Mathf.Max(0f, juiceClock - GlOf(p.Id).ShotAt)) : 0f;

        /// The front hand on the foregrip, following the gun's tilt (CharacterModels.cs, hold 1).
        void GlFrontHand(Player p, float wk, float rhx, float rhy, ref float lhx, ref float lhy)
        {
            float tilt = 0f;
            if (Juicy)
            {
                GlPose(p);
                tilt = mGlTilt;
            }
            // the mockup's foregrip hand (37, -7) about the gun's own origin, turned by the tilt, then into the weapon frame
            float gx = 37f, gy = -7f, c = Mathf.Cos(tilt), sn = Mathf.Sin(tilt);
            float x = gx * c - gy * sn, y = gx * sn + gy * c;
            lhx = rhx + (x - GlGripX) * GlU * wk;
            lhy = rhy + (y - GlGripY) * GlU * wk;
        }

        // --- the model (CharacterModels.cs DrawHeld) -------------------------------------------------------------------
        // Drawn in the mockup's units, the rear hand (GlGripX, GlGripY) at the weapon frame's origin; the tilt turns it about the
        // mockup's origin; shadows drop straight down the screen as the mockup's canvas shadows do.

        float glC = 1f, glS;  // the tilt's cos and sin while drawing

        void GlXY(float x, float y, out float fx, out float fy)
        {
            fx = ((x * glC - y * glS) - GlGripX) * GlU;
            fy = ((x * glS + y * glC) - GlGripY) * GlU;
        }

        void GlPoly(Rgba c, params float[] p)
        {
            var q = new float[p.Length];
            for (int i = 0; i + 1 < p.Length; i += 2)
                GlXY(p[i], p[i + 1], out q[i], out q[i + 1]);
            MPoly(c, q);
        }

        /// A rounded rectangle (its corners cut), as the mockup's rr.
        void GlRR(float x, float y, float w, float h, float r, Rgba c)
        {
            r = Mathf.Min(r * 0.6f, Mathf.Min(w, h) * 0.5f);
            GlPoly(c, x + w * 0.5f, y + h * 0.5f, x + r, y, x + w - r, y, x + w, y + r, x + w, y + h - r, x + w - r, y + h, x + r, y + h, x, y + h - r, x, y + r, x + r, y);
        }

        void GlCirc(float x, float y, float r, Rgba c, int segs = 14)
        {
            float fx, fy;
            GlXY(x, y, out fx, out fy);
            MCirc(fx, fy, r * GlU, c, segs);
        }

        /// The shadow under a shape: the same rounded rect, dark, dropped down the screen.
        void GlShadowRR(float x, float y, float w, float h, float drop)
        {
            V2 off = new V2(1f, drop) * mU;
            float r = Mathf.Min(w, h) * 0.3f;
            float[] p = { x + r, y, x + w - r, y, x + w, y + r, x + w, y + h - r, x + w - r, y + h, x + r, y + h, x, y + h - r, x, y + r };
            SetColor(Mc(GlShadow));
            float cx, cy;
            GlXY(x + w * 0.5f, y + h * 0.5f, out cx, out cy);
            V2 mid = MW(cx, cy) + off, prev = V2.Zero, first = V2.Zero;
            for (int i = 0; i < p.Length; i += 2)
            {
                float fx, fy;
                GlXY(p[i], p[i + 1], out fx, out fy);
                V2 v = MW(fx, fy) + off;
                if (i == 0) first = v; else Tri(mid, prev, v);
                prev = v;
            }
            Tri(mid, prev, first);
        }

        void GlShadowCirc(float x, float y, float r, float drop)
        {
            float fx, fy;
            GlXY(x, y, out fx, out fy);
            Circle(MW(fx, fy) + new V2(1f, drop) * mU, r * GlU * mU * mWs, Mc(GlShadow), 16);
        }

        /// A round seen end-on in its chamber (the mockup's drawRound).
        void GlRound(float x, float y, float r, int mode)
        {
            GlCirc(x, y, r, mode == 0 ? GlHeBody : GlSmBody, 10);
            GlCirc(x, y, r * 0.55f, mode == 0 ? GlHeTip : GlTeal, 8);
            GlCirc(x - r * 0.3f, y - r * 0.3f, r * 0.25f, new Rgba(1f, 1f, 1f, 0.35f), 6);
        }

        void DrawGrenadeLauncherFx()
        {
            var p = mHeldOwner;
            if (p == null)
            {
                DrawGrenadeLauncherPlain();
                return;
            }
            GlPose(p);
            glC = Mathf.Cos(mGlTilt - 0.08f * mGlKick); glS = Mathf.Sin(mGlTilt - 0.08f * mGlKick);
            // stock and recoil pad
            GlShadowRR(-14f, -5f, 18f, 10f, 2.5f);
            GlPoly(GlStock, -5f, 0f, -14f, -4f, 4f, -5f, 4f, 5f, -14f, 4f);
            GlRR(-16f, -4.5f, 2.6f, 9f, 0.5f, GlPad);
            // receiver and its groove
            GlShadowRR(2f, -6f, 26f, 12f, 2.5f);
            GlRR(2f, -6f, 26f, 12f, 2.5f, GlRecv);
            GlRR(4f, -1f, 22f, 2f, 0.3f, GlGroove);
            // the barrel: a fat tube, lit from above, with heat-shroud slots
            GlShadowRR(26f, -5f, 26f, 10f, 2.5f);
            GlRR(26f, -5f, 26f, 10f, 2f, GlBarMid);
            GlRR(26f, -5f, 26f, 3.2f, 1.2f, GlBarLight);
            GlRR(26f, 2f, 26f, 3f, 1.2f, GlBarDark);
            for (float x = 30f; x < 46f; x += 4f)
                GlRR(x, -3.6f, 2f, 7.2f, 1f, GlSlot);
            if (mGlHeat > 0.02f)  // hot from the muzzle back
            {
                GlRR(40f, -5f, 14f, 10f, 2f, new Rgba(1f, 0.43f, 0.16f, 0.45f * mGlHeat));
                GlRR(47f, -5f, 7f, 10f, 2f, new Rgba(1f, 0.6f, 0.3f, 0.5f * mGlHeat));
            }
            GlRR(50f, -6f, 4.5f, 12f, 1.2f, GlRingCol);
            GlRR(50f, -6f, 1f, 12f, 0.2f, new Rgba(1f, 1f, 1f, 0.18f));
            GlRR(34f, -10f, 6f, 5f, 1.5f, GlGrip);                       // foregrip
            GlRR(19f, -7.6f, 7f, 2f, 0.3f, GlRingCol); GlRR(24.6f, -7.6f, 1.2f, 2f, 0.2f, GlAmber);  // the flip-up sight
            // the drum
            float keepA = mAlpha;
            mAlpha *= mGlA;
            float dx = 15f, dy = mGlDy;
            GlShadowCirc(dx, dy, 10.5f, 2.5f + Mathf.Abs(dy) * 0.15f);
            GlCirc(dx, dy, 10.5f, GlDrumCol, 22);
            GlCircRing(dx, dy, 10.2f, 1.6f, mGlMode == 0 ? GlAmber : GlTeal);
            float rot = mGlDrum + mGlSpin, step = MathX.Tau / mGlChambers;
            for (int i = 0; i < mGlChambers; i++)  // flutes between the chambers
            {
                float a = rot + i * step + step * 0.5f;
                GlCirc(dx + Mathf.Cos(a) * 8.6f, dy + Mathf.Sin(a) * 8.6f, 1.25f, new Rgba(0.08f, 0.09f, 0.07f, 0.55f), 8);
            }
            float cr = mGlChambers > 6 ? 2.4f : 2.9f, cd = mGlChambers > 6 ? 6.3f : 5.8f;
            for (int i = 0; i < mGlChambers; i++)
            {
                float a = rot + i * step, x = dx + Mathf.Cos(a) * cd, y = dy + Mathf.Sin(a) * cd;
                GlCirc(x, y, cr, GlHole, 10);
                if (((i - mGlFront + mGlChambers) % mGlChambers) < mGlLoaded)
                    GlRound(x, y, cr * 0.88f, mGlMode);
            }
            GlCirc(dx, dy, 1.8f, GlBrass, 10); GlCirc(dx, dy, 0.7f, GlBrassD, 6);
            mAlpha = keepA;
            var s = GlOf(p.Id);
            float mx, my, ux, uy;
            GlXY(54f, 0f, out mx, out my);
            GlXY(dx, 0f, out ux, out uy);
            s.Muzzle = MW(mx, my);     // (the flash goes here)
            s.DrumAt = MW(ux, uy);     // (the hulls spill from here)
        }

        void GlCircRing(float x, float y, float r, float w, Rgba c)
        {
            float fx, fy;
            GlXY(x, y, out fx, out fy);
            Arc(MW(fx, fy), r * GlU * mU * mWs, w * GlU * mU * mWs, Mc(c), 24);
        }

        /// The model with the effects off: a plain launcher.
        void DrawGrenadeLauncherPlain()
        {
            glC = 1f; glS = 0f;
            GlRR(-14f, -4f, 18f, 8f, 1f, GlStock); GlRR(2f, -6f, 26f, 12f, 2f, GlRecv); GlRR(26f, -5f, 28f, 10f, 2f, GlBarMid);
            GlCirc(15f, 0f, 10.5f, GlDrumCol, 18); GlCirc(15f, 0f, 3f, GlAmber, 10);
        }

        // --- each frame: shots, the swap, the reload, the trails ----------------------------------------------------------

        void UpdateGrenadeLauncherLooks(float dt)
        {
            for (int i = glTexts.Count - 1; i >= 0; i--)
            {
                var g = glTexts[i];
                g.T += dt;
                g.Pos += new V2(0f, -(g.Big ? 18f : 30f) * dt);
                if (g.T >= g.Life) glTexts.RemoveAt(i); else glTexts[i] = g;
            }
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon.Id != WeaponId.GrenadeLauncher)
                    continue;
                var s = GlOf(p.Id);
                V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal();
                int mode = GlDrawnMode(p), n = GlChambersOf(p, mode);
                if (s.Shot < 0)
                    s.Shot = p.GlShot;
                if (p.GlShot != s.Shot)
                {
                    s.Shot = p.GlShot;
                    s.ShotAt = juiceClock;
                    s.Heat = Mathf.Min(1f, s.Heat + 0.45f);
                    s.DrumTo -= MathX.Tau / n;
                    OnGlShot(p, s, p.GlMode == 1);
                }
                s.Heat *= Mathf.Exp(-1.6f * dt);
                s.Drum += (s.DrumTo - s.Drum) * Mathf.Min(1f, dt * 20f);
                float sw = GlSwapAt(p);
                if (sw >= Game.GlSwapFlip && s.Swap < Game.GlSwapFlip)  // the new drum's name over you
                    glTexts.Add(new GlText { Pos = p.Disp + new V2(0f, -34f), Text = p.GlMode == 0 ? "HE ROUNDS" : "SMOKE ROUNDS", Col = p.GlMode == 0 ? GlHeLabel : GlSmLabel, Life = 1.1f, Big = true });
                if (sw >= 0.8f && s.Swap < 0.8f)  // it clacks home
                {
                    V2 at = DrumOrNear(p, s);
                    Ring(at, 22f, 0.3f, p.GlMode == 0 ? GlHeLabel : GlSmLabel);
                    Sparks(at, new Rgba(1f, 0.9f, 0.7f), 6, 140f, 0.18f, 1.2f);
                    Shake(0.05f);
                }
                if (sw < 0f && s.Swap >= 0f)
                    s.DrumTo = s.Drum = Mathf.Round(s.DrumTo / (MathX.Tau / n)) * (MathX.Tau / n);
                s.Swap = sw;
                float rl = GlReloadAt(p);
                if (rl >= 0.25f && s.Reload < 0.25f)  // the hulls spill out of the swung-out drum
                {
                    V2 at = DrumOrNear(p, s) + side * 10f;
                    for (int k = 0; k < Game.GlDrumOf(p); k++)
                    {
                        float a = p.Aim + MathX.Pi / 2f + Rnd(-0.6f, 0.6f);
                        AddPart(new Part { Kind = PBrass, Pos = at, Vel = V2.FromAngle(a) * Rnd(40f, 110f), Rot = Rnd(0f, MathX.Tau), Spin = Rnd(-18f, 18f), A0 = 4f, A1 = Rnd(80f, 150f), Life = Rnd(1.4f, 2f), Max = 2f, Size = 3f, Col = BrassCol });
                    }
                }
                if (rl < 0f && s.Reload >= 0f && p.GlReload <= 0f)  // full again: it clicks shut
                {
                    Ring(DrumOrNear(p, s), 18f, 0.25f, new Rgba(1f, 0.82f, 0.55f));
                    Shake(0.04f);
                    s.DrumTo = s.Drum = Mathf.Round(s.DrumTo / (MathX.Tau / n)) * (MathX.Tau / n);
                }
                s.Reload = rl;
            }
            // grenades in flight: their smoke trail and embers, a puff of dust where each lands and hops
            foreach (var b in game.Bullets.Values)
            {
                if (b.Kind != Defs.GrenadeShot || ShotWeapon(b) != WeaponId.GrenadeLauncher)
                    continue;
                float z;
                int phase;
                V2 at = GlNadeAt(b, out z, out phase);
                if (phase == 0 && Rnd(0f, 1f) < dt / 0.018f)
                {
                    AddPart(new Part { Kind = PDust, Pos = at + new V2(0f, -z), Vel = new V2(Rnd(-8f, 8f), Rnd(-8f, 8f)), Life = 0.45f, Max = 0.45f, Size = 2.4f, Col = new Rgba(0.67f, 0.65f, 0.59f) });
                    if (Rnd(0f, 1f) < 0.5f)
                        AddPart(new Part { Kind = PSpark, Pos = at + new V2(0f, -z), Vel = new V2(Rnd(-15f, 15f), Rnd(-15f, 15f)), Life = 0.25f, Max = 0.25f, Size = 1f, Col = new Rgba(1f, 0.63f, 0.27f) });
                }
                GlLandPuff(b.Id, phase, at);
            }
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.SmokeShell)
                    UpdateGlSmoke(z, dt);
            glGone.Clear();
            foreach (var kv in glPhase)
                if (!game.Bullets.ContainsKey(kv.Key) && !game.Zones.ContainsKey(-kv.Key - 1))
                    glGone.Add(kv.Key);
            foreach (int id in glGone)
                glPhase.Remove(id);
        }

        V2 DrumOrNear(Player p, GlSeen s) => !s.DrumAt.IsZero && s.DrumAt.DistanceTo(p.Disp) < 80f ? s.DrumAt : p.Disp + V2.FromAngle(p.Aim) * 12f;

        /// A puff of dust each time something lobbed comes down (key: a bullet id, or -zone id - 1).
        void GlLandPuff(int key, int phase, V2 at)
        {
            int was;
            if (!glPhase.TryGetValue(key, out was))
                was = phase;
            if (phase > was)
            {
                AddPart(new Part { Kind = PDust, Pos = at, Life = 0.35f, Max = 0.35f, Size = 6f, Col = new Rgba(0.75f, 0.69f, 0.55f) });
                AddPart(new Part { Kind = PRing, Pos = at, Life = 0.3f, Max = 0.3f, Size = 12f, Col = new Rgba(0.75f, 0.69f, 0.55f, 0.6f) });
            }
            glPhase[key] = phase;
        }

        /// The muzzle's flash, smoke ring and sparks, and the kick's shake.
        void OnGlShot(Player p, GlSeen s, bool smoke)
        {
            V2 dir = V2.FromAngle(p.Aim);
            V2 at = !s.Muzzle.IsZero && s.Muzzle.DistanceTo(p.Disp) < 110f ? s.Muzzle : p.Disp + dir * 52f;
            Rgba warm = smoke ? new Rgba(0.47f, 0.9f, 0.82f) : new Rgba(1f, 0.67f, 0.27f);
            AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = p.Aim, Life = 0.09f, Max = 0.09f, Size = smoke ? 22f : 30f, Col = warm });
            AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = p.Aim, Life = 0.06f, Max = 0.06f, Size = smoke ? 14f : 18f, Col = new Rgba(1f, 0.97f, 0.88f) });
            AddPart(new Part { Kind = PFlash, Pos = at, Life = 0.08f, Max = 0.08f, Size = 12f, Col = warm });
            AddPart(new Part { Kind = PRing, Pos = at, Life = 0.25f, Max = 0.25f, Size = 16f, Col = smoke ? new Rgba(0.51f, 0.9f, 0.84f) : new Rgba(1f, 0.78f, 0.51f) });
            Rgba puff = smoke ? new Rgba(0.59f, 0.78f, 0.76f) : new Rgba(0.59f, 0.59f, 0.55f);
            for (int i = 0; i < 7; i++)
            {
                float a = p.Aim + Rnd(-0.9f, 0.9f), sp = Rnd(20f, 90f);
                AddPart(new Part { Kind = PDust, Pos = at, Vel = V2.FromAngle(a) * sp + dir * 30f, Life = Rnd(0.5f, 0.9f), Max = 0.9f, Size = Rnd(3f, 5f), Col = puff });
            }
            for (int i = 0; i < 6; i++)
            {
                float a = p.Aim + Rnd(-0.5f, 0.5f);
                AddPart(new Part { Kind = PSpark, Pos = at, Vel = V2.FromAngle(a) * Rnd(160f, 320f), Life = Rnd(0.12f, 0.25f), Max = 0.25f, Size = 1.4f, Col = new Rgba(1f, 0.82f, 0.55f) });
            }
            if (p.Id == game.MyId)
                Shake(smoke ? 0.05f : 0.08f);
        }

        // --- HE grenades -----------------------------------------------------------------------------------------------

        /// Where an HE grenade is drawn (on the floor under it), how high it is, and its phase: 0 flying, 1 hopping.
        /// The host's bullet flies straight to its end; the look replays the arc and the hop from where it set out.
        V2 GlNadeAt(Bullet b, out float z, out int phase)
        {
            float T = Mathf.Max(0.05f, b.Life0), e = Mathf.Clamp(T - b.Life, 0f, T), sp = b.Vel.Length;
            V2 dir = sp > 0.01f ? b.Vel / sp : new V2(1f, 0f), origin = b.Pos - b.Vel * e;
            float tm = Mathf.Max(0.01f, T - Game.GlHopTime), dm = Mathf.Max(1f, sp * T - Game.GlHop);
            if (e < tm)
            {
                float k = e / tm;
                z = Game.GlPeak(dm) * 4f * k * (1f - k);
                phase = 0;
                return origin + dir * dm * k;
            }
            float u = Mathf.Clamp01((e - tm) / Game.GlHopTime);
            z = 8f * 4f * u * (1f - u);
            phase = 1;
            return origin + dir * (dm + Game.GlHop * u);
        }

        /// An HE round from the side (the mockup's drawShell): olive body, red nose, brass base, turning as it flies.
        void GlShell(V2 at, float r, float rot)
        {
            V2 d = V2.FromAngle(rot), s = d.Orthogonal();
            SetColor(GlHeBody);
            Quad(at - d * (r * 1.3f) - s * r, at + d * (r * 0.9f) - s * r, at + d * (r * 0.9f) + s * r, at - d * (r * 1.3f) + s * r);
            SetColor(GlHeTip);
            for (int i = 0; i < 6; i++)
            {
                float a0 = -MathX.Pi / 2f + MathX.Pi * i / 6f, a1 = -MathX.Pi / 2f + MathX.Pi * (i + 1) / 6f;
                V2 c = at + d * (r * 0.9f);
                Tri(c, c + d * (Mathf.Cos(a0) * r) + s * (Mathf.Sin(a0) * r), c + d * (Mathf.Cos(a1) * r) + s * (Mathf.Sin(a1) * r));
            }
            SetColor(GlBrass);
            Quad(at - d * (r * 1.3f) - s * r, at - d * (r * 0.7f) - s * r, at - d * (r * 0.7f) + s * r, at - d * (r * 1.3f) + s * r);
        }

        /// ShotLooks.cs DrawWeaponShot: the grenade's shadow on the floor, and the grenade up on its arc, swelling as it climbs.
        void DrawGlGrenade(Bullet b, float t)
        {
            float z;
            int phase;
            V2 at = GlNadeAt(b, out z, out phase);
            Ellipse(at + new V2(1f, 2f), 5f - Mathf.Min(2f, z / 30f), 2.6f, new Rgba(0f, 0f, 0f, 0.4f - Mathf.Min(0.25f, z / 200f)));
            float sc = 1f + z / 90f;
            GlShell(at + new V2(0f, -z), 3.2f * sc, b.Id * 1.7f + (b.Life0 - b.Life) * (phase == 0 ? 14f : 7f));
        }

        // --- the HE blast ------------------------------------------------------------------------------------------------

        /// A Grenade Launcher blast: Core/GrenadeLauncher.cs sends it as a Boom in GlBoomColor (nothing else uses it).
        bool IsGlBoom(Effect fx) => fxOn && fx.Kind == FxKind.Boom && SameRgb(fx.Color, Game.GlBoomColor);

        /// The blast over its 0.6 s: a white-hot flash, a hard shockwave ring and a slower orange one (the fireballs are parts).
        void DrawGlBlast(Effect fx, float f)
        {
            float t = f * Mathf.Max(0.01f, fx.Dur), R = fx.Radius;
            if (t < 0.14f)
            {
                float q = 1f - t / 0.14f, r = R * 0.95f * (1.2f - 0.2f * q);
                FanGradient(fx.Pos, r, new Rgba(1f, 0.82f, 0.47f, 0.75f * q), new Rgba(1f, 0.55f, 0.2f, 0f), 24);
                FanGradient(fx.Pos, r * 0.3f, new Rgba(1f, 1f, 0.94f, q), new Rgba(1f, 0.82f, 0.47f, 0.6f * q), 16);
            }
            if (t < 0.38f)
            {
                float q = 1f - t / 0.38f;
                Arc(fx.Pos, R * 1.35f * GlEaseOut(1f - q) + 1f, 4f * q + 0.4f, new Rgba(1f, 0.86f, 0.67f, q), 40);
            }
            {
                float q = 1f - Mathf.Clamp01(t / 0.6f);
                Arc(fx.Pos, R * 0.9f * GlEaseOut(1f - q) + 1f, 2f * q + 0.4f, new Rgba(1f, 0.59f, 0.27f, q), 36);
            }
        }

        /// What the blast leaves (once, from Juice's new-effects pass): fireballs, smoke, sparks, debris, embers and a scorch.
        void GlBlastParts(Effect fx)
        {
            V2 c = fx.Pos;
            float R = fx.Radius;
            for (int i = 0; i < 9; i++)
            {
                V2 d = V2.FromAngle(Rnd(0f, MathX.Tau));
                float life = Rnd(0.35f, 0.65f);
                AddPart(new Part { Kind = PGlFire, Pos = c + d * Rnd(0f, 14f), Vel = d * Rnd(10f, 70f) + new V2(0f, -10f), Life = life, Max = life, Size = Rnd(12f, 22f) * R / 72f });
            }
            for (int i = 0; i < 12; i++)
            {
                V2 d = V2.FromAngle(Rnd(0f, MathX.Tau));
                float life = Rnd(1.1f, 1.9f);
                AddPart(new Part { Kind = PDust, Pos = c, Vel = d * Rnd(15f, 55f) + new V2(0f, -14f), Life = life, Max = life, Size = Rnd(9f, 15f), Col = new Rgba(0.22f, 0.2f, 0.19f) });
            }
            Sparks(c, new Rgba(1f, 0.78f, 0.47f), 26, 480f, 0.4f, 1.8f);
            for (int i = 0; i < 10; i++)
            {
                V2 d = V2.FromAngle(Rnd(0f, MathX.Tau));
                AddPart(new Part { Kind = PTumble, Pos = c, Vel = d * Rnd(60f, 170f), Rot = Rnd(0f, MathX.Tau), Spin = Rnd(-14f, 14f), Life = Rnd(0.9f, 1.4f), Max = 1.4f, Size = Rnd(1.5f, 2.5f), Col = i % 3 == 0 ? new Rgba(0.47f, 0.42f, 0.34f) : new Rgba(0.27f, 0.26f, 0.23f) });
            }
            for (int i = 0; i < 14; i++)
            {
                V2 d = V2.FromAngle(Rnd(0f, MathX.Tau));
                float life = Rnd(0.7f, 1.5f);
                AddPart(new Part { Kind = PSpark, Pos = c, Vel = d * Rnd(30f, 120f) + new V2(0f, -30f), Life = life, Max = life, Size = Rnd(1f, 1.8f), Col = new Rgba(1f, 0.63f, 0.27f) });
            }
            Scorch(c, R * 0.62f);
            var me = game.Me;
            if (me != null && me.Pos.DistanceTo(c) < 700f)
                Shake(0.16f);
        }

        /// Juice.cs's part pass: a fireball (white-hot, then orange, then a dark red as it dies), or a smoke-shell puff.
        void DrawGlPart(Part p, float f)
        {
            if (p.Kind == PGlFire)
            {
                float r = p.Size * (1f + (1f - f) * 0.9f);
                Rgba col = f > 0.6f ? new Rgba(1f, 0.93f, 0.67f) : f > 0.3f ? new Rgba(1f, 0.59f, 0.24f) : new Rgba(0.67f, 0.27f, 0.16f);
                FanGradient(p.Pos, r, col.WithAlpha(0.5f * f), col.WithAlpha(0f), 14);
                return;
            }
            float pr = p.Size * (1f + (1f - f) * 1.6f);
            FanGradient(p.Pos, pr, new Rgba(0.74f, 0.84f, 0.82f, 0.22f * f), new Rgba(0.63f, 0.75f, 0.73f, 0f), 12);
        }

        // --- the smoke shell -------------------------------------------------------------------------------------------

        /// Where a canister is (on the floor under it), how high, and its phase: 0 flying, 1 and 2 bouncing, 3 open.
        static V2 GlCanAt(Zone z, out float lift, out int phase)
        {
            float age = z.Duration - z.T, dist = z.GuardFrac, fl = Game.GlFlight(dist);
            V2 dir = V2.FromAngle(z.Aim), aim = z.Pos - dir * (Game.GlSmokeHop1 + Game.GlSmokeHop2), from = aim - dir * dist;
            if (age < fl)
            {
                float k = age / fl;
                lift = Game.GlPeak(dist) * 4f * k * (1f - k);
                phase = 0;
                return from + dir * dist * k;
            }
            if (age < fl + Game.GlSmokeHopT1)
            {
                float u = (age - fl) / Game.GlSmokeHopT1;
                lift = 8f * 4f * u * (1f - u);
                phase = 1;
                return aim + dir * Game.GlSmokeHop1 * u;
            }
            if (age < fl + Game.GlSmokeHopT1 + Game.GlSmokeHopT2)
            {
                float u = (age - fl - Game.GlSmokeHopT1) / Game.GlSmokeHopT2;
                lift = 3f * 4f * u * (1f - u);
                phase = 2;
                return aim + dir * (Game.GlSmokeHop1 + Game.GlSmokeHop2 * u);
            }
            lift = 0f;
            phase = 3;
            return z.Pos;
        }

        /// The canister: grey steel, lit along the top, a teal band round its middle, dark caps at both ends.
        void GlCanister(V2 at, float rot, float sc, float a)
        {
            V2 d = V2.FromAngle(rot) * sc, s = d.Orthogonal();
            Ellipse(at + new V2(1.5f, 2.5f) * sc, 7f * sc, 3.5f * sc, new Rgba(0f, 0f, 0f, 0.35f * a));
            SetColor(GlCanMid.WithAlpha(a)); Quad(at - d * 6.5f - s * 3.4f, at + d * 6.5f - s * 3.4f, at + d * 6.5f + s * 3.4f, at - d * 6.5f + s * 3.4f);
            SetColor(GlCanLight.WithAlpha(a)); Quad(at - d * 6.5f - s * 3.4f, at + d * 6.5f - s * 3.4f, at + d * 6.5f - s * 1.4f, at - d * 6.5f - s * 1.4f);
            SetColor(GlCanDark.WithAlpha(a)); Quad(at - d * 6.5f + s * 1.6f, at + d * 6.5f + s * 1.6f, at + d * 6.5f + s * 3.4f, at - d * 6.5f + s * 3.4f);
            SetColor(GlTeal.WithAlpha(a)); Quad(at - d * 1.6f - s * 3.4f, at + d * 1.6f - s * 3.4f, at + d * 1.6f + s * 3.4f, at - d * 1.6f + s * 3.4f);
            SetColor(GlCanEnd.WithAlpha(a));
            Quad(at - d * 7.2f - s * 2.4f, at - d * 5.8f - s * 2.4f, at - d * 5.8f + s * 2.4f, at - d * 7.2f + s * 2.4f);
            Quad(at + d * 5.8f - s * 2.4f, at + d * 7.2f - s * 2.4f, at + d * 7.2f + s * 2.4f, at + d * 5.8f + s * 2.4f);
        }

        /// How the canister lies: tumbling while it flies and bounces, then settled.
        static float GlCanRot(Zone z)
        {
            float age = z.Duration - z.T, land = Game.GlSmokeLand(z.GuardFrac);
            return z.Aim + z.Id * 0.9f + 9f * Mathf.Min(age, land) * (age < Game.GlFlight(z.GuardFrac) ? 1f : 0.5f);
        }

        /// How far the cloud reaches as drawn: out as it billows, a little farther as it thins at the end.
        static float GlCloudR(Zone z) => Game.GlSmokeReach(z) * (z.T < 1f ? 1f + 0.18f * (1f - z.T) : 1f);

        /// WorldEnemies.cs DrawZone, under the enemies: the canister on its way in, then the cloud's floor, edge and the canister.
        void DrawGlSmoke(Zone z, float t)
        {
            float lift;
            int phase;
            V2 at = GlCanAt(z, out lift, out phase);
            float rot = GlCanRot(z);
            if (phase < 3)
            {
                GlCanister(at + new V2(0f, -lift), rot, 1f + lift / 90f, 1f);
                return;
            }
            float R = GlCloudR(z), A = Mathf.Clamp01(z.T);
            if (R > 2f)
            {
                FanGradient(z.Pos, R, new Rgba(0.59f, 0.71f, 0.69f, 0.2f * A), new Rgba(0.51f, 0.65f, 0.63f, 0.1f * A), 36);
                int dashes = Mathf.Max(8, Mathf.RoundToInt(MathX.Tau * R / 11f));
                float off = -t * 10f / R, sweep = MathX.Tau / dashes * (5f / 11f);
                for (int i = 0; i < dashes; i++)
                    Arc(z.Pos, R, 1.2f, new Rgba(0.37f, 0.83f, 0.74f, 0.4f * A), 3, off + MathX.Tau * i / dashes, sweep);
            }
            GlCanister(z.Pos, rot, 1f, A);
        }

        /// Over the enemies: each cloud's churning puffs, the jammed enemies' grey and their icon.
        void DrawGlSmokeOver(float t)
        {
            if (!Juicy)
                return;
            bool any = false;
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.SmokeShell) { any = true; break; }
            if (!any)
                return;
            foreach (var e in game.Enemies.Values)  // smothered: a grey film over each enemy in a cloud (the puffs go over it)
                if (game.InGlSmoke(e, true))
                    Circle(e.Disp, e.Radius + 1f, new Rgba(0.73f, 0.81f, 0.8f, 0.25f), 18);
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.SmokeShell)
                    continue;
                any = true;
                float since = z.Duration - z.T - Game.GlSmokeLand(z.GuardFrac);
                if (since < 0f)
                    continue;
                float R = GlCloudR(z), A = Mathf.Clamp01(z.T);
                for (int i = 0; i < 28; i++)
                {
                    float h1 = Hash01(z.Id * 131 + i * 7), h2 = Hash01(z.Id * 71 + i * 13), h3 = Hash01(z.Id * 29 + i * 31), h4 = Hash01(z.Id * 17 + i * 3);
                    float a = h1 * MathX.Tau + (h4 - 0.5f) * 0.44f * since, d = Mathf.Sqrt(0.02f + 0.98f * h2) * R * 0.78f, sz = 0.55f + 0.45f * h3;
                    V2 at = z.Pos + V2.FromAngle(a) * d + new V2(0f, -since * 2f * sz);
                    float r = R * 0.34f * sz * (1f + 0.1f * Mathf.Sin(since * 1.4f + h4 * MathX.Tau));
                    FanGradient(at, r, new Rgba(0.76f, 0.84f, 0.82f, 0.13f * A), new Rgba(0.63f, 0.75f, 0.73f, 0f), 14);
                }
            }
            if (!any)
                return;
            foreach (var e in game.Enemies.Values)
            {
                if (!game.InGlSmoke(e, true))
                    continue;
                bool big = e.Def.IsBoss || e.Def.Mini;
                if (big)
                    continue;  // (a boss's SLOWED tag is text: DrawGlHud)
                float iy = e.Disp.Y - e.Radius - 18f + Mathf.Sin(t * 5f + e.Id) * 1.5f;
                V2 ic = new V2(e.Disp.X, iy);
                Circle(ic, 6.5f, new Rgba(0.06f, 0.08f, 0.07f, 0.85f), 14);
                SetColor(Hx(0xd9a441)); Quad(ic + new V2(-3.5f, -1.4f), ic + new V2(2.5f, -1.4f), ic + new V2(2.5f, 1.4f), ic + new V2(-3.5f, 1.4f));
                Arc(ic, 5.6f, 1.4f, Hx(0xff6a4d), 16);
                Line(ic + new V2(-4f, 4f), ic + new V2(4f, -4f), 1.4f, Hx(0xff6a4d));
            }
        }

        static float Hash01(int n)
        {
            unchecked
            {
                uint x = (uint)n * 2654435761u;
                x ^= x >> 13; x *= 0x5bd1e995; x ^= x >> 15;
                return (x & 0xffffff) / (float)0x1000000;
            }
        }

        /// Each frame for a smoke zone: a teal wisp trail in flight, dust at each bounce, the burst as it opens, the vents hissing,
        /// coughs from the enemies jammed in it and now and then a dry click.
        void UpdateGlSmoke(Zone z, float dt)
        {
            float lift;
            int phase;
            V2 at = GlCanAt(z, out lift, out phase);
            if (phase == 0 && Rnd(0f, 1f) < dt / 0.018f)
                AddPart(new Part { Kind = PDust, Pos = at + new V2(0f, -lift), Vel = new V2(Rnd(-8f, 8f), Rnd(-8f, 8f)), Life = 0.7f, Max = 0.7f, Size = 3f, Col = new Rgba(0.55f, 0.84f, 0.78f) });
            int was;
            bool opened = phase == 3 && glPhase.TryGetValue(-z.Id - 1, out was) && was < 3;
            GlLandPuff(-z.Id - 1, Mathf.Min(phase, 2) + (phase == 3 ? 1 : 0), at);
            if (opened)  // it pops: a ring and a rush of smoke
            {
                Ring(z.Pos, z.Radius * 1.1f, 0.5f, new Rgba(0.47f, 0.9f, 0.82f));
                for (int i = 0; i < 18; i++)
                {
                    V2 d = V2.FromAngle(Rnd(0f, MathX.Tau));
                    float life = Rnd(0.6f, 1.1f);
                    AddPart(new Part { Kind = PGlPuff, Pos = z.Pos, Vel = d * Rnd(80f, 200f) * (z.Radius / Game.GlSmokeRadius), Life = life, Max = life, Size = Rnd(8f, 14f) });
                }
                var me = game.Me;
                if (me != null && me.Pos.DistanceTo(z.Pos) < 700f)
                    Shake(0.05f);
            }
            if (phase < 3 || z.T < 1f)
                return;
            float rot = GlCanRot(z);
            if (Rnd(0f, 1f) < dt / 0.05f)
                for (int s = -1; s <= 1; s += 2)  // a jet out of each end
                {
                    float a = rot + (s < 0 ? MathX.Pi : 0f) + Rnd(-0.3f, 0.3f);
                    AddPart(new Part { Kind = PGlPuff, Pos = z.Pos + V2.FromAngle(rot) * (7f * s), Vel = V2.FromAngle(a) * Rnd(50f, 90f), Life = Rnd(0.5f, 0.9f), Max = 0.9f, Size = Rnd(3f, 6f) });
                }
            float reach = Game.GlSmokeReach(z);
            foreach (var e in game.Enemies.Values)
            {
                if (e.Def.IsBoss || e.Def.Mini || e.Disp.FartherThan(z.Pos, reach + e.Radius * 0.3f))
                    continue;
                if (Rnd(0f, 1f) < dt / 0.7f)  // a cough
                    AddPart(new Part { Kind = PDust, Pos = e.Disp + new V2(Rnd(-4f, 4f), -e.Radius * 0.4f), Vel = new V2(Rnd(-12f, 12f), -22f), Life = 0.6f, Max = 0.6f, Size = 2f, Col = new Rgba(0.82f, 0.86f, 0.84f) });
                if (Rnd(0f, 1f) < dt / 2.4f)  // it tries to shoot
                    glTexts.Add(new GlText { Pos = e.Disp + new V2(0f, -e.Radius - 10f), Text = "*click*", Col = Hx(0x9fb3b6), Life = 0.75f });
            }
        }

        // --- the stock behind you, and the words ------------------------------------------------------------------------

        /// Behind your own player: the drum (its rounds lit, a ring filling while it reloads) and the three smoke canisters, the
        /// next one ringed as it comes back.
        void DrawGlPips(Player p)
        {
            if (p.Downed || p.Id != game.MyId)
                return;
            V2 back = V2.FromAngle(p.Aim + MathX.Pi), side = back.Orthogonal();
            V2 c = p.Disp + back * 34f - side * 14f;
            int mode = p.GlMode, n = GlChambersOf(p, 0), he = Mathf.Max(0, Game.GlDrumOf(p) - p.GlUsed);
            Rgba col = mode == 0 ? GlHeLabel : GlSmLabel;
            Circle(c, 11f, new Rgba(0.04f, 0.05f, 0.04f, 0.6f), 18);
            Arc(c, 10f, 1.6f, GlHeLabel.WithAlpha(mode == 0 ? 0.9f : 0.35f), 22);
            for (int i = 0; i < n; i++)
            {
                V2 at = c + V2.FromAngle(-MathX.Pi / 2f + MathX.Tau * i / n) * 6f;
                Circle(at, n > 6 ? 1.8f : 2.2f, i < he && p.GlReload <= 0f ? GlHeTip : new Rgba(0.11f, 0.13f, 0.1f), 8);
            }
            float rl = GlReloadAt(p);
            if (rl >= 0f)
                Arc(c, 13.5f, 2f, GlHeLabel.WithAlpha(0.8f), 24, -MathX.Pi / 2f, MathX.Tau * rl);
            int smoke = Game.GlSmokeMax - p.GlSmokeUsed;
            for (int i = 0; i < Game.GlSmokeMax; i++)
            {
                V2 at = c + side * (24f + i * 11f);
                Rgba body = i < smoke ? GlTeal.WithAlpha(mode == 1 ? 1f : 0.55f) : new Rgba(0.11f, 0.13f, 0.1f, 0.8f);
                SetColor(body);
                Quad(at + new V2(-3.5f, -4.5f), at + new V2(3.5f, -4.5f), at + new V2(3.5f, 4.5f), at + new V2(-3.5f, 4.5f));
                if (i == smoke && p.GlCharge > 0f)
                    Arc(at, 7f, 1.6f, GlSmLabel.WithAlpha(0.85f), 16, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(p.GlCharge));
            }
            if (p.GlSwap > 0f)
                Arc(c, 13.5f, 1.4f, col.WithAlpha(0.7f), 24, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(GlSwapAt(p)));
        }

        /// The HUD pass (Juice.cs, with the damage numbers): the drum's name on a swap, dry clicks, and SLOWED over a boss in smoke.
        void DrawGlHud()
        {
            foreach (var g in glTexts)
            {
                float f = g.T / g.Life, k = Mathf.Min(1f, f < 0.2f ? f * 5f : (1f - f) * 1.8f);
                V2 at = ToUi(g.Pos);
                Ui.Shadowed(new Rect(at.X - 70f, at.Y - 11f, 140f, 22f), g.Text, g.Big ? 18 : 12, new Color(g.Col.R, g.Col.G, g.Col.B, Mathf.Clamp01(k)));
            }
            bool any = false;
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.SmokeShell) { any = true; break; }
            if (!any)
                return;
            foreach (var e in game.Enemies.Values)
            {
                if (!(e.Def.IsBoss || e.Def.Mini) || !game.InGlSmoke(e, true))
                    continue;
                V2 at = ToUi(e.Disp + new V2(0f, -e.Radius - 22f));
                Ui.Fill(new Rect(at.X - 28f, at.Y - 8f, 56f, 15f), new Color(0.06f, 0.08f, 0.07f, 0.8f));
                Ui.Shadowed(new Rect(at.X - 40f, at.Y - 8f, 80f, 15f), "SLOWED", 11, new Color(0.37f, 0.83f, 0.74f, 1f));
            }
        }
    }
}
