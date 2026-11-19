using UnityEngine;

namespace Arena
{
    /// The enemies that don't shoot (Core/NonShooters.cs): the hook (look B of Docs/Mockups/non-shooter-enemies.html,
    /// Harpoon: a thin aim line that blinks faster as the throw nears, a barbed harpoon on a cable), the Acolyte's cone
    /// (SwordsmanLook.cs) and the Turnkey's kin (TurnkeyLook.cs).
    /// Drawn the same with Visual effects on or off; the timers come from the snapshot (PhaseT, T2).
    public partial class ArenaApp
    {
        static readonly Rgba HookCable = new Rgba(0.85f, 0.8f, 0.96f), FuseDanger = new Rgba(1f, 0.3f, 0.3f);  // (FuseDanger: the Turnkey's trap rings, TurnkeyLook.cs)

        /// Behind the body: the hook's aim line, cable and hook, the Acolyte's cone, the Turnkey's traps.
        void DrawNonShooterUnder(Enemy e, V2 pos, float r, float t)
        {
            if (Game.IsTurnkeyKin(e.Type))
            {
                DrawTurnkeyUnder(e, pos, r, t);  // TurnkeyLook.cs
                return;
            }
            switch (e.Type)
            {
                case EnemyType.HookThrower:
                    DrawHookUnder(e, pos, r, t);
                    break;
                case EnemyType.Acolyte:
                    DrawSwordsmanUnder(e, pos, r, t);  // the cone it cuts (SwordsmanLook.cs)
                    break;
            }
        }

        void DrawHookUnder(Enemy e, V2 pos, float r, float t)
        {
            V2 dir = V2.FromAngle(e.Facing);
            if (e.Phase == 1)
            {
                // The aim: a thin line to the harpoon's reach, blinking faster as the throw nears, with a chevron at the end.
                float f = 1f - Mathf.Clamp01(e.PhaseT / Game.HookThrowAim), rate = 6f + f * 22f;
                bool on = Mathf.Sin(t * rate) > -0.2f;
                var c = e.Def.Color.WithAlpha(on ? 0.9f : 0.25f);
                V2 end = pos + dir * Game.HookReach;
                Line(pos + dir * r, end, 2f, c);
                Line(end - V2.FromAngle(e.Facing - 0.5f) * 14f, end, 2f, c);
                Line(end - V2.FromAngle(e.Facing + 0.5f) * 14f, end, 2f, c);
            }
            else if (e.Phase >= 2 && e.T2 > r)
            {
                V2 tip = pos + dir * e.T2;
                Line(pos, tip, 1.6f, HookCable.WithAlpha(0.9f));
                DrawHarpoon(tip, e.Facing, 1.6f);
            }
        }

        /// A barbed harpoon head pointing along `ang`.
        void DrawHarpoon(V2 at, float ang, float s)
        {
            V2.Axes(ang, out V2 f, out V2 side);
            var c = new Rgba(0.92f, 0.88f, 1f);
            Tri3(at + f * 8f * s, at - side * 4f * s, at + f * 2f * s, c);
            Tri3(at + f * 8f * s, at + f * 2f * s, at + side * 4f * s, c);
            Line(at - f * 5f * s, at + f * 2f * s, 1.6f * s, c);
        }

        void Tri3(V2 a, V2 b, V2 c, Rgba col)
        {
            SetColor(col);
            Tri(a, b, c);
        }

        /// The body, in place of the usual polygon. col is already tinted by status and the hit flash.
        void DrawNonShooterBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            if (Game.IsTurnkeyKin(e.Type))
            {
                DrawTurnkeyBody(e, pos, r, col, t);  // the Turnkey, its door and traps (TurnkeyLook.cs)
                return;
            }
            switch (e.Type)
            {
                case EnemyType.Acolyte:
                    DrawSwordsmanBody(e, pos, r, col, t);  // SwordsmanLook.cs
                    break;
            }
        }
    }
}
