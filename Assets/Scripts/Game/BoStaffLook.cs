using UnityEngine;

namespace Arena
{
    /// The Bo Staff's extra looks (Core/BoStaff.cs; Docs/Mockups/bo-staff-rework.html). Its slash, jab and spin are ordinary
    /// melee swings, drawn by MeleeLooks.cs with the Bo Staff's sage energy trail. This file adds the shove (FxKind.StaffShove: the
    /// staff turns sideways, is drawn back, driven out as a bar, then drawn in, with pressure arcs rolling off its front) and the
    /// three pips over the staff-bearer's head that show where the chain is.
    public partial class ArenaApp
    {
        const float ShovePull = 0.1f, ShovePush = 0.1f, ShoveHoldT = 0.09f, ShoveBack = 0.1f;  // the pull-back, the drive out, the hold at the front, the draw in (the host lands the shove after ShovePull: Game.BoShoveWind)

        /// How far out the bar's middle is `T` seconds into the shove (`front` is where it ends).
        static float ShoveDist(float T, float front)
        {
            if (T < ShovePull)
                return Mathf.Lerp(18f, 6f, EaseOut3(T / ShovePull));
            if (T < ShovePull + ShovePush)
                return Mathf.Lerp(6f, front, EaseOut3((T - ShovePull) / ShovePush));
            float held = ShovePull + ShovePush + ShoveHoldT;
            if (T < held)
                return front;
            return Mathf.Lerp(front, 18f, Mathf.Clamp01((T - held) / ShoveBack));
        }

        /// One layer of the shove: the solid staff and sparks (light false) or the energy (light true, the additive glow pass).
        /// False: not a player's shove (nothing to draw it with).
        bool DrawStaffShoveLayer(Effect fx, bool light)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, half = fx.Radius;
            if (reach < 1f)
                return false;
            var seen = MeleeOwnerOf(fx, 0.5f, false, ShovePull + ShovePush + ShoveHoldT + ShoveBack + 0.1f);
            if (seen == null)
                return false;
            V2 o = MeleeOrigin(fx, seen), dir = to / reach, side = dir.Orthogonal();
            var col = MeleeColor(seen.Weapon, fx.Color);
            float T = fx.T, front = Mathf.Max(30f, reach - 6f);
            float d = ShoveDist(T, front);
            float gone = ShovePull + ShovePush + ShoveHoldT + ShoveBack;
            float fade = T < gone ? 1f : 1f - Mathf.Clamp01((T - gone) / 0.12f);
            V2 c = o + dir * d;
            V2 a = c - side * half, b = c + side * half;
            float landed = T - ShovePull;  // time since it hit
            if (light)
            {
                // The bar's glow, brightest as it drives out, and three pressure arcs rolling off its front.
                float drive = T < ShovePull ? 0.35f : 1f;
                SoftLine(a, b, 22f * fade * drive + 4f, col.WithAlpha(0.5f * fade * drive));
                SoftLine(a, b, 9f * fade + 2f, col.WithAlpha(0.6f * fade));
                Line(a, b, 2.5f, Rgba.White.WithAlpha(0.9f * fade));
                if (landed > 0f)
                {
                    float wv = EaseOut3(Mathf.Clamp01(landed / 0.35f));
                    V2 centre = o + dir * (front - 70f);
                    float aim = to.Angle;
                    for (int i = 0; i < 3; i++)
                    {
                        float k = Mathf.Clamp01(wv * 1.15f - i * 0.12f), r = 70f + 18f * (i + 1) * k + 6f * i;
                        float al = (0.75f - 0.2f * i) * (1f - Mathf.Clamp01(landed / 0.45f));
                        if (al > 0.01f)
                            Arc(centre, r, 3.5f - i * 0.6f, col.WithAlpha(al), 20, aim - 0.62f, 1.24f);
                    }
                }
                return true;
            }
            // The solid staff: a pole across the bar with its two ferrules and grip bands, then sparks where it lands.
            if (fade > 0f)
            {
                Line(a, b, 4.5f, MeleeHandle.WithAlpha(fade));
                Line(a, a + (b - a).Normalized() * 9f, 6f, MeleeGuard.WithAlpha(fade));
                Line(b, b - (b - a).Normalized() * 9f, 6f, MeleeGuard.WithAlpha(fade));
                Line(c - dir * 3f, c + dir * 3f, 5f, MeleeGuard.WithAlpha(fade * 0.9f));
                Line(c + side * 22f - dir * 2.5f, c + side * 22f + dir * 2.5f, 3f, MeleeGuard.WithAlpha(fade * 0.8f));
                Line(c - side * 22f - dir * 2.5f, c - side * 22f + dir * 2.5f, 3f, MeleeGuard.WithAlpha(fade * 0.8f));
            }
            if (landed > 0f && landed < 0.3f)
            {
                uint r = (uint)MeleeSeed(fx);
                for (int i = 0; i < 12; i++)
                {
                    float lat = (Rand01(ref r) * 2f - 1f) * half, v = 60f + 70f * Rand01(ref r), age = landed - 0.04f * Rand01(ref r);
                    if (age < 0f)
                        continue;
                    V2 p = o + dir * (front + 4f + v * age) + side * lat * (1f + 0.3f * age);
                    Square(p, 3f, (i % 3 == 0 ? Rgba.White : col).WithAlpha(0.9f * (1f - age / 0.3f)));
                }
            }
            return true;
        }

        /// With Visual effects off, a plain bar sliding out and fading.
        void DrawStaffShovePlain(Effect fx, float f)
        {
            V2 to = fx.Pos2 - fx.Pos, dir = to.Normalized(), side = dir.Orthogonal();
            float k = Mathf.Clamp01(fx.T / 0.2f);
            V2 c = fx.Pos + dir * (18f + (to.Length - 24f) * EaseOut3(k));
            Line(c - side * fx.Radius, c + side * fx.Radius, 4f, fx.Color.WithAlpha(0.9f * (1f - f)));
        }

        /// Three pips over the Bo Staff's bearer: a lit pip for each step done since the chain began; with two lit, the third (the
        /// spin) pulses, since the next click is the finisher. There's no timer, so this is the only way to see where you are.
        void DrawBoStaffPips(Player p, float t)
        {
            if (p.Downed)
                return;
            V2 o = p.Disp + new V2(-14f, -(p.Def.BodyR + 14f));
            FillRect(o.X - 9f, o.Y - 6f, 46f, 12f, new Rgba(0f, 0f, 0f, 0.4f));
            for (int i = 0; i < 3; i++)
            {
                V2 at = o + new V2(i * 14f, 0f);
                bool lit = i < p.BoStep, finisher = i == 2 && p.BoStep == 2;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 12f);
                if (finisher)
                    Arc(at, 6f + 2f * pulse, 1.8f, Game.BoStaffSage.WithAlpha(0.5f + 0.4f * pulse), 12);
                Circle(at, i == 2 ? 4.5f : 3.5f, lit ? Game.BoStaffSage : new Rgba(0.78f, 0.9f, 0.62f, 0.25f), 10);
            }
        }
    }
}
