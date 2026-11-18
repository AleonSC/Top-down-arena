using UnityEngine;

namespace Arena
{
    /// The Medic's bay in its tiny room (Medic.cs), in the floating rooms' neon style (NodeLook.cs), in the Medic's rose: a cot with a
    /// pillow and blanket, an IV stand whose bag drips down a tube, a cabinet with a cross on the wall behind and a heartbeat line
    /// running under it all. Preview: EXPANDMAP=0 CARDROOM=medic.
    public partial class ArenaApp
    {
        void DrawMedicBay(V2 c)
        {
            var ink = KindInk(RoomKind.Medic, PathId.None);
            float t = game.AnimT, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            Circle(c, 76f, ink.WithAlpha(0.06f + 0.03f * pulse), 28);

            // The cabinet on the wall behind, a cross on its door.
            FillRect(c.X - 28f, c.Y - 62f, 56f, 30f, ink.WithAlpha(0.16f));
            NodeFrame(c.X - 28f, c.Y - 62f, 56f, 30f, 2f, ink.WithAlpha(0.75f));
            FillRect(c.X - 3f, c.Y - 57f, 6f, 20f, ink.WithAlpha(0.6f + 0.35f * pulse));
            FillRect(c.X - 10f, c.Y - 50f, 20f, 6f, ink.WithAlpha(0.6f + 0.35f * pulse));

            // The cot: a frame, a pillow at its head, a blanket over the rest, little legs.
            FillRect(c.X - 58f, c.Y - 14f, 92f, 28f, ink.WithAlpha(0.2f));
            NodeFrame(c.X - 58f, c.Y - 14f, 92f, 28f, 3f, ink.WithAlpha(0.9f));
            FillRect(c.X - 53f, c.Y - 9f, 16f, 18f, ink.WithAlpha(0.75f));
            FillRect(c.X - 32f, c.Y - 11f, 62f, 22f, ink.WithAlpha(0.32f));
            Line(new V2(c.X - 32f, c.Y - 11f), new V2(c.X - 32f, c.Y + 11f), 2f, ink.WithAlpha(0.7f));  // the blanket's turned-down edge
            for (int s = 0; s < 2; s++)
            {
                FillRect(c.X - 56f + s * 84f, c.Y + 14f, 4f, 6f, ink.WithAlpha(0.6f));
            }

            // The IV stand: a pole on a foot, an arm, the bag (its level breathing) and a tube down to the cot with a drop running along it.
            float x = c.X + 52f;
            Line(new V2(x, c.Y + 26f), new V2(x, c.Y - 46f), 3f, ink.WithAlpha(0.85f));
            Line(new V2(x - 10f, c.Y + 26f), new V2(x + 10f, c.Y + 26f), 3f, ink.WithAlpha(0.85f));
            Line(new V2(x, c.Y - 46f), new V2(x - 10f, c.Y - 46f), 2f, ink.WithAlpha(0.85f));
            float level = 0.55f + 0.15f * Mathf.Sin(t * 0.8f);
            FillRect(x - 19f, c.Y - 42f, 16f, 22f, ink.WithAlpha(0.12f));
            FillRect(x - 17f, c.Y - 40f + (1f - level) * 18f, 12f, level * 18f, ink.WithAlpha(0.55f));
            NodeFrame(x - 19f, c.Y - 42f, 16f, 22f, 2f, ink.WithAlpha(0.9f));
            var tubeA = new V2(x - 11f, c.Y - 20f);
            var tubeB = new V2(c.X + 26f, c.Y - 4f);
            Line(tubeA, tubeB, 1.5f, ink.WithAlpha(0.5f));
            float drop = Mathf.Repeat(t * 0.7f, 1f);
            Circle(tubeA + (tubeB - tubeA) * drop, 2.5f, ink.WithAlpha(0.95f), 8);

            // The heartbeat line under it all, a bright blip running along it.
            float y = c.Y + 46f;
            var pts = new[]
            {
                new V2(c.X - 46f, y), new V2(c.X - 16f, y), new V2(c.X - 10f, y - 5f), new V2(c.X - 5f, y), new V2(c.X, y), new V2(c.X + 4f, y - 18f),
                new V2(c.X + 9f, y + 12f), new V2(c.X + 13f, y), new V2(c.X + 22f, y), new V2(c.X + 27f, y - 4f), new V2(c.X + 32f, y), new V2(c.X + 46f, y),
            };
            for (int i = 0; i + 1 < pts.Length; i++)
                Line(pts[i], pts[i + 1], 2f, ink.WithAlpha(0.45f));
            float run = Mathf.Repeat(t * 0.6f, 1f) * (pts.Length - 1);
            int k = Mathf.Min(pts.Length - 2, (int)run);
            Circle(pts[k] + (pts[k + 1] - pts[k]) * (run - k), 3.5f, ink.WithAlpha(0.95f), 10);
        }
    }
}
