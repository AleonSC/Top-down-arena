using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Poison Dart's look (Docs/Mockups/poison-dart.html, look A "Toxic Drip"), drawing only:
    ///   - FxKind.VenomDart (Pos = you, Pos2 = the enemy, Radius = its id): a steel needle with green fletching and a short
    ///     green trail flies in, a lime ring pops where it lands, and the dart stays stuck in that enemy while it's poisoned;
    ///   - a poisoned enemy (Enemy.Venomed): tinted sickly green, dripping green, and a drop with a dart through it in the
    ///     status row;
    ///   - FxKind.VenomSpread (Pos = the corpse, Pos2 = an enemy it passed to): a green splash where it died and a glob
    ///     lobbed onto each new victim, landing in a puff.
    /// With Visual effects off: a plain green line for the dart and the spread, and the tint and the icon.
    public partial class ArenaApp
    {
        static readonly Rgba VenomGreen = new Rgba(0.56f, 0.94f, 0.35f), VenomLime = new Rgba(0.82f, 1f, 0.59f),
            VenomDeep = new Rgba(0.16f, 0.47f, 0.16f), DartSteel = new Rgba(0.81f, 0.85f, 0.87f), DartFletch = new Rgba(0.37f, 0.75f, 0.23f);

        readonly HashSet<int> dartStuck = new HashSet<int>();  // enemies a dart hit (not ones the poison spread to)
        readonly List<int> dartGone = new List<int>();

        /// A needle pointing along dir with its tip at tip.
        void DrawNeedle(V2 tip, V2 dir, float len, float a)
        {
            V2 n = dir.Orthogonal(), tail = tip - dir * len;
            Line(tail, tip - dir * 2f, 2.2f, DartSteel.WithAlpha(a));
            SetColor(VenomGreen.WithAlpha(a));
            Quad(tip + dir * 3f, tip - dir * 2f + n * 2.2f, tip - dir * 2f - n * 2.2f, tip + dir * 3f);
            SetColor(DartFletch.WithAlpha(a));
            Quad(tail, tail - dir * 5f + n * 4f, tail + dir * 3f, tail - dir * 5f - n * 4f);
        }

        /// FxKind.VenomDart.
        void DrawVenomDartFx(Effect fx)
        {
            V2 d = fx.Pos2 - fx.Pos;
            float len = d.Length;
            if (len < 1f)
                return;
            V2 dir = d / len;
            float fly = Mathf.Clamp01(fx.T / Game.VenomDartFly);
            V2 head = fx.Pos + d * fly;
            if (!fxOn)
            {
                if (fly < 1f)
                    Line(head - dir * 14f, head, 2f, VenomGreen);
                return;
            }
            if (fly < 1f)
            {
                float tail = Mathf.Min(70f, len * fly);
                const int Segs = 6;
                for (int i = 0; i < Segs; i++)  // the trail fades out behind the needle
                {
                    float a = (float)i / Segs, b = (float)(i + 1) / Segs;
                    Line(head - dir * tail * (1f - a), head - dir * tail * (1f - b), 3f, VenomGreen.WithAlpha(0.7f * b));
                }
                DrawNeedle(head, dir, 11f, 1f);
                return;
            }
            dartStuck.Add((int)fx.Radius);
            float k = (fx.T - Game.VenomDartFly) / Mathf.Max(0.01f, fx.Dur - Game.VenomDartFly);
            Arc(fx.Pos2, 14f + 16f * EaseOut3(k), 2f, VenomLime.WithAlpha(1f - k), 24);  // the pop where it lands
        }

        /// FxKind.VenomSpread.
        void DrawVenomSpreadFx(Effect fx)
        {
            V2 from = fx.Pos, to = fx.Pos2;
            float fly = Mathf.Clamp01(fx.T / Game.VenomGlobFly);
            if (!fxOn)
            {
                if (fly < 1f)
                    Line(V2.Lerp(from, to, Mathf.Max(0f, fly - 0.2f)), V2.Lerp(from, to, fly), 2f, VenomGreen);
                return;
            }
            float burst = fx.T / 0.6f;
            if (burst < 1f)  // the splash where it died (each glob draws it faintly; together they make one)
            {
                float k = EaseOut3(burst);
                GlowDisc(from, 14f + 26f * k, VenomGreen, 0.25f * (1f - burst));
                for (int i = 0; i < 5; i++)
                {
                    float an = i / 5f * MathX.Tau + fx.Radius * 0.7f;
                    V2 at = from + V2.FromAngle(an) * (8f + 36f * k) + new V2(0f, 10f * burst * burst);
                    Circle(at, 2.6f * (1f - burst * 0.6f), VenomLime.WithAlpha(1f - burst), 6);
                }
            }
            if (fly < 1f)  // the glob, lobbed: an arc up and over, droplets trailing it
            {
                for (int g = 4; g >= 0; g--)
                {
                    float v = Mathf.Max(0f, fly - g * 0.06f);
                    V2 at = V2.Lerp(from, to, v) + new V2(0f, -40f * Mathf.Sin(Mathf.PI * v));
                    if (g == 0)
                        Circle(at, 4.5f, VenomLime, 10);
                    else
                        Circle(at, 4f - g * 0.6f, VenomGreen.WithAlpha(0.45f * (1f - g / 5f)), 8);
                }
            }
            else
            {
                float k = (fx.T - Game.VenomGlobFly) / Mathf.Max(0.01f, fx.Dur - Game.VenomGlobFly);
                GlowDisc(to, 10f + 14f * k, VenomGreen, 0.5f * (1f - k));
            }
        }

        /// A poisoned enemy, from DrawEnemyStatus: the tint, the drips and the stuck dart. Returns the tinted colour.
        Rgba DrawVenomed(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            col = col.Mix(new Rgba(0.43f, 0.78f, 0.27f), 0.4f + 0.1f * Mathf.Sin(t * 7f + e.Id));
            for (int k = 0; k < 3; k++)  // drips falling off it
            {
                float f = MathX.Fmod(t * 1.1f + k / 3f + e.Id * 0.37f, 1f);
                V2 at = pos + new V2((k - 1f) * r * 0.45f, r * 0.4f + f * r * 1.3f);
                Circle(at, (2f + r * 0.08f) * (1f - f * 0.5f), VenomGreen.WithAlpha(0.9f * (1f - f)), 6);
            }
            if (fxOn && dartStuck.Contains(e.Id))  // the dart that started it, still in it
            {
                float an = -2.3f + Hash01(e.Id + 4f) * 0.8f;
                V2 dir = V2.FromAngle(an + MathX.Pi);
                DrawNeedle(pos - dir * (r * 0.3f), dir, 9f, 1f);
            }
            return col;
        }

        /// The status-row icon: a green drop with a dart through it.
        void DrawVenomIcon(V2 c, float size)
        {
            Circle(c + new V2(0f, 2f), size * 0.55f, VenomGreen, 10);
            Polygon(c + new V2(0f, -2f), size * 0.55f, 3, -MathX.Pi / 2f, VenomGreen);
            Line(c + new V2(-size * 0.75f, size * 0.7f), c + new V2(size * 0.75f, -size * 0.7f), 1.5f, DartSteel);
        }

        /// Forget the stuck darts of enemies that are gone or no longer poisoned (once a frame, from DrawWorld's enemy pass).
        void PruneDarts()
        {
            if (dartStuck.Count == 0)
                return;
            dartGone.Clear();
            foreach (int id in dartStuck)
            {
                Enemy e;
                if (!game.Enemies.TryGetValue(id, out e) || !e.Venomed)
                    dartGone.Add(id);
            }
            foreach (int id in dartGone)
                dartStuck.Remove(id);
        }
    }
}
