using UnityEngine;

namespace Arena
{
    /// The Wolf Whistle's pack (Core/WolfPack.cs), drawn as real wolves from above instead of the old grey triangles.
    ///   - A wolf: a lean grey body with a darker saddle and a pale ruff, a bushy tail that swings as it runs, four
    ///     slim legs striding, a long cream muzzle with a black nose, pricked ears and amber eyes. Its jaw opens and its
    ///     head lunges before each bite (the Bear's guessed bite: BearViewOf, BearLooks.cs). Every wolf (Alpha Call's and
    ///     Legion's too) uses it.
    ///   - An alpha (Scale > 1: Game.AlphaSize wolves in it): bigger, a charcoal coat with a thick silver mane, a
    ///     scar over one eye, ember eyes and a faint ring under it with one notch per wolf in it.
    ///   - A pack wolf's health (Minion.GuardFrac) as a thin bar under it once it's hurt.
    ///   - Behind its owner: PackMax paw pips, lit for the slots the pack fills (the alpha's bigger), and a ring closing
    ///     round the next empty one as kills come in (Player.PackKills).
    public partial class ArenaApp
    {
        static readonly Rgba WolfBody = Hx(0x8b909a), WolfSaddle = Hx(0x5b606a), WolfRuff = Hx(0xa9aeb7), WolfEdge = Hx(0x2b2e35),
            WolfMuzzle = Hx(0xddd6c6), WolfPaw = Hx(0x474b54), WolfEar = Hx(0x3c3f47), WolfEarIn = Hx(0x9a7f78), WolfNose = Hx(0x111215),
            WolfEye = Hx(0xffb63c), WolfTailTip = Hx(0x3a3d44), WolfMouth = Hx(0x3a0d12), WolfFang = Hx(0xfffaf0);
        static readonly Rgba AlphaBody = Hx(0x4a4d57), AlphaSaddle = Hx(0x2a2c33), AlphaMane = Hx(0xcfd4de), AlphaMuzzle = Hx(0xa69f92),
            AlphaEye = Hx(0xff6a2a), AlphaScar = Hx(0xd8c8c0), AlphaGlow = new Rgba(1f, 0.45f, 0.2f);

        void DrawWolf(Minion m, float t)
        {
            var v = BearViewOf(m, t);
            bool alpha = m.Scale > 1.01f;
            int size = Game.AlphaSize(m);
            bAlpha = m.T >= 0f && m.T < 1.5f && MathX.Fmod(t, 0.2f) < 0.1f ? 0.4f : 1f;
            bS = m.Scale;
            bO = m.Disp;
            bDir = V2.FromAngle(m.Aim);
            bSide = new V2(-bDir.Y, bDir.X);
            float run = v.Run, st = Mathf.Sin(v.Phase * 1.3f), br = 1f + 0.03f * Mathf.Sin(t * 3.4f + m.Id);
            float jaw = 0f, lunge = 0f;
            if (v.Biting)
            {
                float age = v.Cycle >= 0.44f ? v.Cycle - 0.6f : v.Cycle;   // the bite lands at 0; the jaw opens in the 0.16 s before it
                if (age >= -0.16f && age < 0f)
                    jaw = (age + 0.16f) / 0.16f;
                else if (age >= 0f && age < 0.14f)
                    jaw = 1f - age / 0.14f;
                if (age >= -0.12f && age < 0.22f)
                    lunge = 6f * Mathf.Sin(MathX.Pi * Mathf.Clamp01((age + 0.12f) / 0.34f));
            }
            Rgba body = alpha ? AlphaBody : WolfBody, saddle = alpha ? AlphaSaddle : WolfSaddle, ruff = alpha ? AlphaMane : WolfRuff,
                muzzle = alpha ? AlphaMuzzle : WolfMuzzle, eye = jaw > 0.5f ? new Rgba(1f, 0.35f, 0.29f) : alpha ? AlphaEye : WolfEye;

            if (alpha && fxOn)  // the alpha's ring: one notch per wolf in it
            {
                float r = 20f * bS;
                Circle(bO, r + 3f, AlphaGlow.WithAlpha(0.08f * bAlpha), 24);
                Arc(bO, r, 1.6f, AlphaGlow.WithAlpha(0.45f * bAlpha), 32);
                for (int i = 0; i < size; i++)
                {
                    V2 d = V2.FromAngle(m.Aim + MathX.Pi + (i - (size - 1) / 2f) * 0.32f);
                    Line(bO + d * (r - 3f), bO + d * (r + 3f), 2.2f, AlphaGlow.WithAlpha(0.85f * bAlpha));
                }
            }
            BEll(-1f, 2.5f, 17f, 8f, 0f, new Rgba(0f, 0f, 0f, 0.25f));  // shadow

            // Tail: a bushy brush behind, hanging low and swinging with the stride (wagging when it stands).
            float sway = run > 0.3f ? st * 3.5f * run : 2.2f * Mathf.Sin(t * 5f + m.Id);
            float[] tailR = { 2.2f, 2.8f, 3.0f, 2.2f };
            for (int pass = 0; pass < 2; pass++)
                for (int k = 0; k < 4; k++)
                {
                    float f = k / 3f, x = -11.5f - k * 3.3f, y = sway * f * f, r = tailR[k] + (pass == 0 ? 0.9f : 0f);
                    BEll(x, y, r + 0.8f, r, 0f, pass == 0 ? WolfEdge : k == 3 ? WolfTailTip : k == 0 ? body : saddle);
                }

            // Legs: four slim legs in a trot (diagonal pairs together), the paws showing out at the sides.
            float[] legX = { 9.5f, 9.5f, -8.5f, -8.5f }, legY = { -6.4f, 6.4f, -5.8f, 5.8f }, phase = { 1f, -1f, -1f, 1f };
            for (int i = 0; i < 4; i++)
            {
                float ox = st * 5f * phase[i] * run, px = legX[i] + ox, py = legY[i];
                BLine(legX[i] * 0.8f, legY[i] * 0.5f, px, py, 3f, WolfEdge);
                BLine(legX[i] * 0.8f, legY[i] * 0.5f, px, py, 1.8f, body);
                BEll(px + 0.6f, py, 2.3f, 1.8f, 0f, WolfEdge);
                BEll(px + 0.6f, py, 1.7f, 1.3f, 0f, WolfPaw);
            }

            // Body: narrow hips, a deeper chest, the dark saddle down the spine and a pale ruff at the neck.
            BEll(-7f, 0f, 7.4f * br, 5.9f * br, 0f, WolfEdge);
            BEll(3f, 0f, 8.8f * br, 7f * br, 0f, WolfEdge);
            BEll(-7f, 0f, 6.4f * br, 4.9f * br, 0f, body);
            BEll(3f, 0f, 7.8f * br, 6f * br, 0f, body);
            BEll(-2.5f, 0f, 10.5f, 2.3f, 0f, saddle);
            BEll(-9.5f, 0f, 3.2f, 2.8f, 0f, saddle);
            if (alpha)
                for (int s = -1; s <= 1; s += 2)  // the mane's shaggy points down the shoulders
                    for (int k = 0; k < 3; k++)
                        BTri(ruff, 9f - k * 2.6f, s * (5.4f - k * 0.5f), 4.8f - k * 2.6f, s * (8.4f - k * 0.7f), 6.4f - k * 2.6f, s * (4.4f - k * 0.5f));
            BEll(8.5f, 0f, 3.6f, alpha ? 6.4f : 5.2f, 0f, ruff);
            BEll(7f, 0f, 2.2f, 1.8f, 0f, saddle.WithAlpha(0.8f));

            // Head: skull, a long tapered muzzle with a black nose, pricked ears on top; the jaws part before a bite.
            float hx = 13.5f + lunge;
            BEll(hx, 0f, 5.6f, 5.2f, 0f, WolfEdge);
            BEll(hx, 0f, 4.8f, 4.4f, 0f, body);
            BEll(hx - 1.4f, 0f, 2.4f, 1.6f, 0f, saddle.WithAlpha(0.8f));  // the dark brow
            if (jaw > 0.05f)
            {
                BEll(hx + 6f, 0f, 5.2f, 1.2f + 2.6f * jaw, 0f, WolfMouth);
                for (int s = -1; s <= 1; s += 2)
                {
                    float jy = s * (0.9f + 2.2f * jaw), rot = s * 0.22f * jaw;
                    BEll(hx + 5.4f, jy, 4.8f, 1.5f, rot, WolfEdge);
                    BEll(hx + 5.4f, jy, 4.2f, 1.0f, rot, muzzle);
                    BTri(WolfFang, hx + 8.8f, jy - s * 0.6f, hx + 9.9f, jy - s * 0.4f, hx + 9.2f, jy - s * 2.2f);  // the fangs, pointing in
                    BTri(WolfFang, hx + 5.6f, jy - s * 0.6f, hx + 6.6f, jy - s * 0.5f, hx + 6.0f, jy - s * 1.8f);
                }
                BCirc(hx + 10f, -(0.9f + 2.2f * jaw), 1.3f, WolfNose);
            }
            else
            {
                BEll(hx + 5.4f, 0f, 5.2f, 2.7f, 0f, WolfEdge);
                BEll(hx + 5.4f, 0f, 4.6f, 2.1f, 0f, muzzle);
                BLine(hx + 6.5f, 0f, hx + 9.4f, 0f, 0.6f, WolfEdge.WithAlpha(0.6f));  // the line of its mouth
                BCirc(hx + 9.9f, 0f, 1.5f, WolfNose);
            }
            for (int s = -1; s <= 1; s += 2)  // ears: dark triangles pricked up and back, pink inside
            {
                BTri(WolfEdge, hx - 0.6f, s * 1.4f, hx - 1.6f, s * 5.2f, hx - 6.4f, s * 4.6f);
                BTri(WolfEar, hx - 1.1f, s * 1.9f, hx - 1.9f, s * 4.6f, hx - 5.4f, s * 4.2f);
                BTri(WolfEarIn, hx - 1.6f, s * 2.6f, hx - 2.1f, s * 4.0f, hx - 4.2f, s * 3.8f);
            }
            if (alpha && fxOn)
            {
                BCirc(hx + 1.8f, -2.5f, 2.4f, AlphaGlow.WithAlpha(0.25f));
                BCirc(hx + 1.8f, 2.5f, 2.4f, AlphaGlow.WithAlpha(0.25f));
            }
            BEll(hx + 1.8f, -2.5f, 1.2f, 0.8f, -0.35f, eye);
            BEll(hx + 1.8f, 2.5f, 1.2f, 0.8f, 0.35f, eye);
            if (alpha)  // a pale scar raked over its left eye
                BLine(hx + 3.8f, -4.6f, hx - 0.4f, -0.6f, 0.8f, AlphaScar);
            bAlpha = 1f;

            if (m.T >= 0f && m.Duration > 0f)  // Alpha Call's and Legion's wolves: their time
                Arc(m.Disp, m.Def.Radius * m.Scale + 5f, 2f, m.Def.Color.WithAlpha(0.5f), 24, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(m.T / m.Duration));
            else if (m.T < 0f && (m.MaxHp > 0f ? m.Hp < m.MaxHp - 0.01f : m.GuardFrac < 0.995f))  // a pack wolf's health, once it's hurt
            {
                float f = Mathf.Clamp01(m.MaxHp > 0f ? m.Hp / m.MaxHp : m.GuardFrac), w = 22f * m.Scale;
                V2 bar = m.Disp + new V2(-w / 2f, 14f * m.Scale);
                Line(bar, bar + new V2(w, 0f), 3.5f, new Rgba(0f, 0f, 0f, 0.6f));
                if (f > 0f)
                    Line(bar, bar + new V2(w * f, 0f), 3.5f, f > 0.35f ? (alpha ? AlphaMane : WolfRuff) : new Rgba(1f, 0.38f, 0.38f));
            }
        }

        /// Behind a Wolf Whistle player: one paw pip per pack slot, lit for the wolves you have (the alpha's bigger and
        /// ember), and a ring closing round the next empty one as your kills bring the next wolf.
        void DrawPackPips(Player p)
        {
            if (p.Downed)
                return;
            int wolves = 0, alphaSize = 0;
            foreach (var m in game.Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.Wolf && m.T < 0f)  // (a pack wolf never runs out; Companion isn't sent)
                {
                    wolves++;
                    if (m.Scale > 1.01f)
                        alphaSize = Game.AlphaSize(m);
                }
            V2.Axes(p.Aim + MathX.Pi, out V2 back, out V2 side);
            for (int i = 0; i < Game.PackMax; i++)
            {
                V2 c = p.Disp + back * 30f + side * ((i - (Game.PackMax - 1) / 2f) * 11f);
                bool on = i < wolves, big = on && i == 0 && alphaSize > 0;
                Rgba col = big ? AlphaGlow : on ? WolfRuff : new Rgba(1f, 1f, 1f, 0.16f);
                float s = big ? 1.35f : 1f;
                if (big && fxOn)
                    Circle(c, 6.5f, AlphaGlow.WithAlpha(0.18f), 12);
                Circle(c + back * (1.2f * s), 2.3f * s, col, 10);  // the pad
                for (int k = -1; k <= 1; k++)  // three toes
                    Circle(c - back * (2.4f * s) + side * (k * 1.9f * s), 1.05f * s, col, 6);
                if (i == wolves && p.PackKills > 0)
                    Arc(c, 5.6f, 1.6f, WolfRuff.WithAlpha(0.8f), 16, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(p.PackKills / (float)Game.PackKillsPerWolf));
            }
        }
    }
}
