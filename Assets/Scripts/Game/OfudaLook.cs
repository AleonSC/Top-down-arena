using UnityEngine;

namespace Arena
{
    /// The Ofuda's look (Core/Ofuda.cs), from the "Ofuda Rework" mockup artifact. Drawing only.
    ///   - a charm: a fluttering paper strip with a soft glow; the Healing Talisman has a green cross and a green glow
    ///     with sparkles in its wake, the Blast Ofuda a red seal and a red glow;
    ///   - a Blast Ofuda stuck to an enemy (riding its drawn position) or lying on the ground: the charm pulsing faster as
    ///     its fuse runs out, a red ring growing to the blast's reach, and a gold fuse arc round it.
    public partial class ArenaApp
    {
        static readonly Rgba OfudaPaper = new Rgba(1f, 0.94f, 0.78f), OfudaSeal = new Rgba(0.78f, 0.12f, 0.16f), OfudaInk = new Rgba(0.18f, 0.68f, 0.39f);

        /// One paper charm at `pos`, its long side along `dir`; `flutter` 0-1 narrows it as it turns in the air.
        void DrawOfudaCharm(V2 pos, V2 dir, float flutter, bool blast, float glow)
        {
            V2 side = dir.Orthogonal();
            Rgba halo = blast ? Game.OfudaRed : Game.TalismanGreen;
            Circle(pos, 9f + 4f * glow, halo.WithAlpha(0.12f + 0.12f * glow), 14);
            Circle(pos, 6f + 2f * glow, halo.WithAlpha(0.18f + 0.15f * glow), 12);
            V2 u = dir * 7.5f, s = side * (2f + 2f * flutter);
            SetColor(OfudaPaper);
            Quad(pos - u - s, pos + u - s, pos + u + s, pos - u + s);
            if (blast)
            {
                Circle(pos - dir * 1f, 2.6f, OfudaSeal, 8);
                Line(pos + dir * 2f, pos + dir * 5.5f, 1.3f, OfudaSeal);
            }
            else
            {
                Line(pos - dir * 5f, pos + dir * 5f, 1.4f, OfudaInk);
                Line(pos - side * (1.5f + 1.2f * flutter), pos + side * (1.5f + 1.2f * flutter), 1.4f, OfudaInk);
            }
        }

        /// A charm in flight (Defs.OfudaShot or Defs.BlastOfudaShot).
        void DrawOfudaShot(Bullet b, float t)
        {
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized();
            bool blast = b.Kind == Defs.BlastOfudaShot;
            Rgba trail = blast ? Game.OfudaRed : Game.TalismanGreen;
            for (int k = 1; k <= 4; k++)  // a short trail of glints behind it
                Circle(b.Pos - dir * (6f + 7f * k) + dir.Orthogonal() * (1.5f * Mathf.Sin(t * 18f + k * 1.7f + b.Id)), 2.2f - 0.35f * k, trail.WithAlpha(0.4f - 0.08f * k), 6);
            float flutter = 0.5f + 0.5f * Mathf.Sin(t * 14f + b.Id);
            DrawOfudaCharm(b.Pos, dir, flutter, blast, blast ? 0.3f : 0.5f + 0.3f * flutter);
        }

        /// A Healing Talisman bursting (FxKind.TalismanBurst): a soft green flash, a ring out to the burst's reach and
        /// scraps of paper flung out, turning as they go.
        void DrawTalismanBurstFx(Effect fx, float f)
        {
            float k = 1f - f, ease = 1f - k * k;
            Circle(fx.Pos, fx.Radius * (0.3f + 0.7f * ease), Game.TalismanGreen.WithAlpha(0.22f * k), 28);
            Arc(fx.Pos, fx.Radius * (0.35f + 0.65f * ease), 1f + 2.5f * k, Game.TalismanGreen.WithAlpha(0.9f * k), 32);
            Arc(fx.Pos, fx.Radius * 0.55f * ease, 1.2f, OfudaPaper.WithAlpha(0.6f * k), 24);
            for (int i = 0; i < 8; i++)
            {
                float a = MathX.Tau * i / 8f + fx.Pos.X * 0.01f;
                V2 at = fx.Pos + V2.FromAngle(a) * fx.Radius * (0.2f + 0.85f * ease);
                V2 u = V2.FromAngle(a + f * 6f + i) * 3f, s = u.Orthogonal() * 0.6f;
                SetColor(OfudaPaper.WithAlpha(k));
                Quad(at - u - s, at + u - s, at + u + s, at - u + s);
            }
            if (f < 0.35f)  // the cross flares at the heart
            {
                float c = 1f - f / 0.35f;
                Line(fx.Pos - new V2(0f, 9f), fx.Pos + new V2(0f, 9f), 2.5f, Game.TalismanGreen.WithAlpha(c));
                Line(fx.Pos - new V2(6f, 2f), fx.Pos + new V2(6f, -2f), 2.5f, Game.TalismanGreen.WithAlpha(c));
            }
        }

        /// A Blast Ofuda stuck to an enemy or lying on the ground, its fuse burning (ZoneKind.BlastOfuda).
        void DrawBlastOfuda(Zone z, float t)
        {
            V2 at = z.Pos;
            Enemy e;
            if (z.Side != 0 && game.Enemies.TryGetValue(z.Side, out e))
                at = e.Disp + (z.Pos - e.Pos);  // ride the enemy where it is drawn
            float lit = z.Duration > 0f ? Mathf.Clamp01(1f - z.T / z.Duration) : 1f;  // 0 just stuck, 1 about to blow
            float beat = 0.5f + 0.5f * Mathf.Sin(t * (10f + 30f * lit));
            Circle(at, z.Radius * (0.25f + 0.75f * lit), Game.OfudaRed.WithAlpha(0.05f + 0.07f * lit), 32);
            Arc(at, z.Radius * (0.25f + 0.75f * lit), 1.5f + lit, Game.OfudaRed.WithAlpha(0.3f + 0.4f * lit), 32);
            if (z.Side == 0)
                Arc(at, z.Radius, 1f, Game.OfudaRed.WithAlpha(0.18f), 32);  // where the blast will reach
            Arc(at, 13f, 2f, Game.OverchargeGold.WithAlpha(0.85f), 20, -MathX.Pi / 2f, MathX.Tau * (1f - lit));  // the fuse
            DrawOfudaCharm(at, V2.FromAngle(z.Aim), 0.4f, true, 0.4f + 0.6f * beat * lit);
        }
    }
}
