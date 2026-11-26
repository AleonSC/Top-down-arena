using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// How map nodes look (Docs/Nodes.md): a breathing ring and mark in empty space until a room is built round one, then a fixture of that
    /// room: a shop's stall, a Black Market's pedestal, a chest. Preview: EXPANDFLOAT=1, or EXPANDNODE=shop|Black Market|chest[,spent].
    public partial class ArenaApp
    {
        static readonly Rgba ChestInk = new Rgba(0.82f, 0.5f, 1f);

        static readonly Rgba RelicInk = new Rgba(0.78f, 0.5f, 1f);  // (the Relic chest's violet, ChestLooks.cs)

        static readonly Rgba FogInk = new Rgba(0.72f, 0.78f, 0.88f);

        /// A node's colour: a fogged one is grey.
        static Rgba NodeInkOf(MapNode n) { if (n.Hidden) return FogInk; if (n.Kind == NodeKind.Relic) { Rgba ri; int sh; RelicStyle(Relics.Held(n), out ri, out sh); return ri; } return NodeInk(n.Kind); }

        /// A chest node's colour once seen: its chest kind's ink (Gold, Piece chest, Buildables or Upgrade).
        Rgba NodeInkOfChest(MapNode n) => !n.Hidden && n.Kind == NodeKind.Chest ? ChestInkOf(game.NodeChestKind(n)) : NodeInkOf(n);

        static readonly string[] ChestKindNames = { "Upgrade chest", "Gold chest", "Vector chest", "Relic chest", "Piece chest", "Buildables chest", "Gadget chest" };

        /// A node's name for tips and the legend ("Unknown" while fogged).
        string NodeName(MapNode n) => n.Hidden ? "Unknown node" : n.Kind == NodeKind.Shrine ? "Health shrine" : n.Kind == NodeKind.Chest ? ChestKindNames[(int)game.NodeChestKind(n)] : n.Kind == NodeKind.Relic ? "Relic: " + Relics.Name(Relics.Held(n)) : NodeName(n.Kind);

        /// What a node does, for its tip.
        string NodeBlurb(MapNode n) => (Expansion.IsChest(n.Kind) || n.Kind == NodeKind.Relic ? TierNames[Expansion.NodeTier(n)] + " tier. " : "") + NodeBlurbOf(n);

        static readonly string[] TierNames = { "Inner", "Middle", "Outer" };

        string NodeBlurbOf(MapNode n)
        {
            if (n.Hidden)
                return "Build a room round it to claim it. "
                    + "Walk within 8 tiles of it to see what it is.";
            if (n.Kind == NodeKind.Shrine)
                return "Build a room round it, then walk up to it: once per player, it fills your health and adds " + Mathf.RoundToInt(Shrines.OverFrac * 100f) + "% of your max health past max (kept until you take damage or go down).";
            if (n.Kind == NodeKind.Relic)
                return "Build a room round it and walk up to it to take this relic. Your old one is left in its place for anyone to take. " + Relics.Desc(Relics.Held(n));
            if (n.Kind == NodeKind.Chest)
                switch (game.NodeChestKind(n))
                {
                    case ChestKind.Gold: return "Build a room round it and touch the chest: it bursts into gold coins for everyone to pick up.";
                    case ChestKind.Piece: return "Build a room round it and touch the chest: every player gets a piece card.";
                    case ChestKind.Buildable: return "Build a room round it and touch the chest: every player gets a Black Market build in their stash.";
                    default: return "Build a room round it and touch the chest: every player gets their own upgrade card.";
                }
            return "Build a room round it to claim it.";
        }

        static Rgba NodeInk(NodeKind k) => k == NodeKind.Shrine ? ShrineInk : k == NodeKind.Relic ? RelicInk : ChestInk;

        /// A node kind's name for tips and legends.
        static string NodeName(NodeKind k) => k == NodeKind.BreachLoot ? "Breach loot" : k == NodeKind.Shrine ? "Health shrine" : k.ToString();

        /// A map node on the arena floor: the ghost marker while nobody has built a room round it, its fixture once one has.
        void DrawNodeMark(MapNode n, bool claimed, V2 at, int index = -1)
        {
            if (claimed && n.Kind == NodeKind.Shrine)
            {
                DrawShrine(at, index >= 0 && game.ShrineUsed(game.Me, index), game.AnimT);  // ShrineLook.cs
                return;
            }
            if (n.Kind == NodeKind.BreachLoot)
            {
                DrawBreachLootNode(n);  // EngineerLook.cs
                return;
            }
            if (claimed)
            {
                DrawNodeFixture(at, n.Kind, n.Spent, n.Kind == NodeKind.Chest ? game.NodeChestKind(n) : ChestKind.Upgrade, n.Kind == NodeKind.Relic ? Relics.Held(n) : RelicId.None);  // (a chest or relic stands in its room's middle: Game.NodeSpot)
                return;
            }
            // (owner, Nov 18: nodes stand out: a lit disc, a bold double ring, a beacon ping rippling out and turning dashes, the glyph at full size)
            float t = game.AnimT, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.5f);
            var ink = NodeInkOfChest(n);
            var col = ink.WithAlpha(0.85f + 0.15f * pulse);
            V2 c = n.Pos;
            Circle(c, 92f, ink.WithAlpha(0.05f + 0.04f * pulse), 32);
            Circle(c, 58f, ink.WithAlpha(0.12f + 0.06f * pulse), 28);
            Arc(c, 58f, 5f, col, 32);
            Arc(c, 70f, 2f, ink.WithAlpha(0.45f), 32);
            for (int k = 0; k < 4; k++)  // dashes turning round it
                Arc(c, 80f, 3f, ink.WithAlpha(0.6f), 8, t * 0.7f + k * MathX.Tau / 4f, 0.7f);
            for (int k = 0; k < 2; k++)  // the beacon: two rings rippling out
            {
                float ph = (t * 0.45f + k * 0.5f) % 1f;
                Arc(c, 60f + 110f * ph, 3f, ink.WithAlpha(0.5f * (1f - ph)), 40);
            }
            if (Expansion.IsChest(n.Kind) || n.Kind == NodeKind.Relic)
                DrawNodeTier(c, Expansion.NodeTier(n), t);
            if (n.Hidden)  // (an old save's fogged node, until the host clears it)
            {
                FogMark(c, 1.3f, col);
                return;
            }
            switch (n.Kind)
            {
                case NodeKind.Relic: { Rgba ri; int sh; RelicStyle(Relics.Held(n), out ri, out sh); RelicGlyph(c + new V2(0f, Mathf.Sin(t * 2.6f) * 3f), 1.25f, sh, col, Mathf.Sin(t * 1.2f)); break; }
                case NodeKind.Shrine: ShrineCross(c + new V2(0f, Mathf.Sin(t * 2.6f) * 3f), 1.5f, col); break;
                default:
                    ChestBox(c + new V2(0f, 6f), 1.2f, col, false);
                    ChestEmblem(game.NodeChestKind(n), c + new V2(0f, -34f + Mathf.Sin(t * 2.6f) * 3f), 1.8f, col, col, col.A);  // what it holds
                    break;
            }
        }

        /// An opened Black Market room's pedestal (RoomsLook.cs).
        public void DrawMarketPedestal(V2 c)
        {
            var ink = BlackMarketInk;
            float t = game.AnimT, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            Circle(c, 70f, ink.WithAlpha(0.06f + 0.03f * pulse), 28);
            Circle(c, 30f, ink.WithAlpha(0.2f), 24);
            Arc(c, 30f, 4f, ink.WithAlpha(0.9f), 24);
            Line(c - new V2(16f, 0f), c + new V2(16f, 0f), 6f, ink.WithAlpha(0.95f));
            Line(c - new V2(0f, 16f), c + new V2(0f, 16f), 6f, ink.WithAlpha(0.95f));
            for (int k = 0; k < 4; k++)  // dashes turning slowly round it
                Arc(c, 46f, 3f, ink.WithAlpha(0.55f), 8, t * 0.8f + k * MathX.Tau / 4f, 0.9f);
        }

        /// A node's fixture (the relic, vector arrow or chest), centred on the node.
        void DrawNodeFixture(V2 c, NodeKind kind, bool spent, ChestKind chest = ChestKind.Upgrade, RelicId relic = RelicId.None)
        {
            var ink = kind == NodeKind.Chest ? ChestInkOf(chest) : NodeInk(kind);
            float t = game.AnimT, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            switch (kind)
            {
                case NodeKind.Relic:
                {
                    // (a relic node's pedestal, RelicLook.cs: the artefact it holds floats above it; once taken it stands empty and dim)
                    Rgba rink; int shape;
                    RelicStyle(relic, out rink, out shape);
                    DrawRelicPedestal(c, t, spent ? -1 : shape, spent ? rink.WithAlpha(0.35f) : rink, 1f);
                    break;
                }
                default:
                    if (!spent)
                    {
                        Circle(c, 62f, ink.WithAlpha(0.05f + 0.05f * pulse), 28);
                        Arc(c, 66f + 4f * pulse, 2f, ink.WithAlpha(0.35f + 0.25f * pulse), 28);  // walk up to it
                    }
                    break;  // (a claimed chest is the real prop, Props.cs; nothing is drawn once it is spent)
            }
        }

        internal static float PreviewOpenAge = -1f;  // preview RELICOPEN=<s> / VECOPEN=<s>: every spent relic or vector chest node opened that long ago

        /// A chest opening (the relic and vector chest nodes, and every chest prop): the chest shakes and flashes, the lid flies up, rings burst out in its ink, light pours up with
        /// its emblems rising, and the prize (the relic's gem, or the vector arrow) climbs out, spins and shoots off; then it settles dim with the lid up.
        void DrawNodeOpening(V2 c, float t, float age, ChestKind kind, float fade = 1f)
        {
            if (age >= 3f)
            {
                DrawChestLook(c, 0.3f * fade, t, kind, true, 99f);  // opened, lid up and dim
                return;
            }
            float settle = Mathf.Clamp01((age - 2.2f) / 0.8f), a = Mathf.Lerp(1f, 0.3f, settle) * fade;
            V2 shake = age < 0.12f ? new V2(Mathf.Sin(t * 120f), Mathf.Cos(t * 97f)) * 2.5f * (1f - age / 0.12f) : default(V2);
            DrawChestLook(c + shake, a, t, kind, true, age);
            DrawOpeningFx(c, t, age, kind, fade);
        }

        /// A chest opening's burst over the chest (the hologram's, or the plain wooden one's): the flash, two rings, sparks, and the prize climbing out,
        /// turning, then shooting off. age: seconds since it opened; fade scales it all.
        void DrawOpeningFx(V2 c, float t, float age, ChestKind kind, float fade)
        {
            if (age >= 2.1f)
                return;
            var ink = ChestInkOf(kind);
            float hw = Game.ChestW / 2f, hh = Game.ChestH / 2f;
            if (age < 0.3f)  // the flash
            {
                float f = 1f - age / 0.3f;
                FillRect(c.X - hw, c.Y - hh, hw * 2f, hh * 2f, new Rgba(1f, 1f, 1f, 0.6f * f * fade));
                Circle(c, hw + 18f, ink.Mix(new Rgba(1f, 1f, 1f), 0.5f).WithAlpha(0.35f * f * fade), 28);
            }
            for (int k = 0; k < 2; k++)  // two rings bursting out
            {
                float rk = Mathf.Clamp01((age - k * 0.15f) / 0.7f);
                if (rk <= 0f || rk >= 1f)
                    continue;
                GlowArc(c, 30f + 120f * rk, 2.5f - k, ink.WithAlpha(0.8f * (1f - rk) * fade), 40);
            }
            for (int k = 0; k < 8; k++)  // sparks flung out
            {
                float sk = Mathf.Clamp01(age / 0.6f);
                if (sk >= 1f)
                    break;
                V2 d = V2.FromAngle(k * MathX.Tau / 8f + 0.3f);
                GlowLine(c + d * (20f + 70f * sk), c + d * (30f + 90f * sk), 1.5f, ink.Mix(new Rgba(1f, 1f, 1f), 0.5f).WithAlpha((1f - sk) * fade));
            }
            // The prize climbs out (0.2-1.2 s), hangs spinning, then shoots up and away (1.6-2.1 s).
            if (age > 0.2f && age < 2.1f)
            {
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 0.2f) / 1f)), go = Mathf.Clamp01((age - 1.6f) / 0.5f);
                float scale = 0.6f + 0.8f * rise + 0.6f * go, al = (1f - go) * fade;
                V2 p = c + new V2(0f, -hh - 20f - 70f * rise - 160f * go * go + Mathf.Sin(t * 4f) * 2f * rise);
                Circle(p, 18f * scale, ink.WithAlpha(0.18f * al), 20);
                float spin = Mathf.Cos(age * 6f);  // turning: squash it side to side
                if (kind == ChestKind.Gold)
                    CoinSpin(p, scale, spin, ink.WithAlpha(al));
                else if (kind == ChestKind.Vector)
                    VectorMark(p, 0.75f * scale * Mathf.Max(0.3f, Mathf.Abs(spin)), ink.Mix(new Rgba(1f, 1f, 1f), 0.4f).WithAlpha(al));
                else if (kind == ChestKind.Relic)
                    RelicGemSpin(p, scale, spin, ink.WithAlpha(al));
                else  // Upgrade, Piece chest, Buildables: their emblem, turning (ChestLooks.cs)
                {
                    var em = ink.WithAlpha(al);
                    ChestEmblem(kind, p, 1.6f * scale * Mathf.Max(0.3f, Mathf.Abs(spin)), em, ink.Mix(new Rgba(1f, 1f, 1f), 0.6f).WithAlpha(al), al);
                }
            }
        }

        /// A big gold coin turning: an ellipse squashed by spin (-1..1), its rim, and a bright mark on the face.
        void CoinSpin(V2 c, float k, float spin, Rgba col)
        {
            float r = 11f * k, w = r * Mathf.Max(0.12f, Mathf.Abs(spin));
            const int n = 20;
            SetColor(col.WithAlpha(col.A * 0.45f));
            for (int i = 0; i < n; i++)
            {
                float a0 = i * MathX.Tau / n, a1 = (i + 1) * MathX.Tau / n;
                Tri(c, c + new V2(Mathf.Cos(a0) * w, Mathf.Sin(a0) * r), c + new V2(Mathf.Cos(a1) * w, Mathf.Sin(a1) * r));
            }
            var hot = col.Mix(new Rgba(1f, 1f, 1f), 0.6f).WithAlpha(col.A);
            for (int i = 0; i < n; i++)
            {
                float a0 = i * MathX.Tau / n, a1 = (i + 1) * MathX.Tau / n;
                GlowLine(c + new V2(Mathf.Cos(a0) * w, Mathf.Sin(a0) * r), c + new V2(Mathf.Cos(a1) * w, Mathf.Sin(a1) * r), 1.5f, hot);
            }
            if (Mathf.Abs(spin) > 0.35f)
                GlowLine(c + new V2(0f, -r * 0.45f), c + new V2(0f, r * 0.45f), 1.5f, hot);
        }

        /// The relic's gem turning: a diamond squashed by spin (-1..1) with a bright face.
        void RelicGemSpin(V2 c, float k, float spin, Rgba col)
        {
            float w = 9f * k * Mathf.Max(0.15f, Mathf.Abs(spin)), up = 11f * k, dn = 13f * k, mid = 3f * k;
            V2 top = c + new V2(0f, -up), l = c + new V2(-w, -mid), r = c + new V2(w, -mid), bot = c + new V2(0f, dn);
            SetColor(col.WithAlpha(col.A * 0.45f));
            Quad(top, r, bot, l);
            var hot = col.Mix(new Rgba(1f, 1f, 1f), 0.6f).WithAlpha(col.A);
            GlowLine(top, r, 1.5f, hot); GlowLine(r, bot, 1.5f, hot); GlowLine(bot, l, 1.5f, hot); GlowLine(l, top, 1.5f, hot);
            GlowLine(l, r, 1f, col);
        }

        /// The HUD chip for your relic slot (Relics.cs), with the wave panel's other chips.
        bool DrawRelicChip(float x, float y)
        {
            var me = game.Me;
            if (me == null || me.Relic == RelicId.None)
                return false;
            string text = "RELIC  " + Relics.Name(me.Relic).ToUpperInvariant();
            float w = 30f + text.Length * 6.4f;
            var chip = new Rect(x, y, w, 20f);
            var ink = RelicInk.ToColor();
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), ink);
            Ui.Label(new Rect(chip.x + 12f, chip.y, w, chip.height), text, 10, ink, TextAnchor.MiddleLeft, true);
            return true;
        }

        /// A fogged node's "?": a hook over a stem and a dot, scaled.
        void FogMark(V2 c, float k, Rgba col)
        {
            Arc(c + new V2(0f, -12f * k), 11f * k, 4f * k, col, 14, MathX.Tau * 0.5f, MathX.Tau * 0.6f);
            Line(c + new V2(8.9f * k, -5.5f * k), c + new V2(0f, 3f * k), 4f * k, col);
            Line(c + new V2(0f, 3f * k), c + new V2(0f, 9f * k), 4f * k, col);
            Circle(c + new V2(0f, 18f * k), 3f * k, col, 8);
        }

        /// The vector mark: an arrow pointing up and right with a bar under it, scaled.
        void VectorMark(V2 c, float k, Rgba col)
        {
            V2 a = c + new V2(-12f, 10f) * k, b = c + new V2(12f, -10f) * k;
            Line(a, b, 3.5f * k, col);
            Line(b, b + new V2(-11f, 0f) * k, 3.5f * k, col);
            Line(b, b + new V2(0f, 11f) * k, 3.5f * k, col);
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

        /// The tier metals (owner Nov 18, Docs/Nodes.md "Node tiers"): bronze inner, silver middle, gold outer.
        static readonly Rgba[] TierInk = { new Rgba(0.88f, 0.56f, 0.3f), new Rgba(0.8f, 0.87f, 0.95f), new Rgba(1f, 0.83f, 0.25f) };

        /// A prize node's tier round its ghost marker (owner Nov 18): HUD corner brackets in the tier's metal and, under the node, a row of three
        /// square pixel cells, filled up to the tier, the rest outlined dim; a cursor cell blinks after them.
        void DrawNodeTier(V2 c, int tier, float t)
        {
            var ink = TierInk[tier];
            float glint = 0.75f + 0.25f * Mathf.Sin(t * 3f), h = 118f, arm = 26f, cell = 22f, gap = 6f;
            var bc = ink.WithAlpha(0.85f * glint);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    var p = c + new V2(sx * h, sy * h);
                    Line(p, p - new V2(sx * arm, 0f), 4f, bc);
                    Line(p, p - new V2(0f, sy * arm), 4f, bc);
                }
            float x0 = c.X - (3f * cell + 2f * gap) / 2f, y = c.Y + h - cell + 2f;
            for (int k = 0; k < 3; k++)
            {
                float x = x0 + k * (cell + gap);
                if (k <= tier)
                    FillRect(x, y, cell, cell, ink.WithAlpha(glint));
                else
                    NodeFrame(x, y, cell, cell, 3f, ink.WithAlpha(0.3f));
            }
            if ((int)(t * 2f) % 2 == 0)
                FillRect(x0 + 3f * (cell + gap), y + cell - 6f, 12f, 6f, ink.WithAlpha(0.8f));
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
