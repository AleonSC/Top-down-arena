using UnityEngine;

namespace Arena
{
    /// The Revolver's Pistol Whip (Core/PistolWhip.cs), drawn from its FxKind.PistolWhip's age:
    ///   - the swing: a brass crescent across the arc, thick at the gun and thinning behind it, with sparks off the edge;
    ///   - the hand: the right hand swings the gun from your right side across to your left (WhipHandPose, for the model),
    ///     then eases back to aiming.
    public partial class ArenaApp
    {
        const float WhipRecover = 0.22f;

        /// The newest whip `id` swung, or null.
        Effect WhipOf(int id)
        {
            Effect last = null;
            foreach (var fx in FxOf(FxKind.PistolWhip))
                if (fx.Kind == FxKind.PistolWhip && (int)fx.Radius == id && (last == null || fx.T < last.T))
                    last = fx;
            return last;
        }

        /// How far round the gun is, from +half (your right) to -half (your left), `T` into the swing.
        static float WhipAt(float T, float half) => half - 2f * half * EaseOut3(Mathf.Clamp01(T / Game.WhipSweep));

        /// The swing's shape `fx.T` in: where it's centred, its reach, where it starts and where the gun is now.
        bool WhipShape(Effect fx, out V2 o, out float aim, out float reach, out float half, out float start, out float at)
        {
            Player owner;
            o = game.Players.TryGetValue((int)fx.Radius, out owner) ? owner.Disp : fx.Pos;
            V2 to = fx.Pos2 - fx.Pos;
            aim = to.Angle; reach = to.Length; half = Game.WhipArc * MathX.Pi / 360f;
            start = aim + half; at = aim + WhipAt(fx.T, half);
            return fx.T <= Game.WhipFxTime && reach > 1f;
        }

        /// FxKind.PistolWhip, the solid layer: sparks off the edge and a white flash along the arc as it lands (with Visual
        /// effects off, a plain band where the crescent would be).
        void DrawPistolWhipFx(Effect fx)
        {
            V2 o; float aim, reach, half, start, at;
            if (!WhipShape(fx, out o, out aim, out reach, out half, out start, out at))
                return;
            float T = fx.T;
            var col = Game.WhipColor;
            if (!fxOn)
            {
                float k = 1f - Mathf.Clamp01((T - Game.WhipSweep) / 0.2f);
                Arc(o, reach * 0.88f, reach * 0.18f, col.WithAlpha(0.45f * k), 28, start, at - start);
                return;
            }
            MeleeSparks(o, start, at - start, reach, T, Game.WhipSweep, col, (int)(fx.Pos.X * 7f + fx.Pos.Y * 13f) & 0xffff, false, 18);
            float flash = 1f - Mathf.Clamp01((T - Game.WhipWind) / 0.14f);
            if (T >= Game.WhipWind && flash > 0f)
                Arc(o, reach + 2f, 3f * flash + 1f, Rgba.White.WithAlpha(0.75f * flash), 28, aim - half, 2f * half);
        }

        /// The glow pass (DrawMeleeGlow): every whip's brass crescent, thick at the gun and thinning behind it.
        void DrawPistolWhipGlow()
        {
            foreach (var fx in game.Effects)
            {
                V2 o; float aim, reach, half, start, at;
                if (fx.Kind != FxKind.PistolWhip || !WhipShape(fx, out o, out aim, out reach, out half, out start, out at))
                    continue;
                KatanaCrescent(o, reach, start, at, fx.T, Game.WhipSweep, Game.WhipColor, 0.13f);
            }
        }

        /// The model's right hand during a whip: swung round in front of you, the gun turned along the swing (`rot`).
        void WhipHandPose(Player p, ref float rhx, ref float rhy, out float rot)
        {
            rot = 0f;
            if (p.Weapon.Id != WeaponId.Revolver)
                return;
            var last = WhipOf(p.Id);
            if (last == null || last.T > Game.WhipSweep + WhipRecover)
                return;
            float half = Game.WhipArc * MathX.Pi / 360f * 0.85f;
            float a = WhipAt(last.T, half);
            float back = Mathf.Clamp01((last.T - Game.WhipSweep) / WhipRecover), mix = 1f - back * back * (3f - 2f * back);
            float hx = Mathf.Cos(a) * 21f, hy = Mathf.Sin(a) * 21f;
            rhx += (hx - rhx) * mix; rhy += (hy - rhy) * mix;
            rot = (a - 0.6f) * mix;  // the gun turned a little behind the swing, the butt leading
        }
    }
}
