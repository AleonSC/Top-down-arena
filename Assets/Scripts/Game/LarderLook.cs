using UnityEngine;

namespace Arena
{
    /// The Devourer's faction, The Larder (Core/LarderFaction.cs), drawn as in `Docs/Mockups/devourer-faction.html`:
    ///   Brood (Gnawling, Gulper): a lump of pink flesh that is mostly mouth, no eyes, a row of bone teeth on each lip, two
    ///     pale pustules. The Gnawling's mouth chomps as it runs; the Gulper's cheeks swell over the wind-up, then its mouth
    ///     gapes while it breathes in.
    ///   Butcher: a hooded robe, a stained apron on its front, a bone mask, a cleaver that rises over the wind-up and
    ///     sweeps across the front in the chop.
    ///   Carcass Bearer (a Riot Shield): the Butcher's robe behind a side of meat (fat marbling, a bone at each end) that
    ///     cracks as it wears and flashes before the bash; broken, it's gone (a morsel lies in front: DrawMorsel).
    ///   Bile Spitter (a Gas Grenadier): a brood body with a green bile sac on its back that swells over the wind-up;
    ///     its lob (DrawBileLobFx, from GasLobLook.cs) is a green ring that fills and a glob on an arc.
    ///   Gutworm: a brood head on three shrinking segments trailing behind it; underground it's only a ripple of earth
    ///     (DrawLarderFloor), then a red mark that fills before it erupts.
    ///   Hook Thrower: the Butcher's robe with a meat hook swinging round it on a short chain; its line and thrown hook are
    ///     the Hookman's (DrawHookUnder, NonShooterLooks.cs).
    ///   Maw Priest: a dark red robe and hood; the Devourer's jaw in front of it, a 90° maw with bone teeth on both edges
    ///     (DrawLarderFloor), glowing as it charges.
    ///   A fed Gnawling (it ate a morsel) is drawn a third bigger and darker.
    /// DrawLarderFloor draws the tells under everyone: the Butcher's half-circle filling, the Gulper's cone and its breath.
    public partial class ArenaApp
    {
        static readonly Rgba LarderInk = new Rgba(0.12f, 0.04f, 0.06f, 0.95f), LarderMaw = new Rgba(0.16f, 0.03f, 0.06f),
            LarderBone = new Rgba(0.95f, 0.9f, 0.81f), LarderFlesh = new Rgba(0.91f, 0.53f, 0.6f), LarderBlood = new Rgba(0.67f, 0.12f, 0.18f),
            LarderApron = new Rgba(0.85f, 0.8f, 0.71f), LarderHood = new Rgba(0.23f, 0.14f, 0.13f), LarderSteel = new Rgba(0.81f, 0.84f, 0.85f);

        void DrawLarderBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            switch (e.Type)
            {
                case EnemyType.Gnawling:
                    if (e.LarderStage == Game.GnawFed)
                        DrawBrood(pos, r * 1.35f, e.Facing, 0.5f + 0.5f * Mathf.Sin(t * 12f + e.Id), col.Mix(LarderBlood, 0.3f), e.Id, 1.15f);
                    else
                        DrawBrood(pos, r, e.Facing, 0.5f + 0.5f * Mathf.Sin(t * 16f + e.Id), col, e.Id, 1f);
                    break;
                case EnemyType.Gulper:
                {
                    float wind = Game.LarderWind(e);
                    bool breathing = e.LarderStage == Game.LarderActing;
                    float swell = breathing ? 0.1f : wind * 0.15f;
                    DrawBrood(pos, r * (1f + swell), e.Facing, breathing ? 1f : 0.12f * (1f - wind), col, e.Id, 1f + wind * 0.8f);
                    break;
                }
                case EnemyType.Butcher: DrawButcher(e, pos, r, col, t); break;
                case EnemyType.CarcassBearer: DrawCarcassBearer(e, pos, r, col, t); break;
                case EnemyType.BileSpitter: DrawBileSpitter(e, pos, r, col, t); break;
                case EnemyType.Gutworm: DrawGutworm(e, pos, r, col, t); break;
                case EnemyType.HookThrower: DrawHookThrower(e, pos, r, col, t); break;
                case EnemyType.MawPriest: DrawMawPriest(e, pos, r, col, t); break;
            }
        }

        /// A lump's outline: `n` points, each pushed out a little by a fixed jitter from `seed`.
        V2 LumpPt(V2 c, float r, int n, int i, float rot, int seed) =>
            c + V2.FromAngle(rot + MathX.Tau * i / n) * (r * (0.88f + 0.18f * OrbHash(seed * 17 + i * 5)));

        /// A brood body facing `face`: a fleshy lump, a mouth opened `open` (0-1) with teeth on both lips. `cheeks` puffs
        /// the sides (1 is none).
        void DrawBrood(V2 pos, float r, float face, float open, Rgba col, int seed, float cheeks)
        {
            const int n = 12;
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            SetColor(LarderInk);
            for (int i = 0; i < n; i++)
                Tri(pos, LumpPt(pos, r + 2f, n, i, face, seed), LumpPt(pos, r + 2f, n, i + 1, face, seed));
            SetColor(col.Mix(new Rgba(0.35f, 0.12f, 0.17f), 0.35f));
            for (int i = 0; i < n; i++)
                Tri(pos, LumpPt(pos, r, n, i, face, seed), LumpPt(pos, r, n, i + 1, face, seed));
            if (cheeks > 1.01f)
                for (int k = -1; k <= 1; k += 2)
                    Circle(pos + s * k * r * 0.45f + f * r * 0.15f, r * 0.42f * (cheeks - 0.6f), col.Mix(Rgba.White, 0.12f), 14);
            Circle(pos - f * r * 0.15f + s * r * 0.1f, r * 0.62f, col, 16);  // the lit middle
            Circle(pos + new V2(-r * 0.32f, -r * 0.36f), r * 0.25f, col.Mix(Rgba.White, 0.3f).WithAlpha(0.55f), 10);
            Circle(pos - f * r * 0.4f - s * r * 0.45f, r * 0.1f, new Rgba(1f, 0.88f, 0.84f, 0.55f), 8);  // pustules
            Circle(pos - f * r * 0.55f + s * r * 0.3f, r * 0.08f, new Rgba(1f, 0.88f, 0.84f, 0.5f), 8);
            // the mouth: a dark wedge from just behind the middle out past the front, teeth along both lips
            float half = 0.25f + open * 0.6f;
            V2 mc = pos - f * r * 0.05f;
            Wedge(mc, r * 1.02f, face, half, LarderMaw, 10);
            for (int k = -1; k <= 1; k += 2)
                for (int j = 1; j <= 3; j++)
                {
                    float ea = face + k * half;
                    V2 at = mc + V2.FromAngle(ea) * (r * (0.32f + j * 0.2f));
                    Tooth(at, ea - k * MathX.Pi / 2f, r * 0.2f);
                }
        }

        /// The Butcher (a Guard): a hooded robe, its apron, a bone mask, the cleaver up over the wind-up and across in the chop.
        /// A Larder body facing `face`: a hooded robe, a stained apron on its front, a bone mask.
        void LarderRobe(V2 pos, float r, float face, Rgba col)
        {
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            Ellipse(pos, r * 1.05f + 1.8f, r * 1.15f + 1.8f, face, LarderInk, 16);
            Ellipse(pos, r * 1.05f, r * 1.15f, face, col, 16);
            Quad(pos + f * r * 0.15f - s * r * 0.55f, pos + f * r * 1.0f - s * r * 0.5f, pos + f * r * 1.0f + s * r * 0.5f, pos + f * r * 0.15f + s * r * 0.55f, LarderApron);
            Ellipse(pos + f * r * 0.7f + s * r * 0.18f, r * 0.3f, r * 0.2f, face + 0.5f, LarderBlood.WithAlpha(0.8f), 10);
            Circle(pos, r * 0.62f + 1.5f, LarderInk, 14);
            Circle(pos, r * 0.62f, LarderHood, 14);
            Ellipse(pos + f * r * 0.34f, r * 0.26f, r * 0.38f, face, LarderBone, 12);
            for (int k = -1; k <= 1; k += 2)
                Circle(pos + f * r * 0.42f + s * k * r * 0.14f, r * 0.08f, LarderMaw, 6);
        }

        void DrawButcher(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            float wind = Game.LarderWind(e), chop = e.LarderStage == Game.LarderActing ? 1f - Mathf.Clamp01(e.LarderT / Game.ChopFlash) : -1f;
            LarderRobe(pos, r, e.Facing, col);
            // the cleaver in its right hand: held forward, raised back over the wind-up, swept across in the chop
            float ca = chop >= 0f ? e.Facing + 1.4f - chop * 2.8f : e.Facing + 1.1f + wind * 0.9f;
            V2 hand = pos + V2.FromAngle(ca) * r * 0.95f, cf = V2.FromAngle(ca), cs = cf.Orthogonal();
            Line(pos + s * r * 0.5f, hand, 3.5f, LarderHood);
            Line(hand, hand + cf * r * 0.45f, 2.6f, new Rgba(0.29f, 0.17f, 0.11f));
            V2 b0 = hand + cf * r * 0.45f, b1 = hand + cf * r * 1.25f;
            Quad(b0 - cs * r * 0.2f, b1 - cs * r * 0.2f, b1 + cs * r * 0.45f, b0 + cs * r * 0.45f, LarderInk);
            Quad(b0 - cs * r * 0.12f, b1 - cs * r * 0.12f, b1 + cs * r * 0.37f, b0 + cs * r * 0.37f, LarderSteel);
            Quad(b1 - cs * r * 0.12f - cf * r * 0.3f, b1 - cs * r * 0.12f, b1 + cs * r * 0.1f, b1 + cs * r * 0.1f - cf * r * 0.3f, LarderBlood.WithAlpha(0.85f));
            if (wind > 0.6f && Mathf.Sin(t * 50f) > 0f)
                Circle(b1, 3f, Rgba.White.WithAlpha(0.8f), 8);  // a glint just before it falls
        }

        /// A side of meat standing across `f` at `c`: ink edge, red flesh with fat marbling, a bone knuckle at each end,
        /// `cracks` dark tears (0-3), a white flash.
        void CarcassSlab(V2 c, V2 f, float halfW, float halfL, int cracks, float flash, float a)
        {
            V2 s = f.Orthogonal();
            Quad(c - f * (halfW + 1.8f) - s * (halfL + 1.8f), c + f * (halfW + 1.8f) - s * (halfL + 1.8f), c + f * (halfW + 1.8f) + s * (halfL + 1.8f), c - f * (halfW + 1.8f) + s * (halfL + 1.8f), LarderInk.WithAlpha(0.95f * a));
            Quad(c - f * halfW - s * halfL, c + f * halfW - s * halfL, c + f * halfW + s * halfL, c - f * halfW + s * halfL, new Rgba(0.74f, 0.26f, 0.33f, a));
            Quad(c + f * halfW * 0.2f - s * halfL, c + f * halfW - s * halfL, c + f * halfW + s * halfL, c + f * halfW * 0.2f + s * halfL, new Rgba(0.85f, 0.38f, 0.43f, a));  // the lit face
            var fat = new Rgba(1f, 0.92f, 0.86f, 0.6f * a);
            Line(c - s * halfL * 0.7f - f * halfW * 0.3f, c - s * halfL * 0.2f + f * halfW * 0.4f, 1.6f, fat);
            Line(c + s * halfL * 0.15f - f * halfW * 0.4f, c + s * halfL * 0.6f + f * halfW * 0.3f, 1.6f, fat);
            for (int k = -1; k <= 1; k += 2)
            {
                V2 end = c + s * k * (halfL + 2f);
                Circle(end, halfW * 0.7f + 1.2f, LarderInk.WithAlpha(a), 10);
                Circle(end, halfW * 0.7f, LarderBone.WithAlpha(a), 10);
            }
            var tear = LarderMaw.WithAlpha(0.9f * a);
            if (cracks >= 1) Line(c - s * halfL * 0.55f - f * halfW, c - s * halfL * 0.35f + f * halfW * 0.5f, 1.8f, tear);
            if (cracks >= 2) Line(c + s * halfL * 0.5f + f * halfW, c + s * halfL * 0.3f - f * halfW * 0.4f, 1.8f, tear);
            if (cracks >= 3) Line(c - f * halfW * 0.8f + s * halfL * 0.05f, c + f * halfW * 0.7f - s * halfL * 0.1f, 1.8f, tear);
            if (flash > 0f)
                Quad(c - f * halfW - s * halfL, c + f * halfW - s * halfL, c + f * halfW + s * halfL, c - f * halfW + s * halfL, new Rgba(1f, 1f, 1f, 0.8f * flash * a));
        }

        /// The Carcass Bearer (a Riot Shield): arms out to a side of meat held in front; it flashes before the bash and drops
        /// low after it. Broken, the meat is gone (it lies as a morsel) and the bearer wobbles while it staggers.
        void DrawCarcassBearer(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            int crack = Game.RiotCrack(e);
            byte st = Game.RiotState(e);
            bool broken = crack >= Game.RiotCracked;
            float face = e.Facing + (broken && st == Game.RiotStaggered ? Mathf.Sin(t * 30f) * 0.18f : 0f);
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            if (!broken)
                for (int k = -1; k <= 1; k += 2)
                    Line(pos + f * r * 0.35f + s * k * r * 0.6f, pos + f * r * 0.95f + s * k * r * 0.75f, 3f, LarderHood);
            LarderRobe(pos, r * 0.9f, face, col);
            if (broken)
                return;
            float low = st == Game.RiotLow ? 0.7f : 1f;
            float flash = st == Game.RiotWinding ? (Mathf.Sin(t * 50f) > 0f ? 1f : 0.3f) : 0f;
            CarcassSlab(pos + f * (r * 1.15f), f, r * 0.3f, r * 1.25f * low, crack, flash, low < 1f ? 0.75f : 1f);
        }

        /// The Bile Spitter (a Gas Grenadier): a brood body with a green sac on its back that swells as it winds up and
        /// empties as it throws.
        void DrawBileSpitter(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float swell = 0f;
            if (GasLobOf(e, out float age, out float throwAt, out float flight))
                swell = age < throwAt ? Mathf.Clamp01(age / Mathf.Max(0.01f, throwAt)) : Mathf.Clamp01(1f - (age - throwAt) / 0.25f);
            V2 f = V2.FromAngle(e.Facing);
            V2 sc = pos - f * r * 0.8f;
            float sr = r * (0.75f + 0.25f * swell);
            Circle(sc, sr + 1.6f, new Rgba(0.11f, 0.16f, 0.03f), 16);
            Circle(sc, sr, Game.BileColor.Mix(new Rgba(0.36f, 0.5f, 0.11f), 0.35f), 16);
            Circle(sc + new V2(-sr * 0.3f, -sr * 0.3f), sr * 0.35f, new Rgba(0.91f, 1f, 0.6f, 0.6f), 10);
            DrawBrood(pos, r, e.Facing, 0.15f + 0.6f * swell, col, e.Id, 1f + swell * 0.4f);
        }

        /// A Bile Spitter's lob (GasLobLook.cs): a green ring on where you were heading that fills over the wind-up, then the
        /// glob on an arc with its shadow under it.
        void DrawBileLobFx(Enemy e, Effect fx, float age, float throwAt, float flight, V2 land, float R)
        {
            var green = Game.BileColor;
            float tellP = throwAt > 0f ? Mathf.Clamp01(age / Mathf.Max(0.01f, throwAt)) : 1f;
            if (age < throwAt + flight)
            {
                Circle(land, R, green.WithAlpha(0.07f), 32);
                Circle(land, R * tellP, green.WithAlpha(0.14f), 32);
                Arc(land, R, 2f, green.WithAlpha(0.85f), 32);
            }
            if (age >= throwAt && age < throwAt + flight)
            {
                float u = (age - throwAt) / flight;
                V2 from = e.Disp + V2.FromAngle(e.Facing) * e.Radius;
                V2 ground = V2.Lerp(from, land, u);
                float h = 80f * Mathf.Sin(Mathf.PI * u);
                Ellipse(ground + new V2(0f, 3f), 6f + 3f * u, 4f + 2f * u, 0f, new Rgba(0f, 0f, 0f, 0.35f), 12);
                Circle(ground - new V2(0f, h), 7.5f, new Rgba(0.24f, 0.33f, 0.07f), 14);
                Circle(ground - new V2(0f, h), 6f, new Rgba(0.78f, 0.94f, 0.38f), 14);
                Circle(ground - new V2(1.5f, h + 1.5f), 2.2f, new Rgba(0.95f, 1f, 0.75f), 8);
            }
        }

        /// The Gutworm on the surface: three segments trailing behind, wriggling, then the head.
        void DrawGutworm(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            for (int k = 3; k >= 1; k--)
            {
                V2 at = pos - f * (r * 0.85f * k) + s * (Mathf.Sin(t * 9f - k * 1.3f + e.Id) * r * 0.25f);
                float sr = r * (0.85f - 0.15f * k);
                Circle(at, sr + 1.8f, LarderInk, 12);
                Circle(at, sr, col.Mix(new Rgba(0.35f, 0.12f, 0.17f), 0.25f + 0.1f * k), 12);
                Line(at - s * sr * 0.8f, at + s * sr * 0.8f, 1.2f, LarderInk.WithAlpha(0.5f));
            }
            DrawBrood(pos, r, e.Facing, 0.4f + 0.4f * Mathf.Sin(t * 10f + e.Id), col, e.Id + 3, 1f);
        }

        /// The Hook Thrower: the robe, and while it waits its hook swinging round it on a short chain.
        void DrawHookThrower(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var body = e.Phase == 4 ? col.Mix(Rgba.White, 0.3f + 0.25f * Mathf.Sin(t * 16f)) : col;  // open after a miss
            LarderRobe(pos, r, e.Facing, body);
            if (e.Phase != 0)
                return;  // (its line, and the hook out on its chain: DrawHookUnder)
            float a = t * (5f + (e.PhaseT < 1f ? 6f : 0f)) + e.Id;
            V2 hand = pos + V2.FromAngle(e.Facing + 1f) * r * 0.9f, hook = hand + V2.FromAngle(a) * r * 1.3f;
            Line(hand, hook, 1.5f, new Rgba(0.6f, 0.58f, 0.56f));
            V2 hf = V2.FromAngle(a), hs = hf.Orthogonal();
            Arc(hook + hs * 3f, 3.5f, 2.2f, LarderSteel, 10, a + MathX.Pi * 0.5f, MathX.Pi * 1.2f);
            Line(hook - hf * 3f, hook + hf * 2f, 2.2f, LarderSteel);
        }

        /// The Maw Priest: a dark red robe and hood with a bone mask; its maw is drawn on the floor in front of it.
        void DrawMawPriest(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            LarderRobe(pos, r, e.Facing, col);
            if (e.Winding)
                Arc(pos, r + 4f, 2f, new Rgba(1f, 0.35f, 0.42f, 0.5f + 0.4f * Mathf.Sin(t * 30f)), 20);
        }

        /// Under everyone: the Butcher's half-circle filling over its wind-up and its chop's white sweep; the Gulper's cone
        /// showing over its swell and filled while it breathes in, with streaks rushing to its mouth.
        void DrawLarderFloor(float t)
        {
            var red = new Rgba(1f, 0.35f, 0.42f);
            var pink = new Rgba(0.91f, 0.53f, 0.6f);
            foreach (var e in game.Enemies.Values)
            {
                if (e.Type == EnemyType.Butcher && e.LarderStage != Game.LarderIdle)
                {
                    V2 pos = e.Disp;
                    float half = MathX.Pi / 2f, R = Game.ChopReach;
                    if (e.LarderStage == Game.LarderWinding)
                    {
                        float w = Game.LarderWind(e);
                        Wedge(pos, R, e.Facing, half, red.WithAlpha(0.1f), 20);
                        Wedge(pos, R * w, e.Facing, half, red.WithAlpha(0.18f), 20);
                        Arc(pos, R, 2f, red.WithAlpha(0.75f), 20, e.Facing - half, half * 2f);
                    }
                    else
                    {
                        float u = 1f - Mathf.Clamp01(e.LarderT / Game.ChopFlash);
                        Wedge(pos, R, e.Facing, half, Rgba.White.WithAlpha(0.16f * (1f - u)), 20);
                        Arc(pos, R * 0.85f, 7f, Rgba.White.WithAlpha(0.9f * (1f - u * 0.5f)), 12, e.Facing + half - u * half * 2f - 0.5f, 0.5f);
                    }
                }
                else if (e.Type == EnemyType.Gutworm && e.LarderStage != Game.LarderIdle)
                {
                    V2 pos = e.Disp;
                    for (int j = 0; j < 3; j++)  // a ripple of earth, like the Devourer's, smaller
                    {
                        float f = (t * 40f + j * 14f) % 42f;
                        Arc(pos, 16f + f, 2.5f, new Rgba(0.54f, 0.36f, 0.3f, 0.6f - f / 80f), 20);
                    }
                    for (int j = 0; j < 4; j++)
                        Circle(pos + new V2(Mathf.Cos(j * 1.6f + t * 5f) * 14f, Mathf.Sin(j * 1.9f + t * 6f) * 10f), 3f, new Rgba(0.5f, 0.32f, 0.27f), 8);
                    if (e.LarderStage == Game.LarderActing)
                    {
                        float k = 1f - Mathf.Clamp01(e.LarderT / Game.GutMark);
                        Circle(pos, Game.GutMarkR, red.WithAlpha(0.08f + 0.12f * k), 28);
                        Arc(pos, Game.GutMarkR, 2f, red.WithAlpha(0.85f), 28);
                        Circle(pos, Game.GutMarkR * k, red.WithAlpha(0.18f), 28);
                    }
                }
                else if (e.Type == EnemyType.MawPriest && !game.IsSpawning(e))
                {
                    V2 pos = e.Disp;
                    float half = Game.PriestMawHalf, R = e.Radius * 4.5f;
                    Wedge(pos, R, e.Facing, half, LarderMaw.WithAlpha(0.45f), 16);
                    Wedge(pos, R, e.Facing, half, red.WithAlpha(e.Winding ? 0.22f : 0.1f), 16);
                    Arc(pos, R, 1.5f, red.WithAlpha(0.7f), 16, e.Facing - half, half * 2f);
                    for (int k = -1; k <= 1; k += 2)
                        for (int j = 1; j <= 5; j++)
                        {
                            float ea = e.Facing + k * half;
                            Tooth(pos + V2.FromAngle(ea) * (e.Radius + j * (R - e.Radius) / 6f), ea - k * MathX.Pi / 2f, 6f);
                        }
                }
                else if (e.Type == EnemyType.Gulper && e.LarderStage != Game.LarderIdle)
                {
                    V2 pos = e.Disp;
                    float half = Game.GulpHalf, R = Game.GulpReach;
                    if (e.LarderStage == Game.LarderWinding)
                    {
                        float w = Game.LarderWind(e);
                        Wedge(pos, R, e.Facing, half, pink.WithAlpha(0.06f + 0.08f * w), 20);
                        Arc(pos, R, 1.5f, pink.WithAlpha(0.3f + 0.4f * w), 20, e.Facing - half, half * 2f);
                    }
                    else
                    {
                        Wedge(pos, R, e.Facing, half, pink.WithAlpha(0.2f), 20);
                        Arc(pos, R, 2f, pink.WithAlpha(0.8f), 20, e.Facing - half, half * 2f);
                        for (int i = 0; i < 9; i++)  // the breath: streaks rushing in to the mouth
                        {
                            float k = (t * 1.8f + i / 9f) % 1f, a = e.Facing + (i / 8f - 0.5f) * half * 1.8f, d = R * (1f - k);
                            if (d < e.Radius)
                                continue;
                            Line(pos + V2.FromAngle(a) * d, pos + V2.FromAngle(a) * (d + 18f), 1.5f, new Rgba(1f, 0.86f, 0.88f, 0.2f + 0.6f * k));
                        }
                    }
                }
            }
        }
    }
}
