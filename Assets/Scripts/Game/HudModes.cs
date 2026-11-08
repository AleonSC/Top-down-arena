using System;
using UnityEngine;

namespace Arena
{
    /// The HUD modes on H: 0 full (Hud.cs), 1 corner text, 2 on the player, 3 hidden. The two partial modes keep only
    /// what you act on mid-fight: health, ability cooldowns and enemies left (a boss's slim bar stays, as when hidden), in the
    /// CRT console's look: Docs/Mockups/crt-hud-modes.html, A (Phosphor Corners) and E (Callout Tag). Every number sits by its
    /// key, and the ult shows its charge % as on the full HUD. Local to each player: no protocol change.
    public partial class ArenaApp
    {
        public const int HudModeCount = 4;
        static readonly string[] HudModeNames = { "Full", "Corner text", "On the player", "Hidden" };
        int hudMode;          // 0 full, 1 corner text, 2 on the player, 3 hidden
        float miniT;          // 0..1: the partial HUD easing in and out

        void SetHudMode(int mode, bool save = true)
        {
            hudMode = ((mode % HudModeCount) + HudModeCount) % HudModeCount;
            hudShown = hudMode == 0;  // the full console only shows in mode 0 (Hud.cs)
            if (save)
            {
                PlayerPrefs.SetInt("hud_mode", hudMode);  // remembered between launches
                PlayerPrefs.Save();
            }
        }

        /// Eases the partial HUD in or out (called every frame from Update).
        void TickMiniHud()
        {
            bool want = (hudMode == 1 || hudMode == 2) && !LaunchHoldsHud && !BossIntroHoldsHud && !RecallActive;
            miniT = MathX.MoveTowards(miniT, want ? 1f : 0f, Time.unscaledDeltaTime / HudFadeTime);
        }

        void DrawMiniHud(Player me)
        {
            if (me == null || miniT <= 0f)
                return;
            float a = miniT * miniT * (3f - 2f * miniT);
            if (hudMode == 2)
                MiniOnPlayer(me, a);
            else if (hudMode == 1)
                MiniCorners(me, a);
        }

        // ---- The CRT pieces both partial HUDs share (Docs/Mockups/crt-hud-modes.html) --------------------------------------------

        const float MiniLow = 0.25f;  // below this much health the readouts turn red and blink

        /// a, dropped to 35% on the off beat of a 1 s blink (steady with reduced effects).
        float MiniBlink(float a) => !CabCalm && (int)(Time.unscaledTime * 2f) % 2 == 1 ? a * 0.35f : a;

        static Color MiniA(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        /// Glowing CRT text, faded by a.
        void MiniText(Rect r, string s, int size, Color c, float a, TextAnchor align = TextAnchor.MiddleLeft) => HudCrt(r, s, size, MiniA(c, a), align);

        /// A segmented bar: n cells with a 2 px gap, the first frac of them lit.
        static void MiniCells(Rect r, float frac, int n, Color c, float a)
        {
            float cw = (r.width - 2f * (n - 1)) / n;
            int lit = Mathf.RoundToInt(Mathf.Clamp01(frac) * n);
            for (int i = 0; i < n; i++)
                Ui.Fill(new Rect(r.x + i * (cw + 2f), r.y, cw, r.height), MiniA(c, (i < lit ? 1f : 0.18f) * a));
        }

        /// The lobby's bracket corners, small: a 10 px L on each corner, 10 px outside r.
        static void MiniBrackets(Rect r, Color c, float a)
        {
            var k = MiniA(c, 0.75f * a);
            float x0 = r.x - 10f, y0 = r.y - 10f, x1 = r.xMax + 10f, y1 = r.yMax + 10f;
            Ui.Fill(new Rect(x0, y0, 10f, 2f), k); Ui.Fill(new Rect(x0, y0, 2f, 10f), k);
            Ui.Fill(new Rect(x1 - 10f, y0, 10f, 2f), k); Ui.Fill(new Rect(x1 - 2f, y0, 2f, 10f), k);
            Ui.Fill(new Rect(x0, y1 - 2f, 10f, 2f), k); Ui.Fill(new Rect(x0, y1 - 10f, 2f, 10f), k);
            Ui.Fill(new Rect(x1 - 10f, y1 - 2f, 10f, 2f), k); Ui.Fill(new Rect(x1 - 2f, y1 - 10f, 2f, 10f), k);
        }

        /// Scanlines over a readout: a dark line every 3 px.
        static void MiniScan(Rect r, float a)
        {
            var c = new Color(0f, 0f, 0f, 0.28f * a);
            for (float y = r.y; y < r.yMax; y += 3f)
                Ui.Fill(new Rect(r.x, y, r.width, 1f), c);
        }

        /// A keycap: inverse video (dark text on a block of c) when lit, else a thin outline in c. Returns its width.
        float MiniCap(float x, float y, float h, string key, int size, Color c, bool lit, float a, bool centre = false)
        {
            float w = CrtUi.TitleWidth(key, size) + 10f;
            if (centre)
                x -= w / 2f;
            var r = new Rect(x, y, w, h);
            if (lit)
                Ui.Fill(r, MiniA(c, a));
            else
                CrtKit.Frame(r, 1f, MiniA(c, a));
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;
            Ui.Label(new Rect(r.x, r.y + 1f, r.width, r.height), key, size, lit ? MiniA(CrtStyle.Bg, a) : MiniA(c, a), TextAnchor.MiddleCenter, false);
            Ui.CrtFont = was;
            return w;
        }

        /// The glass behind a small plate: the screen colour, a thin phosphor rim and the bezel round it, scanlines over it all
        /// (call MiniScan after the content).
        static void MiniPlate(Rect r, Color ink, float a)
        {
            Ui.Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), MiniA(CrtStyle.Bezel, 0.9f * a));
            Ui.Fill(r, MiniA(CrtStyle.Bg, 0.9f * a));
            CrtKit.Frame(r, 1f, MiniA(ink, 0.55f * a));
        }

        /// Slot i's state: false for an empty slot. ready: a charge is up. frac: how far the recharge has come (1 when ready).
        bool MiniSlot(Player me, int i, out bool ready, out float frac)
        {
            var slot = me.Slots[i];
            ready = slot.Charges > 0;
            frac = 1f;
            if (slot.Id == AbilityId.None)
                return false;
            if (slot.Charges < slot.MaxCharges)
                frac = Mathf.Clamp01(1f - slot.Timer / Mathf.Max(0.01f, game.Cooldown(me, i)));
            if (ready && i != Upgrades.UltSlot)
                frac = 1f;
            return true;
        }

        static string MiniSecs(float t) => t.ToString(t < 10f ? "0.0" : "0");

        /// The wave as "WAVE 06 / 50" (or the hunt's or practice's own count).
        string MiniWave()
        {
            var practice = game.Practice;
            if (practice != null)
                return "PRACTICE " + Math.Max(1, game.PracticeIndex + 1).ToString("00") + " / " + practice.Waves.Count;
            if (game.Hunt != null)
                return "HUNT WAVE " + game.HuntWave.ToString("00") + " / " + Hunts.Length;
            return "WAVE " + game.Wave.ToString("00") + (game.Endless ? "" : " / " + Game.MaxWave);
        }

        // ---- A: Phosphor Corners. Health bottom-left, the keys bottom-centre, the wave top-right; text only, framed by brackets.

        void MiniCorners(Player me, float a)
        {
            Color ink = CrtUi.Ink, dim = CrtUi.Dim, red = CrtStyle.Danger;
            float frac = Mathf.Clamp01(me.Hp / Mathf.Max(1f, me.MaxHp));
            bool low = !me.Downed && frac < MiniLow;

            // Health (or the revive)
            var hr = new Rect(40f, H - 110f, 230f, 76f);
            Color hc = me.Downed || low ? red : ink;
            MiniBrackets(hr, hc, a);
            if (me.Downed)
            {
                MiniText(new Rect(hr.x, hr.y, hr.width, 16f), "REVIVE  " + Mathf.RoundToInt(me.Revive * 100f) + "%", 16, red, a);
                MiniText(new Rect(hr.x, hr.y + 14f, hr.width, 46f), "DOWNED", 46, red, MiniBlink(a));
                MiniCells(new Rect(hr.x, hr.y + 64f, hr.width, 8f), me.Revive, 20, red, a);
            }
            else
            {
                string hp = Mathf.CeilToInt(me.Hp).ToString();
                MiniText(new Rect(hr.x, hr.y, hr.width, 16f), "HEALTH", 16, dim, a);
                MiniText(new Rect(hr.x, hr.y + 12f, hr.width, 50f), hp, 52, hc, a);
                MiniText(new Rect(hr.x + CrtUi.TitleWidth(hp, 52) + 8f, hr.y + 30f, 120f, 26f), "/ " + Mathf.CeilToInt(me.MaxHp), 24, low ? red : dim, a);
                MiniCells(new Rect(hr.x, hr.y + 64f, hr.width, 8f), frac, 20, hc, low ? MiniBlink(a) : a);
            }
            MiniScan(new Rect(hr.x - 4f, hr.y - 4f, hr.width + 8f, hr.height + 8f), a);

            // The keys: each keycap over its seconds left (RDY when ready) and a six-cell recharge bar; the ult in gold with its charge.
            if (!me.Downed)
            {
                int shown = 0;
                for (int i = 0; i < Upgrades.SlotCount; i++)
                    if (me.Slots[i].Id != AbilityId.None)
                        shown++;
                const float colW = 62f, gap = 16f;
                float x = W / 2f - (shown * colW + (shown - 1) * gap) / 2f, y = H - 92f;
                for (int i = 0; i < Upgrades.SlotCount; i++)
                {
                    if (!MiniSlot(me, i, out bool ready, out float f))
                        continue;
                    bool ult = i == Upgrades.UltSlot;
                    bool full = ult ? me.Slots[i].Charges > 0 : ready;
                    Color c = ult ? Ui.Gold : ready ? KeyGreen : dim;
                    float cx = x + colW / 2f;
                    MiniCap(cx, y, 20f, ult ? "ULT·" + SlotKeys[i] : SlotKeys[i], 18, c, full, ult && full ? 0.7f * a + 0.3f * a * Mathf.Sin(Time.unscaledTime * 4f) : a, true);
                    string val = ult ? (full ? "READY" : Mathf.FloorToInt(f * 100f) + "%") : ready ? "RDY" : MiniSecs(me.Slots[i].Timer);
                    MiniText(new Rect(x - 10f, y + 24f, colW + 20f, 24f), val, 24, ult ? Ui.Gold : ready ? KeyGreen : new Color(0.91f, 0.94f, 1f), a, TextAnchor.MiddleCenter);
                    MiniCells(new Rect(x, y + 52f, colW, 4f), f, 6, ult ? Ui.Gold : ready ? KeyGreen : ink, a);
                    x += colW + gap;
                }
                MiniScan(new Rect(W / 2f - 260f, y - 2f, 520f, 62f), a);
            }

            // The wave and the enemies left
            var wr = new Rect(W - 240f, 40f, 200f, 76f);
            MiniBrackets(wr, ink, a);
            MiniText(new Rect(wr.x, wr.y, wr.width, 16f), MiniWave(), 16, dim, a, TextAnchor.MiddleRight);
            MiniText(new Rect(wr.x, wr.y + 12f, wr.width, 50f), game.Remaining.ToString(), 52, ink, a, TextAnchor.MiddleRight);
            MiniText(new Rect(wr.x, wr.y + 60f, wr.width, 16f), "ENEMIES LEFT", 16, dim, a, TextAnchor.MiddleRight);
            MiniScan(new Rect(wr.x - 4f, wr.y - 4f, wr.width + 8f, wr.height + 8f), a);
        }

        // ---- E: Callout Tag. A leader line from you to a small glass tag (up and to the left; it flips right near the left wall
        // and below you near the top), and the wave line at the top centre. The tag hides while you're downed: the DOWNED band
        // and the revive plate by your body already say it.

        static string MiniShortKey(string k) => k == "SPACE" ? "SPC" : k == "SHIFT" ? "SHF" : k;

        void MiniOnPlayer(Player me, float a)
        {
            if (!me.Downed)
                MiniTag(me, a);
            MiniWaveLine(a);
        }

        void MiniTag(Player me, float a)
        {
            Color ink = CrtUi.Ink, dim = CrtUi.Dim, red = CrtStyle.Danger;
            V2 at = ToUi(me.Disp);
            float frac = Mathf.Clamp01(me.Hp / Mathf.Max(1f, me.MaxHp));
            bool low = frac < MiniLow;
            Color tone = low ? red : ink;

            // Where the tag goes
            const float tw = 176f, th = 62f, off = 52f;
            float r = me.Def.BodyR * UiWorldScale;
            bool right = at.X - off - tw < 30f, below = at.Y - 92f < 70f;
            float tx = right ? at.X + off : at.X - off - tw, ty = below ? at.Y + 30f : at.Y - 92f;

            // The leader: a dot on your edge, a diagonal out to the tag's level, then a short run into its side.
            float sx = right ? 1f : -1f, sy = below ? 1f : -1f;
            var p0 = new Vector2(at.X + sx * r * 0.75f, at.Y + sy * r * 0.75f);
            float ly = below ? ty + 16f : ty + th - 16f, edge = right ? tx : tx + tw;
            var p1 = new Vector2(edge - sx * 14f, ly);
            var lc = MiniA(tone, 0.8f * a);
            Ui.Fill(new Rect(p0.x - 2f, p0.y - 2f, 5f, 5f), lc);
            int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p0, p1) / 1.5f));
            for (int s = 0; s <= steps; s++)
            {
                float u = (float)s / steps;
                var p = new Vector2(p0.x + (p1.x - p0.x) * u, p0.y + (p1.y - p0.y) * u);
                Ui.Fill(new Rect(p.x - 0.75f, p.y - 0.75f, 1.5f, 1.5f), lc);
            }
            Ui.Fill(new Rect(Mathf.Min(p1.x, edge), ly - 0.75f, Mathf.Abs(edge - p1.x), 1.5f), lc);

            // The tag
            var tr = new Rect(tx, ty, tw, th);
            MiniPlate(tr, tone, a);
            float cx = tx + 8f, cw = tw - 16f;
            string hp = Mathf.CeilToInt(me.Hp).ToString(), max = "/" + Mathf.CeilToInt(me.MaxHp);
            float mw = CrtUi.TitleWidth(max, 15);
            MiniText(new Rect(cx, ty + 6f, 40f, 18f), "HP", 16, low ? red : dim, a);
            MiniText(new Rect(cx, ty + 3f, cw - mw - 2f, 22f), hp, 24, low ? red : Color.white, a, TextAnchor.MiddleRight);
            MiniText(new Rect(cx + cw - mw, ty + 9f, mw, 16f), max, 15, low ? red : dim, a);
            MiniCells(new Rect(cx, ty + 26f, cw, 5f), frac, 12, tone, low ? MiniBlink(a) : a);

            // The chips: the key, then its seconds left; lit green when ready; the ult in gold with its charge.
            float x = cx, y = ty + 38f;
            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                if (!MiniSlot(me, i, out bool ready, out float f))
                    continue;
                bool ult = i == Upgrades.UltSlot;
                if (ult)
                {
                    bool full = me.Slots[i].Charges > 0;
                    float ua = full && !CabCalm ? a * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f)) : a;
                    x += MiniCap(x, y, 16f, SlotKeys[i] + (full ? " ULT" : " " + Mathf.FloorToInt(f * 100f) + "%"), 15, Ui.Gold, full, ua) + 4f;
                }
                else if (ready)
                    x += MiniCap(x, y, 16f, MiniShortKey(SlotKeys[i]), 15, KeyGreen, true, a) + 4f;
                else
                    x += MiniCap(x, y, 16f, MiniShortKey(SlotKeys[i]) + " " + Mathf.CeilToInt(me.Slots[i].Timer), 15, dim, false, a) + 4f;
            }
            MiniScan(tr, a);
        }

        /// The wave line, top centre: WAVE 06/50  23 LEFT
        void MiniWaveLine(float a)
        {
            Color ink = CrtUi.Ink, dim = CrtUi.Dim;
            string wave = MiniWave(), left = game.Remaining.ToString();
            float w1 = CrtUi.TitleWidth(wave, 20), w2 = CrtUi.TitleWidth(left, 30), w3 = CrtUi.TitleWidth("LEFT", 20);
            float total = w1 + 14f + w2 + 6f + w3, x0 = W / 2f - total / 2f, y0 = 30f;
            MiniBrackets(new Rect(x0, y0, total, 30f), ink, a);
            MiniText(new Rect(x0, y0 + 6f, w1 + 4f, 22f), wave, 20, dim, a);
            MiniText(new Rect(x0 + w1 + 14f, y0, w2 + 4f, 32f), left, 30, ink, a);
            MiniText(new Rect(x0 + w1 + 20f + w2, y0 + 6f, w3 + 4f, 22f), "LEFT", 20, dim, a);
            MiniScan(new Rect(x0 - 4f, y0 - 4f, total + 8f, 38f), a);
        }
    }
}
