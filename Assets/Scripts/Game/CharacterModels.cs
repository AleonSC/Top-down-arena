using UnityEngine;

namespace Arena
{
    /// The player models (Docs/Mockups/character-models.html, colour-by-class): every player is a top-down figure
    /// built from the same layers: a back piece (pack, robe, cape, scarf...), shoulders, the role's trim, arms, the
    /// held weapon and a head. Role sets the build and the trim colour (blue Tank, green Healer, orange DPS), the
    /// class sets the costume and palette, the weapon sets the hands and the prop. The body circle (BodyR) and the
    /// bright hit core stay as before, so what a shot can hit reads the same.
    ///
    /// Everything is drawn in a local frame: x along the aim, y to the player's right, in "Tank units" (BodyR = 18),
    /// scaled to the role's real body size. A weapon draws in its own frame inside that (MFrame): the main grip at
    /// (0,0), +x along the aim. To rework a weapon's model, edit its case in DrawHeld and keep the grip and hold.
    public partial class ArenaApp
    {
        // --- the drawing frame ---
        V2 mO, mDir, mSide;          // the player's centre and the aim and right-hand unit vectors
        float mU = 1f, mAim, mT;     // local unit -> arena px, the aim angle, the time
        float mPump, mHeat; int mLit = -1;  // the Tank guns' pose with Visual effects on: the front hand's pump, the Flak's glow, the Riot's lit rib (TankGunLooks.cs)
        float mOx, mOy, mAng, mWs = 1f;  // the current sub-frame (a weapon): where its origin is in the body frame, its turn and size
        bool mMir;                   // mirrored (the second gun of a pair)
        float mAlpha = 1f, mWhite;   // the player's alpha (i-frames, Vanish) and hit flash
        float mTintA; Rgba mTintCol; // the model mixed toward a colour, like the hit flash (going down and standing back up: DownedLooks.cs)

        static Rgba Hx(int rgb, float a = 1f) => new Rgba(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        V2 MW(float x, float y)
        {
            float lx = x * mWs, ly = (mMir ? -y : y) * mWs;
            float c = Mathf.Cos(mAng), s = Mathf.Sin(mAng);
            float px = mOx + lx * c - ly * s, py = mOy + lx * s + ly * c;
            return mO + (mDir * px + mSide * py) * mU;
        }

        Rgba Mc(Rgba c)
        {
            if (mWhite > 0f) c = c.Mix(Rgba.White, mWhite);
            if (mTintA > 0f) c = c.Mix(mTintCol, mTintA);
            return c.WithAlpha(c.A * mAlpha);
        }

        void MLine(float x1, float y1, float x2, float y2, float w, Rgba c) => Line(MW(x1, y1), MW(x2, y2), w * mU * mWs, Mc(c));
        void MCirc(float x, float y, float r, Rgba c, int segs = 12) => Circle(MW(x, y), r * mU * mWs, Mc(c), segs);

        /// A polygon filled as a fan from its first point (so that point must see all the others), x,y pairs.
        void MPoly(Rgba c, params float[] p)
        {
            SetColor(Mc(c));
            V2 a = MW(p[0], p[1]);
            for (int i = 2; i + 3 < p.Length; i += 2)
                Tri(a, MW(p[i], p[i + 1]), MW(p[i + 2], p[i + 3]));
        }

        void MRect(float x, float y, float w, float h, Rgba c) => MPoly(c, x, y, x + w, y, x + w, y + h, x, y + h);

        void MEll(float x, float y, float rx, float ry, Rgba c, float rot = 0f)
        {
            SetColor(Mc(c));
            float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
            V2 mid = MW(x, y), prev = mid;
            for (int i = 0; i <= 14; i++)
            {
                float a = MathX.Tau * i / 14f, ex = rx * Mathf.Cos(a), ey = ry * Mathf.Sin(a);
                V2 next = MW(x + ex * cr - ey * sr, y + ex * sr + ey * cr);
                if (i > 0) Tri(mid, prev, next);
                prev = next;
            }
        }

        void MStar(float x, float y, float R, float r, int n, float rot, Rgba c)
        {
            SetColor(Mc(c));
            V2 mid = MW(x, y), prev = MW(x + Mathf.Cos(rot) * R, y + Mathf.Sin(rot) * R);
            for (int i = 1; i <= n * 2; i++)
            {
                float a = rot + i * MathX.Pi / n, q = i % 2 == 1 ? r : R;
                V2 next = MW(x + Mathf.Cos(a) * q, y + Mathf.Sin(a) * q);
                Tri(mid, prev, next);
                prev = next;
            }
        }

        /// An arc of the local circle at (x,y), local angles a0 to a1 (0 = along the aim, positive toward the right hand).
        void MArc(float x, float y, float r, float a0, float a1, float w, Rgba c)
        {
            float sg = mMir ? -1f : 1f;
            int segs = Mathf.Max(6, (int)(Mathf.Abs(a1 - a0) * 4f) + 3);
            Arc(MW(x, y), r * mU * mWs, w * mU * mWs, Mc(c), segs, mAim + sg * a0 + mAng, sg * (a1 - a0));
        }

        void MFrame(float ox, float oy, float ang, float ws, bool mir) { mOx = ox; mOy = oy; mAng = ang; mWs = ws; mMir = mir; }

        // --- palettes: the class sets the body, the role sets only the trim ---
        struct ModelPal { public Rgba Base, Dark, Sleeve, Glove; }
        static ModelPal Pal(int b, int d, int s, int g) => new ModelPal { Base = Hx(b), Dark = Hx(d), Sleeve = Hx(s), Glove = Hx(g) };
        static readonly ModelPal[] ModelPals =   // in Kit order
        {
            Pal(0x5b6b52, 0x2d3829, 0x46543e, 0x20242a),  // Soldier: olive
            Pal(0x6b4fb0, 0x33235c, 0x573f95, 0xe6dcff),  // Elementalist: violet
            Pal(0x3f7a4a, 0x1d3b25, 0x2f6039, 0xcfe9b8),  // Summoner: forest
            Pal(0xaab6c8, 0x5d6a7c, 0x8795ab, 0xcfd8e6),  // Knight: steel
            Pal(0x2a2a38, 0x13131a, 0x1d1d28, 0x0e0e14),  // Ninja: charcoal
            Pal(0x8c1626, 0x3a0a12, 0x6a101c, 0x2a0a10),  // Hemomancer: crimson
        };
        static readonly Rgba MSteel = Hx(0xcfd8e6), MDark = Hx(0x3a4250), MWood = Hx(0x8a6a45), MGold = Hx(0xe8c25a), MBone = Hx(0xefe9d8), MWhite = Hx(0xffffff);

        // Shoulder half-depth, half-width, head radius and head offset, per role (Tank, Healer, DPS).
        static readonly float[][] ModelBuild = { new[] { 11f, 17f, 9f, 0.5f }, new[] { 10f, 14.5f, 9f, 0f }, new[] { 9f, 12.5f, 9.5f, 2.5f } };

        /// The whole model: back, shoulders, role trim, arms, held weapon, head. `col` carries the alpha and hit flash.
        void DrawPlayerModel(Player p, float r, float t, Rgba col)
        {
            int kit = (int)p.Kit, role = Mathf.Min((int)p.Role, 2);  // (a Wildcard wears the DPS build in its own purple trim)
            ModelPal K = ModelPals[kit];
            Rgba RC = p.Def.Color;
            float[] sh = ModelBuild[role];
            mO = p.Disp; mAim = p.Aim; mDir = V2.FromAngle(p.Aim); mSide = new V2(-mDir.Y, mDir.X); mU = r / 18f; mT = t;
            mAlpha = col.A; mWhite = p.Flash > 0f ? 0.85f : 0f;
            MFrame(0f, 0f, 0f, 1f, false);
            float rx = sh[0], ry = sh[1], w = Mathf.Sin(t * 3f) * 1.6f;

            MEll(1f, 2f, rx + 2f, ry + 1f, new Rgba(0f, 0f, 0f, 0.28f));
            ModelBack(p, kit, role, K, RC, rx, ry, w, t);
            MEll(-1f, 0f, rx, ry, K.Dark); MEll(-1f, 0f, rx - 1.6f, ry - 1.6f, K.Base);
            ModelRoleTrim(role, K, RC, rx, ry, t);

            if (p.Weapon.Id == WeaponId.BearTotem)  // a claw on each hand and the totem on the back, not a held weapon (BearLooks.cs)
            {
                DrawBearModel(p, K, ry);
                ModelHead(p, kit, role, K, RC, r, sh, t);
                return;
            }

            // Arms and the weapon.
            float sway = Mathf.Sin(t * 2.4f) * 1.2f, u = mU, wk = (0.8f + 0.2f * u) / u;
            int hold; float offX;
            HoldOf(p.Weapon.Id, out hold, out offX);
            if (p.Weapon.Id == WeaponId.MendersBow)
                wk *= 1.7f;  // the Mender's Bow is drawn much bigger than other weapons (limbs, string and arrow scale together)
            float rhx, rhy, lhx, lhy;
            switch (hold)
            {
                case 0: rhx = 15f; rhy = 10.5f; lhx = 8f; lhy = -13.5f + sway; break;       // one hand
                case 1: rhx = 13f; rhy = 4.5f; lhx = 13f + offX * wk; lhy = 4.5f; break;     // two hands on the weapon
                case 2: rhx = 14f; rhy = 11f; lhx = 14f; lhy = -11f; break;                  // a gun in each hand
                default: rhx = 11f; rhy = 3.5f; lhx = 7f; lhy = -13f + sway; break;          // open palm, a staff or a floating prop
            }
            float gKick, gPump, gHeat; int gLit;
            TankGunPose(p, out gKick, out gPump, out gHeat, out gLit);  // the Scattergun, Flak Cannon and Riot Repeater kick and pump (TankGunLooks.cs)
            float dKick;
            DpsGunPose(p, out dKick);  // the Twin Rifle, Railgun and Burst SMG kick, charge and spin (DpsGunLooks.cs)
            gKick += dKick;
            rhx -= gKick; lhx -= gKick + gPump * wk;
            mPump = gPump; mHeat = gHeat; mLit = gLit;
            bool ownArms = p.Weapon.Id == WeaponId.TwinBlades;  // TwinBladesLook.cs draws the arms and both sabres, held and swung
            if (!ownArms)
            {
                ModelArm(K, 1f, ry * 0.72f, rhx, rhy);
                ModelArm(K, 1f, -ry * 0.72f, lhx, lhy);
            }
            float twist = mDpsOn && p.Weapon.Id == WeaponId.TwinRifle ? 0.12f : 0f;  // the Twin Rifle's guns angle in toward where the streams cross
            MFrame(rhx, rhy, -twist, wk, false);
            float keepA = mAlpha;
            mAlpha *= p.Weapon.Melee && fxOn ? HeldPhase(p.Id) : 1f;  // a melee weapon phases out for its swing and back in after
            if (mAlpha > 0.01f && !ownArms)
            {
                DrawHeld(p, t, hold);
                if (hold == 2) { MFrame(lhx, lhy, twist, wk, true); DrawHeld(p, t, hold); }
            }
            mAlpha = keepA;
            MFrame(0f, 0f, 0f, 1f, false);
            if (!ownArms) { MCirc(rhx, rhy, 3.3f, K.Glove); MCirc(lhx, lhy, 3.3f, K.Glove); }

            ModelHead(p, kit, role, K, RC, r, sh, t);
        }

        void ModelArm(ModelPal K, float sx, float sy, float hx, float hy)
        {
            MLine(sx, sy, hx, hy, 6f, K.Dark);
            MLine(sx, sy, hx, hy, 4.4f, K.Sleeve);
        }

        void ModelBack(Player p, int kit, int role, ModelPal K, Rgba RC, float rx, float ry, float w, float t)
        {
            switch ((Kit)kit)
            {
                case Kit.Soldier:  // a backpack: big plated pack (Tank), med-pack (Healer), slim ammo pouches (DPS)
                {
                    float b = role == 0 ? 1f : role == 1 ? 0.85f : 0.7f;
                    MRect(-18f, -7f * b - 1f, 11f, 14f * b + 2f, K.Dark); MRect(-17f, -6f * b, 9f, 12f * b, K.Base);
                    if (role == 1) { MRect(-14.6f, -1.2f, 4.6f, 2.4f, MWhite); MRect(-13.5f, -2.3f, 2.4f, 4.6f, MWhite); }
                    else if (role == 0) MRect(-19f, -9f, 3f, 18f, RC);
                    else { MLine(-13f, -5f, -13f, 5f, 1.6f, RC); MRect(-15f, -ry + 1f, 5f, 3f, Hx(0x333b44)); MRect(-15f, ry - 4f, 5f, 3f, Hx(0x333b44)); }
                    break;
                }
                case Kit.Elementalist:  // a flared robe, its hem swaying
                    MPoly(K.Dark, -2f, 0f, -2f, -ry - 1f, -13f, -ry - 5f + w, -22f, -9f + w, -27f, 0f, -22f, 9f - w, -13f, ry + 5f - w, -2f, ry + 1f);
                    MPoly(K.Base, -2f, 0f, -2f, -ry + 2f, -12f, -ry - 1f + w, -20f, -6f + w, -24f, 0f, -20f, 6f - w, -12f, ry + 1f - w, -2f, ry - 2f);
                    MLine(-24f, 0f, -10f, 0f, 1.2f, RC);
                    break;
                case Kit.Summoner:  // a mantle of leaves
                    for (int i = 0; i < 7; i++)
                    {
                        float a = MathX.Pi * 0.62f + i * (MathX.Pi * 0.76f / 6f), x = Mathf.Cos(a) * (ry + 1f), y = Mathf.Sin(a) * (ry + 1f);
                        MEll(x * 0.85f - 2f, y * 0.95f, 6.5f, 3.4f, i % 2 == 1 ? K.Base : Hx(0x52965c), a + 1.57f);
                    }
                    break;
                case Kit.Knight:  // a cape in the role's colour
                {
                    Rgba cc = role == 0 ? Hx(0x2c4f99) : role == 1 ? Hx(0xf1efe6) : Hx(0xa8222e);
                    MPoly(cc, -1f, 0f, -1f, -ry * 0.85f, -19f - w, -ry * 0.9f - 1f, -23f - w, 0f, -19f - w, ry * 0.9f + 1f, -1f, ry * 0.85f);
                    MLine(-3f, 0f, -21f - w, 0f, 1.1f, new Rgba(0f, 0f, 0f, 0.25f));
                    if (role == 1) MLine(-3f, 0f, -22f - w, 0f, 2f, MGold);
                    break;
                }
                case Kit.Ninja:  // two scarf ribbons in the role colour
                    for (int k = 0; k < 2; k++)
                    {
                        float px = -8f, py = k == 1 ? 4f : -4f;
                        for (int i = 1; i <= 7; i++)
                        {
                            float f = i / 7f, x = -8f - f * (18f + 5f * k), y = (k == 1 ? 4f : -4f) + Mathf.Sin(t * 8f + i * 0.9f + k) * 4.5f * f;
                            MLine(px, py, x, y, 4f - 2.4f * f, RC); px = x; py = y;
                        }
                    }
                    break;
                default:  // Hemomancer: a tattered cloak, dripping
                {
                    float[] jag = new float[20];
                    jag[0] = -2f; jag[1] = 0f; jag[2] = -2f; jag[3] = -ry;
                    for (int i = 0; i <= 6; i++) { jag[4 + 2 * i] = -16f - (i % 2 == 0 ? 6f : 0f) - w * (i % 2); jag[5 + 2 * i] = -ry + 2f * ry * i / 6f; }
                    jag[18] = -2f; jag[19] = ry;
                    MPoly(K.Dark, jag);
                    MLine(-4f, 0f, -17f, 0f, 1f, new Rgba(0.88f, 0.12f, 0.23f, 0.5f));
                    float d = MathX.Fmod(t * 6f, 8f);
                    MCirc(-18f, 2f, 1.3f, new Rgba(0.88f, 0.12f, 0.23f, 0.8f), 6); MCirc(-20f - d, -3f, 1.1f, new Rgba(0.88f, 0.12f, 0.23f, 0.6f), 6);
                    break;
                }
            }
        }

        void ModelRoleTrim(int role, ModelPal K, Rgba RC, float rx, float ry, float t)
        {
            if (role == 0)  // Tank: plate pauldrons and a chest plate
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    MCirc(0f, s * (ry - 1f), 7.2f, K.Dark, 14); MCirc(0f, s * (ry - 1f), 6f, K.Base, 14);
                    MArc(0f, s * (ry - 1f), 6f, 0f, MathX.Tau, 1.6f, RC);
                }
                MRect(-4f, -8f, 13f, 16f, new Rgba(1f, 1f, 1f, 0.14f));
                MLine(-4f, -8f, 9f, -8f, 1.4f, RC); MLine(9f, -8f, 9f, 8f, 1.4f, RC); MLine(9f, 8f, -4f, 8f, 1.4f, RC); MLine(-4f, 8f, -4f, -8f, 1.4f, RC);
            }
            else if (role == 1)  // Healer: a white stole over the shoulders
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    MLine(5f, s * ry * 0.55f, -14f, s * 2.6f, 3.2f, Hx(0xf4fff6));
                    MLine(-2f, s * ry * 0.4f, -14f, s * 2.6f, 1.2f, RC);
                }
                MLine(-13f, -3f, -13f, 3f, 2.4f, RC);
            }
            else  // DPS: small pauldrons, a sash and a speed streak
            {
                for (int s = -1; s <= 1; s += 2) MCirc(0f, s * (ry - 1f), 4.4f, K.Dark);
                MLine(5f, -ry * 0.65f, -6f, ry * 0.65f, 3.4f, RC);
                float sw = Mathf.Sin(t * 6f);
                MLine(-rx - 2f, -3f, -rx - 9f - sw, -3f, 1.3f, RC); MLine(-rx - 2f, 3f, -rx - 11f - sw, 3f, 1.3f, RC);
            }
        }

        void ModelHead(Player p, int kit, int role, ModelPal K, Rgba RC, float bodyR, float[] sh, float t)
        {
            float x = sh[3], r = sh[2], w = Mathf.Sin(t * 3f) * 1.4f, ry = sh[1];
            if (role == 1) MArc(x, 0f, r + 3.4f, 0f, MathX.Tau, 1.3f, new Rgba(0.35f, 1f, 0.55f, 0.75f));  // the healer's halo
            switch ((Kit)kit)
            {
                case Kit.Soldier:  // a helmet with a glowing visor
                    MCirc(x, 0f, r, K.Dark, 14); MCirc(x, 0f, r - 1.2f, K.Base, 14);
                    MRect(x + r * 0.15f, -r * 0.55f, r * 0.75f, r * 1.1f, Hx(0x10161c)); MLine(x + r * 0.55f, -r * 0.4f, x + r * 0.55f, r * 0.4f, 1.2f, Hx(0x7bd0ff));
                    if (role == 0) { MRect(x - 4f, -r - 1.5f, 8f, 2.8f, K.Dark); MRect(x - 4f, r - 1.3f, 8f, 2.8f, K.Dark); MLine(x - r + 1f, 0f, x - 1f, 0f, 1.8f, RC); }
                    if (role == 1) { MRect(x - 7f, -1.1f, 8f, 2.2f, MWhite); MRect(x - 4.1f, -4f, 2.2f, 8f, MWhite); }
                    if (role == 2) { MLine(x - r + 1f, -3f, x - r - 8f - w, -4f, 2f, RC); MLine(x - r + 1f, 3f, x - r - 10f - w, 4f, 2f, RC); MCirc(x + 3f, -3.2f, 2.1f, Hx(0x7bd0ff), 8); MCirc(x + 3f, 3.2f, 2.1f, Hx(0x7bd0ff), 8); }
                    break;
                case Kit.Elementalist:  // a wide pointed hat seen from above: brim, cone and a gem
                {
                    float br = r + (role == 0 ? 5f : 4f);
                    MCirc(x, 0f, br, K.Dark, 18); MCirc(x, 0f, br - 1.4f, K.Base, 18); MArc(x, 0f, br - 2.6f, 0f, MathX.Tau, 1f, new Rgba(1f, 1f, 1f, 0.18f));
                    MCirc(x - 1.5f, 0f, r * 0.62f, Hx(0x463375), 12); MCirc(x - 1.5f, 0f, r * 0.62f - 1.6f, K.Base, 12);
                    MArc(x, 0f, br - 4.4f, 0f, MathX.Tau, 1.6f, RC);
                    MCirc(x - 1.5f, 0f, 2.6f, role == 0 ? Hx(0xff8a3a) : role == 1 ? Hx(0xffe9a0) : Hx(0xbfe9ff), 8); MCirc(x - 2f, -0.6f, 1f, MWhite, 6);
                    break;
                }
                case Kit.Summoner:  // a hood with a trailing point and glowing eyes; motes circle the body
                    MPoly(K.Dark, x - r + 1f, 0f, x - r + 1f, -5.5f, x - r - 10f - w, 0f, x - r + 1f, 5.5f);
                    MCirc(x, 0f, r + 1.2f, K.Dark, 14); MCirc(x, 0f, r - 0.4f, K.Base, 14);
                    MArc(x, 0f, r - 2f, -0.9f, 0.9f, 1.4f, Hx(0x8fdc98));
                    MCirc(x + r * 0.55f, -3f, 1.3f, Hx(0xc8ffd0), 6); MCirc(x + r * 0.55f, 3f, 1.3f, Hx(0xc8ffd0), 6);
                    if (role == 0) { MLine(x - 1f, -r + 1f, x - 4f, -r - 5f, 1.8f, MBone); MLine(x - 1f, r - 1f, x - 4f, r + 5f, 1.8f, MBone); }
                    if (role == 1) MArc(x, 0f, r - 3.4f, 2.2f, 4.1f, 1.6f, Hx(0xffe9a0));
                    if (role == 2) MLine(x - r, 0f, x - r - 5f, 0f, 2f, RC);
                    for (int k = 0; k < 3; k++)
                        Circle(mO + V2.FromAngle(t * 2f + MathX.Tau * k / 3f) * (bodyR + 7f), 2.5f, Mc(new Rgba(0.6f, 1f, 0.7f, 0.9f)), 8);
                    break;
                case Kit.Knight:  // a steel helm with a crest and a plume streaming behind
                {
                    Rgba cc = role == 0 ? Hx(0x7fa6ff) : role == 1 ? Hx(0xfffbe8) : Hx(0xff5a66);
                    MCirc(x, 0f, r, K.Dark, 14); MCirc(x, 0f, r - 1.2f, K.Base, 14);
                    MArc(x, 0f, r * 0.62f, -0.9f, 0.9f, 2.6f, new Rgba(0f, 0f, 0f, 0.55f));
                    MLine(x - r, 0f, x + r * 0.2f, 0f, role == 0 ? 3.4f : 2.4f, cc);
                    for (int i = 0; i < 4; i++) MLine(x - r - 1f - i * 3f, 0f, x - r - 4f - i * 3f, Mathf.Sin(t * 6f + i) * (1.6f + i * 0.5f), 3f - i * 0.5f, cc);
                    if (role == 1) MArc(x, 0f, r - 2.4f, 0f, MathX.Tau, 1.2f, MGold);
                    if (role == 2) MCirc(x + r * 0.45f, 0f, 2.4f, K.Glove, 8);
                    if (role == 0) { MRect(x - 2f, -r - 2f, 6f, 3f, K.Dark); MRect(x - 2f, r - 1f, 6f, 3f, K.Dark); }
                    break;
                }
                case Kit.Ninja:  // a dark wrap, a headband in the role colour and an eye slit
                    MCirc(x, 0f, r, K.Dark, 14); MCirc(x, 0f, r - 1.2f, K.Base, 14);
                    MArc(x, 0f, r - 2.6f, -1.2f, 1.2f, 3.2f, RC);
                    MLine(x - r + 2f, 0f, x - r - 5f, Mathf.Sin(t * 6f) * 1.5f, 2.4f, RC);
                    MRect(x + r * 0.4f, -r * 0.4f, 1.6f, r * 0.8f, MWhite);
                    if (role == 0) MRect(x + 1f, -3f, 4f, 6f, Hx(0x8795ab));
                    if (role == 1) MRect(x - 1f, -3.5f, 4f, 7f, Hx(0xfff0cf));
                    if (role == 2) MLine(x - 1f, 0f, x - r - 8f, 0f, 2f, K.Base);
                    break;
                default:  // Hemomancer: a crimson hood, a blood-drop gem, drops circling that glow as health falls
                {
                    MPoly(K.Dark, x - r + 2f, 0f, x - r + 2f, -5f, x - r - 8f, 0f, x - r + 2f, 5f);
                    MCirc(x, 0f, r + 0.6f, K.Dark, 14); MCirc(x, 0f, r - 1f, K.Base, 14);
                    MArc(x, 0f, r - 2.4f, -1f, 1f, 1.4f, Hx(0xe0203a));
                    MCirc(x + r * 0.5f, -2.6f, 1.2f, Hx(0xffd0d6), 6); MCirc(x + r * 0.5f, 2.6f, 1.2f, Hx(0xffd0d6), 6);
                    MPoly(Hx(0xc4122e), x - 1f, 0f, x + 1f, 0f, x - 1.4f, -2.2f, x - 3.2f, 0f, x - 1.4f, 2.2f);
                    if (role == 0)
                        for (int i = -3; i <= 3; i++)
                        {
                            float a = MathX.Pi + i * 0.38f;
                            MPoly(MBone, x + Mathf.Cos(a) * (r - 1f), Mathf.Sin(a) * (r - 1f), x + Mathf.Cos(a - 0.15f) * (r + 3f), Mathf.Sin(a - 0.15f) * (r + 3f), x + Mathf.Cos(a + 0.15f) * (r + 3f), Mathf.Sin(a + 0.15f) * (r + 3f));
                        }
                    if (role == 1) MArc(x, 0f, r - 3.8f, 2.2f, 4.1f, 1.4f, MBone);
                    float miss = p.MaxHp > 0f ? MathX.Clamp01(1f - p.Hp / p.MaxHp) : 0f;
                    for (int k = 0; k < 3; k++)
                    {
                        V2 at = mO + V2.FromAngle(t * 1.6f + MathX.Tau * k / 3f) * (bodyR + 8f);
                        DrawDrop(at, 2.6f, V2.FromAngle(t * 1.6f + MathX.Tau * k / 3f + MathX.Pi / 2f), Mc(BloodRed.WithAlpha(0.55f + 0.45f * miss)));
                    }
                    break;
                }
            }
        }

        /// How a weapon is held: 0 one hand, 1 two hands (offX: where the second one grips along the weapon), 2 a gun
        /// in each hand, 3 an open palm, a staff or a floating prop.
        static void HoldOf(WeaponId id, out int hold, out float offX)
        {
            hold = 0; offX = 0f;
            switch (id)
            {
                case WeaponId.Scattergun: case WeaponId.RiotRepeater: case WeaponId.FrostLance: hold = 1; offX = 14f; break;
                case WeaponId.FlakCannon: case WeaponId.Halberd: case WeaponId.BoStaff: case WeaponId.BoneSpear: hold = 1; offX = 10f; break;
                case WeaponId.LightBolt: case WeaponId.BurstSmg: case WeaponId.HolyMace: hold = 1; offX = 8f; break;
                case WeaponId.MendersBow: case WeaponId.Railgun: hold = 1; offX = 18f; break;
                case WeaponId.Flamethrower: case WeaponId.Warhammer: hold = 1; offX = 12f; break;
                case WeaponId.Lance: hold = 1; offX = -4f; break;
                case WeaponId.Greatsword: hold = 1; offX = -8f; break;
                case WeaponId.TwinRifle: case WeaponId.TwinBlades: hold = 2; break;
                case WeaponId.RadiantOrb: case WeaponId.Fireball: case WeaponId.ArcaneMissiles: case WeaponId.ClotBolt: case WeaponId.SanguineOrb: case WeaponId.Implosion:
                case WeaponId.GolemStaff: case WeaponId.SpriteStaff: case WeaponId.Nimbus: case WeaponId.SpiritChalk: hold = 3; break;
            }
        }

        /// The weapon in its own frame: grip at the origin, +x along the aim. Reworked models go here.
        void DrawHeld(Player p, float t, int hold)
        {
            switch (p.Weapon.Id)
            {
                // ---- Soldier
                case WeaponId.Scattergun:
                    MRect(-12f, -2.6f, 12f, 5.2f, MWood); MRect(0f, -3.2f, 16f, 6.4f, MDark); MLine(2f, -1.7f, 29f, -1.7f, 2.4f, MSteel); MLine(2f, 1.7f, 29f, 1.7f, 2.4f, MSteel); MRect(12f - mPump, -4f, 9f, 8f, Hx(0x6a4b2e));
                    break;
                case WeaponId.FlakCannon:
                    MRect(-6f, -4f, 12f, 8f, MDark); MPoly(Hx(0x55606e), 6f, 0f, 6f, -5f, 26f, -6f, 32f, -9f, 32f, 9f, 26f, 6f, 6f, 5f);
                {
                    var band = Hx(0xff9d3a).Mix(new Rgba(1f, 0.95f, 0.75f), mHeat * 0.7f);  // the bands glow hot after a shot
                    MLine(14f, -6f, 14f, 6f, 2f, band); MLine(22f, -7f, 22f, 7f, 2f, band); MCirc(9f, -7f, 4.5f, Hx(0x3b4350));
                    if (mHeat > 0.1f)
                        MLine(32f, -8f, 32f, 8f, 2f, new Rgba(1f, 0.48f, 0.24f, mHeat * 0.7f));
                    break;
                }
                case WeaponId.RiotRepeater:
                    MRect(-8f, -2.5f, 8f, 5f, MWood); MRect(0f, -3.5f, 26f, 7f, MDark); MRect(6f, -5.5f, 14f, 11f, Hx(0x4c5a68));
                    for (int i = 0; i < 4; i++) MLine(8f + i * 3.2f, -4f, 8f + i * 3.2f, 4f, i == mLit ? 1.4f : 1f, i == mLit ? Hx(0x7bd8ff) : Hx(0x1b2026));  // one rib lights as a round cycles
                    MLine(24f, -1.5f, 31f, -1.5f, 2.2f, MSteel);
                    break;
                case WeaponId.LightBolt:  // Repulsion Cannon: a bell-mouthed emitter
                    MRect(-4f, -3f, 10f, 6f, MDark); MPoly(Hx(0x3d6b5a), 6f, 0f, 6f, -4f, 22f, -11f, 22f, 11f, 6f, 4f);
                    for (int i = 0; i < 3; i++) MArc(10f, 0f, 6f + i * 4f, -0.85f, 0.85f, 1.5f, Hx(0x9dffcf));
                    break;
                case WeaponId.Pistol:  // Revolver
                {
                    float kick, spin;
                    bool fx = GunLook(p, out kick, out spin);  // with Visual effects on: it kicks back and the cylinder clicks round (GunBowLooks.cs)
                    if (fx)
                        MFrame(mOx - 4f * kick, mOy, -0.12f * kick, mWs, false);
                    MRect(-5f, -2.2f, 7f, 8f, MWood); MRect(0f, -2.6f, 10f, 5.2f, MDark); MLine(8f, 0f, 17f, 0f, 3f, MSteel); MCirc(5f, 0f, 3.6f, Hx(0x6a7685), 10);
                    if (fx)
                        for (int i = 0; i < 6; i++)
                            MCirc(5f + Mathf.Cos(spin + i * MathX.Tau / 6f) * 2.1f, Mathf.Sin(spin + i * MathX.Tau / 6f) * 2.1f, 0.8f, Hx(0x1b2026), 6);
                    else
                        MCirc(5f, 0f, 1.4f, Hx(0x1b2026), 8);
                    break;
                }
                case WeaponId.MendersBow:
                {
                    if (Juicy)  // Visual effects on: limbs that flex, a string that snaps (GunBowLooks.cs)
                    {
                        DrawBowFlex(p);
                        break;
                    }
                    float draw = p.DrawT > 0f ? MathX.Clamp01(p.DrawT / 1.2f) : 0.35f;
                    MArc(2f, 0f, 16f, -1.05f, 1.05f, 2.6f, Hx(0x7a5a3a));
                    float nock = 4f - 6f * draw;
                    MLine(10f, -13.9f, nock, 0f, 0.8f, Hx(0xeaeaea)); MLine(nock, 0f, 10f, 13.9f, 0.8f, Hx(0xeaeaea));
                    MLine(nock, 0f, 26f, 0f, 1.4f, Hx(0xcfe9d6)); MPoly(Hx(0x59ff8c), 26f, 0f, 26f, -2f, 31f, 0f, 26f, 2f); MCirc(18f, 0f, 2f, Hx(0x59ff8c), 8);
                    break;
                }
                case WeaponId.TwinRifle:
                    if (mDpsOn) { DrawTwinRifleFx(); break; }  // DpsGunLooks.cs
                    MRect(-6f, -2f, 8f, 4f, MWood); MRect(0f, -2.6f, 14f, 5.2f, MDark); MLine(12f, 0f, 26f, 0f, 2.4f, MSteel); MRect(5f, -5f, 8f, 2.6f, Hx(0x7bd0ff));
                    break;
                case WeaponId.Railgun:
                    if (mDpsOn) { DrawRailgunFx(); break; }  // DpsGunLooks.cs
                    MRect(-8f, -3f, 10f, 6f, MDark); MLine(2f, -3f, 38f, -3f, 2.2f, MSteel); MLine(2f, 3f, 38f, 3f, 2.2f, MSteel);
                    for (int i = 0; i < 3; i++) MRect(10f + i * 8f, -4.6f, 3f, 9.2f, Hx(0x52c9ff));
                    MLine(4f, 0f, 36f, 0f, 1.5f, Hx(0xbff3ff));
                    break;
                case WeaponId.BurstSmg:
                    if (mDpsOn) { DrawBurstSmgFx(); break; }  // DpsGunLooks.cs
                    MRect(-6f, -2.5f, 8f, 5f, MDark); MRect(0f, -3.4f, 14f, 6.8f, Hx(0x4a5560)); MRect(4f, 2f, 5f, 8f, Hx(0x22272e));
                    for (int i = 0; i < 3; i++) { float y = Mathf.Sin(t * 9f + i * 2.09f) * 2.2f; MLine(14f, y, 24f, y, 1.8f, MSteel); }
                    break;
                // ---- Elementalist
                case WeaponId.Flamethrower:
                    MRect(-6f, -3f, 10f, 6f, MDark); MRect(2f, -2.2f, 22f, 4.4f, Hx(0x7a3b22)); MCirc(26f, 0f, 3.2f, Hx(0x2a2a2a), 8);
                    MPoly(new Rgba(1f, 0.59f, 0.16f, 0.85f), 28f, 0f, 28f, -2f, 36f + Mathf.Sin(t * 20f) * 2f, 0f, 28f, 2f);
                    MRect(0f, 5f, 14f, 6f, Hx(0xb8442a)); MLine(6f, 5f, 6f, 11f, 1.4f, Hx(0xffd24a));
                    break;
                case WeaponId.FrostLance:
                    MLine(-8f, 0f, 30f, 0f, 2.6f, Hx(0x9ab9d6)); MPoly(Hx(0xbff1ff), 30f, -4f, 44f, 0f, 30f, 4f); MPoly(Hx(0x9fe6ff), 22f, -3f, 27f, -7f, 29f, -3f); MPoly(Hx(0x9fe6ff), 22f, 3f, 27f, 7f, 29f, 3f);
                    MCirc(16f, 0f, 2f, Hx(0xe8fbff), 8);
                    break;
                case WeaponId.StormHammer:
                    MLine(-6f, 0f, 22f, 0f, 3f, MWood); MRect(20f, -8f, 12f, 16f, Hx(0x7d8796)); MRect(26f, -8f, 3f, 16f, Hx(0xffe04a)); MLine(32f, -4f, 36f, -1f, 1.2f, Hx(0xffe04a)); MLine(36f, -1f, 33f, 3f, 1.2f, Hx(0xffe04a));
                    break;
                case WeaponId.RadiantOrb:
                    MCirc(12f, 0f, 10f, Hx(0xffe078, 0.25f), 14); MCirc(12f, 0f, 6.5f, Hx(0xffe9a0), 12); MCirc(12f, 0f, 3.5f, MWhite, 10); MArc(12f, 0f, 9.5f, t * 2f, t * 2f + 2.5f, 1.5f, Hx(0xffd24a));
                    break;
                case WeaponId.ChainLight:
                {
                    MRect(-6f, -2f, 12f, 4f, MSteel);
                    MLine(6f, 0f, 18f, -5f, 1.8f, Hx(0x8fd8ff)); MLine(6f, 0f, 20f, 0f, 1.8f, Hx(0x8fd8ff)); MLine(6f, 0f, 18f, 5f, 1.8f, Hx(0x8fd8ff));
                    float j = Mathf.Sin(t * 30f);
                    MLine(18f, -5f, 21f, -2f + j, 1.2f, MWhite); MLine(21f, -2f + j, 20f, 0f, 1.2f, MWhite); MLine(20f, 0f, 21f, 2f - j, 1.2f, MWhite); MLine(21f, 2f - j, 18f, 5f, 1.2f, MWhite);
                    break;
                }
                case WeaponId.Nimbus:
                {
                    // A short wand under a little storm cloud that flickers with lightning.
                    MLine(-8f, 0f, 14f, 0f, 2.4f, MWood);
                    MCirc(18f, -3f, 5.5f, Hx(0x8a98c4), 10); MCirc(24f, -4f, 4.5f, Hx(0xa7b4dc), 10); MCirc(22f, 2f, 5f, Hx(0x7d8cba), 10); MCirc(26f, 1f, 3.5f, Hx(0xb9c5e8), 8);
                    if (Mathf.Sin(t * 9f) > 0.55f)
                    {
                        MLine(22f, 5f, 20f, 9f, 1.4f, Hx(0xfff3a0));
                        MLine(20f, 9f, 23f, 11f, 1.4f, Hx(0xfff3a0));
                    }
                    break;
                }
                case WeaponId.Fireball:
                    MCirc(12f, 0f, 9f, Hx(0xff781e, 0.3f), 14); MCirc(12f, 0f, 6f, Hx(0xff7a1c), 12); MCirc(12f, 0f, 3.2f, Hx(0xffd35a), 10);
                    for (int i = 0; i < 5; i++) { float a = t * 5f + i * 1.26f; MLine(12f + Mathf.Cos(a) * 5f, Mathf.Sin(a) * 5f, 12f + Mathf.Cos(a) * 10.5f, Mathf.Sin(a) * 10.5f, 2.2f, new Rgba(1f, 0.59f, 0.16f, 0.8f)); }
                    break;
                case WeaponId.ArcLightning:
                {
                    MLine(0f, -4f, 16f, -4f, 2.2f, MSteel); MLine(0f, 4f, 16f, 4f, 2.2f, MSteel); MCirc(16f, -4f, 1.8f, Hx(0xbfe9ff), 6); MCirc(16f, 4f, 1.8f, Hx(0xbfe9ff), 6);
                    float j = Mathf.Sin(t * 35f) * 1.5f;
                    MLine(16f, -4f, 20f, -1f + j, 1.3f, MWhite); MLine(20f, -1f + j, 14f, 1f - j, 1.3f, MWhite); MLine(14f, 1f - j, 16f, 4f, 1.3f, MWhite);
                    break;
                }
                case WeaponId.ArcaneMissiles:
                    MArc(12f, 0f, 11f, 0f, MathX.Tau, 0.8f, Hx(0x8fe3ff, 0.4f));
                    for (int i = 0; i < 3; i++)  // three ice shards circling
                    {
                        float a = t * 2f + i * 2.09f, x = 12f + Mathf.Cos(a) * 11f, y = Mathf.Sin(a) * 11f, dx = -Mathf.Sin(a), dy = Mathf.Cos(a);
                        MPoly(Hx(0xe6fbff), x + dx * 5f, y + dy * 5f, x - dy * 2f, y + dx * 2f, x - dx * 2.2f, y - dy * 2.2f);
                        MPoly(Hx(0x4fa8e0), x + dx * 5f, y + dy * 5f, x + dy * 2f, y - dx * 2f, x - dx * 2.2f, y - dy * 2.2f);
                    }
                    break;
                // ---- Summoner
                case WeaponId.GolemStaff:
                    MLine(-12f, 0f, 28f, 0f, 3.2f, MWood); MPoly(Hx(0x8d8a82), 30f, 0f, 26f, -6f, 34f, -4f, 36f, 3f, 30f, 7f, 24f, 3f); MCirc(30f, 0f, 5f, Hx(0xffb03a, 0.3f), 10); MCirc(30f, 0f, 2.5f, Hx(0xffb03a), 8);
                    break;
                case WeaponId.BoneGuard:
                    MLine(-10f, 0f, 26f, 0f, 3f, MBone); MCirc(30f, 0f, 5.5f, MBone, 10); MCirc(31f, -2f, 1.2f, Hx(0x1b2026), 6); MCirc(31f, 2f, 1.2f, Hx(0x1b2026), 6); MRect(28f, 3.5f, 6f, 2.2f, MBone);
                    for (int i = 0; i < 2; i++) { float a = t * 2.5f + i * MathX.Pi; MCirc(30f + Mathf.Cos(a) * 10f, Mathf.Sin(a) * 10f, 1.8f, MBone, 6); }
                    break;
                case WeaponId.BearTotem:
                    MRect(-4f, -5f, 6f, 10f, Hx(0x8a5a36)); MCirc(2f, 0f, 4.5f, Hx(0x6b4a2e), 10);
                    for (int i = -1; i <= 1; i++) MLine(5f, i * 0.35f * 8f, 14f, i * 0.35f * 14f, 1.8f, Hx(0xf0e6c8));
                    break;
                case WeaponId.SpriteStaff:
                    MLine(-10f, 0f, 24f, 0f, 2.6f, MWood); MArc(28f, 0f, 5.5f, 0f, MathX.Tau, 2f, Hx(0x59d36a)); MCirc(28f, 0f, 3f, Hx(0xb8ffc4), 8);
                    for (int i = 0; i < 2; i++) { float a = t * 3f + i * MathX.Pi; MCirc(28f + Mathf.Cos(a) * 9f, Mathf.Sin(a) * 9f, 1.8f, Hx(0x9dffb0), 6); }
                    break;
                case WeaponId.TreantRod:
                {
                    // A gnarled root staff: dark bark with a light edge, a few thorns, and a green bud curling at the tip.
                    float px = -8f, py = 0f;
                    for (int i = 1; i <= 9; i++)
                    {
                        float x = -8f + i * 4f, y = Mathf.Sin(i * 0.9f) * 1.8f, w = 3.8f - i * 0.16f;
                        MLine(px, py, x, y, w, Hx(0x8a5a2b)); MLine(px, py, x, y, w * 0.35f, Hx(0xc58a48));
                        if (i == 3 || i == 6) MLine(x, y, x + 1.5f, y + (i == 3 ? -5f : 5f), 1.3f, Hx(0xd9c9a0));  // thorns
                        px = x; py = y;
                    }
                    MCirc(-8f, 0f, 3f, Hx(0x4d3217), 8);  // the knotted butt
                    MPoly(Hx(0x59c46a), 28f, 0f, 24f, -6f, 33f, -3f); MPoly(Hx(0x59c46a), 28f, 0f, 24f, 6f, 33f, 3f);  // two leaves
                    MCirc(29f, 0f, 2.6f, Hx(0xcfffb0), 8);
                    break;
                }
                case WeaponId.SpiritChalk:
                {
                    // A stick of chalk with a glowing spirit tip, shedding a few motes.
                    MRect(2f, -3.5f, 18f, 7f, Hx(0xdfeaf5));
                    MRect(2f, -3.5f, 18f, 2.4f, Hx(0xffffff, 0.6f));
                    MCirc(21f, 0f, 4.6f, Hx(0xbfe6ff, 0.5f), 12); MCirc(21f, 0f, 2.6f, MWhite, 8);
                    for (int i = 0; i < 3; i++) { float a = t * 3f + i * 2.1f; MCirc(24f + Mathf.Cos(a) * 5f, Mathf.Sin(a) * 6f, 1.1f, Hx(0xdff3ff), 5); }
                    break;
                }
                case WeaponId.WolfWhistle:
                    MLine(-2f, 0f, 18f, 0f, 2.2f, Hx(0x7a6a55)); MCirc(19f, 0f, 1.6f, Hx(0xe85a5a), 6); MLine(-6f, 6f, -2f, 1f, 0.8f, Hx(0xcfcfcf)); MCirc(-6f, 6f, 3f, MBone, 8); MCirc(-6f, 6f, 1f, Hx(0x3a3a3a), 6);
                    break;
                case WeaponId.WispLantern:
                    MLine(-8f, 0f, 10f, 0f, 2.4f, Hx(0x6b5a45)); MRect(10f, -5f, 10f, 10f, Hx(0x2c3a46)); MRect(12f, -3f, 6f, 6f, Hx(0xaaf0ff));
                    for (int i = 0; i < 3; i++) { float a = t * 2.5f + i * 2.09f; MCirc(15f + Mathf.Cos(a) * 11f, Mathf.Sin(a) * 11f, 2.2f, Hx(0xcffaff), 6); }
                    break;
                case WeaponId.ImpCaller:
                    MLine(-8f, 0f, 18f, 0f, 2.6f, Hx(0x5a2a2a)); MCirc(20f, 0f, 4f, Hx(0xc0392b), 10); MPoly(Hx(0xff6a3a), 19f, -3f, 16f, -8f, 22f, -4f); MPoly(Hx(0xff6a3a), 19f, 3f, 16f, 8f, 22f, 4f);
                    MCirc(22f, -1.5f, 0.9f, Hx(0xffe04a), 6); MCirc(22f, 1.5f, 0.9f, Hx(0xffe04a), 6);
                    for (int i = 0; i < 2; i++) MPoly(Hx(0xff7a3a), 8f, -7f + i * 18f + Mathf.Sin(t * 4f + i) * 1.5f, 13f, -7f + i * 18f, 8f, -5f + i * 18f);
                    break;
                // ---- Knight
                case WeaponId.Warhammer:
                {
                    if (fxOn)
                        break;  // with Visual effects on, MeleeLooks.cs draws it in two hands: held (DrawHammerIdle), raised while charging and swung
                    float c = p.DrawT > 0f ? game.BowCharge(p, p.DrawT) : 0f;  // charging with them off: raised up and back over the shoulder, glowing
                    MFrame(mOx, mOy, -1.9f * c, mWs, false);
                    MLine(-8f, 0f, 30f, 0f, 3.2f, MWood); MRect(28f, -9f, 10f, 18f, Hx(0xb9c4d4)); MPoly(MSteel, 38f, 0f, 38f, -2f, 45f, 0f, 38f, 2f); MPoly(MSteel, 28f, 0f, 28f, -2f, 23f, 0f, 28f, 2f);
                    MLine(28f, -5f, 38f, -5f, 1f, MDark); MLine(28f, 5f, 38f, 5f, 1f, MDark);
                    if (c > 0f) MCirc(33f, 0f, 9f + 5f * c, new Rgba(1f, 0.82f, 0.3f, 0.15f + 0.3f * c), 14);
                    break;
                }
                case WeaponId.Lance:
                    MLine(-12f, 0f, 40f, 0f, 3.6f, Hx(0xc9a35e)); MPoly(MSteel, 40f, 0f, 40f, -3.5f, 56f, 0f, 40f, 3.5f); MCirc(8f, 0f, 7f, Hx(0xaeb9c9), 12); MCirc(8f, 0f, 3f, Hx(0x7c8797), 8);
                    break;
                case WeaponId.Halberd:
                    if (fxOn)
                        break;  // with Visual effects on, HalberdLook.cs draws it in two hands
                    MLine(-10f, 0f, 40f, 0f, 3f, MWood); MPoly(MSteel, 28f, 0f, 30f, -11f, 40f, -8f, 36f, -2f); MPoly(MSteel, 40f, -1.5f, 52f, 0f, 40f, 1.5f); MPoly(MSteel, 30f, 2f, 36f, 7f, 34f, 1f);
                    break;
                case WeaponId.HolyMace:  // Quarterstaff: an iron shaft, steel bands, a lantern head whose crystal and pips follow the Jab stacks
                {
                    int cap = game.StaffCap(p), n = Mathf.Min(cap, (int)p.StaffStacks);
                    float lit = cap > 0 ? (float)n / cap : 0f;
                    MLine(-28f, 0f, 46f, 0f, 4.2f, Hx(0x12151c)); MLine(-28f, 0f, 46f, 0f, 3f, Hx(0x4a5262));
                    MLine(14f, 0f, 15.5f, 0f, 5f, Hx(0x9aa4b8)); MLine(-10f, 0f, -8.5f, 0f, 5f, Hx(0x9aa4b8)); MLine(36f, 0f, 37.5f, 0f, 5f, Hx(0x9aa4b8));
                    MPoly(Hx(0xb9c2d4), -36f, 0f, -28f, -2f, -28f, 2f);
                    MPoly(Hx(0x9aa4b8), 55f, 0f, 51f, -5.5f, 44f, -5.5f, 40f, 0f, 44f, 5.5f, 51f, 5.5f);
                    MPoly(Hx(0x0e1016), 53f, 0f, 50f, -4f, 45f, -4f, 42.5f, 0f, 45f, 4f, 50f, 4f);
                    if (lit > 0f) MCirc(47.5f, 0f, 6f + 6f * lit, Hx(0xffd36a, 0.12f + 0.25f * lit), 12);
                    MPoly(lit > 0f ? Hx(0xffd36a, 0.6f + 0.4f * lit) : Hx(0x6b7388), 47.5f, -3.6f, 51f, 0f, 47.5f, 3.6f, 44f, 0f);
                    for (int i = 0; i < n; i++) { float a = t * 1.4f + i * MathX.Tau / cap; MCirc(47.5f + Mathf.Cos(a) * 9.5f, Mathf.Sin(a) * 9.5f, 1.8f, Hx(0xffd36a), 6); }
                    break;
                }
                case WeaponId.Lightbringer:
                    MLine(2f, 0f, 34f, 0f, 7f, Hx(0xfff0aa, 0.25f)); MLine(2f, 0f, 34f, 0f, 3.6f, Hx(0xfff3c0)); MPoly(Hx(0xfff3c0), 33f, 0f, 33f, -2f, 40f, 0f, 33f, 2f);
                    MLine(2f, -6f, 2f, 6f, 3f, MGold); MLine(-6f, 0f, 2f, 0f, 3f, Hx(0x6b4a2e)); MCirc(-7f, 0f, 2.6f, MGold, 8);
                    break;
                case WeaponId.Censer:
                {
                    float y = Mathf.Sin(t * 2f) * 4f;
                    MLine(0f, 0f, 12f, y, 1.2f, Hx(0xc9b26b)); MCirc(14f, y, 5f, MGold, 10); MCirc(13f, y - 1.5f, 1f, Hx(0x2a1d08), 6); MCirc(15f, y + 1.5f, 1f, Hx(0x2a1d08), 6);
                    MArc(14f, y, 8f, t, t + 1.4f, 1.2f, Hx(0xfff0c8, 0.35f));
                    break;
                }
                case WeaponId.TwinBlades:
                    MLine(-4f, 0f, 0f, 0f, 3f, Hx(0x6b4a2e)); MLine(0f, -4f, 0f, 4f, 2.6f, MGold); MLine(1f, 0f, 22f, 0f, 3f, MSteel); MPoly(MSteel, 21f, 0f, 21f, -1.6f, 26f, 0f, 21f, 1.6f);
                    break;
                case WeaponId.Greatsword:
                    MPoly(Hx(0xd7e0ee), 3f, 0f, 3f, -3.5f, 40f, -3.5f, 46f, 0f, 40f, 3.5f, 3f, 3.5f); MLine(10f, 0f, 40f, 0f, 0.8f, Hx(0x8fa0b8)); MRect(-1f, -9f, 4f, 18f, MGold); MLine(-12f, 0f, -1f, 0f, 3.4f, Hx(0x6b4a2e)); MCirc(-14f, 0f, 3f, MGold, 8);
                    break;
                case WeaponId.Rapier:
                    MLine(2f, 0f, 38f, 0f, 1.6f, Hx(0xeef4ff)); MArc(2f, 0f, 6f, -1.6f, 1.6f, 2f, MGold); MLine(-8f, 0f, 0f, 0f, 2.4f, Hx(0x5a3a2a)); MLine(-1f, -6f, -1f, 6f, 1.4f, MGold);
                    break;
                // ---- Ninja
                case WeaponId.Kusarigama:
                {
                    MLine(-4f, 0f, 12f, 0f, 2.6f, MWood); MArc(14f, -6f, 8f, 0.2f, 1.9f, 2.6f, MSteel);
                    if (p.HookT <= 0f && !p.KusariWhirl)  // (a thrown or whirling weight is drawn in the world: KusarigamaLook.cs)
                        for (int i = 0; i < 9; i++) { float a = t * 1.5f + i * 0.7f; MCirc(-4f + Mathf.Cos(a) * 6f, 8f + Mathf.Sin(a) * 4f, 1f, Hx(0xb8c2d0), 6); }
                    break;
                }
                case WeaponId.BoStaff:
                    MLine(-22f, 0f, 26f, 0f, 3.2f, MWood); MCirc(26f, 0f, 2.2f, Hx(0x1d1d26), 6); MCirc(-22f, 0f, 2.2f, Hx(0x1d1d26), 6); MLine(-2f, -2f, -2f, 2f, 1.4f, Hx(0x1d1d26)); MLine(14f, -2f, 14f, 2f, 1.4f, Hx(0x1d1d26));
                    break;
                case WeaponId.FumaShuriken:
                    MStar(12f, 0f, 11f, 3.5f, 4, t * 2f, MSteel); MCirc(12f, 0f, 2.4f, Hx(0x1b2026), 8);
                    break;
                case WeaponId.MedicineDarts:
                    for (int i = -1; i <= 1; i++) { float a = i * 0.3f; MLine(0f, 0f, 12f * Mathf.Cos(a), 12f * Mathf.Sin(a), 1.3f, MSteel); MCirc(12f * Mathf.Cos(a), 12f * Mathf.Sin(a), 1.3f, Hx(0x7fffa3), 6); }
                    break;
                case WeaponId.SpiritFan:
                    for (int i = -4; i < 4; i++) MPoly(Hx(0xbefad7, 0.85f), 0f, 0f, 16f * Mathf.Cos(i * 0.25f), 16f * Mathf.Sin(i * 0.25f), 16f * Mathf.Cos((i + 1) * 0.25f), 16f * Mathf.Sin((i + 1) * 0.25f));
                    for (int i = -2; i <= 2; i++) MLine(0f, 0f, 16f * Mathf.Cos(i * 0.5f), 16f * Mathf.Sin(i * 0.5f), 0.8f, Hx(0x4a8a66));
                    MArc(0f, 0f, 16f, -1f, 1f, 1.4f, Hx(0x7ad0a0));
                    break;
                case WeaponId.Ofuda:
                    MFrame(mOx, mOy, Mathf.Sin(t * 3f) * 0.1f, mWs, false);
                    MRect(4f, -4.5f, 13f, 9f, Hx(0xfff0cf)); MRect(8f, -1.5f, 6f, 3f, Hx(0xd93b3b));
                    MFrame(mOx, mOy, 0f, mWs, false);
                    MRect(14f, -13f + Mathf.Sin(t * 2f), 8f, 5f, Hx(0xfff0cf, 0.6f)); MRect(18f, 9f + Mathf.Sin(t * 2.4f), 8f, 5f, Hx(0xfff0cf, 0.6f));
                    break;
                case WeaponId.Shuriken:
                    MStar(6f, 0f, 6.5f, 2f, 4, t * 4f, MSteel);
                    break;
                case WeaponId.Katana:
                    MLine(-6f, 0f, 0f, 0f, 3f, Hx(0x1d1d26)); MCirc(1f, 0f, 3.4f, MGold, 8); MCirc(1f, 0f, 1.4f, Hx(0x1d1d26), 6);
                    if (p.KatanaMeter >= Game.KatanaMeterMax)  // strike meter full: sheathed, ready to cut, a dark scabbard with a glint running along it
                    {
                        MLine(1f, 0f, 36f, 0f, 3.2f, Hx(0x16131f));
                        float gl = MathX.Fmod(t * 1.6f, 1f) * 35f + 1f, sp = 0.5f + 0.5f * Mathf.Sin(t * 12f);
                        MLine(gl - 3f, 0f, gl + 3f, 0f, 1.5f, new Rgba(1f, 1f, 1f, sp)); MLine(gl, -3f, gl, 3f, 1.5f, new Rgba(1f, 1f, 1f, sp));
                    }
                    else { MLine(1f, 0f, 36f, 0f, 2.4f, Hx(0xdfe7f2)); MLine(2f, -0.9f, 35f, -0.9f, 0.7f, MWhite); MPoly(Hx(0xdfe7f2), 35f, 0f, 35f, -1.2f, 40f, 0f, 35f, 1.2f); }
                    break;
                case WeaponId.Kunai:
                    MLine(-1f, 0f, 5f, 0f, 2.2f, Hx(0x2a2a2a)); MArc(-4f, 0f, 3f, 0f, MathX.Tau, 1.2f, MSteel); MPoly(MSteel, 5f, 0f, 10f, -3f, 18f, 0f, 10f, 3f);
                    break;
                // ---- Hemomancer
                case WeaponId.BloodLash:
                {
                    MLine(-4f, 0f, 6f, 0f, 3f, Hx(0x3a0a10));
                    float px = 6f, py = 0f;
                    for (int i = 1; i <= 9; i++) { float x = 6f + i * 3.6f, y = Mathf.Sin(t * 8f + i * 0.9f) * 2.8f * i / 9f; MLine(px, py, x, y, 3f - i * 0.25f, Hx(0xe0203a)); px = x; py = y; }
                    MCirc(px, py, 1.5f, Hx(0xff9aa6), 6);
                    break;
                }
                case WeaponId.ClotBolt:
                    MCirc(12f, 0f, 7f, Hx(0x3a0710), 12); MCirc(11f, -2f, 3f, Hx(0xa8182b), 8); MCirc(14f, 2f, 2f, Hx(0xe0203a), 8); MCirc(12f, 8f + MathX.Fmod(t * 8f, 4f), 1.4f, Hx(0xc4122e), 6);
                    break;
                case WeaponId.LeechBeam:  // Leech Tendrils
                    MCirc(2f, 0f, 3.4f, Hx(0x3a0710), 8);
                    for (int k = -1; k <= 1; k++)
                    {
                        float px = 3f, py = 0f;
                        for (int i = 1; i <= 7; i++) { float x = 3f + i * 2.6f, y = k * i * 1.4f + Mathf.Sin(t * 7f + i * 1.1f + k * 2f) * 1.3f * i / 7f; MLine(px, py, x, y, 2.4f - i * 0.22f, Hx(0xe0203a)); px = x; py = y; }
                    }
                    break;
                case WeaponId.TransfusionDarts:
                    MLine(-4f, 0f, 0f, 0f, 2f, Hx(0x999999)); MRect(0f, -2.6f, 16f, 5.2f, Hx(0xdddddd)); MRect(2f, -1.6f, 10f, 3.2f, Hx(0xd71f3b)); MLine(16f, 0f, 26f, 0f, 1.2f, MSteel);
                    MLine(13f, -1.6f, 13f, 1.6f, 1f, Hx(0x59ff8c)); MLine(11.4f, 0f, 14.6f, 0f, 1f, Hx(0x59ff8c));
                    break;
                case WeaponId.SanguineOrb:
                {
                    MCirc(12f, 0f, 10f, Hx(0xc81428, 0.25f), 14); MCirc(12f, 0f, 7f, Hx(0x8e0f22), 12); MArc(12f, 0f, 4.5f, t * 3f, t * 3f + 3.5f, 1.6f, Hx(0xff6b7e));
                    for (int i = 0; i < 3; i++) { float a = -t * 2f + i * 2.09f; MCirc(12f + Mathf.Cos(a) * 10f, Mathf.Sin(a) * 10f, 1.6f, Hx(0xe0203a), 6); }
                    // The heal reserve: an arc round you filling as you coat enemies, glowing once there's enough to coat an ally.
                    float full = MathX.Clamp01(p.BloodReserve / Game.ReserveMax);
                    bool ready = p.BloodReserve >= Game.AllyCoatCost;
                    float r = p.Def.BodyR;
                    Arc(p.Disp, r + 5f, 2.5f, Mc(BloodDark.WithAlpha(0.6f)), 20, MathX.Pi * 0.2f, MathX.Pi * 0.6f);
                    if (full > 0f)
                        Arc(p.Disp, r + 5f, 2.5f, Mc((ready ? new Rgba(1f, 0.35f, 0.45f) : BloodRed).WithAlpha(ready ? 0.8f + 0.2f * Mathf.Sin(t * 8f) : 0.8f)), 20,
                            MathX.Pi * 0.8f - MathX.Pi * 0.6f * full, MathX.Pi * 0.6f * full);
                    break;
                }
                case WeaponId.VeinTether:
                    MArc(6f, 0f, 10f, -1.3f, -0.2f, 2.8f, MBone); MArc(6f, 0f, 10f, 0.2f, 1.3f, 2.8f, MBone); MCirc(8f, 0f, 3.8f, Hx(0xc4122e), 10); MCirc(7f, -1f, 1.2f, Hx(0xff9aa6), 6); MLine(-6f, 0f, 5f, 0f, 1.4f, Hx(0xe0203a));
                    break;
                case WeaponId.BloodBolts:
                    MPoly(Hx(0xb3122a), 2f, 0f, 10f, -3f, 22f, 0f, 10f, 3f); MPoly(Hx(0x7a0c1d), 4f, 3f, 10f, 8f, 14f, 4f); MPoly(Hx(0x7a0c1d), 4f, -3f, 10f, -8f, 14f, -4f); MLine(4f, 0f, 20f, 0f, 1f, Hx(0xff8896));
                    break;
                case WeaponId.Implosion:
                    MCirc(2f, 0f, 4.2f, Hx(0x3a0710), 8);
                    for (int i = 0; i < 3; i++) { float rr = 14f - MathX.Fmod(t * 10f + i * 4.7f, 14f); MArc(14f, 0f, Mathf.Max(rr, 1f), 0f, MathX.Tau, 1.4f, new Rgba(0.88f, 0.12f, 0.23f, 0.2f + 0.6f * (1f - rr / 14f))); }
                    MCirc(14f, 0f, 2.4f, Hx(0x12020a), 8);
                    break;
                case WeaponId.BoneSpear:
                    MLine(-14f, 0f, 34f, 0f, 3f, MBone); MLine(0f, -1.6f, 0f, 1.6f, 1f, Hx(0xb9b09a)); MLine(12f, -1.6f, 12f, 1.6f, 1f, Hx(0xb9b09a));
                    MPoly(MBone, 34f, 0f, 34f, -4.5f, 48f, 0f, 34f, 4.5f); MPoly(MBone, 36f, -4.5f, 34f, -8f, 39f, -4f); MPoly(MBone, 36f, 4.5f, 34f, 8f, 39f, 4f); MLine(34f, 0f, 46f, 0f, 1.4f, Hx(0xb3122a));
                    break;
            }
        }
    }
}
