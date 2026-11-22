using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Game Mode screen's page for Quick Play, Campaign, Boss Hunt and Survival, drawn from one model (`ModePage`, built by
    /// each mode's file) in the loadout's look: a slim header (ring, name, fact chips, tagline), the list under its heading on
    /// the left, the scope and a dossier box on the right, then YOUR RECORDS (tiles) and RULES (lamp tags) along the bottom.
    /// Preview: `LOBBYTAB=8 shot.sh x lobby` (10 Campaign with CAMPAIGN=n, SURVLOBBY=2 Survival, 4 Boss Hunt with HUNT=1).
    public partial class ArenaApp
    {
        static List<ModePip2> ModeLadder(int waves, Color acc, bool endless, int every = 1)
        {
            var l = new List<ModePip2>();
            for (int i = 1; i < waves; i++)
                l.Add(new ModePip2 { Label = every == 1 || i % every == 0 ? i.ToString() : "", Col = CrtStyle.Ink, W = every == 1 ? 22f : 12f });
            l.Add(new ModePip2 { Label = "BOSS", Col = acc, Strong = true, W = 36f });
            if (endless)
                l.Add(new ModePip2 { Label = "ENDLESS", Col = CrtStyle.Gold, Strong = true, W = 62f });
            return l;
        }

        class ModeRowData
        {
            public string Num, Name, Chip;
            public bool Known, Lit;
            public EnemyType Model;
            public Color ChipCol;
        }

        class ModePip2
        {
            public string Label;
            public Color Col;
            public bool Strong;
            public float W;
        }

        /// What the page shows for one row: the scope's labels and model, the name, the copy, the ladder and the records.
        class ModeFocus
        {
            public string ScopeL, ScopeR, Eyebrow, Name, Copy;
            public bool Unknown;
            public EnemyType Model;
            public Color CopyCol = Color.white;
            public List<ModePip2> Ladder = new List<ModePip2>();
            public string LadderHead = "THE RUN";
            public string[] RecVals, RecLabels;
        }

        class ModePage
        {
            public string Title, Facts, Tagline, Ring, ListHead, ListCap, Note;
            public Color Acc;
            public float Pct = -1f;
            public List<ModeRowData> Rows = new List<ModeRowData>();
            public int Default, Shown;
            public Func<int, ModeFocus> Focus;
            public Action<Rect, int> ScopeDraw;  // in place of the model (Survival's graph)
            public Action<int> OnPick;           // a click on a known row (Boss Hunt's pick)
            public string[] Rules;
        }

        void DrawModePage(Rect r, float t, ModePage p)
        {
            LobbyKit.Panel(r);
            float top = ModeHeadBand(r, p) + 6f;
            float x = r.x + 20f, w = r.width - 40f, bottom = r.yMax - 18f;
            const float listW = 290f, gap = 22f;
            int shown = ModeList(x, top, listW, bottom - top, p, out var rows);
            var f = p.Focus(shown);

            // Right: the scope under its heading, then the dossier.
            float x2 = x + listW + gap, w2 = w - listW - gap, y2 = top;
            LobbyKit.Heading(x2, y2, w2, f.ScopeL, f.ScopeR, fxReduced, 14);
            y2 += 36f;
            const float lowH = 128f, dosH = 132f;
            float scopeH = bottom - lowH - 14f - dosH - 10f - y2;
            var scope = new Rect(x2, y2, w2, scopeH);
            ModeScopeArea(scope, p, f);
            y2 += scopeH + 10f;
            ModeDossier(new Rect(x2, y2, w2, dosH), p, f, p.Rows[shown].Chip, p.Rows[shown].ChipCol);

            // The bottom strip: records (tiles) and the rules (lamp tags).
            float ly = bottom - lowH, rw = w2 * 0.5f, uw = w2 - rw - gap;
            LobbyKit.Heading(x2, ly, rw, "YOUR RECORDS", null, fxReduced, 14);
            int n = f.RecVals.Length, per = n == 3 ? 3 : n <= 4 ? 2 : (n + 1) / 2;
            float tw = (rw - 8f * (per - 1)) / per;
            for (int i = 0; i < n; i++)
                LobbyKit.Tile(new Rect(x2 + (i % per) * (tw + 8f), ly + 36f + (i / per) * 48f, tw, 42f), f.RecVals[i], f.RecLabels[i], fxReduced);
            float ux = x2 + rw + gap;
            LobbyKit.Heading(ux, ly, uw, "RULES", null, fxReduced, 14);
            ModeRuleTags(ux, ly + 36f, uw, p.Rules);
            ModeModels(rows, scope, t, p, f, shown);
        }

        /// The slim header: the ring, the name, the fact chips, the tagline to the right of the chips.
        static float ModeHeadBand(Rect r, ModePage p)
        {
            float x = r.x + 20f, y = r.y + 14f, tx = x;
            if (p.Pct >= 0f)
            {
                ModeRing(new Vector2(x + 26f, y + 26f), 24f, p.Pct, p.Acc, p.Ring);
                tx = x + 66f;
            }
            LobbyKit.Spaced(new Rect(tx, y - 2f, 400f, 28f), p.Title, 22, p.Acc, TextAnchor.MiddleLeft, 5f);
            float cx = ModeChips(tx, y + 30f, p.Facts, p.Acc);
            Ui.Label(new Rect(cx + 8f, y + 30f, r.xMax - 20f - cx - 8f, 18f), p.Tagline, 12, CrtStyle.Desc, TextAnchor.MiddleLeft);
            Ui.Fill(new Rect(r.x + 20f, y + 60f, r.width - 40f, 1f), p.Acc.WithAlpha(0.3f));
            return y + 70f;
        }

        /// The list column under its heading; rows shrink to fit. Returns the row shown (hovered, else the page's default).
        int ModeList(float x, float y, float w, float h, ModePage p, out Rect[] rows)
        {
            LobbyKit.Heading(x, y, w, p.ListHead, p.ListCap, fxReduced, 14);
            y += 36f;
            h -= 36f;
            int n = p.Rows.Count;
            float step = Mathf.Min(48f, h / n), rowH = step - 5f;
            rows = new Rect[n];
            int hover = -1;
            var mp = Event.current.mousePosition;
            for (int i = 0; i < n; i++)
            {
                rows[i] = new Rect(x, y + i * step, w, rowH);
                if (rows[i].Contains(mp) && p.Rows[i].Known)
                    hover = i;
            }
            int shown = p.Shown = hover >= 0 ? hover : p.Default;
            if (hover >= 0 && p.OnPick != null && Ui.HitArea(rows[hover]))
                p.OnPick(hover);
            for (int i = 0; i < n; i++)
            {
                var d = p.Rows[i];
                ModeRow(rows[i], d.Num, d.Name, d.Known, d.Lit, i == shown, i == hover, p.Acc, d.Chip, d.ChipCol);
            }
            return shown;
        }

        /// The dossier under the scope: eyebrow, name, copy and the ladder, in a recessed box with an accent bar.
        static void ModeDossier(Rect b, ModePage p, ModeFocus f, string shownChip, Color shownCol)
        {
            DkSub(b);
            Ui.Fill(new Rect(b.x, b.y, 3f, b.height), p.Acc.WithAlpha(0.8f));
            float x = b.x + 16f, w = b.width - 32f, y = b.y + 10f;
            LobbyKit.Spaced(new Rect(x, y, w, 14f), shownChip, 10, shownCol, TextAnchor.MiddleLeft, 1.6f);
            Ui.Glitch(new Rect(x, y + 14f, w, 28f), f.Name, 20, Color.white, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(x, y + 44f, w, 36f), f.Copy, 12, f.CopyCol, TextAnchor.UpperLeft, false, true);
            if (!string.IsNullOrEmpty(p.Note))
                Ui.Label(new Rect(x, y + 64f, w, 18f), p.Note, 11, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
            if (f.Ladder.Count > 0)
            {
                float ly = b.yMax - 28f;
                LobbyKit.Spaced(new Rect(x, ly, 70f, 20f), f.LadderHead, 9, CrtStyle.Dim, TextAnchor.MiddleLeft, 1.4f);
                float total = 0f;
                foreach (var pp in f.Ladder) total += pp.W + 4f;
                float px = x + 76f, k = Mathf.Min(1f, (w - 76f) / total);
                foreach (var pp in f.Ladder)
                {
                    ModePip(new Rect(px, ly, pp.W * k, 20f), pp.Label, pp.Col, pp.Strong);
                    px += (pp.W + 4f) * k;
                }
            }
        }

        /// The rules as lamp tags (lit: something the mode adds; dim: something it takes away), wrapped across w.
        float ModeRuleTags(float x, float y, float w, string[] rules)
        {
            float cx = x;
            foreach (var rule in rules)
            {
                bool takes = rule.StartsWith("NO ");
                float tw = LobbyKit.SpacedWidth(rule, 10, 0.5f) + 30f;
                if (cx + tw > x + w)
                {
                    cx = x;
                    y += 27f;
                }
                var tr = new Rect(cx, y, tw, 22f);
                Color c = takes ? CrtStyle.Ink.WithAlpha(0.55f) : CrtStyle.Gold;
                Ui.Fill(tr, c.WithAlpha(0.06f));
                CrtKit.Frame(tr, 1f, c.WithAlpha(0.35f));
                Ui.Fill(new Rect(tr.x + 9f, tr.center.y - 3f, 6f, 6f), takes ? CrtStyle.Dim.WithAlpha(0.5f) : CrtStyle.Gold);
                LobbyKit.Spaced(new Rect(tr.x + 21f, tr.y, tw - 22f, 22f), rule, 10, takes ? CrtStyle.Ink.WithAlpha(0.7f) : CrtStyle.Gold, TextAnchor.MiddleLeft, 0.5f);
                cx += tw + 5f;
            }
            return y + 24f;
        }

        void ModeScopeArea(Rect scope, ModePage p, ModeFocus f)
        {
            ModeScope(scope, "", "", p.ScopeDraw == null);
            if (p.ScopeDraw != null)
                p.ScopeDraw(new Rect(scope.x + 14f, scope.y + 30f, scope.width - 28f, scope.height - 48f), p.Shown);
            else if (f.Unknown)
                CrtUi.Text(scope, "?", 72, PhosGreen.WithAlpha(0.7f), TextAnchor.MiddleCenter, true, FxCalm);
        }

        void ModeModels(Rect[] rows, Rect scope, float t, ModePage p, ModeFocus f, int shown)
        {
            if (BeginUiGl())
            {
                for (int i = 0; i < rows.Length; i++)
                    if (p.Rows[i].Known)
                        ModeRowModel(rows[i], p.Rows[i].Model, t, i == shown);
                if (p.ScopeDraw == null && !f.Unknown)
                    ModeScopeModel(scope, f.Model, t);
                EndUiGl();
            }
            ModeScopeFinish(scope);
        }
    }
}
