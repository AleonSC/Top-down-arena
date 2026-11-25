using UnityEngine;

namespace Arena
{
    /// The DPS Engineer's Bumble Remote (Core/BumbleRemote.cs): the Bumble Bot (a robot bee in gold plating with the Engineer's cyan
    /// trim, beating glass wings, a visor and a stinger cannon), its shadow, the tether to its pilot and the lock ring on its target;
    /// FxKind.BumbleBurst (the Swarm Burst's landing); the stingers it fires; the remote in the Engineer's hands.
    public partial class ArenaApp
    {
        static readonly Rgba BumPlate = Hx(0xf0a51e), BumShine = Hx(0xffd66b), BumDark = Hx(0x15181e), BumWing = new Rgba(0.7f, 0.9f, 1f, 0.2f);

        /// MinionKind.BumbleBot. Wind: 0 cruising, 1 Fire held (wings faster, cannon glowing), 2 diving (bigger, a gold trail).
        void DrawBumbleBot(Minion m, float t)
        {
            float r = m.Def.Radius, dive = m.Wind == 2f ? 1f : 0f, held = m.Wind == 1f ? 1f : 0f;
            float bob = Mathf.Sin(t * 5f + m.Id) * 2.5f * (1f - dive), lift = 16f - 6f * dive;
            V2 at = m.Disp + new V2(0f, -lift + bob);
            float aim = m.Aim, sc = r / 18f * (1f + 0.22f * dive);
            V2.Axes(aim, out V2 f, out V2 s);

            Ellipse(m.Disp + new V2(0f, 4f), r * 0.95f, r * 0.42f, 0f, new Rgba(0f, 0f, 0f, 0.28f), 16);  // its shadow on the floor
            Player owner;
            if (game.Players.TryGetValue(m.Owner, out owner) && !owner.Downed)  // the tether: dashes running from the pilot to the bot
            {
                V2 a = owner.Disp, b = at, ab = b - a;
                float len = ab.Length;
                if (len > 40f)
                {
                    V2 dir = ab / len;
                    float off = (t * 60f) % 14f;
                    for (float d = 20f + off; d < len - r; d += 14f)
                        Line(a + dir * d, a + dir * Mathf.Min(d + 6f, len - r), 1.2f, EngCyan.WithAlpha(0.22f));
                }
            }
            Enemy lockOn;
            if (m.LinkTo != 0 && game.Enemies.TryGetValue(m.LinkTo, out lockOn) && dive == 0f)  // the lock ring on what it's shooting
            {
                float rr = lockOn.Radius + 7f;
                for (int i = 0; i < 4; i++)
                    Arc(lockOn.Disp, rr, 1.6f, BumPlate.WithAlpha(0.75f), 4, t * 2.5f + i * MathX.Pi * 0.5f - 0.3f, 0.6f);
            }
            if (dive > 0f)
                for (int i = 1; i <= 5; i++)  // the dive's gold streak
                    Line(at - f * (r * 0.6f * i), at - f * (r * 0.6f * (i + 1)), (6f - i) * sc, BumPlate.WithAlpha(0.4f * (1f - i / 6f)));

            float beat = 0.5f + 0.5f * Mathf.Sin(t * (held > 0f ? 70f : 48f) + m.Id);
            for (int k = -1; k <= 1; k += 2)  // two pairs of glass wings
            {
                V2 w1 = at - f * (2f * sc) + s * (k * (11f + 4f * beat) * sc);
                V2 w2 = at - f * (9f * sc) + s * (k * (9f + 3f * beat) * sc);
                Ellipse(w1, 11f * sc, (5f + 3f * beat) * sc, aim + k * 0.45f, BumWing, 14);
                Ellipse(w2, 8f * sc, (3.5f + 2f * beat) * sc, aim + k * 0.8f, BumWing.WithAlpha(0.14f), 12);
                Line(at - f * (2f * sc), w1 + s * (k * 7f * sc), 1.2f, EngCyan.WithAlpha(0.75f));  // the wing's cyan spar
            }
            // the abdomen: gold plates with dark bands and cyan seams, tapering to the stinger
            V2 tail = at - f * (9f * sc);
            Ellipse(tail, 13.5f * sc, 10.5f * sc, aim, BumDark, 18);
            Ellipse(tail, 11.5f * sc, 8.5f * sc, aim, BumPlate, 18);
            Ellipse(tail + f * (2f * sc) - s * (3f * sc), 6f * sc, 3f * sc, aim, BumShine, 12);  // the plating's shine
            for (int i = 0; i < 3; i++)
            {
                V2 c = tail + f * ((3f - 5.2f * i) * sc);
                float half = (8f - 1.6f * i) * sc;
                Line(c + s * half, c - s * half, 3.4f * sc, BumDark);
            }
            Line(tail + f * (6f * sc) + s * (5f * sc), tail - f * (8f * sc) + s * (3f * sc), 0.8f, EngCyan.WithAlpha(0.7f));
            Line(tail + f * (6f * sc) - s * (5f * sc), tail - f * (8f * sc) - s * (3f * sc), 0.8f, EngCyan.WithAlpha(0.7f));
            Line(tail - f * (10.5f * sc), tail - f * (16f * sc), 2f * sc, BumDark);  // the rear stinger
            // the thorax: a steel hex with a cyan core
            Polygon(at + f * (3f * sc), 7.5f * sc, 6, aim, EngIron);
            Circle(at + f * (3f * sc), 3.2f * sc, EngCyan.WithAlpha(0.6f + 0.4f * beat), 10);
            // the head: dark helmet, a wraparound cyan visor, antennae
            V2 head = at + f * (12f * sc);
            Circle(head, 7.4f * sc, BumDark, 16);
            Arc(head, 5f * sc, 2.8f * sc, EngCyan.WithAlpha(0.95f), 12, aim - 1.1f, 2.2f);  // the visor
            Circle(head + f * (3f * sc) - s * (2f * sc), 1.2f * sc, Rgba.White.WithAlpha(0.9f), 6);
            for (int k = -1; k <= 1; k += 2)
            {
                V2 tip = head + f * (9f * sc) + s * (k * 6f * sc);
                Line(head + f * (3f * sc) + s * (k * 2f * sc), tip, 1.1f, BumDark);
                Circle(tip, 1.5f * sc, held > 0f && Mathf.Sin(t * 20f) > 0f ? Rgba.White : BumPlate, 6);
            }
            // the stinger cannon slung under the head, glowing while Fire is held
            V2 gun = head + f * (6f * sc) + s * (3.5f * sc);
            Line(head + s * (3.5f * sc), gun, 3f * sc, EngIron);
            Circle(gun, (1.8f + 1.4f * held * beat) * sc, EngCyan.WithAlpha(0.5f + 0.5f * held), 8);
        }

        /// FxKind.BumbleBurst: Pos where it landed, Radius the blast. A gold shockwave with a cyan hex grid, flying plate shards.
        void DrawBumbleBurstFx(Effect fx)
        {
            float age = fx.T, k = Mathf.Clamp01(age / 0.25f), ease = 1f - (1f - k) * (1f - k) * (1f - k);
            float fade = 1f - Mathf.Clamp01((age - 0.2f) / Mathf.Max(0.05f, fx.Dur - 0.2f)), R = Mathf.Max(8f, fx.Radius);
            Circle(fx.Pos, R * ease, BumPlate.WithAlpha(0.16f * fade), 32);
            Arc(fx.Pos, R * ease, 5f * fade + 1f, BumPlate.WithAlpha(fade), 40);
            Arc(fx.Pos, R * ease * 0.7f, 1.6f, EngCyan.WithAlpha(0.7f * fade), 30);
            if (age < 0.12f)
                Circle(fx.Pos, 26f * (1f - age / 0.12f) + 6f, Rgba.White.WithAlpha(0.8f), 16);  // the flash
            if (!fxOn)
                return;
            for (int i = 0; i < 6; i++)  // the hex grid flaring out
            {
                float a = i * MathX.Pi / 3f;
                V2 p0 = fx.Pos + V2.FromAngle(a) * R * ease * 0.7f, p1 = fx.Pos + V2.FromAngle(a + MathX.Pi / 3f) * R * ease * 0.7f;
                Line(p0, p1, 1.4f, EngCyan.WithAlpha(0.55f * fade));
                Line(fx.Pos, p0, 1f, EngCyan.WithAlpha(0.3f * fade));
            }
            for (int i = 0; i < 10; i++)  // plate shards and sparks thrown out
            {
                float a = i * MathX.Tau / 10f + BeeHash(i * 2.7f) * 0.5f, d = R * (0.5f + 0.6f * BeeHash(i * 4.1f)) * ease;
                V2 c = fx.Pos + V2.FromAngle(a) * d;
                Line(c, c + V2.FromAngle(a) * 6f, 2.2f, (i % 2 == 0 ? BumPlate : EngCyan).WithAlpha(fade));
            }
        }

        /// A Bumble Bot stinger (its Defs.DpsShot): a dark needle with a gold barb and a short cyan streak.
        void DrawStinger(V2 pos, float ang)
        {
            V2 f = V2.FromAngle(ang);
            Line(pos - f * 12f, pos - f * 4f, 1.6f, EngCyan.WithAlpha(0.35f));
            Line(pos - f * 5f, pos + f * 4f, 2.2f, BumDark);
            Line(pos + f * 1f, pos + f * 5f, 1.6f, BumPlate);
        }

        /// The remote in the Engineer's hands (model frame, x along the aim): a chunky controller with an antenna, its light
        /// blinking faster while Fire is held.
        void DrawBumbleRemoteTool(ModelPal K, float ry, float t, float fire)
        {
            float rhx = 11f, rhy = 3.5f, lhx = 11f, lhy = -3.5f;
            ModelArm(K, 1f, ry * 0.72f, rhx, rhy);
            ModelArm(K, 1f, -ry * 0.72f, lhx, lhy);
            MRect(8f, -6f, 9f, 12f, Hx(0x1c2129));
            MRect(9f, -5f, 7f, 10f, Hx(0x5d6878));
            MCirc(12.5f, -2.2f, 1.6f, BumPlate, 8);  // the stick
            MCirc(12.5f, 2.4f, 1.2f, EngCyan, 6);    // the burst button
            MLine(16f, -4.5f, 22f, -7.5f, 1f, EngSteel);
            bool on = Mathf.Sin(t * (fire > 0f ? 26f : 6f)) > 0f;
            MCirc(22f, -7.5f, 1.5f, on ? BumPlate : Hx(0x6b5a2a), 6);
            MCirc(rhx, rhy, 3.3f, K.Glove); MCirc(lhx, lhy, 3.3f, K.Glove);
        }
    }
}
