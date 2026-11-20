using UnityEngine;

namespace Arena
{
    /// Relic artefacts and the pedestal a built-round relic node stands on (mockup, Docs/RelicNodes.md): a stone plinth with a glowing top
    /// plate, the artefact floating and turning above it in its faction's ink, a thin beam and motes. Preview: RELICSHEET=1 (mode hud, ZOOM=1 CAMAT=640,470).
    public partial class ArenaApp
    {
        internal static bool PreviewRelicSheet;  // preview RELICSHEET=1: draw the artefact sheet at (0,0) beside the map

        /// One boss faction's artefacts: its ink and the shape ids (RelicGlyph) of the 1-3 relics its nodes hold.
        static readonly (string Name, Rgba Ink, int[] Shapes, string[] Names)[] RelicFactions =
        {
            ("Warden",       new Rgba(0.35f, 0.75f, 1f),    new[] { 0 },       new[] { "Warden's Seal" }),
            ("Hive Queen",   new Rgba(1f, 0.78f, 0.2f),     new[] { 1, 2, 14 }, new[] { "Brood Crown", "Royal Jelly", "Stinger" }),
            ("Reaper",       new Rgba(0.7f, 0.55f, 1f),     new[] { 3, 4 },    new[] { "Twin Scythe", "Dusk Lantern" }),
            ("Singularity",  new Rgba(0.55f, 0.6f, 1f),     new[] { 5, 6 },    new[] { "Event Horizon", "Gravity Well" }),
            ("Colossus",     new Rgba(0.95f, 0.6f, 0.3f),   new[] { 7 },       new[] { "Stone Heart" }),
            ("Duelist",      new Rgba(1f, 0.4f, 0.45f),     new[] { 9, 15 },   new[] { "Silver Foil", "Duel Gem" }),
            ("Lich",         new Rgba(0.4f, 1f, 0.7f),      new[] { 10, 12, 8 }, new[] { "Phylactery", "Grave Fang", "Cold Heart" }),
            ("Storm Herald", new Rgba(0.9f, 0.95f, 0.4f),   new[] { 11, 2 },   new[] { "Storm Coil", "Static Cell" }),
            ("Devourer",     new Rgba(0.75f, 0.9f, 0.3f),   new[] { 12 },      new[] { "Maw Tooth" }),
            ("Eclipse",      new Rgba(1f, 0.55f, 0.85f),    new[] { 13, 6, 15 }, new[] { "Black Sun", "Corona Spiral", "Umbra Shard" }),
        };

        internal static int PreviewRelicPage;  // preview RELICPAGE=0|1: page 0 = the node before and after + the first five factions, page 1 = the other five

        /// The sheet, laid round the player (the camera's middle stays empty): page 0 shows the node before and after a room is built round it,
        /// then boss factions in columns, each relic on its pedestal.
        void DrawRelicSheet()
        {
            float t = game.AnimT;
            V2 o = game.Me != null ? game.Me.Disp : new V2(0f, 0f);
            float[] slots = { -220f, -110f, 110f, 220f, 330f };
            if (PreviewRelicPage == 0)
            {
                var node = new MapNode { Kind = NodeKind.Relic, X = (short)(o.X - 440f), Y = (short)(o.Y - 120f) };
                DrawNodeMark(node, false, new V2(o.X - 440f, o.Y - 120f));  // before: the ghost marker
                DrawRelicPedestal(new V2(o.X - 440f, o.Y + 90f), t, 8, RelicInk, 1f);  // after: built round, a pedestal in the cell
            }
            for (int s = 0; s < 5; s++)
            {
                int f = PreviewRelicPage * 5 + s;
                var fac = RelicFactions[f];
                float x = o.X + slots[s];
                Line(new V2(x - 46f, o.Y - 190f), new V2(x + 46f, o.Y - 190f), 2f, fac.Ink.WithAlpha(0.8f));
                for (int r = 0; r < fac.Shapes.Length; r++)
                    DrawRelicPedestal(new V2(x, o.Y - 130f + r * 130f), t + f * 0.37f + r * 0.8f, fac.Shapes[r], fac.Ink, 1f);
            }
        }

        /// A relic node's fixture once its room is built: the plinth, the beam and the floating artefact (c = the cell's middle).
        void DrawRelicPedestal(V2 c, float t, int shape, Rgba ink, float k)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            var stone = new Rgba(0.16f, 0.16f, 0.2f, 0.95f);
            var edge = ink.Mix(new Rgba(1f, 1f, 1f), 0.15f);
            Circle(c + new V2(0f, 26f * k), 56f * k, ink.WithAlpha(0.05f + 0.04f * pulse), 28);  // the glow on the floor
            Arc(c + new V2(0f, 28f * k), Relics.TakeReach * k, 2f, ink.WithAlpha(0.25f + 0.2f * pulse), 28);  // walk up to it
            // base slab, shaft, top plate
            FillRect(c.X - 30f * k, c.Y + 34f * k, 60f * k, 10f * k, stone);
            NodeFrame(c.X - 30f * k, c.Y + 34f * k, 60f * k, 10f * k, 2f, edge.WithAlpha(0.7f));
            SetColor(stone);
            Quad(new V2(c.X - 14f * k, c.Y + 34f * k), new V2(c.X + 14f * k, c.Y + 34f * k), new V2(c.X + 10f * k, c.Y + 12f * k), new V2(c.X - 10f * k, c.Y + 12f * k));
            Line(new V2(c.X - 14f * k, c.Y + 34f * k), new V2(c.X - 10f * k, c.Y + 12f * k), 2f, edge.WithAlpha(0.6f));
            Line(new V2(c.X + 14f * k, c.Y + 34f * k), new V2(c.X + 10f * k, c.Y + 12f * k), 2f, edge.WithAlpha(0.6f));
            FillRect(c.X - 22f * k, c.Y + 4f * k, 44f * k, 8f * k, stone);
            NodeFrame(c.X - 22f * k, c.Y + 4f * k, 44f * k, 8f * k, 2f, edge.WithAlpha(0.9f));
            FillRect(c.X - 20f * k, c.Y + 4f * k, 40f * k, 2.5f * k, ink.WithAlpha(0.5f + 0.3f * pulse));  // the lit plate
            // beam up from the plate
            FillRect(c.X - 5f * k, c.Y - 36f * k, 10f * k, 40f * k, ink.WithAlpha(0.08f + 0.05f * pulse));
            for (int i = 0; i < 4; i++)  // motes rising
            {
                float ph = (t * 0.5f + i * 0.25f) % 1f;
                Circle(c + new V2(Mathf.Sin(t * 1.3f + i * 1.9f) * 14f * k, (2f - ph * 44f) * k), 1.8f * k, ink.WithAlpha(0.7f * (1f - ph)), 6);
            }
            // the artefact, floating and turning
            V2 at = c + new V2(0f, -26f * k + Mathf.Sin(t * 2.4f) * 4f * k);
            Circle(at, 26f * k, ink.WithAlpha(0.10f + 0.05f * pulse), 20);
            Arc(at, 24f * k, 1.5f, ink.WithAlpha(0.45f), 24, t * 0.9f, 1.6f);
            Arc(at, 24f * k, 1.5f, ink.WithAlpha(0.45f), 24, t * 0.9f + MathX.Pi, 1.6f);
            RelicGlyph(at, k, shape, ink, Mathf.Sin(t * 1.2f));
        }

        V2 rgC; float rgK, rgSx; Rgba rgCol;
        V2 RgP(float x, float y) { return rgC + new V2(x * rgSx, y) * rgK; }
        void RgL(V2 a, V2 b, float w = 2.5f) { GlowLine(a, b, w * rgK, rgCol); }
        void RgPoly(params V2[] p) { for (int i = 0; i < p.Length; i++) RgL(p[i], p[(i + 1) % p.Length]); }

        /// One artefact's shape (0 seal, 1 crown, 2 hex, 3 crescent, 4 lantern, 5 horizon, 6 spiral, 7 stone heart, 8 heart, 9 foil, 10 flask, 11 bolt, 12 fang, 13 black sun, 14 stinger, 15 gem), turning by spin (-1..1).
        void RelicGlyph(V2 c, float k, int shape, Rgba col, float spin)
        {
            float sx = Mathf.Max(0.25f, Mathf.Abs(spin));  // squashes sideways as it turns
            var hot = col.Mix(new Rgba(1f, 1f, 1f), 0.6f);
            rgC = c; rgK = k; rgSx = sx; rgCol = col;
            switch (shape)
            {
                case 0:  // seal: ring with a cross
                    Arc(c, 15f * k, 3f * k, col, 20); RgL(RgP(0, -15), RgP(0, 15)); RgL(RgP(-15, 0), RgP(15, 0));
                    Circle(c, 3.5f * k, hot, 8); break;
                case 1:  // crown
                    RgPoly(RgP(-16, 10), RgP(-16, -6), RgP(-8, 2), RgP(0, -14), RgP(8, 2), RgP(16, -6), RgP(16, 10)); RgL(RgP(-16, 10), RgP(16, 10), 3f);
                    Circle(RgP(0, -14), 2.5f * k, hot, 8); break;
                case 2:  // hex
                    RgPoly(RgP(0, -17), RgP(15, -8), RgP(15, 8), RgP(0, 17), RgP(-15, 8), RgP(-15, -8)); RgPoly(RgP(0, -7), RgP(6, -3), RgP(6, 3), RgP(0, 7), RgP(-6, 3), RgP(-6, -3)); break;
                case 3:  // crescent
                    Arc(c, 16f * k, 4f * k, col, 20, 0.9f, 4.6f); Arc(c + new V2(5f * sx, 0f) * k, 12f * k, 2f * k, col.WithAlpha(0.5f), 16, 0.9f, 4.6f); break;
                case 4:  // lantern
                    RgPoly(RgP(-8, -8), RgP(8, -8), RgP(11, 12), RgP(-11, 12)); RgL(RgP(-5, -8), RgP(-5, -16)); RgL(RgP(5, -8), RgP(5, -16)); RgL(RgP(-5, -16), RgP(5, -16));
                    Circle(RgP(0, 3), 4f * k, hot, 8); break;
                case 5:  // event horizon: ring and a dark core
                    Arc(c, 16f * k, 3f * k, col, 22); Arc(c, 10f * k, 2f * k, col.WithAlpha(0.6f), 18); Circle(c, 5f * k, new Rgba(0f, 0f, 0f, 0.9f), 12); Arc(c, 5f * k, 1.5f * k, hot, 12); break;
                case 6:  // spiral
                    for (int i = 0; i < 28; i++) { float a0 = i * 0.32f, a1 = (i + 1) * 0.32f, r0 = 2f + i * 0.55f, r1 = 2f + (i + 1) * 0.55f; RgL(RgP(Mathf.Cos(a0) * r0, Mathf.Sin(a0) * r0), RgP(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1), 2f); } break;
                case 7:  // stone heart: a heart in a block
                    RgPoly(RgP(-14, -12), RgP(14, -12), RgP(14, 12), RgP(-14, 12)); RgPoly(RgP(0, 8), RgP(-8, -1), RgP(-6, -7), RgP(0, -3), RgP(6, -7), RgP(8, -1)); break;
                case 8:  // heart
                    RgPoly(RgP(0, 14), RgP(-14, -2), RgP(-10, -12), RgP(0, -6), RgP(10, -12), RgP(14, -2)); break;
                case 9:  // foil: a long blade and guard
                    RgL(RgP(0, -20), RgP(0, 8), 3f); RgL(RgP(-9, 8), RgP(9, 8), 3f); RgL(RgP(0, 8), RgP(0, 18), 3.5f); Circle(RgP(0, 19), 2.5f * k, hot, 8); break;
                case 10:  // flask
                    RgPoly(RgP(-4, -14), RgP(4, -14), RgP(4, -6), RgP(13, 12), RgP(-13, 12), RgP(-4, -6)); RgL(RgP(-8, 4), RgP(8, 4), 2f); Circle(RgP(0, 8), 2.5f * k, hot, 8); break;
                case 11:  // bolt
                    RgPoly(RgP(4, -18), RgP(-10, 3), RgP(-1, 3), RgP(-5, 18), RgP(11, -4), RgP(2, -4)); break;
                case 12:  // fang
                    RgPoly(RgP(-9, -14), RgP(9, -14), RgP(2, 16), RgP(-2, 16)); RgL(RgP(-9, -14), RgP(9, -14), 3.5f); break;
                case 13:  // black sun: a dark disc with a corona
                    Circle(c, 10f * k, new Rgba(0f, 0f, 0f, 0.9f), 16); Arc(c, 10f * k, 2.5f * k, hot, 16);
                    for (int i = 0; i < 8; i++) { float a = i * MathX.Tau / 8f; RgL(RgP(Mathf.Cos(a) * 14, Mathf.Sin(a) * 14), RgP(Mathf.Cos(a) * 20, Mathf.Sin(a) * 20)); } break;
                case 14:  // stinger
                    RgPoly(RgP(0, -18), RgP(7, 4), RgP(0, 18), RgP(-7, 4)); RgL(RgP(0, -18), RgP(0, 18), 1.5f); break;
                default:  // gem
                    RgPoly(RgP(0, -18), RgP(14, -4), RgP(0, 18), RgP(-14, -4)); RgL(RgP(-14, -4), RgP(14, -4), 1.5f); RgL(RgP(0, -18), RgP(0, 18), 1.2f); break;
            }
        }
    }
}
