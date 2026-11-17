using UnityEngine;

namespace Arena
{
    /// The Engineer's player model and followers, mockup (Docs/Engineer.md): built like every other class in
    /// CharacterModels.cs (back piece, shoulders, the role's trim, arms, the held weapon, head; local frame in Tank units,
    /// x along the aim), three looks to choose from:
    ///   0 Hi-vis: navy coveralls, an orange safety vest with silver tape, a yellow hard hat with a headlamp, a tool pack
    ///     with a spool of cyan vector cable;
    ///   1 Drafter: teal-grey jumpsuit with glowing cyan seams, a white hard hat with a cyan visor band, a drafting tube;
    ///   2 Welder: rust leather, a welding mask with a lit slit, twin gas tanks.
    /// The weapon, the Vector Forge: a chunky rivet gun with a rivet hopper and a cyan emitter at the nose.
    /// Followers (the Strider): 0 the vector wireframe walker (EngineerLook.cs), 1 a plated walker, 2 a four-legged spider rig.
    /// Only Tools/Preview draws these yet (the `englook` mode, ShotEngineer.cs).
    public partial class ArenaApp
    {
        struct EngPal { public ModelPal K; public Rgba Hat, HatDark, Accent; }

        static EngPal EPal(int b, int d, int s, int g, int hat, int hatD, int acc) =>
            new EngPal { K = Pal(b, d, s, g), Hat = Hx(hat), HatDark = Hx(hatD), Accent = Hx(acc) };

        static readonly EngPal[] EngPals =
        {
            EPal(0x2c3e5c, 0x172238, 0x354a6e, 0xd98a2b, 0xf2c230, 0xa77f12, 0xff7a1f),  // Hi-vis
            EPal(0x2b4a4f, 0x142629, 0x335a60, 0x1c2b2e, 0xe9eef2, 0x8d99a3, 0x5ce1ff),  // Drafter
            EPal(0x8a5228, 0x45260f, 0x734420, 0x3a2a1c, 0x3a4250, 0x1c2129, 0xffb547),  // Welder
        };
        static readonly Rgba EngCyan = new Rgba(0.36f, 0.88f, 1f), EngSteel = Hx(0x8a96a8), EngIron = Hx(0x4a5361);

        void EngFrame(V2 at, float aim, float bodyR, float t)
        {
            mO = at; mAim = aim; mDir = V2.FromAngle(aim); mSide = new V2(-mDir.Y, mDir.X); mU = bodyR / 18f; mT = t;
            mAlpha = 1f; mWhite = 0f; mTintA = 0f;
            MFrame(0f, 0f, 0f, 1f, false);
        }

        /// The Engineer as a DPS: `fire` 0-1 is the rivet gun's kick.
        internal void DrawEngineerModel(V2 at, float aim, float bodyR, int look, float t, float fire = 0f)
        {
            var P = EngPals[look];
            var K = P.K;
            Rgba RC = Defs.Get(Role.Dps).Color;
            EngFrame(at, aim, bodyR, t);
            float rx = 9f, ry = 12.5f;
            MEll(1f, 2f, rx + 2f, ry + 1f, new Rgba(0f, 0f, 0f, 0.28f));

            // the back piece
            if (look == 0)  // tool pack and a cable spool
            {
                MRect(-18f, -8f, 11f, 16f, K.Dark); MRect(-17f, -7f, 9f, 14f, K.Base);
                MCirc(-12.5f, 0f, 5.4f, K.Dark, 16); MArc(-12.5f, 0f, 3.9f, 0f, MathX.Tau, 2.2f, EngCyan); MCirc(-12.5f, 0f, 1.5f, EngSteel, 8);
                MLine(-12.5f, 3.9f, -6f, ry * 0.75f, 1.1f, EngCyan.WithAlpha(0.8f));
                MRect(-18f, -ry + 1f, 4f, 3f, P.Accent); MRect(-18f, ry - 4f, 4f, 3f, P.Accent);
            }
            else if (look == 1)  // a drafting tube slung across the back, a holo emitter
            {
                MLine(-15f, -ry - 3f, -9f, ry + 3f, 6.4f, K.Dark); MLine(-15f, -ry - 3f, -9f, ry + 3f, 4.6f, Hx(0x5d7c82));
                MCirc(-15f, -ry - 3f, 3.2f, EngIron, 10); MCirc(-9f, ry + 3f, 3.2f, EngIron, 10);
                MCirc(-15f, -ry - 3f, 1.4f, EngCyan, 8); MCirc(-9f, ry + 3f, 1.4f, EngCyan, 8);
                MCirc(-17f, 0f, 3f, EngCyan.WithAlpha(0.25f), 10); MCirc(-17f, 0f, 1.6f, EngCyan, 8);
            }
            else  // twin gas tanks and a hose
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    MRect(-21f, s > 0 ? 1f : -9f, 11f, 8f, K.Dark); MRect(-20.2f, s > 0 ? 1.8f : -8.2f, 9.4f, 6.4f, Hx(0x6e7480));
                    MCirc(-10.5f, s * 5f, 2f, EngIron, 8);
                }
                MLine(-10f, 5f, -2f, ry * 0.85f, 1.4f, Hx(0x2a2f36));
            }

            // shoulders and the costume over them
            MEll(-1f, 0f, rx, ry, K.Dark); MEll(-1f, 0f, rx - 1.6f, ry - 1.6f, K.Base);
            if (look == 0)  // the safety vest: orange panels with silver tape
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    MPoly(P.Accent, -8f, s * 3f, -8f, s * (ry - 2.5f), 3f, s * (ry - 3.5f), 3f, s * 3f);
                    MLine(-8f, s * (ry - 5f), 3f, s * (ry - 5.6f), 1.3f, Hx(0xdfe6ee));
                }
            }
            else if (look == 1)  // glowing cyan seams
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    MLine(-7f, s * (ry - 3f), 3f, s * (ry - 4f), 1.1f, EngCyan);
                    MLine(-7f, s * 4f, -7f, s * (ry - 3f), 1.1f, EngCyan.WithAlpha(0.7f));
                }
            }
            else  // a leather apron band and buckles
            {
                MRect(-3f, -ry + 2f, 6f, 2f * ry - 4f, K.Dark);
                MRect(-1.6f, -ry + 3f, 3.2f, 2.6f, Hx(0xc9a043)); MRect(-1.6f, ry - 5.6f, 3.2f, 2.6f, Hx(0xc9a043));
            }
            ModelRoleTrim(2, K, RC, rx, ry, t, true);

            // arms on the Vector Forge (two hands: grip and foregrip)
            float kick = fire * 3f;
            float rhx = 12f - kick, rhy = 5f, lhx = 20f - kick, lhy = 2.5f;
            ModelArm(K, 1f, ry * 0.72f, rhx, rhy);
            ModelArm(K, 1f, -ry * 0.72f, lhx, lhy);
            MFrame(rhx, rhy, 0f, 1f, false);
            DrawVectorForge(t, fire);
            MFrame(0f, 0f, 0f, 1f, false);
            MCirc(rhx, rhy, 3.3f, K.Glove); MCirc(lhx, lhy, 3.3f, K.Glove);

            // the head
            float hx = 2.5f, hr = 8.4f;
            if (look == 2)  // a welding mask with a lit slit
            {
                MCirc(hx, 0f, hr, P.HatDark, 14); MCirc(hx, 0f, hr - 1.2f, P.Hat, 14);
                MRect(hx + 2f, -5.5f, 5.5f, 11f, Hx(0x0c0f13));
                MLine(hx + 5f, -4.5f, hx + 5f, 4.5f, 1.6f, P.Accent); MLine(hx + 5f, -4.5f, hx + 5f, 4.5f, 4f, P.Accent.WithAlpha(0.2f));
                MLine(hx - hr + 1f, -2f, hx - hr + 1f, 2f, 2f, Hx(0x2a2f36));
            }
            else  // a hard hat: brim, shell, the ridge front to back
            {
                MEll(hx + 1f, 0f, hr + 1.8f, hr + 0.9f, P.HatDark);
                MEll(hx + 1f, 0f, hr + 0.8f, hr - 0.1f, P.Hat);
                MCirc(hx - 0.6f, 0f, hr - 1.4f, P.HatDark, 16); MCirc(hx - 0.6f, 0f, hr - 2.4f, P.Hat, 16);
                MLine(hx - hr + 2f, 0f, hx + hr - 1f, 0f, 2.2f, P.HatDark);
                MLine(hx - hr + 2.4f, 0f, hx + hr - 1.6f, 0f, 0.9f, MWhite.WithAlpha(0.45f));
                if (look == 0)  // a headlamp
                {
                    MCirc(hx + hr + 0.6f, 0f, 2.4f, Hx(0x2a2f36), 10); MCirc(hx + hr + 0.9f, 0f, 1.5f, Hx(0xfff4c2), 8);
                    MPoly(new Rgba(1f, 0.95f, 0.7f, 0.08f), hx + hr + 1f, 0f, hx + hr + 26f, -8f, hx + hr + 26f, 8f);
                }
                else  // a cyan visor band at the front
                {
                    MArc(hx - 0.6f, 0f, hr - 0.8f, -0.75f, 0.75f, 2.2f, EngCyan);
                    MArc(hx - 0.6f, 0f, hr - 0.8f, -0.75f, 0.75f, 5f, EngCyan.WithAlpha(0.18f));
                }
            }
        }

        /// The Vector Forge in its weapon frame: grip at (0,0), +x along the aim.
        void DrawVectorForge(float t, float fire)
        {
            MRect(-5f, -4.5f, 18f, 9f, Hx(0x1c2129));          // body
            MRect(-4f, -3.5f, 16f, 7f, Hx(0x5d6878));
            MRect(-2f, 1.5f, 4f, 6f, Hx(0x1c2129));             // pistol grip
            MRect(1f, -8.5f, 9f, 4.4f, Hx(0x1c2129));           // rivet hopper on top
            for (int i = 0; i < 4; i++) MCirc(2.4f + i * 2.1f, -6.3f, 0.9f, EngSteel, 6);
            MRect(13f, -3f, 6f, 6f, Hx(0x1c2129));              // nose
            MLine(3f, -3.5f, 3f, 3.5f, 0.8f, EngCyan.WithAlpha(0.8f)); MLine(6f, -3.5f, 6f, 3.5f, 0.8f, EngCyan.WithAlpha(0.8f));
            float glow = 0.6f + 0.4f * Mathf.Sin(t * 5f) + fire;
            MCirc(19.5f, 0f, 3.6f, EngCyan.WithAlpha(0.18f * glow), 12);
            MCirc(19.5f, 0f, 2.2f, EngCyan, 10); MCirc(19.5f, 0f, 1f, MWhite, 6);
            if (fire > 0f)
            {
                MCirc(23f, 0f, 4.5f * fire, MWhite.WithAlpha(0.7f * fire), 10);
                MLine(21f, 0f, 30f, 0f, 2.4f * fire, EngCyan.WithAlpha(fire));
            }
        }

        /// A rivet in flight (the Vector Forge's shot): a steel slug with a short cyan trail.
        internal void DrawRivet(V2 at, float ang, float scale)
        {
            V2 d = V2.FromAngle(ang), s = new V2(-d.Y, d.X);
            Line(at - d * (22f * scale), at, 3f * scale, EngCyan.WithAlpha(0.3f));
            Line(at - d * (12f * scale), at, 1.6f * scale, EngCyan);
            SetColor(EngSteel); Quad(at - d * 4f * scale - s * 2.2f * scale, at + d * 3f * scale - s * 2.2f * scale, at + d * 3f * scale + s * 2.2f * scale, at - d * 4f * scale + s * 2.2f * scale);
            Circle(at + d * 3f * scale, 2.6f * scale, Hx(0xcfd8e6), 10);
        }

        /// The follower in one of its looks: 0 the wireframe walker, 1 plated, 2 spider rig. `scale` 1 = the Strider's size.
        internal void DrawStriderLook(int look, V2 at, float face, float step, float punch, float hp, bool locked, float t, float scale = 1f)
        {
            if (look == 0)
            {
                DrawStrider(at, face, step, punch, 1f, hp, locked, -1f, t);
                return;
            }
            EngFrame(at, face, 18f * StriderScale * scale * 1.2f, t);
            float l = Mathf.Sin(step) * 6f, p = punch * 9f;
            Rgba hull = Hx(0x34445e), hullD = Hx(0x161e2c), hz = Hx(0xffb547);
            Rgba ink = locked ? EngInkHot : EngInk;
            if (look == 1)  // plated walker
            {
                MEll(1f, 2f, 17f, 22f, new Rgba(0f, 0f, 0f, 0.3f));
                for (int s = -1; s <= 1; s += 2)  // legs: hip, knee, a foot pad
                {
                    float sl = s * l;
                    MLine(-2f, s * 11f, 2f + sl * 0.4f, s * 18f, 5.4f, hullD); MLine(2f + sl * 0.4f, s * 18f, 6f + sl, s * 21f, 4.6f, hullD);
                    MLine(-2f, s * 11f, 2f + sl * 0.4f, s * 18f, 3.4f, EngIron); MLine(2f + sl * 0.4f, s * 18f, 6f + sl, s * 21f, 3f, EngIron);
                    MEll(7f + sl, s * 22f, 4.6f, 3f, hullD); MEll(7f + sl, s * 22f, 3.6f, 2.1f, EngSteel);
                }
                for (int s = -1; s <= 1; s += 2)  // piston arms
                {
                    float ps = s < 0 ? p : p * 0.4f;
                    MLine(4f, s * 11f, 13f + ps, s * 15f, 5f, hullD); MLine(4f, s * 11f, 13f + ps, s * 15f, 3.4f, EngSteel);
                    MRect(13f + ps, s > 0 ? 11.5f : -18.5f, 7f, 7f, hullD); MRect(14f + ps, s > 0 ? 12.5f : -17.5f, 5f, 5f, Hx(0x6e7a8c));
                    MLine(15f + ps, s * 15f, 19f + ps, s * 15f, 1f, ink.WithAlpha(0.8f));
                }
                MPoly(hullD, -12f, -9f, -9f, -12f, 7f, -12f, 11f, -8f, 11f, 8f, 7f, 12f, -9f, 12f, -12f, 9f);  // hull
                MPoly(hull, -10.8f, -8f, -8.3f, -10.6f, 6.4f, -10.6f, 9.6f, -7.3f, 9.6f, 7.3f, 6.4f, 10.6f, -8.3f, 10.6f, -10.8f, 8f);
                for (int i = 0; i < 4; i++)  // hazard stripes on the back
                    MLine(-10.2f, -7f + i * 4.4f, -7.6f, -4.6f + i * 4.4f, 1.6f, hz);
                MLine(-6f, -7f, -6f, 7f, 1f, ink.WithAlpha(0.7f));  // vector seams
                MLine(-6f, 0f, 4f, 0f, 1f, ink.WithAlpha(0.7f));
                MRect(4f, -6f, 6.6f, 12f, Hx(0x0c1118));  // visor
                MLine(9f, -4.6f, 9f, 4.6f, 2.2f, ink); MLine(9f, -4.6f, 9f, 4.6f, 6f, ink.WithAlpha(0.2f));
                MLine(-12f, 3f, -17f, 6f, 1.2f, EngIron);  // antenna
                MCirc(-17f, 6f, 1.4f, Mathf.Sin(t * 6f) > 0f ? hz : EngIron, 6);
            }
            else  // spider rig
            {
                MEll(0f, 0f, 20f, 20f, new Rgba(0f, 0f, 0f, 0.28f));
                for (int k = 0; k < 4; k++)
                {
                    float a = (k < 2 ? 0.75f : 2.35f) * (k % 2 == 0 ? 1f : -1f);
                    float sw = Mathf.Sin(step + k * 1.6f) * 0.2f;
                    V2 d = V2.FromAngle(a + sw);
                    float kx = d.X * 14f, ky = d.Y * 14f, fx = d.X * 22f + (k < 2 ? 3f : -3f), fy = d.Y * 24f;
                    MLine(d.X * 6f, d.Y * 6f, kx, ky, 4.4f, hullD); MLine(kx, ky, fx, fy, 3.6f, hullD);
                    MLine(d.X * 6f, d.Y * 6f, kx, ky, 2.8f, EngSteel); MLine(kx, ky, fx, fy, 2.2f, EngIron);
                    MCirc(kx, ky, 2.4f, hullD, 8); MCirc(fx, fy, 1.8f, EngSteel, 6);
                }
                MLine(6f, 0f, 16f + p, 0f, 4.4f, hullD); MLine(6f, 0f, 16f + p, 0f, 2.8f, EngSteel);  // the claw arm
                MLine(16f + p, 0f, 21f + p, -3.5f, 2.2f, EngSteel); MLine(16f + p, 0f, 21f + p, 3.5f, 2.2f, EngSteel);
                MCirc(0f, 0f, 11.5f, hullD, 18); MCirc(0f, 0f, 10f, hull, 18);
                MArc(0f, 0f, 7.2f, 0f, MathX.Tau, 1.2f, ink.WithAlpha(0.8f));
                MArc(0f, 0f, 7.2f, t * 2f, t * 2f + 1.2f, 2.4f, ink);
                MCirc(-4f, 0f, 2.6f, hz, 8); MCirc(-4f, 0f, 1.2f, hullD, 6);
                MCirc(8f, 0f, 3.6f, Hx(0x0c1118), 10); MCirc(8f, 0f, 2f, ink, 8); MCirc(8f, 0f, 5.5f, ink.WithAlpha(0.15f), 10);
            }
            MFrame(0f, 0f, 0f, 1f, false);
            if (hp >= 0f)
            {
                float bw = 46f * scale, y = -34f * StriderScale * scale;
                FillRect(at.X - bw / 2f, at.Y + y, bw, 4f, new Rgba(0.08f, 0.12f, 0.18f, 0.9f));
                FillRect(at.X - bw / 2f, at.Y + y, bw * Mathf.Clamp01(hp), 4f, hp < 0.3f ? EngHostile : EngInk);
            }
        }

        /// One cell of a Tools/Preview sheet, drawn at the origin (UiModelAt places and zooms it). sheet: 0 the Engineer's looks,
        /// 1 the follower's looks, 2 the kit (weapon fire, rivets, Drag Pylon, Breach hammer).
        internal void DrawEngineerSheetCell(int sheet, int i, float t)
        {
            if (sheet == 0)
                DrawEngineerModel(new V2(-6f, 0f), -0.35f, Defs.Get(Role.Dps).BodyR, i, t, 0f);
            else if (sheet == 1)
                DrawStriderLook(i, V2.Zero, -0.35f, 1.1f, 0.6f, 0.8f, false, t);
            else if (i == 0)  // firing the Vector Forge, rivets in flight
            {
                DrawEngineerModel(new V2(-26f, 6f), 0f, Defs.Get(Role.Dps).BodyR, 0, t, 0.8f);
                DrawRivet(new V2(14f, 6f), 0f, 0.6f); DrawRivet(new V2(34f, 6f), 0f, 0.6f);
            }
            else if (i == 1)
                DrawDragPylon(V2.Zero, 2f, 0f, t);
            else
            {
                var m = new EngineerMock { WallPt = new V2(0f, -75f), Out = new V2(0f, -1f), BreachT = 0.7f };
                DrawBreach(m, t);
            }
        }
    }
}
