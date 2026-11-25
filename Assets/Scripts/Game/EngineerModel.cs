using UnityEngine;

namespace Arena
{
    /// The Engineer's player model, mockup (Docs/Engineer.md): built like every other class in
    /// CharacterModels.cs (back piece, shoulders, the role's trim, arms, the held weapon, head; local frame in Tank units,
    /// x along the aim), three looks to choose from:
    ///   0 Hi-vis: navy coveralls, an orange safety vest with silver tape, a yellow hard hat with a headlamp, a tool pack
    ///     with a spool of cyan vector cable;
    ///   1 Drafter: teal-grey jumpsuit with glowing cyan seams, a white hard hat with a cyan visor band, a drafting tube;
    ///   2 Welder: rust leather, a welding mask with a lit slit, twin gas tanks.
    /// The robots' rivet gun (tool 0): a chunky rivet gun with a rivet hopper and a cyan emitter at the nose.
    /// In matches: look A (picked Nov 17); Tools/Preview draws all three (the `englook` mode, ShotEngineer.cs).
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
            EPal(0x7d8a9c, 0x2a313c, 0x5d6878, 0x3a4250, 0xb9c4d2, 0x55606e, 0x5ce1ff),  // Robot (look 3: the robot Engineers, EngineerBotLook.cs)
        };
        static readonly Rgba EngCyan = new Rgba(0.36f, 0.88f, 1f), EngSteel = Hx(0x8a96a8), EngIron = Hx(0x4a5361);

        void EngFrame(V2 at, float aim, float bodyR, float t, float alpha = 1f, float white = 0f)
        {
            mO = at; mAim = aim; mDir = V2.FromAngle(aim); mSide = new V2(-mDir.Y, mDir.X); mU = bodyR / 18f; mT = t;
            mAlpha = alpha; mWhite = white; mTintA = 0f;
            MFrame(0f, 0f, 0f, 1f, false);
        }

        /// The Engineer as a DPS: `fire` 0-1 is the rivet gun's kick.
        /// `tool`: what it holds: 0 the rivet gun, 1 the Iron Fist, 2 the Stasis Gun, 3 the Vector Blade (EngineerBotLook.cs); `fire` is then
        /// the punch, the stasis beam's strength or the swipe.
        internal void DrawEngineerModel(V2 at, float aim, float bodyR, int look, float t, float fire = 0f, float alpha = 1f, float white = 0f, int role = 2, int tool = 0)
        {
            var P = EngPals[look];
            var K = P.K;
            Rgba RC = Defs.Get((Role)role).Color;
            EngFrame(at, aim, bodyR, t, alpha, white);
            float[] sh = ModelBuild[role];
            float rx = sh[0], ry = sh[1];
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
            else if (look == 3)  // a robot: a power cell with a cyan core
            {
                MRect(-19f, -7f, 12f, 14f, K.Dark); MRect(-18f, -6f, 10f, 12f, EngIron);
                MCirc(-13f, 0f, 4.2f, EngCyan.WithAlpha(0.25f + 0.15f * Mathf.Sin(t * 4f)), 14); MCirc(-13f, 0f, 2.4f, EngCyan, 10);
                MLine(-18f, -4f, -8f, -4f, 0.9f, RC); MLine(-18f, 4f, -8f, 4f, 0.9f, RC);
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
            else if (look == 3)  // plating seams and rivets
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    MLine(-7f, s * (ry - 3f), 4f, s * (ry - 4f), 1f, K.Dark);
                    for (int i = 0; i < 3; i++) MCirc(-6f + i * 4.5f, s * (ry - 5.5f), 0.9f, Hx(0xdfe6ee), 6);
                }
                MLine(-3f, -ry + 3f, -3f, ry - 3f, 1f, K.Dark);
            }
            else  // a leather apron band and buckles
            {
                MRect(-3f, -ry + 2f, 6f, 2f * ry - 4f, K.Dark);
                MRect(-1.6f, -ry + 3f, 3.2f, 2.6f, Hx(0xc9a043)); MRect(-1.6f, ry - 5.6f, 3.2f, 2.6f, Hx(0xc9a043));
            }
            ModelRoleTrim(role, K, RC, rx, ry, t, true);

            if (tool != 0)
                DrawEngineerTool(K, tool, ry, t, fire);  // EngineerBotLook.cs
            else
            {
                // arms on the rivet gun (two hands: grip and foregrip)
                float kick = fire * 3f;
                float rhx = 12f - kick, rhy = 5f, lhx = 20f - kick, lhy = 2.5f;
                ModelArm(K, 1f, ry * 0.72f, rhx, rhy);
                ModelArm(K, 1f, -ry * 0.72f, lhx, lhy);
                MFrame(rhx, rhy, 0f, 1f, false);
                DrawRivetGun(t, fire);
                MFrame(0f, 0f, 0f, 1f, false);
                MCirc(rhx, rhy, 3.3f, K.Glove); MCirc(lhx, lhy, 3.3f, K.Glove);
            }

            // the head
            float hx = sh[3], hr = 8.4f;
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
                    if (look == 3)  // a robot: the stripe in its role's colour and an antenna with a blinking tip
                    {
                        MLine(hx - hr + 2.4f, 0f, hx + hr - 1.6f, 0f, 1.4f, RC);
                        MLine(hx - 3f, -hr + 1f, hx - 9f, -hr - 6f, 1.1f, EngIron);
                        MCirc(hx - 9f, -hr - 6f, 1.6f, Mathf.Sin(t * 6f) > 0f ? RC : EngIron, 8);
                    }
                }
            }
        }

        /// The Engineer's look in a run: the picked look A (Hi-vis), Nov 17. The rivet gun kicks for a moment after each shot.
        public const int EngineerLook = 0;

        void DrawEngineerPlayer(Player p, float r, float t, Rgba col)
        {
            float fire = MathX.Clamp01(1f - (p.Weapon.FireInterval - p.FireCd) / 0.12f);
            if (p.FireCd <= 0f) fire = 0f;
            int tool = EngineerToolOf(p);
            if (tool != 0)
                fire = EngineerToolFire(p, tool);  // EngineerBotLook.cs
            DrawEngineerModel(p.Disp, p.Aim, r, EngineerLook, t, fire, col.A, p.Flash > 0f ? 0.85f : 0f, Mathf.Min((int)p.Role, 2), tool);
        }

        /// KitLook.Body: nothing over the model yet.
        void DrawEngineerBody(Player p, float r, float t) { }

        /// KitLook.Shot: an Engineer's DPS shots are rivets (the Bumble Bot's are stingers).
        bool DrawEngineerShot(Bullet b, Rgba c, float t)
        {
            Player owner;
            if (b.Kind != Defs.DpsShot || game == null || !game.Players.TryGetValue(b.Owner, out owner) || owner.Kit != Kit.Engineer)
                return false;
            if (owner.Weapon.Id == WeaponId.BumbleRemote)
                DrawStinger(b.Pos, b.Vel.Angle);  // the Bumble Bot's stingers (BumbleRemoteLook.cs)
            else
                DrawRivet(b.Pos, b.Vel.Angle, 0.8f);
            return true;
        }

        /// The rivet gun in its weapon frame: grip at (0,0), +x along the aim.
        void DrawRivetGun(float t, float fire)
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

        /// A rivet in flight (a rivet gun's shot): a steel slug with a short cyan trail.
        internal void DrawRivet(V2 at, float ang, float scale)
        {
            V2 d = V2.FromAngle(ang), s = new V2(-d.Y, d.X);
            Line(at - d * (22f * scale), at, 3f * scale, EngCyan.WithAlpha(0.3f));
            Line(at - d * (12f * scale), at, 1.6f * scale, EngCyan);
            SetColor(EngSteel); Quad(at - d * 4f * scale - s * 2.2f * scale, at + d * 3f * scale - s * 2.2f * scale, at + d * 3f * scale + s * 2.2f * scale, at - d * 4f * scale + s * 2.2f * scale);
            Circle(at + d * 3f * scale, 2.6f * scale, Hx(0xcfd8e6), 10);
        }

        /// One cell of a Tools/Preview sheet, drawn at the origin (UiModelAt places and zooms it). sheet: 0 the Engineer's looks,
        /// else the kit (the rivet gun firing, rivets, Drag Pylon, Breach hammer).
        internal void DrawEngineerSheetCell(int sheet, int i, float t)
        {
            if (sheet == 0)
                DrawEngineerModel(new V2(-6f, 0f), -0.35f, Defs.Get(Role.Dps).BodyR, i, t, 0f);
            else if (i == 0)  // firing the rivet gun, rivets in flight
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
