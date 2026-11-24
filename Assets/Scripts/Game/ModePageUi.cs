using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Game Mode screen's page for Quick Play, Campaign, Boss Hunt and Survival, drawn from one model (`ModePage`, built by
    /// each mode's file) in the loadout's look: a slim one-line header (ring, name, tagline), the list under its heading on
    /// the left, a compact scope with the dossier box under it in the middle, and a sidebar on the right: YOUR RECORDS (label and value rows) and
    /// RULES (lit: what the mode adds; dim: what it takes away), with the mode's note at its foot.
    /// Preview: `LOBBYTAB=8 shot.sh x lobby` (10 Campaign with CAMPAIGN=n, SURVLOBBY=2 Survival, 4 Boss Hunt with HUNT=1).
    public partial class ArenaApp
    {
        class ModeRowData
        {
            public string Num, Name, Chip;
            public bool Known, Lit;
            public EnemyType Model;
            public Color ChipCol;
        }

        /// What the page shows for one row: the scope's labels and model, the name, the copy and the records.
        class ModeFocus
        {
            public string ScopeL, ScopeR, Eyebrow, Name, Copy;
            public bool Unknown;
            public EnemyType Model;
            public Color CopyCol = Color.white;
            public string[] RecVals, RecLabels;
        }

        class ModePage
        {
            public string Title, Tagline, Ring, ListHead, ListCap, Note;
            public Color Acc;
            public float Pct = -1f;
            public List<ModeRowData> Rows = new List<ModeRowData>();
            public int Default, Shown;
            public Func<int, ModeFocus> Focus;
            public Action<Rect, int> ScopeDraw;  // in place of the model (Survival's graph)
            public Action<int> OnPick;           // a click on a known row (Boss Hunt's pick)
            public string[] Rules;
            public bool ShowLocked;   // locked rows still show their model (name hidden) and can be hovered (Campaign)
        }

        void DrawModePage(Rect r, float t, ModePage p)
        {
            LobbyKit.Panel(r);
            float top = ModeHeadBand(r, p) + 6f;
            float x = r.x + 20f, w = r.width - 40f, bottom = r.yMax - 18f;
            const float listW = 270f, gap = 22f;
            // Middle: the scope under its heading, then the dossier.
            const float sideW = 250f, dosH = 112f;
            float x2 = x + listW + gap, w2 = w - listW - sideW - 2f * gap, y2 = top + 36f;
            // A compact scope with the dossier under it, centred in the column (the same size on every mode).
            float cw = Mathf.Min(w2, 460f), ch = Mathf.Min(bottom - y2 - dosH - 10f, cw * 0.58f);
            float cy = y2 + Mathf.Max(0f, (bottom - y2 - ch - 10f - dosH) * 0.5f);
            var scope = new Rect(x2 + (w2 - cw) * 0.5f, cy, cw, ch);
            var dos = new Rect(scope.x, scope.yMax + 10f, cw, dosH);
            bool turning = p.ScopeDraw == null && ModeScopeDrag(scope);

            int shown = ModeList(x, top, listW, bottom - top, p, out var rows, turning);
            var f = p.Focus(shown);
            LobbyKit.Heading(x2, top, w2, f.ScopeL, f.ScopeR, fxReduced, 14);
            ModeScopeArea(scope, p, f);
            ModeDossier(dos, p, f, p.Rows[shown].Chip, p.Rows[shown].ChipCol);

            // Vertical separators between the three columns, in the gaps.
            var sepCol = p.Acc.WithAlpha(0.22f);
            Ui.Fill(new Rect(x + listW + gap * 0.5f, top, 1f, bottom - top), sepCol);
            Ui.Fill(new Rect(x2 + w2 + gap * 0.5f, top, 1f, bottom - top), sepCol);

            // Right: the sidebar, records as label and value rows, then the rules, the note at the foot.
            float sx = x2 + w2 + gap, sy = top;
            LobbyKit.Heading(sx, sy, sideW, "YOUR RECORDS", null, fxReduced, 14);
            sy += 38f;
            for (int i = 0; i < f.RecVals.Length; i++)
            {
                var rr = new Rect(sx, sy, sideW, 26f);
                if (i % 2 == 0)
                    Ui.Fill(rr, CrtStyle.Ink.WithAlpha(0.05f));
                Ui.Label(new Rect(rr.x + 10f, rr.y, sideW * 0.6f, rr.height), f.RecLabels[i], 11, CrtStyle.Dim, TextAnchor.MiddleLeft, true);
                LobbyKit.Fit(new Rect(rr.x + sideW * 0.45f, rr.y, sideW * 0.55f - 10f, rr.height), f.RecVals[i], 15, 10, CrtStyle.Gold, TextAnchor.MiddleRight, fxReduced, false);
                sy += 27f;
            }
            sy += 14f;
            LobbyKit.Heading(sx, sy, sideW, "RULES", null, fxReduced, 14);
            sy += 38f;
            foreach (var rule in p.Rules)
            {
                bool takes = rule.StartsWith("NO ");
                Ui.Fill(new Rect(sx + 4f, sy + 7f, 6f, 6f), takes ? CrtStyle.Dim.WithAlpha(0.5f) : CrtStyle.Gold);
                Ui.Label(new Rect(sx + 18f, sy, sideW - 18f, 20f), rule, 11, takes ? CrtStyle.Ink.WithAlpha(0.7f) : CrtStyle.Gold, TextAnchor.MiddleLeft, true);
                sy += 22f;
            }
            if (!string.IsNullOrEmpty(p.Note))
                Ui.Label(new Rect(sx, Mathf.Max(sy + 10f, bottom - 40f), sideW, 40f), p.Note, 11, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
            ModeModels(rows, scope, t, p, f, shown);
        }

        /// The slim header: a small ring, then the name with the tagline beside it on one line, over the accent rule.
        static float ModeHeadBand(Rect r, ModePage p)
        {
            float x = r.x + 20f, y = r.y + 12f, tx = x;
            if (p.Pct >= 0f)
            {
                ModeRing(new Vector2(x + 18f, y + 16f), 17f, p.Pct, p.Acc, p.Ring, 10);
                tx = x + 46f;
            }
            const int titleSz = 18;
            const float titleSp = 4f;
            float tw = LobbyKit.SpacedWidth(p.Title, titleSz, titleSp);
            LobbyKit.Spaced(new Rect(tx, y + 4f, tw + 12f, 24f), p.Title, titleSz, p.Acc, TextAnchor.MiddleLeft, titleSp);
            float gx = tx + tw + 22f;
            Ui.Label(new Rect(gx, y + 6f, r.xMax - 20f - gx, 22f), p.Tagline, 12, CrtStyle.Desc, TextAnchor.MiddleLeft);
            Ui.Fill(new Rect(r.x + 20f, y + 38f, r.width - 40f, 1f), p.Acc.WithAlpha(0.3f));
            return y + 46f;
        }

        /// The list column under its heading; rows shrink to fit. Returns the row shown (hovered, else the page's default).
        /// While `hold` (the scope's model is being turned) the row shown stays put and nothing is hovered.
        int ModeList(float x, float y, float w, float h, ModePage p, out Rect[] rows, bool hold = false)
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
                if (!hold && rows[i].Contains(mp) && (p.Rows[i].Known || p.ShowLocked))
                    hover = i;
            }
            int shown = p.Shown = hold ? Mathf.Clamp(p.Shown, 0, n - 1) : hover >= 0 ? hover : p.Default;
            if (hover >= 0 && p.Rows[hover].Known && p.OnPick != null && Ui.HitArea(rows[hover]))
                p.OnPick(hover);
            for (int i = 0; i < n; i++)
            {
                var d = p.Rows[i];
                ModeRow(rows[i], d.Num, d.Name, d.Known, d.Lit, i == shown, i == hover, p.Acc, d.Chip, d.ChipCol, p.ShowLocked);
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
        }

        void ModeScopeArea(Rect scope, ModePage p, ModeFocus f)
        {
            ModeScope(scope, "", "", p.ScopeDraw == null);
            if (p.ScopeDraw != null)
                p.ScopeDraw(new Rect(scope.x + 14f, scope.y + 30f, scope.width - 28f, scope.height - 48f), p.Shown);
            else if (f.Unknown && !p.ShowLocked)
                CrtUi.Text(scope, "?", 72, PhosGreen.WithAlpha(0.7f), TextAnchor.MiddleCenter, true, FxCalm);
            else
                LobbyKit.Spaced(new Rect(scope.x, scope.yMax - 20f, scope.width, 14f), "DRAG TO ROTATE", 9, PhosGreen.WithAlpha(0.45f), TextAnchor.MiddleCenter, 2f);
        }

        void ModeModels(Rect[] rows, Rect scope, float t, ModePage p, ModeFocus f, int shown)
        {
            if (BeginUiGl())
            {
                for (int i = 0; i < rows.Length; i++)
                    if (p.Rows[i].Known || p.ShowLocked)
                        ModeRowModel(rows[i], p.Rows[i].Model, t, i == shown);
                if (p.ScopeDraw == null && (!f.Unknown || p.ShowLocked))
                    ModeScopeModel(scope, f.Model, t);
                EndUiGl();
            }
            ModeScopeFinish(scope);
        }
    }
}
