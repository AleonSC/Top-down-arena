using UnityEngine;

namespace Arena
{
    /// The Turnkey (Core/Turnkey.cs), in the Warden's smooth officer style (OfficerLook.cs): a prison janitor in mustard
    /// coveralls and a dark beanie, a ring of brass keys swinging at its belt. Working, it crouches over its trap or hammers
    /// at its door, and a ring round it fills as the work nears done (the window to catch it). Its Cell Door is drawn in
    /// CellDoorLook.cs; its Floor Mine is an olive disc with a pressure plate, a red light ticking round it and its trigger
    /// zone dashed on the floor; its Shackle Trap a bear trap (serrated jaws, hinges, a linked chain to a stake); and its
    /// Mop Slick (PatchKind.Slick) a grey-blue wet floor with a yellow sign. Timers come from the snapshot (PhaseT, T2).
    public partial class ArenaApp
    {
        static readonly Rgba TurnkeyOveralls = new Rgba(0.72f, 0.6f, 0.3f), TurnkeyBeanie = new Rgba(0.2f, 0.22f, 0.26f),
            KeyBrass = new Rgba(1f, 0.82f, 0.36f), CellGlow = new Rgba(1f, 0.55f, 0.2f),
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
                case EnemyType.CellDoor: DrawCellDoorFloor(e, pos, t); break;  // (the door itself: DrawCellDoorsOver, CellDoorLook.cs)
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
                // (the door going up on the wall: its outline, CellDoorLook.cs DrawDoorBlueprint)
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

        /// The mine: a squat olive disc with a raised pressure plate and a red LED ring that ticks round when armed; its
        /// blast circle drawn faint and dashed under it, so you see the zone before you step in.
        void DrawFloorMine(Enemy e, V2 pos, float r, float t)
        {
            float arm = Mathf.Clamp01(e.T2 / Game.FloorMineArm);
            bool armed = e.Phase >= 1, beep = e.Phase == 2, blink = Mathf.Sin(t * 60f) > 0f;
            if (armed && !beep)
                for (int k = 0; k < 16; k++)  // the trigger zone, dashed
                    Arc(pos, Game.FloorMineTrigger, 1.2f, AlarmRed.WithAlpha(0.28f), 4, k * MathX.Tau / 16f + t * 0.3f, MathX.Tau / 32f);
            Circle(pos + new V2(1.5f, 2f), r + 1.5f, new Rgba(0f, 0f, 0f, 0.35f), 20);  // its shadow
            Circle(pos, r + OffInk, GuardInk, 20);
            Circle(pos, r, new Rgba(0.33f, 0.36f, 0.27f), 20);
            Arc(pos, r * 0.8f, 1.2f, new Rgba(0.22f, 0.24f, 0.18f), 20);
            Circle(pos, r * 0.5f, new Rgba(0.42f, 0.45f, 0.36f), 14);  // the plate
            Circle(pos - new V2(r * 0.15f, r * 0.15f), r * 0.25f, new Rgba(0.55f, 0.58f, 0.48f), 10);
            for (int k = 0; k < 8; k++)  // its LED ring
            {
                float ang = k * MathX.Tau / 8f;
                bool lit = armed && (beep ? blink : (int)MathX.Fmod(t * 6f, 8f) == k);
                var c = lit ? (beep ? Rgba.White : AlarmRed) : armed ? new Rgba(0.45f, 0.08f, 0.06f) : new Rgba(0.2f, 0.1f, 0.08f);
                Circle(pos + V2.FromAngle(ang) * (r * 0.74f), 1.1f, c, 6);
                if (lit)
                    Circle(pos + V2.FromAngle(ang) * (r * 0.74f), 3.2f, AlarmRed.WithAlpha(0.3f), 8);
            }
            if (!armed)
                Arc(pos, r + 3f, 1.6f, AlarmRed.WithAlpha(0.6f), 20, -MathX.Pi / 2f, MathX.Tau * arm);  // arming
            if (beep)
                Circle(pos, r * 2.2f, AlarmRed.WithAlpha(blink ? 0.5f : 0.15f), 16);
        }

        /// The shackle: a bear trap seen from above, two serrated jaws open on a round frame round a red plate, staked by
        /// a chain; snapped, the jaws meet with a white crack.
        void DrawShackle(Enemy e, V2 pos, float r, float t)
        {
            bool armed = e.Phase >= 1, shut = e.Phase == 2;
            float arm = Mathf.Clamp01(e.T2 / Game.SnareArm);
            V2 stake = pos + new V2(r * 1.6f, r * 1.1f);
            for (int k = 1; k <= 4; k++)  // the chain: alternating links
            {
                V2 c = pos + (stake - pos) * (k / 5f);
                float ang = (stake - pos).Angle + (k % 2 == 0 ? MathX.Pi / 2f : 0f);
                Arc(c, 2f, 1f, TrapSteel.WithAlpha(0.85f), 8);
                Cap(c - V2.FromAngle(ang) * 1.5f, c + V2.FromAngle(ang) * 1.5f, 1f, TrapSteel.Darkened(0.3f));
            }
            Circle(stake, 3.2f + OffInk, GuardInk, 10);
            Circle(stake, 3.2f, new Rgba(0.4f, 0.34f, 0.26f), 10);
            Circle(stake, 1.3f, TrapSteel, 6);
            Circle(pos + new V2(1.5f, 2f), r + 2f, new Rgba(0f, 0f, 0f, 0.35f), 20);
            var steel = armed ? TrapSteel : TrapSteel.Darkened(0.4f);
            Arc(pos, r * 0.95f, 2.2f + OffInk, GuardInk, 24);
            Arc(pos, r * 0.95f, 2.2f, steel.Darkened(0.25f), 24);  // the frame
            float open = shut ? 0.12f : 1f;
            for (int k = -1; k <= 1; k += 2)
            {
                // each jaw: an arc from hinge to hinge (left and right), laid open outward or closed to the middle
                V2 c = pos + new V2(0f, k * r * 0.45f * open);
                float start = k < 0 ? MathX.Pi : 0f;
                Arc(c, r * 0.85f, 2.6f + OffInk, GuardInk, 16, start, MathX.Pi);
                Arc(c, r * 0.85f, 2.6f, steel, 16, start, MathX.Pi);
                for (int j = 1; j <= 5; j++)  // serrated teeth
                {
                    V2 d = V2.FromAngle(start + MathX.Pi * j / 6f);
                    Tri3(c + d * (r * 0.85f) + d.Orthogonal() * 1.6f, c + d * (r * 0.85f) - d.Orthogonal() * 1.6f, c + d * (r * 0.5f), steel);
                }
            }
            Circle(pos - new V2(r, 0f), 2f, steel.Darkened(0.3f), 8);  // the hinges
            Circle(pos + new V2(r, 0f), 2f, steel.Darkened(0.3f), 8);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f + e.Id);
            Circle(pos, 3.4f, armed ? AlarmRed : new Rgba(0.35f, 0.2f, 0.2f), 10);  // the plate you step on
            if (armed && !shut)
                Circle(pos, 6f, AlarmRed.WithAlpha(0.2f * pulse), 12);
            if (!armed)
                Arc(pos, r + 4f, 1.6f, TrapSteel.WithAlpha(0.6f), 20, -MathX.Pi / 2f, MathX.Tau * arm);
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
