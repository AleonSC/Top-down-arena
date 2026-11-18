using UnityEngine;

namespace Arena
{
    /// How map nodes look (Docs/Nodes.md): a breathing ring and mark in empty space until a room is built round one, then a fixture of that
    /// room: a shop's stall, a vendor's pedestal, a chest. Preview: EXPANDFLOAT=1, or EXPANDNODE=shop|vendor|chest[,spent].
    public partial class ArenaApp
    {
        static readonly Rgba ChestInk = new Rgba(0.82f, 0.5f, 1f);

        static readonly Rgba RelicInk = new Rgba(0.78f, 0.5f, 1f);  // (the Relic chest's violet, ChestLooks.cs)

        static readonly Rgba RuleInk = new Rgba(1f, 0.5f, 0.3f);

        static readonly Rgba TowerInk = new Rgba(0.45f, 0.75f, 1f);

        static readonly Rgba VeinInk = new Rgba(1f, 0.9f, 0.25f);

        static readonly Rgba FogInk = new Rgba(0.72f, 0.78f, 0.88f);

        /// A node's colour: a fogged one is grey, a rule node wears its rule's path colour.
        static Rgba NodeInkOf(MapNode n) => n.Hidden ? FogInk : n.Kind == NodeKind.Rule ? Paths.Get(Expansion.RuleFloor(n.Rule)).Accent : NodeInk(n.Kind);

        /// A chest node's colour once seen: its chest kind's ink (Gold, Grid piece, Buildables or Upgrade).
        Rgba NodeInkOfChest(MapNode n) => !n.Hidden && n.Kind == NodeKind.Chest ? ChestInkOf(game.NodeChestKind(n)) : NodeInkOf(n);

        static readonly string[] ChestKindNames = { "Upgrade chest", "Gold chest", "Vector chest", "Relic chest", "Grid piece chest", "Buildables chest" };

        /// A node's name for tips and the legend ("Unknown" while fogged).
        string NodeName(MapNode n) => n.Hidden ? "Unknown node" : n.Kind == NodeKind.Chest ? ChestKindNames[(int)game.NodeChestKind(n)] : n.Kind == NodeKind.Relic ? "Relic: " + Relics.Name(Relics.Held(n)) : n.Kind == NodeKind.Rule ? "Rule node: " + RuleName(n.Rule) : NodeName(n.Kind);

        /// What a node does, for its tip (the rule nodes' blurb is ExpansionUi's RuleBlurb).
        string NodeBlurb(MapNode n)
        {
            if (n.Hidden)
                return (n.Kind == NodeKind.Rule ? "Build a room round all of it (at least 9 x 9 tiles). " : "Build a room round it to claim it. ")
                    + "Walk within 8 tiles of it, or claim a scout tower near it, to see what it is.";
            if (n.Kind == NodeKind.Relic)
                return "Build a room round it and walk up to it to take this relic. Your old one is left in its place for anyone to take. " + Relics.Desc(Relics.Held(n));
            if (n.Kind == NodeKind.Chest)
                switch (game.NodeChestKind(n))
                {
                    case ChestKind.Gold: return "Build a room round it and touch the chest: it bursts into gold coins for everyone to pick up.";
                    case ChestKind.Piece: return "Build a room round it and touch the chest: every player gets a grid piece card.";
                    case ChestKind.Buildable: return "Build a room round it and touch the chest: every player gets a Black Market build in their stash.";
                    default: return "Build a room round it and touch the chest: every player gets their own upgrade card.";
                }
            if (n.Kind == NodeKind.Tower)
                return "Build a room round it to claim it. While it stands, every node within 30 tiles shows what it is, new ones too.";
            if (n.Kind == NodeKind.Vein)
                return "Build a room round it to claim it. Each cleared wave it pays everyone " + NodeScouting.VeinGold + " gold, +" + NodeScouting.VeinGoldPerWave + " per wave number.";
            return "Build a room round it to claim it.";
        }

        static string RuleName(byte rule) => (NodeRule)rule == NodeRule.BloodMoon ? "Blood Moon" : ((NodeRule)rule).ToString();

        static Rgba NodeInk(NodeKind k) => k == NodeKind.Shop ? ShopInk : k == NodeKind.Vendor ? VendorInk : k == NodeKind.Relic ? RelicInk : k == NodeKind.Rule ? RuleInk
            : k == NodeKind.Tower ? TowerInk : k == NodeKind.Vein ? VeinInk : k == NodeKind.VectorChest ? VecCyan : ChestInk;

        /// A node kind's name for tips and legends.
        static string NodeName(NodeKind k) => k == NodeKind.BreachLoot ? "Breach loot" : k == NodeKind.VectorChest ? "Vector chest" : k == NodeKind.Tower ? "Scout tower" : k == NodeKind.Vein ? "Gold vein" : k.ToString();

        /// A map node on the arena floor: the ghost marker while nobody has built a room round it, its fixture once one has.
        void DrawNodeMark(MapNode n, bool claimed, V2 at)
        {
            if (n.Kind == NodeKind.BreachLoot)
            {
                DrawBreachLootNode(n);  // EngineerLook.cs
                return;
            }
            if (claimed)
            {
                if (n.Kind == NodeKind.Rule)  // a rule room's prize shows once it is cleared (a chest, or a relic to walk up to); Frozen pays at once
                {
                    if (n.Cleared && Expansion.RuleHoldsRelic(n))
                        DrawNodeFixture(n.Pos, NodeKind.Relic, n.Spent);
                    else if (n.Cleared && Expansion.RuleHoldsChest(n))
                        DrawNodeFixture(n.Pos, NodeKind.Chest, n.Spent, game.NodeChestKind(n));
                    return;
                }
                DrawNodeFixture(at, n.Kind, n.Spent, n.Kind == NodeKind.Chest ? game.NodeChestKind(n) : ChestKind.Upgrade);  // (a chest or relic stands in its room's middle: Game.NodeSpot)
                return;
            }
            if (n.Kind == NodeKind.Rule)
            {
                DrawRuleNode(n);
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
            if (n.Hidden)  // (an old save's fogged node, until the host clears it)
            {
                FogMark(c, 1.3f, col);
                return;
            }
            switch (n.Kind)
            {
                case NodeKind.Tower: TowerGlyph(c, 1.2f, col); break;
                case NodeKind.Vein: VeinGlyph(c, 1.2f, col); break;
                case NodeKind.Shop: Arc(c, 22f, 5f, col, 20); break;
                case NodeKind.Vendor:
                    Line(c - new V2(24f, 0f), c + new V2(24f, 0f), 6f, col);
                    Line(c - new V2(0f, 24f), c + new V2(0f, 24f), 6f, col);
                    break;
                case NodeKind.Relic: RelicGem(c + new V2(0f, Mathf.Sin(t * 2.6f) * 3f), 1.25f, col); break;
                case NodeKind.VectorChest:
                    ChestBox(c + new V2(0f, 8f), 1.1f, col, false);
                    VectorMark(c + new V2(0f, -34f + Mathf.Sin(t * 2.6f) * 3f), 0.9f, col);
                    break;
                default:
                    ChestBox(c + new V2(0f, 6f), 1.2f, col, false);
                    ChestEmblem(game.NodeChestKind(n), c + new V2(0f, -34f + Mathf.Sin(t * 2.6f) * 3f), 1.8f, col, col, col.A);  // what it holds
                    break;
            }
        }

        /// A rule node (Docs/RuleRooms.md): the whole 8 x 8 tile area a claiming room must enclose, in its rule's colour: a faint tint, hazard hatching,
        /// breathing corner brackets and the rule's mark in the middle.
        void DrawRuleNode(MapNode n)
        {
            var ink = NodeInkOf(n);
            float t = game.AnimT, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.5f), h = Expansion.RuleHalf, s = 2f * h;
            V2 c = n.Pos;
            float x0 = c.X - h, y0 = c.Y - h;
            FillRect(x0, y0, s, s, ink.WithAlpha(0.04f + 0.03f * pulse));
            for (float d = 100f; d < 2f * s; d += 100f)  // 45 degree hatching, cut off at the square's edges
            {
                V2 a = d <= s ? new V2(x0, y0 + d) : new V2(x0 + d - s, y0 + s);
                V2 b = d <= s ? new V2(x0 + d, y0) : new V2(x0 + s, y0 + d - s);
                Line(a, b, 2f, ink.WithAlpha(0.1f));
            }
            var edge = ink.WithAlpha(0.35f + 0.2f * pulse);
            NodeFrame(x0, y0, s, s, 2f, edge);
            var br = ink.WithAlpha(0.65f + 0.3f * pulse);
            for (int sx = 0; sx < 2; sx++)  // corner brackets
                for (int sy = 0; sy < 2; sy++)
                {
                    float cx = sx == 0 ? x0 : x0 + s, cy = sy == 0 ? y0 : y0 + s, dx = sx == 0 ? 1f : -1f, dy = sy == 0 ? 1f : -1f;
                    Line(new V2(cx, cy), new V2(cx + dx * 110f, cy), 8f, br);
                    Line(new V2(cx, cy), new V2(cx, cy + dy * 110f), 8f, br);
                }
            Circle(c, 70f, ink.WithAlpha(0.08f + 0.05f * pulse), 32);
            Arc(c, 56f, 3f, ink.WithAlpha(0.6f), 32);
            for (int k = 0; k < 4; k++)  // dashes turning slowly round the mark
                Arc(c, 66f, 3f, ink.WithAlpha(0.45f), 8, t * 0.6f + k * MathX.Tau / 4f, 0.8f);
            var m = ink.WithAlpha(0.95f);
            if (n.Hidden)  // fogged: its rule shows once someone walks near
            {
                FogMark(c, 1.1f, m);
                return;
            }
            switch ((NodeRule)n.Rule)
            {
                case NodeRule.Dark:  // an eye behind drifting mist
                    Arc(c, 26f, 4f, m, 24, 0f, MathX.Tau);
                    Circle(c, 9f, m, 12);
                    Line(c - new V2(40f, 0f), c - new V2(26f, 0f), 4f, m);
                    Line(c + new V2(26f, 0f), c + new V2(40f, 0f), 4f, m);
                    break;
                case NodeRule.Frozen:  // a snowflake
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * MathX.Tau / 6f + 0.3f;
                        V2 d = new V2(Mathf.Cos(a), Mathf.Sin(a)) * 32f;
                        Line(c - d, c + d, 4f, m);
                    }
                    break;
                case NodeRule.BloodMoon:  // a moon with a drop of blood
                    Circle(c, 24f, m.WithAlpha(0.35f), 24);
                    Arc(c, 24f, 4f, m, 24);
                    Circle(c + new V2(0f, 38f + Mathf.Sin(t * 3f) * 3f), 5f, m, 10);
                    break;
                case NodeRule.Burning:  // a flame: a round base drawn up to a flickering tip, a small flame inside
                {
                    float fl = Mathf.Sin(t * 7f) * 3f;
                    Arc(c + new V2(0f, 10f), 16f, 5f, m, 24);
                    Line(c + new V2(-14f, 2f), c + new V2(fl, -36f), 5f, m);
                    Line(c + new V2(14f, 2f), c + new V2(fl, -36f), 5f, m);
                    Arc(c + new V2(0f, 14f), 7f, 4f, m.WithAlpha(0.7f), 12);
                    break;
                }
                case NodeRule.Swarm:  // a crowd of dots
                    for (int k = 0; k < 7; k++)
                    {
                        float a = k * 2.4f + t * 0.5f, r = 8f + 5f * k;
                        Circle(c + new V2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.8f), 4f, m, 8);
                    }
                    break;
                default:  // Elite: a diamond with a star inside
                    Line(c + new V2(0f, -32f), c + new V2(28f, 0f), 4f, m);
                    Line(c + new V2(28f, 0f), c + new V2(0f, 32f), 4f, m);
                    Line(c + new V2(0f, 32f), c + new V2(-28f, 0f), 4f, m);
                    Line(c + new V2(-28f, 0f), c + new V2(0f, -32f), 4f, m);
                    Circle(c, 7f, m, 12);
                    break;
            }
        }

        /// The shop's stall, the vendor's pedestal or the chest, centred on the node (or on an opened floating shop or vendor room).
        void DrawNodeFixture(V2 c, NodeKind kind, bool spent, ChestKind chest = ChestKind.Upgrade)
        {
            var ink = kind == NodeKind.Chest ? ChestInkOf(chest) : NodeInk(kind);
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
                case NodeKind.Relic:
                {
                    if (spent)
                    {
                        DrawChestLook(c, 0.3f, t, ChestKind.Relic, true, 99f);  // opened, lid up and dim
                        break;
                    }
                    Arc(c, Relics.TakeReach + 18f + 4f * pulse, 2f, ink.WithAlpha(0.35f + 0.25f * pulse), 28);  // walk up to it
                    DrawChestLook(c, 1f, t, ChestKind.Relic, false, 0f);  // ChestLooks.cs
                    for (int k = 0; k < 3; k++)  // motes turning round it
                    {
                        float a = t * 1.4f + k * MathX.Tau / 3f;
                        Circle(c + new V2(Mathf.Cos(a) * 40f, Mathf.Sin(a) * 40f * 0.6f), 2.5f, ink.WithAlpha(0.7f), 8);
                    }
                    break;
                }
                case NodeKind.Tower:  // a mast on a base with a dish, a radar beam sweeping round it, a ping rippling out
                {
                    Circle(c, 70f, ink.WithAlpha(0.05f + 0.04f * pulse), 28);
                    float sweep = t * 1.6f;
                    for (int k = 0; k < 6; k++)  // the beam and its fading trail
                    {
                        float a = sweep - k * 0.09f;
                        Line(c, c + new V2(Mathf.Cos(a), Mathf.Sin(a)) * 84f, 3f - k * 0.35f, ink.WithAlpha(0.42f - k * 0.06f));
                    }
                    float ping = (t * 0.5f) % 1f;
                    Arc(c, 30f + 70f * ping, 2f, ink.WithAlpha(0.5f * (1f - ping)), 32);
                    Arc(c, 84f, 2f, ink.WithAlpha(0.3f), 32);
                    TowerGlyph(c, 1.1f, ink.WithAlpha(0.95f));
                    break;
                }
                case NodeKind.Vein:  // ore in the floor: dark rocks with gold nuggets, sparkles winking
                {
                    Circle(c, 72f, ink.WithAlpha(0.05f + 0.04f * pulse), 28);
                    VeinGlyph(c, 1.3f, ink.WithAlpha(0.95f));
                    for (int k = 0; k < 4; k++)
                    {
                        float ph = (t * 0.9f + k * 0.37f) % 1f, a = k * 1.7f + 0.4f;
                        if (ph > 0.5f)
                            continue;
                        float sz = 7f * Mathf.Sin(ph * 2f * Mathf.PI);
                        V2 p = c + new V2(Mathf.Cos(a) * 38f, Mathf.Sin(a) * 28f);
                        Line(p - new V2(sz, 0f), p + new V2(sz, 0f), 2f, ink.WithAlpha(0.9f));
                        Line(p - new V2(0f, sz), p + new V2(0f, sz), 2f, ink.WithAlpha(0.9f));
                    }
                    break;
                }
                case NodeKind.VectorChest:
                {
                    if (spent)
                        break;
                    Arc(c, Game.VectorChestReach + 18f + 4f * pulse, 2f, ink.WithAlpha(0.35f + 0.25f * pulse), 28);  // walk up to it
                    DrawChestLook(c, 1f, t, ChestKind.Vector, false, 0f);  // ChestLooks.cs
                    for (int k = 0; k < 3; k++)  // motes turning round it
                    {
                        float a = t * 1.4f + k * MathX.Tau / 3f;
                        Circle(c + new V2(Mathf.Cos(a) * 42f, Mathf.Sin(a) * 42f * 0.6f), 2.5f, ink.WithAlpha(0.7f), 8);
                    }
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

        /// The HUD chip for your relic slot (Relics.cs), with the wave panel's other chips.
        bool DrawRelicChip(float x, float y)
        {
            var me = game.Me;
            if (me == null || me.Relic == RelicId.None)
                return false;
            string text = "RELIC  " + Relics.Name(me.Relic).ToUpperInvariant();
            float w = 30f + text.Length * 6.4f;
            var chip = new Rect(x, y, w, 20f);
            var ink = ToColor(RelicInk);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), ink);
            Ui.Label(new Rect(chip.x + 12f, chip.y, w, chip.height), text, 10, ink, TextAnchor.MiddleLeft, true);
            return true;
        }

        /// A relic: a cut diamond, scaled.
        void RelicGem(V2 c, float k, Rgba col)
        {
            V2 top = c + new V2(0f, -20f * k), right = c + new V2(15f * k, -6f * k), bottom = c + new V2(0f, 20f * k), left = c + new V2(-15f * k, -6f * k);
            Line(top, right, 3f * k, col);
            Line(right, bottom, 3f * k, col);
            Line(bottom, left, 3f * k, col);
            Line(left, top, 3f * k, col);
            Line(left, right, 2f * k, col.WithAlpha(col.A * 0.7f));  // the girdle
            Line(top, bottom, 1.5f * k, col.WithAlpha(col.A * 0.45f));
        }

        /// A fogged node's "?": a hook over a stem and a dot, scaled.
        void FogMark(V2 c, float k, Rgba col)
        {
            Arc(c + new V2(0f, -12f * k), 11f * k, 4f * k, col, 14, MathX.Tau * 0.5f, MathX.Tau * 0.6f);
            Line(c + new V2(8.9f * k, -5.5f * k), c + new V2(0f, 3f * k), 4f * k, col);
            Line(c + new V2(0f, 3f * k), c + new V2(0f, 9f * k), 4f * k, col);
            Circle(c + new V2(0f, 18f * k), 3f * k, col, 8);
        }

        /// A scout tower: a mast on legs with a dish at the top and two waves off it, scaled.
        void TowerGlyph(V2 c, float k, Rgba col)
        {
            V2 top = c + new V2(0f, -22f * k);
            Line(c + new V2(-14f * k, 20f * k), top, 3f * k, col);
            Line(c + new V2(14f * k, 20f * k), top, 3f * k, col);
            Line(c + new V2(-8f * k, 4f * k), c + new V2(8f * k, 4f * k), 2.5f * k, col);
            Line(c + new V2(-17f * k, 20f * k), c + new V2(17f * k, 20f * k), 3f * k, col);
            Circle(top, 4.5f * k, col, 10);
            Arc(top, 11f * k, 2.5f * k, col.WithAlpha(col.A * 0.8f), 10, -0.9f, 1.1f);
            Arc(top, 18f * k, 2.5f * k, col.WithAlpha(col.A * 0.55f), 12, -0.9f, 1.1f);
        }

        /// A gold vein: three rocks, each with a cut nugget, scaled.
        void VeinGlyph(V2 c, float k, Rgba col)
        {
            var rock = new Rgba(0.46f, 0.4f, 0.34f, 0.9f * col.A);
            V2 a = c + new V2(-15f * k, 7f * k), b = c + new V2(14f * k, 9f * k), d = c + new V2(0f, -9f * k);
            foreach (var r in new[] { a, b, d })
            {
                Circle(r, 13f * k, rock, 7);
                Arc(r, 13f * k, 2f * k, rock.WithAlpha(rock.A * 0.7f), 7);
            }
            V2[] gems = { a + new V2(2f * k, -2f * k), b + new V2(-2f * k, -1f * k), d + new V2(1f * k, 1f * k) };
            for (int i = 0; i < 3; i++)
            {
                V2 r = gems[i];
                float s = (i == 2 ? 7f : 5f) * k;
                Line(r + new V2(0f, -s), r + new V2(s, 0f), 2.5f * k, col);
                Line(r + new V2(s, 0f), r + new V2(0f, s), 2.5f * k, col);
                Line(r + new V2(0f, s), r + new V2(-s, 0f), 2.5f * k, col);
                Line(r + new V2(-s, 0f), r + new V2(0f, -s), 2.5f * k, col);
                Circle(r, s * 0.55f, col, 6);
            }
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

        void NodeFrame(float x, float y, float w, float h, float t, Rgba c)
        {
            FillRect(x, y, w, t, c);
            FillRect(x, y + h - t, w, t, c);
            FillRect(x, y, t, h, c);
            FillRect(x + w - t, y, t, h, c);
        }
    }
}
