using System;
using Arena;

// TEMPORARY (preview only): candidate Thug (Brute) and Shiv (Lunger) looks, picked by row in the RESKIN scene.
namespace Arena { public partial class ArenaApp
{
    static readonly bool ReskinOn = Environment.GetEnvironmentVariable("RESKIN") != null;
    static int ReskinRow(Enemy e) => Math.Max(0, Math.Min(3, (int)Math.Round((e.Pos.Y - 200f) / 140f)));

    Rgba ReskinColor(Enemy e, Rgba col)
    {
        if (!ReskinOn || (e.Type != EnemyType.Brute && e.Type != EnemyType.Lunger)) return col;
        int v = ReskinRow(e);
        if (v == 0) return col;
        if (e.Type == EnemyType.Brute)
            return v == 1 ? new Rgba(0.95f, 0.5f, 0.12f) : v == 2 ? new Rgba(0.6f, 0.62f, 0.66f) : new Rgba(0.82f, 0.74f, 0.58f);
        return v == 1 ? new Rgba(0.85f, 0.85f, 0.8f) : v == 2 ? new Rgba(0.95f, 0.5f, 0.12f) : new Rgba(0.55f, 0.58f, 0.62f);
    }

    /// true: drawn here instead of the normal face/details
    bool DrawReskin(Enemy e, V2 pos, float r, Rgba col)
    {
        if (!ReskinOn || (e.Type != EnemyType.Brute && e.Type != EnemyType.Lunger)) return false;
        int v = ReskinRow(e);
        if (v == 0) return false;
        V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
        var ink = new Rgba(0.15f, 0.12f, 0.16f, 0.9f);
        var steel = new Rgba(0.82f, 0.86f, 0.9f);
        var white = new Rgba(1f, 1f, 1f, 0.9f);
        if (e.Type == EnemyType.Brute)
        {
            V2 fl = pos + f * r * 0.95f + s * r * 0.6f, fr = pos + f * r * 0.95f - s * r * 0.6f;
            if (v == 1)  // A: orange jumpsuit, number stripe, shackles joining the fists
            {
                Line(pos - s * r * 0.6f, pos + s * r * 0.6f, 4f, white);
                Line(pos - s * r * 0.3f - f * r * 0.35f, pos + s * r * 0.3f - f * r * 0.35f, 4f, white);
                Circle(fl, r * 0.34f, steel, 10); Circle(fr, r * 0.34f, steel, 10);
                Line(fl, fr, 3f, ink);
                Circle(pos + f * r * 0.2f, r * 0.2f, col.Mix(white, 0.4f), 10);  // head
            }
            else if (v == 2)  // B: grey, ball and chain dragging behind
            {
                V2 ball = pos - f * r * 2.1f;
                Line(pos - f * r * 0.7f, ball, 3f, ink);
                Circle(ball, r * 0.5f, ink, 12);
                Circle(ball - s * 2f - f * 2f, r * 0.18f, new Rgba(0.5f, 0.5f, 0.55f), 8);
                Circle(fl, r * 0.34f, steel, 10); Circle(fr, r * 0.34f, steel, 10);
                Line(pos + f * r * 0.2f - s * r * 0.45f, pos + f * r * 0.2f + s * r * 0.45f, 3f, ink);  // brow
            }
            else  // C: tan, taped knuckles, tank-top straps
            {
                Circle(fl, r * 0.34f, white, 10); Circle(fr, r * 0.34f, white, 10);
                Line(fl - s * r * 0.3f, fl + s * r * 0.3f, 3f, ink); Line(fr - s * r * 0.3f, fr + s * r * 0.3f, 3f, ink);
                Line(pos - s * r * 0.5f - f * r * 0.7f, pos - s * r * 0.2f + f * r * 0.2f, 3f, ink);
                Line(pos + s * r * 0.5f - f * r * 0.7f, pos + s * r * 0.2f + f * r * 0.2f, 3f, ink);
                Line(pos + f * r * 0.35f - s * r * 0.4f, pos + f * r * 0.35f + s * r * 0.4f, 3f, new Rgba(0.15f, 0.05f, 0.05f, 0.9f));
            }
        }
        else
        {
            if (v == 1)  // A: a taped shank held out front
            {
                V2 tip = pos + f * r * 2.3f;
                Line(pos + f * r * 0.4f, tip, 3f, steel);
                Line(pos + f * r * 0.4f, pos + f * r * 1.0f, 5f, ink);
                Circle(tip, 2.5f, white, 6);
            }
            else if (v == 2)  // B: orange, two shivs out to the sides
            {
                for (int k = -1; k <= 1; k += 2)
                {
                    V2 root = pos + s * k * r * 0.7f, tip = root + f * r * 1.6f + s * k * r * 0.4f;
                    Line(root, tip, 3f, steel); Line(root, root + f * r * 0.5f, 5f, ink);
                }
            }
            else  // C: grey, mask bandana and a short blade
            {
                Line(pos + f * r * 0.3f - s * r * 0.55f, pos + f * r * 0.3f + s * r * 0.55f, 5f, new Rgba(0.75f, 0.2f, 0.2f));
                Line(pos + f * r * 0.5f, pos + f * r * 1.9f, 3f, steel);
            }
        }
        return true;
    }
}
}
