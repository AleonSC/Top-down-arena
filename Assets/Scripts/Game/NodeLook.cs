using UnityEngine;

namespace Arena
{
    /// How map nodes look (Docs/Nodes.md): a breathing ring and mark in empty space until a room is built round one, then a fixture of that
    /// room: a shop's stall, a vendor's pedestal, a chest (lid up and dim once opened). Preview: EXPANDFLOAT=1, or EXPANDNODE=shop|vendor|chest[,spent].
    public partial class ArenaApp
    {
        static readonly Rgba ChestInk = new Rgba(0.82f, 0.5f, 1f);

        static Rgba NodeInk(NodeKind k) => k == NodeKind.Shop ? ShopInk : k == NodeKind.Vendor ? VendorInk : ChestInk;

        /// A map node on the arena floor: the ghost marker while nobody has built a room round it, its fixture once one has.
        void DrawNodeMark(MapNode n, bool claimed)
        {
            if (claimed)
            {
                DrawNodeFixture(n.Pos, n.Kind, n.Spent);
                return;
            }
            var col = NodeInk(n.Kind).WithAlpha(0.5f + 0.25f * Mathf.Sin(game.AnimT * 2.5f));
            V2 c = n.Pos;
            Arc(c, 36f, 3f, col, 28);
            switch (n.Kind)
            {
                case NodeKind.Shop: Arc(c, 14f, 3f, col, 16); break;
                case NodeKind.Vendor:
                    Line(c - new V2(16f, 0f), c + new V2(16f, 0f), 4f, col);
                    Line(c - new V2(0f, 16f), c + new V2(0f, 16f), 4f, col);
                    break;
                default: ChestBox(c, 0.7f, col, false); break;
            }
        }

        /// The shop's stall, the vendor's pedestal or the chest, centred on the node (or on an opened floating shop or vendor room).
        void DrawNodeFixture(V2 c, NodeKind kind, bool spent)
        {
            var ink = NodeInk(kind);
            float t = game.AnimT, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            switch (kind)
            {
                case NodeKind.Shop:
                {
                    Circle(c, 74f, ink.WithAlpha(0.06f + 0.03f * pulse), 28);
                    FillRect(c.X - 52f, c.Y - 14f, 104f, 28f, ink.WithAlpha(0.2f));
                    NodeFrame(c.X - 52f, c.Y - 14f, 104f, 28f, 3f, ink.WithAlpha(0.9f));
                    for (int i = 0; i < 5; i++)  // the awning
                        FillRect(c.X - 50f + i * 20f, c.Y - 36f, 20f, 18f, ink.WithAlpha(i % 2 == 0 ? 0.55f : 0.18f));
                    NodeFrame(c.X - 50f, c.Y - 36f, 100f, 18f, 2f, ink.WithAlpha(0.8f));
                    Arc(new V2(c.X, c.Y + Mathf.Sin(t * 3f) * 2f), 7f, 3f, ink.WithAlpha(0.95f), 14);  // a coin on the counter
                    break;
                }
                case NodeKind.Vendor:
                {
                    Circle(c, 70f, ink.WithAlpha(0.06f + 0.03f * pulse), 28);
                    Circle(c, 30f, ink.WithAlpha(0.2f), 24);
                    Arc(c, 30f, 4f, ink.WithAlpha(0.9f), 24);
                    Line(c - new V2(16f, 0f), c + new V2(16f, 0f), 6f, ink.WithAlpha(0.95f));
                    Line(c - new V2(0f, 16f), c + new V2(0f, 16f), 6f, ink.WithAlpha(0.95f));
                    for (int k = 0; k < 4; k++)  // dashes turning slowly round it
                        Arc(c, 46f, 3f, ink.WithAlpha(0.55f), 8, t * 0.8f + k * MathX.Tau / 4f, 0.9f);
                    break;
                }
                default:
                    if (!spent)
                    {
                        Circle(c, 62f, ink.WithAlpha(0.05f + 0.05f * pulse), 28);
                        Arc(c, 66f + 4f * pulse, 2f, ink.WithAlpha(0.35f + 0.25f * pulse), 28);  // walk up to it
                    }
                    if (spent)  // (a claimed, unopened chest is the real prop, Props.cs)
                        ChestBox(c, 1f, ink.WithAlpha(spent ? 0.3f : 0.95f), spent);
                    break;
            }
        }

        /// A chest: body and lid (the lid swung up when open), scaled.
        void ChestBox(V2 c, float k, Rgba col, bool open)
        {
            float hw = 28f * k, bh = 26f * k, lh = 16f * k;
            NodeFrame(c.X - hw, c.Y - 6f * k, 2f * hw, bh, 3f * k, col);
            if (!open)
            {
                FillRect(c.X - hw, c.Y - 6f * k - lh, 2f * hw, lh, col.WithAlpha(col.A * 0.35f));
                NodeFrame(c.X - hw, c.Y - 6f * k - lh, 2f * hw, lh, 3f * k, col);
                FillRect(c.X - 4f * k, c.Y - 10f * k, 8f * k, 10f * k, col);  // the clasp
            }
            else
            {
                Line(new V2(c.X - hw, c.Y - 6f * k), new V2(c.X - hw - 8f * k, c.Y - 6f * k - lh - 8f * k), 3f * k, col);
                Line(new V2(c.X + hw, c.Y - 6f * k), new V2(c.X + hw + 8f * k, c.Y - 6f * k - lh - 8f * k), 3f * k, col);
            }
        }

        void NodeFrame(float x, float y, float w, float h, float t, Rgba c)
        {
            FillRect(x, y, w, t, c);
            FillRect(x, y + h - t, w, t, c);
            FillRect(x, y, t, h, c);
            FillRect(x + w - t, y, t, h, c);
        }
    }
}
