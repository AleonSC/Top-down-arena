using UnityEngine;

namespace Arena
{
    /// The Turnkey (Core/Turnkey.cs), in the Warden's smooth officer style (OfficerLook.cs): a prison janitor in mustard
    /// coveralls and a dark beanie, a ring of brass keys swinging at its belt. Working, it crouches over its trap or hammers
    /// at its door, and a ring round it fills as the work nears done (the window to catch it). Its Cell Door is a barred
    /// steel gate (the cell's orange light behind; it swings open as a Rioter steps out; jammed: chained and padlocked),
    /// its Floor Mine a dark disc with an amber light, its Shackle Trap a pair of toothed steel jaws on a staked chain, and
    /// its Mop Slick (PatchKind.Slick) a grey-blue wet floor with a yellow sign. Timers come from the snapshot (PhaseT, T2).
    public partial class ArenaApp
    {
        static readonly Rgba TurnkeyOveralls = new Rgba(0.72f, 0.6f, 0.3f), TurnkeyBeanie = new Rgba(0.2f, 0.22f, 0.26f),
            KeyBrass = new Rgba(1f, 0.82f, 0.36f), DoorSteel = new Rgba(0.56f, 0.62f, 0.7f), CellGlow = new Rgba(1f, 0.55f, 0.2f),
            TrapSteel = new Rgba(0.78f, 0.8f, 0.86f), SlickBlue = new Rgba(0.3f, 0.58f, 0.95f), SignYellow = new Rgba(1f, 0.84f, 0.2f);

        /// Behind the body: a mine's blast ring as it beeps, a door's cell light.
        void DrawTurnkeyUnder(Enemy e, V2 pos, float r, float t)
        {
            if (e.Type == EnemyType.FloorMine && e.Phase == 2)
            {
                bool on = Mathf.Sin(t * 60f) > 0f;
                Circle(pos, Game.FloorMineBlast, FuseDanger.WithAlpha(on ? 0.16f : 0.06f), 40);
                Arc(pos, Game.FloorMineBlast, 2f, FuseDanger.WithAlpha(0.7f), 40);
            }
        }

        void DrawTurnkeyBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            switch (e.Type)
            {
                case EnemyType.Turnkey: DrawTurnkey(e, pos, r, t); break;
                case EnemyType.CellDoor: DrawCellDoor(e, pos, r, t); break;
                case EnemyType.FloorMine: DrawFloorMine(e, pos, r, t); break;
                case EnemyType.ShackleTrap: DrawShackle(e, pos, r, t); break;
            }
        }

        void DrawTurnkey(Enemy e, V2 pos, float r, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 s);
            bool planting = e.Phase == 2, building = e.Phase == 3;
            float work = planting ? 1f - Mathf.Clamp01(e.PhaseT / Game.TurnkeyPlant) : building ? 1f - Mathf.Clamp01(e.PhaseT / Game.TurnkeyBuild) : 0f;
            V2 body = pos + (planting ? f * (r * 0.15f) : V2.Zero);  // leaning over its trap
            // the key ring at its hip, swinging as it runs
            float sway = Mathf.Sin(t * (planting || building ? 3f : 11f) + e.Id) * 0.5f;
            V2 hip = body - s * (r * 0.95f) - f * (r * 0.1f), ring = hip + V2.FromAngle(e.Facing + MathX.Pi + sway) * 5f;
            OfficerTorso(body, r, e.Facing, TurnkeyOveralls);
            Cap(body - f * (r * 0.75f) - s * (r * 0.45f), body - f * (r * 0.75f) + s * (r * 0.45f), 2.6f, new Rgba(0.45f, 0.36f, 0.16f));  // the bib's strap
            Arc(ring, 4.2f, 1.6f + OffInk, GuardInk, 16);
            Arc(ring, 4.2f, 1.6f, KeyBrass, 16);
            for (int k = -1; k <= 1; k++)
            {
                V2 kd = V2.FromAngle(e.Facing + MathX.Pi + sway * 1.4f + k * 0.55f);
                Cap(ring + kd * 3.5f, ring + kd * 9f, 1.8f, KeyBrass);
                Cap(ring + kd * 7f, ring + kd * 7f + kd.Orthogonal() * 2.2f, 1.4f, KeyBrass);  // the bit
            }
            // arms: down to the trap while planting, a hammer raised and brought down while building, else pumping as it runs
            Rgba glove = new Rgba(0.3f, 0.26f, 0.2f);
            if (planting)
            {
                V2 at = body + f * (r * 1.25f);
                OfficerArm(body + s * r * 0.85f, at + s * 4f, 4.2f, TurnkeyOveralls, glove);
                OfficerArm(body - s * r * 0.85f, at - s * 4f, 4.2f, TurnkeyOveralls, glove);
                Circle(at, 3.5f + 2f * Mathf.Sin(t * 14f), KeyBrass.WithAlpha(0.35f), 12);  // the trap being set
            }
            else if (building)
            {
                float lift = Mathf.Abs(Mathf.Sin(t * 9f));
                V2 grip = body + s * (r * 0.9f) + f * (r * (0.5f + 0.5f * lift));
                V2 head = grip + V2.FromAngle(e.Facing + 0.6f - 1.1f * lift) * 13f;
                OfficerArm(body + s * r * 0.85f, grip, 4.2f, TurnkeyOveralls, glove);
                OfficerArm(body - s * r * 0.85f, body - s * r * 0.6f + f * r * 0.9f, 4.2f, TurnkeyOveralls, glove);
                GuardStick(grip, head, 2.6f, new Rgba(0.55f, 0.4f, 0.22f));
                Cap(head - V2.FromAngle(e.Facing + 0.6f - 1.1f * lift).Orthogonal() * 4f, head + V2.FromAngle(e.Facing + 0.6f - 1.1f * lift).Orthogonal() * 4f, 5f, new Rgba(0.35f, 0.37f, 0.42f));
                // the cell going up in front of it: its outline, filling in
                V2 door = pos + f * (r + 18f);
                var ghost = DoorSteel.WithAlpha(0.2f + 0.5f * work);
                for (int k = 0; k < 6; k++)
                    if (k < 1 + (int)(work * 6f))
                        Cap(door - s * 14f + s * (28f * k / 6f), door - s * 14f + s * (28f * (k + 0.6f) / 6f), 3f, ghost);
            }
            else
            {
                float pump = Mathf.Sin(t * 14f + e.Id) * r * 0.35f;
                OfficerArm(body + s * r * 0.9f, body + s * r * 0.9f + f * (r * 0.4f + pump), 4.2f, TurnkeyOveralls, glove);
                OfficerArm(body - s * r * 0.9f, body - s * r * 0.9f + f * (r * 0.4f - pump), 4.2f, TurnkeyOveralls, glove);
            }
            Circle(body, r * 0.56f + OffInk, GuardInk, 28);
            Circle(body, r * 0.56f, OfficerSkin, 28);
            Arc(body, r * 0.5f, r * 0.28f, TurnkeyBeanie, 24, e.Facing + MathX.Pi - 1.7f, 3.4f);  // the beanie over the back of its head
            Circle(body - f * (r * 0.52f), 2.2f, new Rgba(0.85f, 0.25f, 0.2f), 10);  // its bobble
            OfficerFlash(e, body, r);
            if (planting || building)
                Arc(pos, r + 7f, 2.5f, KeyBrass.WithAlpha(0.9f), 32, -MathX.Pi / 2f, MathX.Tau * work);  // the work, filling: catch it now
        }

        /// A cell seen from above: a dark box with bars across it, the barred gate on the side facing you. While its Turnkey
        /// works it the cell glows orange and the gate swings open as a Rioter comes out; jammed, a chain crosses it, padlocked.
        void DrawCellDoor(Enemy e, V2 pos, float r, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 s);
            float w = r * 1.05f, d = r * 0.8f;
            bool jammed = e.Phase == 2;
            V2 fl = pos + f * d - s * w, fr = pos + f * d + s * w, bl = pos - f * d - s * w, br = pos - f * d + s * w;
            float ink = OffInk + 1f;
            Quad(fl + (f - s) * ink, fr + (f + s) * ink, br + (s - f) * ink, bl - (f + s) * ink, GuardInk);
            Quad(fl, fr, br, bl, new Rgba(0.1f, 0.11f, 0.14f));
            if (!jammed)
            {
                float swing = Mathf.Clamp01(e.PhaseT / Game.DoorSwing), a = 0.3f + 0.4f * swing + 0.06f * Mathf.Sin(t * 3f + e.Id);
                Circle(pos, w * 0.75f, CellGlow.WithAlpha(a), 20);  // the light inside
            }
            for (int k = 1; k <= 4; k++)  // the bars over the top
            {
                V2 o = s * (w * (-1f + 2f * k / 5f));
                Cap(pos - f * d + o, pos + f * d + o, 1.8f, DoorSteel.Darkened(0.2f));
            }
            Cap(bl, br, 3f, DoorSteel.Darkened(0.3f));  // the back wall
            // the gate: hinged at the front-left corner, swung open toward you while a Rioter comes out
            float open = jammed ? 0f : Mathf.Sin(Mathf.Clamp01(e.PhaseT / Game.DoorSwing) * MathX.Pi) * 1.2f;
            V2 along = V2.FromAngle(s.Angle + open);
            V2 end = fl + along * (w * 2f);
            Cap(fl, end, 5.2f + 2f, GuardInk);
            Cap(fl, end, 5.2f, DoorSteel);
            for (int k = 1; k <= 5; k++)  // its bars' tops, studs along it
                Circle(fl + along * (w * 2f * k / 6f), 1.5f, new Rgba(0.92f, 0.95f, 1f, 0.8f), 8);
            Circle(fl + along * (w * 1.65f) + V2.FromAngle(along.Angle - MathX.Pi / 2f) * 4.5f, 2.6f, KeyBrass, 12);  // the lock plate
            if (jammed)
            {
                // a chain across the cell, corner to corner, and a red padlock on the gate
                for (int k = 0; k <= 6; k++)
                {
                    Circle(fl + (br - fl) * (k / 6f), 2.4f, new Rgba(0.75f, 0.77f, 0.82f), 10);
                    Circle(fr + (bl - fr) * (k / 6f), 2.4f, new Rgba(0.75f, 0.77f, 0.82f), 10);
                }
                V2 lk = pos + f * (d + 6f);
                Arc(lk + f * 2.5f, 3f, 1.4f, new Rgba(0.75f, 0.77f, 0.82f), 12, e.Facing - MathX.Pi / 2f, MathX.Pi);
                Circle(lk, 4.2f + OffInk, GuardInk, 12);
                Circle(lk, 4.2f, new Rgba(0.88f, 0.26f, 0.2f), 12);
            }
            if (e.Flash > 0f || JuiceFlash(e))
                Quad(fl, fr, br, bl, Rgba.White.WithAlpha(0.7f * HitFlashMix(e)));
        }

        void DrawFloorMine(Enemy e, V2 pos, float r, float t)
        {
            float arm = Mathf.Clamp01(e.T2 / Game.FloorMineArm);
            bool armed = e.Phase >= 1, beep = e.Phase == 2, blink = Mathf.Sin(t * 60f) > 0f;
            Circle(pos, r + OffInk, GuardInk, 20);
            Circle(pos, r, new Rgba(0.3f, 0.31f, 0.28f), 20);
            for (int k = 0; k < 4; k++)  // its four prongs
            {
                V2 d = V2.FromAngle(MathX.Pi / 4f + k * MathX.Pi / 2f);
                Circle(pos + d * (r * 0.72f), 1.3f, new Rgba(0.6f, 0.62f, 0.6f), 6);
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f + e.Id);
            var light = armed && (!beep || blink) ? (beep ? Rgba.White : e.Def.Color) : new Rgba(0.3f, 0.12f, 0.08f);
            Circle(pos, r * 0.38f, light, 10);
            if (armed && !beep)
                Circle(pos, r * 0.8f, e.Def.Color.WithAlpha(0.18f * pulse), 14);
            if (!armed)
                Arc(pos, r + (1f - arm) * 14f, 1.4f, e.Def.Color.WithAlpha(0.55f), 20);
            if (beep)
                Circle(pos, r * 2.2f, FuseDanger.WithAlpha(blink ? 0.5f : 0.15f), 16);
        }

        void DrawShackle(Enemy e, V2 pos, float r, float t)
        {
            bool armed = e.Phase >= 1, shut = e.Phase == 2;
            float arm = Mathf.Clamp01(e.T2 / Game.SnareArm);
            // the chain to its stake
            V2 stake = pos + new V2(r * 1.3f, r * 0.9f);
            for (int k = 1; k <= 3; k++)
                Arc(pos + (stake - pos) * (k / 4f), 1.8f, 1f, TrapSteel.WithAlpha(0.8f), 8);
            Circle(stake, 2.6f, new Rgba(0.35f, 0.3f, 0.25f), 10);
            // the jaws: two half-rings, open flat or snapped shut on an ankle
            float gap = shut ? r * 0.15f : r * 0.85f;
            var steel = armed ? TrapSteel : TrapSteel.Darkened(0.4f);
            for (int k = -1; k <= 1; k += 2)
            {
                V2 c = pos + new V2(0f, k * gap * 0.5f);
                float start = k < 0 ? MathX.Pi : 0f;
                Arc(c, r * 0.8f, 2.6f + OffInk, GuardInk, 16, start, MathX.Pi);
                Arc(c, r * 0.8f, 2.6f, steel, 16, start, MathX.Pi);
                for (int j = 1; j <= 3; j++)  // teeth
                {
                    V2 d = V2.FromAngle(start + MathX.Pi * j / 4f);
                    Line(c + d * (r * 0.8f), c + d * (r * 0.45f), 1.4f, steel);
                }
            }
            Circle(pos, 2.4f, armed ? new Rgba(0.85f, 0.25f, 0.2f) : new Rgba(0.35f, 0.2f, 0.2f), 8);  // the plate you step on
            if (!armed)
                Arc(pos, r + (1f - arm) * 14f, 1.4f, TrapSteel.WithAlpha(0.5f), 20);
            if (shut)
                Arc(pos, r * 1.5f, 2f, Rgba.White.WithAlpha(Mathf.Clamp01(e.PhaseT / Game.SnareLinger)), 20);
        }

        /// A Turnkey's mop slick: a grey-blue wet floor with a sheen, and a yellow wet-floor sign at its edge.
        void DrawSlickPatch(PathPatch pt, float t)
        {
            float r = pt.Radius, a = Mathf.Clamp01(pt.Age / Mathf.Max(0.01f, pt.Warn)) * Mathf.Clamp01((pt.Life - pt.Age) / 0.8f);
            Circle(pt.Pos, r, SlickBlue.WithAlpha(0.32f * a), 28);
            Circle(pt.Pos + new V2(r * 0.2f, r * 0.15f), r * 0.65f, SlickBlue.WithAlpha(0.22f * a), 22);
            Arc(pt.Pos, r, 1.6f, new Rgba(0.75f, 0.88f, 1f, 0.45f * a), 28);
            for (int k = 0; k < 4; k++)  // soap bubbles
            {
                V2 at = pt.Pos + new V2((Hash(pt.Id * 11 + k) - 0.5f) * r * 1.2f, (Hash(pt.Id * 3 + k) - 0.5f) * r * 1.2f);
                Arc(at, 2f + 2f * Hash(pt.Id + k * 7), 1f, new Rgba(0.85f, 0.95f, 1f, 0.5f * a), 10);
            }
            for (int k = 0; k < 3; k++)  // the sheen, drifting
            {
                float u = MathX.Fmod(t * 0.25f + k / 3f + pt.Id * 0.13f, 1f);
                V2 at = pt.Pos + new V2(-r * 0.5f + r * u, -r * 0.3f + k * r * 0.25f);
                Line(at, at + new V2(r * 0.25f, -r * 0.08f), 1.6f, new Rgba(1f, 1f, 1f, 0.3f * a * Mathf.Sin(u * MathX.Pi)));
            }
            // the sign: a yellow A-frame seen from above, with its black stripe
            V2 sign = pt.Pos + new V2(r * 0.72f, -r * 0.62f);
            Tri3(sign + new V2(0f, -7f), sign + new V2(-6f, 5f), sign + new V2(6f, 5f), GuardInk.WithAlpha(a));
            Tri3(sign + new V2(0f, -5.5f), sign + new V2(-4.6f, 4f), sign + new V2(4.6f, 4f), SignYellow.WithAlpha(a));
            Line(sign + new V2(-2.6f, 1.5f), sign + new V2(2.6f, 1.5f), 1.2f, GuardInk.WithAlpha(a));
        }
    }
}
