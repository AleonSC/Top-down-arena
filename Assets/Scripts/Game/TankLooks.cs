using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Tank Soldier's Roar and Bulldoze with Visual effects on (Docs/Mockups/tank-abilities.html, the "Proposed" side),
    /// drawing only; off, the old ring and arc draw as before. Nothing is synced and no gameplay number changes.
    ///
    /// Every cast already sends a FxKind.Callout (Pos = where the caster stood, Pos2 = (player id, AbilityId), lasting
    /// CalloutTime), so its age is the clock for the cast's look and nothing has to be remembered between frames. The old
    /// plain Ring each cast also sends is recognised and skipped (`IsTankRing`).
    ///
    ///   Roar     a short pull-in (0.08 s), then three staggered rings with a filled blast and speed streaks; each enemy flashes
    ///            and pops a "!" as the wave reaches it (they were all retargeted at once). While the taunt runs: six armour
    ///            plates orbit the caster (they flicker in the last second), a rim arc drains with the taunt, dashed tethers
    ///            run to the nearest enemies, a soft pulse leaves every second. All from TauntT, so it shows for allies too.
    ///   Bulldoze a lane showing the 60 px sweep for half a second, then (as the charge runs, 750 px/s for 0.35 s) afterimages,
    ///            speed streaks, dust off both heels, skid marks, a bow wave and the invulnerable flicker; enemies in the lane
    ///            flash and throw sparks as it reaches them (heavy ones throw more); a double ring and a ring of dust where
    ///            it ends. Hits are worked out here from the same lane the game uses, so a knocked enemy can still show one.
    public partial class ArenaApp
    {
        const float RoarRange = 320f, RoarWind = 0.08f;
        const float BullSpeed = 750f, BullTime = 0.35f, BullLane = 30f;  // Balance.cs (private there)
        static readonly Rgba RoarOrange = new Rgba(1f, 0.45f, 0.3f), RoarHot = new Rgba(1f, 0.28f, 0.2f), RoarPale = new Rgba(1f, 0.82f, 0.68f);
        static readonly Rgba BullCyan = new Rgba(0.5f, 0.75f, 1f), BullPale = new Rgba(0.82f, 0.95f, 1f), BullDust = new Rgba(0.6f, 0.55f, 0.47f);

        readonly Dictionary<int, float> roarFull = new Dictionary<int, float>();
        readonly Dictionary<int, float> tankShaken = new Dictionary<int, float>();
        readonly Enemy[] tetherNear = new Enemy[10];
        readonly float[] tetherDist = new float[10];

        void ClearTankLooks()
        {
            roarFull.Clear();
            tankShaken.Clear();
        }

        /// The plain ring a Roar (orange, 320 px, 0.5 s) or a Bulldoze (blue, 60 px, 0.35 s) sends, which these looks replace.
        bool IsTankRing(Effect fx)
        {
            if (!fxOn || fx.Kind != FxKind.Ring)
                return false;
            bool roar = Mathf.Abs(fx.Radius - 320f) < 1f && Mathf.Abs(fx.Dur - 0.5f) < 0.01f && SameRgb(fx.Color, RoarOrange);
            bool bull = Mathf.Abs(fx.Radius - 60f) < 1f && Mathf.Abs(fx.Dur - 0.35f) < 0.01f && SameRgb(fx.Color, BullCyan);
            return roar || bull;
        }

        static bool SameRgb(Rgba a, Rgba b) => Mathf.Abs(a.R - b.R) < 0.01f && Mathf.Abs(a.G - b.G) < 0.01f && Mathf.Abs(a.B - b.B) < 0.01f;

        /// Juice.cs, each frame: a fresh Roar or Bulldoze kicks the camera once (and a Bulldoze holds the picture for a blink
        /// on your own screen).
        void UpdateTankLooks(float t)
        {
            if (!fxOn || game == null || game.ShowcaseMode)  // (the lobby's preview doesn't shake the screen)
                return;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.Callout || fx.T > 0.25f)
                    continue;
                var id = (AbilityId)(int)fx.Pos2.Y;
                if (id != AbilityId.Roar && id != AbilityId.Bulldoze)
                    continue;
                int who = (int)fx.Pos2.X;
                float start = t - fx.T, last;
                if (tankShaken.TryGetValue(who, out last) && Mathf.Abs(last - start) < 0.2f)
                    continue;
                tankShaken[who] = start;
                bool mine = who == game.MyId;
                Shake(id == AbilityId.Roar ? (mine ? 0.35f : 0.15f) : (mine ? 0.22f : 0.1f));
                if (mine && id == AbilityId.Bulldoze)
                    Hold(0.05f);
            }
        }

        /// WorldEffects.cs: a Callout's floor look (the name itself is on the HUD, AbilityLooks.cs).
        void DrawTankCastFx(Effect fx)
        {
            if (!fxOn || fx.Dur <= 0f)
                return;
            var id = (AbilityId)(int)fx.Pos2.Y;
            if (id != AbilityId.Roar && id != AbilityId.Bulldoze)
                return;
            Player p;
            if (!game.Players.TryGetValue((int)fx.Pos2.X, out p) || LaunchHides(p))
                return;
            if (id == AbilityId.Roar)
                DrawRoarWave(fx, p);
            else
                DrawBulldoze(fx, p);
        }

        // --- Roar ------------------------------------------------------------------------------------------------------

        void DrawRoarWave(Effect fx, Player p)
        {
            float a = fx.T, R = RoarRange;
            V2 c = fx.Pos;
            if (a < RoarWind)  // the pull-in: a ring closing on the caster, sparks sucked towards them
            {
                float k = a / RoarWind;
                Arc(c, 110f * (1f - k) + 22f, 3f, RoarHot.WithAlpha(0.8f * k), 40);
                for (int i = 0; i < 10; i++)
                {
                    V2 d = V2.FromAngle(Hash01(i + 2f) * MathX.Tau);
                    float r0 = 112f * (1f - k) + 20f;
                    Line(c + d * r0, c + d * (r0 + 14f), 2f, RoarHot.WithAlpha(0.9f));
                }
            }
            float w = a - RoarWind;
            if (w >= 0f)
            {
                float kd = w / 0.45f;
                if (kd < 1f)  // the filled blast, brighter towards its rim
                {
                    float r1 = R * EaseOut3(kd);
                    for (int i = 0; i < 6; i++)
                    {
                        float u = (i + 1f) / 6f;
                        Arc(c, r1 * (i + 0.5f) / 6f, r1 / 6f + 1f, RoarOrange.WithAlpha(0.2f * u * u * (1f - kd)), 40);
                    }
                }
                float[] dur = { 0.5f, 0.6f, 0.7f }, wid = { 10f, 5f, 3f }, alp = { 0.95f, 0.6f, 0.4f };
                for (int i = 0; i < 3; i++)
                {
                    float k = (w - 0.06f * i) / dur[i];
                    if (k < 0f || k > 1f)
                        continue;
                    Arc(c, R * EaseOut3(k), wid[i] * (1f - 0.6f * k) + 1f, (i == 2 ? RoarPale : RoarOrange).WithAlpha(alp[i] * (1f - k)), 64);
                }
                if (w < 0.28f)  // speed streaks thrown off the front
                    for (int i = 0; i < 28; i++)
                    {
                        V2 d = V2.FromAngle(Hash01(i + 7f) * MathX.Tau);
                        float sp = 700f + 300f * Hash01(i + 31f), rr = 30f + sp * w;
                        Line(c + d * (rr - 0.09f * sp), c + d * rr, 2f, RoarPale.WithAlpha(1f - w / 0.28f));
                    }
            }
            if (a < 0.2f && !p.Downed)  // the slam: a bright flash on the body
                Circle(p.Disp, p.Def.BodyR * (1.5f + 0.5f * a / 0.2f), RoarPale.WithAlpha(0.55f * (1f - a / 0.2f)), 20);

            // Every enemy was retargeted at once; each one shows it as the wave reaches it.
            foreach (var e in game.Enemies.Values)
            {
                if (game.IsSpawning(e) || Game.DevourUnder(e))
                    continue;
                float d = e.Disp.DistanceTo(c);
                if (d >= R)
                    continue;
                float reach = RoarWind + 0.5f * (1f - Mathf.Pow(1f - d / R, 1f / 3f));
                float age = a - reach;
                if (age < 0f || age > 0.65f)
                    continue;
                if (age < 0.16f)
                    Circle(e.Disp, e.Radius + 2f, Rgba.White.WithAlpha(0.7f * (1f - age / 0.16f)), 14);
                float kr = Mathf.Clamp01(age / 0.3f);
                Arc(e.Disp, e.Radius + 24f * EaseOut3(kr), 2.5f, RoarHot.WithAlpha(0.9f * (1f - kr)), 20);
                float fade = 1f - Mathf.Clamp01((age - 0.3f) / 0.35f), rise = 22f * EaseOut3(Mathf.Clamp01(age / 0.4f));
                V2 at = e.Disp + new V2(0f, -e.Radius - 12f - rise);
                Line(at + new V2(0f, -9f), at + new V2(0f, 1f), 3.5f, RoarHot.WithAlpha(fade));
                Circle(at + new V2(0f, 7f), 2.4f, RoarHot.WithAlpha(fade), 8);
            }
        }

        /// While a Roar's taunt runs (from DrawPlayerLinks, replacing the old throbbing arc): the plates, the draining rim, the
        /// aura, a pulse each second and tethers to the nearest enemies.
        void DrawRoarSustain(Player p, float t)
        {
            float left = p.TauntT, full;
            if (!roarFull.TryGetValue(p.Id, out full) || left > full + 0.05f)
                roarFull[p.Id] = full = left;
            float R = p.Def.BodyR, fl = left < 1f ? (Mathf.Sin(t * 38f) > 0f ? 1f : 0.2f) : 1f, pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);
            V2 at = p.Disp;
            Circle(at, 66f, RoarOrange.WithAlpha(0.05f + 0.025f * pulse), 36);
            Circle(at, 44f, RoarOrange.WithAlpha(0.06f + 0.03f * pulse), 28);

            float since = full - left, k = (since % 1f) / 0.7f;  // a soft pulse a second apart, after the cast's own wave
            if (since > 1f && k < 1f)
                Arc(at, 34f + 66f * EaseOut3(k), 2f, RoarOrange.WithAlpha(0.35f * (1f - k)), 40);

            // Dashed tethers to the ten nearest enemies.
            int n = 0;
            foreach (var e in game.Enemies.Values)
            {
                if (game.IsSpawning(e) || Game.DevourUnder(e))
                    continue;
                float d = e.Disp.DistanceTo(at);
                int slot = n < tetherNear.Length ? n : tetherNear.Length - 1;
                if (n >= tetherNear.Length && d >= tetherDist[slot])
                    continue;
                while (slot > 0 && tetherDist[slot - 1] > d)
                {
                    tetherNear[slot] = tetherNear[slot - 1];
                    tetherDist[slot] = tetherDist[slot - 1];
                    slot--;
                }
                tetherNear[slot] = e;
                tetherDist[slot] = d;
                if (n < tetherNear.Length)
                    n++;
            }
            var tc = RoarHot.WithAlpha(0.38f * Mathf.Min(1f, left));
            for (int i = 0; i < n; i++)
            {
                V2 a = tetherNear[i].Disp, b = at, dir = (b - a).Normalized();
                float len = a.DistanceTo(b), off = (t * 70f) % 13f;
                for (float s = -off; s < len - R; s += 13f)
                {
                    float s0 = Mathf.Max(0f, s), s1 = Mathf.Min(len - R - 6f, s + 6f);
                    if (s1 > s0 && s0 > tetherNear[i].Radius)
                        Line(a + dir * s0, a + dir * s1, 1.5f, tc);
                }
            }

            float rr = R + 15f, rot = t * 1.6f;  // six plates turning round the body
            for (int i = 0; i < 6; i++)
            {
                float a0 = rot + i * MathX.Tau / 6f;
                Arc(at, rr, 5f, RoarOrange.WithAlpha(0.92f * fl), 8, a0, 0.72f);
                Arc(at, rr - 5f, 1.5f, RoarPale.WithAlpha(0.55f * fl), 8, a0 + 0.08f, 0.56f);
            }
            float share = Mathf.Clamp01(left / Mathf.Max(0.01f, full));  // the rim drains as the taunt runs out
            Arc(at, R + 27f, 2f, RoarPale.WithAlpha(0.65f * fl), 48, -MathX.Pi / 2f, MathX.Tau * share);
        }

        // --- Bulldoze --------------------------------------------------------------------------------------------------

        void DashLine(V2 a, V2 b, float dash, float gap, float offset, float width, Rgba c)
        {
            float len = a.DistanceTo(b);
            if (len < 1f)
                return;
            V2 d = (b - a) / len;
            for (float s = -offset; s < len; s += dash + gap)
            {
                float s0 = Mathf.Max(0f, s), s1 = Mathf.Min(len, s + dash);
                if (s1 > s0)
                    Line(a + d * s0, a + d * s1, width, c);
            }
        }

        void DrawBulldoze(Effect fx, Player p)
        {
            float a = fx.T;
            V2 from = fx.Pos, dir;
            V2 moved = p.Disp - from;
            dir = moved.Length > 12f ? moved.Normalized() : V2.FromAngle(p.Aim);
            V2 n = dir.Orthogonal();
            float L = BullSpeed * BullTime;
            V2 end = from + dir * L;
            end = new V2(Mathf.Clamp(end.X, p.Def.BodyR, Game.Width - p.Def.BodyR), Mathf.Clamp(end.Y, p.Def.BodyR, Game.Height - p.Def.BodyR));
            L = from.DistanceTo(end);
            float run = Mathf.Clamp(a * BullSpeed, 0f, L);  // how far along the lane the charge has got
            bool charging = a < BullTime;
            float R = p.Def.BodyR;
            Rgba body = p.Def.Color;

            if (a < 0.6f)  // the lane: the 60 px the charge sweeps, with chevrons running down it
            {
                float f = 1f - a / 0.6f;
                SetColor(BullCyan.WithAlpha(0.07f * f));
                Quad(from + n * BullLane, end + n * BullLane, end - n * BullLane, from - n * BullLane);
                DashLine(from + n * BullLane, end + n * BullLane, 8f, 6f, a * 120f, 1.5f, BullCyan.WithAlpha(0.55f * f));
                DashLine(from - n * BullLane, end - n * BullLane, 8f, 6f, a * 120f, 1.5f, BullCyan.WithAlpha(0.55f * f));
                for (int i = 0; i < 5; i++)
                {
                    float s = 40f + ((i * 52f + a * 260f) % Mathf.Max(41f, L - 40f));
                    if (s > L)
                        continue;
                    V2 q = from + dir * s;
                    Line(q - dir * 8f + n * 10f, q, 3f, BullPale.WithAlpha(0.7f * f));
                    Line(q - dir * 8f - n * 10f, q, 3f, BullPale.WithAlpha(0.7f * f));
                }
            }
            if (a < 1.1f && run > 4f)  // skid marks from both heels, fading with the callout
                for (int s = -1; s <= 1; s += 2)
                    Line(from + n * (11f * s), from + dir * run + n * (11f * s), 2f, new Rgba(0f, 0f, 0f, 0.4f * (1f - a / 1.1f)));

            if (charging)
            {
                for (int i = 1; i <= 5; i++)  // afterimages
                {
                    float ia = a - 0.03f * i;
                    if (ia <= 0f)
                        break;
                    Circle(from + dir * Mathf.Min(L, ia * BullSpeed), R, body.WithAlpha(0.38f * (1f - i / 6f)), 18);
                }
                for (int i = 0; i < 10; i++)  // speed streaks and the dust off both heels, laid down along the path
                {
                    float u = Hash01(i * 3.1f + 1f), ta = Mathf.Max(0f, a - u * 0.2f);
                    V2 q = from + dir * Mathf.Min(L, ta * BullSpeed) + n * ((Hash01(i + 9f) - 0.5f) * 40f) - dir * 22f;
                    Line(q, q - dir * (22f + 8f * u), 2f, BullPale.WithAlpha(1f - u));
                }
                for (int i = 0; i < 14; i++)
                {
                    float u = (i + 0.5f) / 14f, te = a - u * 0.5f;
                    if (te < 0f || te > BullTime)
                        continue;
                    float side = (i % 2 == 0 ? 1f : -1f), age = u;
                    V2 q = from + dir * Mathf.Min(L, te * BullSpeed) + n * (side * (14f + 36f * age * (0.5f + Hash01(i + 4f)))) - dir * (10f + 30f * age);
                    Circle(q, 5f + 10f * age, BullDust.WithAlpha(0.32f * (1f - age)), 8);
                }
                V2 c = p.Disp;  // the bow wave rides the body, and the invulnerable window flickers
                Circle(c + dir * 10f, 46f, BullPale.WithAlpha(0.1f), 20);
                float ang = dir.Angle;
                Arc(c, 28f, 4f, BullPale.WithAlpha(0.95f), 14, ang - 0.95f, 1.9f);
                Arc(c, 38f, 2.5f, BullCyan.WithAlpha(0.55f), 14, ang - 0.7f, 1.4f);
                if (Mathf.Sin(game.AnimT * 60f) > -0.2f)
                    Arc(c, 24f, 2f, BullCyan.WithAlpha(0.5f), 20);
            }

            foreach (var e in game.Enemies.Values)  // contact: a flash, a ring and sparks as the charge reaches each enemy in the lane
            {
                if (game.IsSpawning(e))
                    continue;
                V2 rel = e.Disp - from;
                float along = Mathf.Clamp(rel.X * dir.X + rel.Y * dir.Y, 0f, L);
                V2 near = from + dir * along;
                float d = e.Disp.DistanceTo(near);
                bool heavy = e.Def.IsBoss || e.Def.Heavy, exact = d <= e.Radius + BullLane;
                if (!exact && (heavy || d > e.Radius + BullLane + 70f))  // (a light one is shoved up to 70 px sideways when hit)
                    continue;
                float age = a - Mathf.Max(0f, (along - e.Radius - 16f) / BullSpeed);
                if (age < 0f || age > 0.28f)
                    continue;
                V2 hit = near + (e.Disp - near).LimitLength(e.Radius);
                float kr = Mathf.Clamp01(age / 0.2f);
                if (exact && age < 0.14f)
                    Circle(e.Disp, e.Radius + 2f, Rgba.White.WithAlpha(0.6f * (1f - age / 0.14f)), 14);
                Arc(hit, e.Radius * 0.6f + 26f * EaseOut3(kr), 3f, Rgba.White.WithAlpha(0.8f * (1f - kr)), 20);
                int sparks = heavy ? 14 : 8;
                for (int i = 0; i < sparks; i++)
                {
                    float sa = dir.Angle + (Hash01(i + e.Id * 1.7f) - 0.5f) * 2.8f, sp = 180f + 200f * Hash01(i + 40f + e.Id);
                    V2 sd = V2.FromAngle(sa), q = hit + sd * (sp * age * (heavy ? 0.8f : 1f));
                    Line(q - sd * 9f, q, 2f, BullPale.WithAlpha(1f - age / 0.28f));
                }
            }

            float land = a - BullTime;  // the stomp where it ends
            if (land >= 0f)
            {
                float k1 = land / 0.42f, k2 = (land - 0.05f) / 0.3f;
                if (k1 < 1f)
                    Arc(end, 20f + 72f * EaseOut3(k1), 5f * (1f - k1) + 1f, BullCyan.WithAlpha(0.9f * (1f - k1)), 48);
                if (k2 >= 0f && k2 < 1f)
                    Arc(end, 16f + 46f * EaseOut3(k2), 3f * (1f - k2) + 1f, Rgba.White.WithAlpha(0.6f * (1f - k2)), 40);
                float kd = land / 0.6f;
                if (kd < 1f)
                    for (int i = 0; i < 14; i++)
                    {
                        V2 d = V2.FromAngle(i / 14f * MathX.Tau + Hash01(i + 5f) * 0.4f);
                        float rr = 14f + (70f + 80f * Hash01(i + 17f)) * EaseOut3(kd);
                        Circle(end + d * rr, 8f * (1f + kd), BullDust.WithAlpha(0.4f * (1f - kd)), 8);
                    }
            }
        }
    }
}
