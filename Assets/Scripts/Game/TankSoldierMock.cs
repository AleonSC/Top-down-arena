using UnityEngine;

namespace Arena
{
    /// MOCKUP (Tools/Preview only, TANKLOOK=<0-2>, ShotTankLook.cs): three concepts for a reworked Tank Soldier model, drawn with
    /// the real model primitives in place of the Soldier's back, torso and trim, and head (arms and the held gun stay as they are).
    ///   0 Bulwark: a riot heavy: stacked lamellar pauldrons, a wide glowing visor band, a radio pack with a whip antenna.
    ///   1 Exo Frame: a powered frame: a hex reactor on the back feeding the shoulders, angular plated guards, a mono-eye.
    ///   2 Breacher: a curved ballistic shield slung on the back, a bandolier of shells, a brimmed helmet with twin goggles.
    public partial class ArenaApp
    {
        internal static int tankLookMock = -1;  // Tools/Preview only
        internal static int hitMarkMock = -1;   // Tools/Preview only (HITMARK=<0-3>): the hitbox mark's options, -1 the white ring

        /// Hitbox mark options: 0 none, 1 a small bright core dot only, 2 a faint thin ring in the role colour + core dot,
        /// 3 four corner ticks round the hitbox (a reticle) + core dot.
        void DrawHitMarkMock(Player p, Rgba col, float t)
        {
            float h = p.Def.HitR;
            Rgba rc = p.Def.Color;
            if (hitMarkMock == 0) return;
            if (hitMarkMock == 2) Arc(p.Disp, h - 0.5f, 1f, rc.WithAlpha(0.45f * col.A), 20);
            if (hitMarkMock == 3)
                for (int i = 0; i < 4; i++)
                    Arc(p.Disp, h - 0.5f, 1.4f, rc.Mix(Rgba.White, 0.5f).WithAlpha(0.8f * col.A), 4, p.Aim + MathX.Pi * 0.25f + i * MathX.Pi * 0.5f - 0.3f, 0.6f);
            Circle(p.Disp, h * 0.3f + 0.6f, new Rgba(0f, 0f, 0f, 0.5f * col.A), 10);
            Circle(p.Disp, h * 0.3f, Rgba.White.WithAlpha(col.A), 10);
        }
        int mTankLook = -1;                     // this model's concept, or -1 for the normal look

        static readonly Rgba TmSteel = Hx(0x9aa6b4), TmSteelHi = Hx(0xc9d3de), TmPlate = Hx(0x6f7d66), TmInk = Hx(0x161b14);

        /// Torso, back and trim for the mocked Tank Soldier (drawn over the normal torso).
        void TankMockBody(ModelPal K, Rgba RC, float rx, float ry, float t)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
            Rgba glow = RC.Mix(Rgba.White, 0.35f);
            switch (mTankLook)
            {
                case 0:  // Bulwark
                {
                    // Radio pack and its whip antenna, swaying behind.
                    MRect(-21f, -9f, 12f, 18f, TmInk); MRect(-20f, -8f, 10f, 16f, K.Dark);
                    MRect(-19f, -6.5f, 3.2f, 13f, K.Base); MRect(-14.5f, -6.5f, 3.2f, 13f, K.Base);
                    float sw = Mathf.Sin(t * 2.2f) * 3f;
                    MLine(-18f, 6f, -26f, 8f + sw, 1.3f, TmInk); MLine(-26f, 8f + sw, -30f, 9f + sw * 1.4f, 1f, TmInk);
                    MCirc(-30f, 9f + sw * 1.4f, 1.8f, RC.WithAlpha(0.5f + 0.5f * pulse), 8);
                    // Wide torso and a steel chest plate with the role stripe.
                    MEll(-1f, 0f, rx + 1f, ry + 0.5f, TmInk); MEll(-1f, 0f, rx - 0.4f, ry - 1f, K.Base);
                    MPoly(TmSteel, 9.5f, 0f, 6f, -9.5f, -5f, -9f, -5f, 9f, 6f, 9.5f);
                    MPoly(TmSteelHi, 8f, 0f, 5f, -7.5f, 1f, -7.5f, 1f, 7.5f, 5f, 7.5f);
                    MLine(-4.5f, 0f, 9f, 0f, 2.2f, RC);
                    // Stacked lamellar pauldrons: three plates a shoulder, stepping outward, the last one edged in the role colour.
                    for (int s = -1; s <= 1; s += 2)
                        for (int i = 0; i < 3; i++)
                        {
                            float y = s * (ry - 6f + i * 3.4f), w = 8.5f - i * 1.2f;
                            MEll(1f - i * 0.6f, y, w + 1.2f, 4.6f, TmInk);
                            MEll(1f - i * 0.6f, y, w, 3.5f, i == 2 ? TmSteelHi : TmSteel);
                            if (i == 2) MArc(1f - i * 0.6f, y, 3.6f, s > 0 ? 0f : MathX.Pi, s > 0 ? MathX.Pi : MathX.Tau, 1.3f, RC);
                        }
                    break;
                }
                case 1:  // Exo Frame
                {
                    // Hex reactor on the back, its core pulsing, two feed pipes to the shoulders.
                    MLine(-13f, -4f, -2f, -ry + 3f, 2.6f, TmInk); MLine(-13f, 4f, -2f, ry - 3f, 2.6f, TmInk);
                    MLine(-13f, -4f, -2f, -ry + 3f, 1.2f, glow.WithAlpha(0.55f + 0.45f * pulse)); MLine(-13f, 4f, -2f, ry - 3f, 1.2f, glow.WithAlpha(0.55f + 0.45f * pulse));
                    HexAt(-15f, 0f, 9f, TmInk); HexAt(-15f, 0f, 7.6f, MDark);
                    HexAt(-15f, 0f, 4.6f + pulse * 0.8f, RC.WithAlpha(0.8f)); HexAt(-15f, 0f, 2.6f, Rgba.White.WithAlpha(0.6f + 0.4f * pulse));
                    // Torso: dark frame with a steel chevron chest.
                    MEll(-1f, 0f, rx, ry - 1f, TmInk); MEll(-1f, 0f, rx - 1.4f, ry - 2.4f, MDark);
                    MPoly(TmSteel, 10f, 0f, 3f, -8.5f, -4f, -6f, -1f, 0f, -4f, 6f, 3f, 8.5f);
                    MLine(-1f, 0f, 9f, 0f, 1.4f, RC);
                    // Angular shoulder guards with role-colour chevrons.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float y0 = s * (ry - 7f), y1 = s * (ry + 3f);
                        MPoly(TmInk, -1f, y0, 9f, s * (ry - 4f), 6f, y1, -7f, y1 + s * 0.5f, -8f, s * (ry - 5f));
                        MPoly(TmSteel, 0f, s * (ry - 5.6f), 7.4f, s * (ry - 3.6f), 5f, s * (ry + 1.6f), -6f, s * (ry + 1.8f), -6.6f, s * (ry - 4.4f));
                        MLine(5f, s * (ry - 3f), 1f, s * (ry + 0.6f), 1.5f, RC); MLine(1f, s * (ry - 3f), -3f, s * (ry + 0.6f), 1.5f, RC);
                        MCirc(-4f, s * (ry - 3.5f), 1.1f, TmSteelHi, 6);
                    }
                    break;
                }
                default:  // Breacher
                {
                    // A curved ballistic shield slung across the back: steel arc, a viewport slit, straps.
                    MArc(-6f, 0f, 16f, MathX.Pi - 1.25f, MathX.Pi + 1.25f, 7f, TmInk);
                    MArc(-6f, 0f, 16f, MathX.Pi - 1.18f, MathX.Pi + 1.18f, 5f, TmSteel);
                    MArc(-6f, 0f, 17.6f, MathX.Pi - 1.1f, MathX.Pi + 1.1f, 1.2f, TmSteelHi);
                    MArc(-6f, 0f, 14.2f, MathX.Pi - 0.32f, MathX.Pi + 0.32f, 1.4f, RC.WithAlpha(0.9f));
                    // Torso: padded olive with a bandolier of scattergun shells.
                    MEll(-1f, 0f, rx + 0.5f, ry, TmInk); MEll(-1f, 0f, rx - 1f, ry - 1.4f, K.Base);
                    MLine(7f, -ry + 4f, -8f, ry - 4f, 4.6f, Hx(0x4a3a24));
                    for (int i = 0; i < 5; i++)
                    {
                        float f = (i + 0.5f) / 5f, sx = Mathf.Lerp(6f, -7f, f), sy = Mathf.Lerp(-ry + 5f, ry - 5f, f);
                        MCirc(sx, sy, 1.9f, Hx(0xb8402e), 8); MCirc(sx + 0.6f, sy, 1f, MGold, 6);
                    }
                    // Domed riveted pauldrons, role-colour rim.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float y = s * (ry - 0.5f);
                        MCirc(0f, y, 7.8f, TmInk, 14); MCirc(0f, y, 6.6f, TmPlate, 14); MCirc(-1f, y - s * 1f, 3.4f, Hx(0x8a9a80), 10);
                        MArc(0f, y, 6.8f, 0f, MathX.Tau, 1.4f, RC);
                        for (int k = 0; k < 4; k++) { float a = k * MathX.Pi * 0.5f + 0.6f; MCirc(Mathf.Cos(a) * 4.6f, y + Mathf.Sin(a) * 4.6f, 0.75f, TmSteelHi, 6); }
                    }
                    break;
                }
            }
        }

        void TankMockHead(ModelPal K, Rgba RC, float x, float r, float t)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
            Rgba visor = Hx(0x7bd0ff);
            switch (mTankLook)
            {
                case 0:  // Bulwark: a heavy helmet, a ridge, a wide glowing visor band
                    for (int s = -1; s <= 1; s += 2)  // cheek guards jutting past the hit ring, a lamp on each
                    {
                        MPoly(TmInk, x + 2f, s * (r - 2f), x + 7f, s * (r + 1f), x + 4f, s * (r + 5.5f), x - 5f, s * (r + 4.5f), x - 6f, s * (r - 1f));
                        MPoly(TmSteel, x + 1.6f, s * (r - 1f), x + 5.6f, s * (r + 1.4f), x + 3.2f, s * (r + 4.2f), x - 4.4f, s * (r + 3.4f), x - 5f, s * (r - 0.4f));
                        MCirc(x + 3.4f, s * (r + 2f), 1.3f, Hx(0x7bd0ff).WithAlpha(0.7f + 0.3f * pulse), 8);
                    }
                    MCirc(x, 0f, r + 1f, TmInk, 16); MCirc(x, 0f, r - 0.2f, K.Base, 16);
                    MLine(x - r + 1f, 0f, x + r * 0.3f, 0f, 3f, K.Dark); MLine(x - r + 1.5f, 0f, x + r * 0.2f, 0f, 1.2f, RC);
                    MArc(x, 0f, r - 1.6f, -1.05f, 1.05f, 3.6f, TmInk);
                    MArc(x, 0f, r - 1.6f, -0.95f, 0.95f, 2f, visor.WithAlpha(0.75f + 0.25f * pulse));
                    MArc(x, 0f, r - 1.6f, -0.4f, 0.4f, 0.9f, Rgba.White.WithAlpha(0.8f));
                    break;
                case 1:  // Exo Frame: a collar ring, a compact helm and a single mono-eye
                    MArc(x - 1f, 0f, r + 1.6f, MathX.Pi - 1.6f, MathX.Pi + 1.6f, 2.2f, TmSteel);
                    MCirc(x, 0f, r - 0.4f, TmInk, 16); MCirc(x, 0f, r - 1.6f, MDark, 16);
                    MPoly(TmSteel, x + r - 1.4f, 0f, x + 1f, -r + 2.2f, x - r + 3f, -2f, x - r + 3f, 2f, x + 1f, r - 2.2f);
                    MPoly(TmInk, x + r + 7f, 0f, x + r + 4f, -4.6f, x + r - 3f, -4.2f, x + r - 3f, 4.2f, x + r + 4f, 4.6f);  // a sensor snout out past the hit ring
                    MPoly(TmSteel, x + r + 5.6f, 0f, x + r + 3f, -3.4f, x + r - 2f, -3.2f, x + r - 2f, 3.2f, x + r + 3f, 3.4f);
                    MCirc(x + r + 2.6f, 0f, 2.5f, TmInk, 12);
                    MCirc(x + r + 2.6f, 0f, 1.9f, RC.Mix(Rgba.White, 0.3f), 12);
                    MCirc(x + r + 3f, 0f, 0.8f + 0.3f * pulse, Rgba.White, 8);
                    MLine(x + r + 6f, 0f, x + r + 16f, 0f, 1.6f, RC.WithAlpha(0.18f + 0.12f * pulse));  // its scan beam
                    break;
                default:  // Breacher: a brimmed helmet and twin round goggles
                    MCirc(x - 0.5f, 0f, r + 5f, TmInk, 20); MCirc(x - 0.5f, 0f, r + 3.8f, K.Dark, 20); MArc(x - 0.5f, 0f, r + 3f, 0f, MathX.Tau, 0.8f, K.Base);
                    MCirc(x, 0f, r - 0.6f, K.Base, 16);
                    MLine(x - r + 1f, -2.2f, x - r + 1f, 2.2f, 1.6f, RC);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float gx = x + r + 2.6f, gy = s * 4f;  // goggles pushed up on the brim, out past the hit ring
                        MCirc(gx, gy, 3.4f, TmInk, 12);
                        MCirc(gx, gy, 2.5f, visor.WithAlpha(0.7f + 0.3f * pulse), 12);
                        MCirc(gx + 0.6f, gy - 0.7f, 0.9f, Rgba.White, 6);
                    }
                    MLine(x + r + 2.6f, -1.2f, x + r + 2.6f, 1.2f, 1.6f, TmInk);
                    break;
            }
        }

        void HexAt(float x, float y, float r, Rgba c)
        {
            float[] p = new float[12];
            for (int i = 0; i < 6; i++) { float a = i * MathX.Pi / 3f + MathX.Pi / 6f; p[i * 2] = x + Mathf.Cos(a) * r; p[i * 2 + 1] = y + Mathf.Sin(a) * r; }
            MPoly(c, p);
        }
    }
}
