using UnityEngine;

namespace Arena
{
    /// The Arms Dealer's stall in its tiny room (Docs/ArmsDealer.md), in the floating rooms' neon style (NodeLook.cs): a gun rack on a
    /// counter, a rifle laid across it, ammo crates at its sides and a crosshair turning slowly over it. Preview: EXPANDMAP=0 CARDROOM=arms.
    public partial class ArenaApp
    {
        void DrawArmsStall(V2 c)
        {
            var ink = KindInk(RoomKind.Arms, PathId.None);
            float t = game.AnimT, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            Circle(c, 76f, ink.WithAlpha(0.06f + 0.03f * pulse), 28);

            // The rack: a bar with four rifles standing in it, barrels up.
            NodeFrame(c.X - 50f, c.Y - 44f, 100f, 6f, 2f, ink.WithAlpha(0.8f));
            for (int i = 0; i < 4; i++)
            {
                float x = c.X - 36f + i * 24f;
                FillRect(x - 2f, c.Y - 58f, 4f, 30f, ink.WithAlpha(i % 2 == 0 ? 0.85f : 0.6f));  // barrel and stock
                FillRect(x - 4f, c.Y - 34f, 8f, 12f, ink.WithAlpha(0.45f));                        // the stock's butt
            }

            // The counter, with a rifle laid across it.
            FillRect(c.X - 54f, c.Y - 12f, 108f, 26f, ink.WithAlpha(0.2f));
            NodeFrame(c.X - 54f, c.Y - 12f, 108f, 26f, 3f, ink.WithAlpha(0.9f));
            float bob = Mathf.Sin(t * 3f) * 1.5f;
            Line(new V2(c.X - 34f, c.Y + 1f + bob), new V2(c.X + 30f, c.Y + 1f + bob), 4f, ink.WithAlpha(0.95f));  // barrel
            FillRect(c.X - 40f, c.Y - 3f + bob, 16f, 9f, ink.WithAlpha(0.95f));                                     // stock
            FillRect(c.X - 10f, c.Y + 2f + bob, 5f, 8f, ink.WithAlpha(0.8f));                                       // grip
            FillRect(c.X + 2f, c.Y + 2f + bob, 6f, 9f, ink.WithAlpha(0.7f));                                        // magazine

            // Ammo crates at its sides.
            for (int s = -1; s <= 1; s += 2)
            {
                float x = c.X + s * 66f - 10f;
                FillRect(x, c.Y + 18f, 20f, 16f, ink.WithAlpha(0.22f));
                NodeFrame(x, c.Y + 18f, 20f, 16f, 2f, ink.WithAlpha(0.75f));
                Line(new V2(x, c.Y + 18f), new V2(x + 20f, c.Y + 34f), 1.5f, ink.WithAlpha(0.5f));
            }

            // The crosshair turning over the stall.
            var h = new V2(c.X, c.Y + 50f);
            Arc(h, 12f, 2f, ink.WithAlpha(0.55f + 0.3f * pulse), 18);
            for (int k = 0; k < 4; k++)
            {
                float a = t * 0.9f + k * MathX.Tau / 4f;
                var d = new V2(Mathf.Cos(a), Mathf.Sin(a));
                Line(h + d * 7f, h + d * 18f, 2f, ink.WithAlpha(0.8f));
            }
        }
    }
}
