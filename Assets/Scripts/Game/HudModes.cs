using System;
using UnityEngine;

namespace Arena
{
    /// The HUD modes on H: 0 corner text (the default), 1 on the player, 2 hidden. (The full console of Hud.cs is no longer a
    /// mode: hudShown stays false.) The corner HUD keeps what you act on mid-fight plus the run's state: health and level,
    /// teammates, the keys with their mastery, the wave and enemies left, and lives, gold, vectors, the path and relic (a boss's
    /// slim bar stays, as when hidden). Both are in the
    /// CRT console's look: Docs/Mockups/crt-hud-modes.html, A (Phosphor Corners) and E (Callout Tag). Every number sits by its
    /// key with the ability's name, and the ultimate shows its charge % as on the full HUD. Local to each player: no protocol change.
    public partial class ArenaApp
    {
        public const int HudModeCount = 3;
        public const int HudCorner = 0, HudOnPlayer = 1, HudHidden = 2;
        static readonly string[] HudModeNames = { "Corner", "On the player", "Hidden" };
        int hudMode;          // HudCorner, HudOnPlayer or HudHidden
        float miniT = 1f;     // 0..1: the partial HUD easing in and out

        /// The saved mode: "hud_view" (0 corner, 1 on the player, 2 hidden), else the old four-mode "hud_mode" moved over
        /// (full and corner text both become corner).
        static int SavedHudMode() => PlayerPrefs.HasKey("hud_view") ? PlayerPrefs.GetInt("hud_view", 0) : Math.Max(0, PlayerPrefs.GetInt("hud_mode", 0) - 1);

        void SetHudMode(int mode, bool save = true)
        {
            hudMode = ((mode % HudModeCount) + HudModeCount) % HudModeCount;
            hudShown = false;  // the full console (Hud.cs) is retired
            if (save)
            {
                PlayerPrefs.SetInt("hud_view", hudMode);  // remembered between launches
                PlayerPrefs.Save();
            }
        }

        /// Eases the partial HUD in or out (called every frame from Update).
        void TickMiniHud()
        {
            bool want = hudMode != HudHidden && !LaunchHoldsHud && !BossIntroHoldsHud && !RecallActive;
            miniT = MathX.MoveTowards(miniT, want ? 1f : 0f, Time.unscaledDeltaTime / HudFadeTime);
        }

        void DrawMiniHud(Player me)
        {
            if (me == null || miniT <= 0f)
                return;
            float a = miniT * miniT * (3f - 2f * miniT);
            if (hudMode == HudOnPlayer)
                MiniOnPlayer(me, a);
            else if (hudMode == HudCorner)
                MiniCorners(me, a);
        }

        // ---- The CRT pieces both partial HUDs share (Docs/Mockups/crt-hud-modes.html) --------------------------------------------

        const float MiniLow = 0.25f;  // below this much health the readouts turn red and blink

        /// a, dropped to 35% on the off beat of a 1 s blink (steady with reduced effects).
        float MiniBlink(float a) => !FxCalm && (int)(Time.unscaledTime * 2f) % 2 == 1 ? a * 0.35f : a;


        /// Glowing CRT text, faded by a.
        void MiniText(Rect r, string s, int size, Color c, float a, TextAnchor align = TextAnchor.MiddleLeft) => HudCrt(r, s, size, c.ScaleAlpha(a), align);

        /// A segmented bar: n cells with a 2 px gap, the first frac of them lit.
        static void MiniCells(Rect r, float frac, int n, Color c, float a)
        {
            float cw = (r.width - 2f * (n - 1)) / n;
            int lit = Mathf.RoundToInt(Mathf.Clamp01(frac) * n);
            for (int i = 0; i < n; i++)
                Ui.Fill(new Rect(r.x + i * (cw + 2f), r.y, cw, r.height), c.ScaleAlpha((i < lit ? 1f : 0.18f) * a));
        }

        /// The Hemomancer's blood floor (Game.HemoFloor) over MiniCells: a crimson tick past the cells it guards (the first
        /// fifth), above and below them; brighter, and the guarded cells rimmed, while you stand at the floor.
        static void MiniBloodFloor(Rect r, Player p, int n, float a)
        {
            if (p.Downed || p.Kit != Kit.Hemomancer || p.MaxHp <= 0f)
                return;
            float cw = (r.width - 2f * (n - 1)) / n;
            int under = Mathf.Clamp(Mathf.RoundToInt(Game.HemoFloor * n), 1, n);
            bool at = Game.AtHealthFloor(p);
            Color red = CrtStyle.Danger;
            float x = r.x + under * (cw + 2f) - 1.5f, rise = Mathf.Max(3f, r.height * 0.5f);
            Ui.Fill(new Rect(x - 0.5f, r.y - rise, 2f, r.height + 2f * rise), red.ScaleAlpha((at ? 1f : 0.8f) * a));
            if (at)
                for (int i = 0; i < under; i++)
                {
                    var c = new Rect(r.x + i * (cw + 2f), r.y, cw, r.height);
                    Ui.Fill(new Rect(c.x, c.y, c.width, 1f), red.ScaleAlpha(a));
                    Ui.Fill(new Rect(c.x, c.yMax - 1f, c.width, 1f), red.ScaleAlpha(a));
                }
        }

        /// A wound (Combat.cs, Docs/PowerStacking.md) over MiniCells: the cells at the right end that no heal reaches, in red with a bright
        /// rim, and a tall red tick where healing stops.
        void MiniWoundCells(Rect r, Player p, int n, float a)
        {
            if (p.Downed || p.Wound <= 0.5f || p.MaxHp <= 0f)
                return;
            float cw = (r.width - 2f * (n - 1)) / n;
            int hurt = Mathf.Clamp(Mathf.RoundToInt(p.Wound / p.MaxHp * n), 1, n);
            Color red = CrtStyle.Danger;
            for (int i = n - hurt; i < n; i++)
            {
                var c = new Rect(r.x + i * (cw + 2f), r.y, cw, r.height);
                Ui.Fill(c, red.ScaleAlpha(0.45f * a));
                Ui.Fill(new Rect(c.x, c.y, c.width, 1f), red.ScaleAlpha(a));
                Ui.Fill(new Rect(c.x, c.yMax - 1f, c.width, 1f), red.ScaleAlpha(a));
            }
            float edge = r.x + (n - hurt) * (cw + 2f) - 2f;
            float fl = WoundFlash(p);  // just wounded: the tick flashes white and stands out further (HealthBars.cs)
            if (fl > 0f)
                Ui.Fill(new Rect(edge - 3f, r.y - 6f, 8f, r.height + 12f), red.ScaleAlpha(0.7f * fl * a));
            float tw = 2f + 2f * fl, th = 3f + 4f * fl;
            Ui.Fill(new Rect(edge + 1f - tw / 2f, r.y - th, tw, r.height + 2f * th), Color.Lerp(red, Color.white, fl).ScaleAlpha(a));
        }

        /// The lobby's bracket corners, small: a 10 px L on each corner, 10 px outside r.
        static void MiniBrackets(Rect r, Color c, float a)
        {
            var k = c.ScaleAlpha(0.75f * a);
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
                Ui.Fill(r, c.ScaleAlpha(a));
            else
                CrtKit.Frame(r, 1f, c.ScaleAlpha(a));
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;
            Ui.Label(new Rect(r.x, r.y + 1f, r.width, r.height), key, size, lit ? CrtStyle.Bg.ScaleAlpha(a) : c.ScaleAlpha(a), TextAnchor.MiddleCenter, false);
            Ui.CrtFont = was;
            return w;
        }

        /// The glass behind a small plate: the screen colour, a thin phosphor rim and the bezel round it, scanlines over it all
        /// (call MiniScan after the content).
        static void MiniPlate(Rect r, Color ink, float a)
        {
            Ui.Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), CrtStyle.Bezel.ScaleAlpha(0.9f * a));
            Ui.Fill(r, CrtStyle.Bg.ScaleAlpha(0.9f * a));
            CrtKit.Frame(r, 1f, ink.ScaleAlpha(0.55f * a));
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
            if (slot.Id == AbilityId.TelekineticCrush)
                frac = Mathf.Clamp01(me.CrushMeter);  // a toggle on a force meter: the meter (TelekineticCrush.cs)
            return true;
        }

        /// Slot i's ability name, upper case as the CRT text is.
        string MiniName(Player me, int i) => Upgrades.Ability(me.Slots[i].Id).Name.ToUpperInvariant();

        static string MiniSecs(float t) => t.ToString(t < 10f ? "0.0" : "0");

        /// The wave as "WAVE 06 / 50" (or the hunt's own count).
        string MiniWave()
        {
            if (game.DrillOn)
                return "TRAINING";  // (no waves: DrillGame.cs)
            if (game.HangoutOn)
                return "HANGOUT";  // (no waves: HangoutGame.cs)
            if (game.Hunt != null)
                return "HUNT WAVE " + game.HuntWave.ToString("00") + " / " + game.HuntLength;
            return "WAVE " + game.ShownWave.ToString("00") + (game.Endless ? "" : " / " + game.ShownLastWave);
        }

        // ---- A: Phosphor Corners. Health bottom-left, the keys bottom-centre, the wave bottom-right; text only, framed by brackets.

        void MiniCorners(Player me, float a)
        {
            Color ink = CrtStyle.Ink, dim = CrtStyle.Dim, red = CrtStyle.Danger;
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
                string max = "/ " + Mathf.CeilToInt(me.MaxHp);
                float mx = hr.x + CrtUi.TitleWidth(hp, 52) + 8f;
                MiniText(new Rect(mx, hr.y + 30f, 120f, 26f), max, 24, low ? red : dim, a);
                MiniCells(new Rect(hr.x, hr.y + 64f, hr.width, 8f), frac, 20, hc, low ? MiniBlink(a) : a);
                MiniWoundCells(new Rect(hr.x, hr.y + 64f, hr.width, 8f), me, 20, a);
                MiniBloodFloor(new Rect(hr.x, hr.y + 64f, hr.width, 8f), me, 20, a);  // a Hemomancer's blood floor
                if (me.Wound > 0.5f)  // how much of your health no heal brings back, on the title row
                    MiniText(new Rect(hr.x, hr.y, hr.width, 16f), "WOUND -" + Mathf.CeilToInt(me.Wound), 16, red, a, TextAnchor.MiddleRight);
                float extraX = mx + CrtUi.TitleWidth(max, 24) + 8f;
                if (me.IronCap > 0f)  // Ironhide (Ironhide.cs: a Tank's, or Shared Plating's gift): steel after the max, dim while the refill waits
                {
                    string iron = "+" + Mathf.CeilToInt(Game.Ironhide(me));
                    MiniText(new Rect(extraX, hr.y + 30f, 90f, 26f), iron, 24, me.IronWait > 0f ? dim : CrtStyle.Steel, a);
                    extraX += CrtUi.TitleWidth(iron, 24) + 8f;
                }
                if (Game.Armored(me))  // an Armor Drop vest's armor (ArmorDrop.cs): on the title row after HEALTH, and five steel cells over the health cells
                {
                    MiniText(new Rect(hr.x + CrtUi.TitleWidth("HEALTH", 16) + 12f, hr.y, 120f, 16f), "ARMOR " + Mathf.CeilToInt(me.Armor), 16, CrtStyle.Steel, a);
                    MiniCells(new Rect(hr.x, hr.y + (me.OverHp > 0f ? 55f : 59f), hr.width, 3f), Mathf.Clamp01(me.Armor / Game.ArmorCap), 5, CrtStyle.Steel, a);
                }
                if (me.OverHp > 0f)  // a health shrine's health past max (Shrines.cs): gold, after the max and as a gold strip over the cells
                {
                    MiniText(new Rect(extraX, hr.y + 30f, 90f, 26f), "+" + Mathf.CeilToInt(me.OverHp), 24, CrtStyle.Gold, a);
                    MiniCells(new Rect(hr.x, hr.y + 59f, hr.width, 3f), Mathf.Clamp01(me.OverHp / Mathf.Max(1f, me.MaxHp)), 20, CrtStyle.Gold, a);
                }
            }
            if (!me.Downed)  // the level's progress: a thin gold line under the health cells
            {
                float xf = Mathf.Clamp01(me.Xp / Defs.XpToNext(me.Level));
                string lv = "LV " + me.Level;  // your level at the line's right end, level with it
                float lw = CrtUi.TitleWidth(lv, 14), lineW = hr.width - lw - 8f;
                Ui.Fill(new Rect(hr.x, hr.y + 77f, lineW, 2f), CrtStyle.Gold.ScaleAlpha(0.18f * a));
                Ui.Fill(new Rect(hr.x, hr.y + 77f, lineW * xf, 2f), CrtStyle.Gold.ScaleAlpha(a));
                MiniText(new Rect(hr.x + lineW + 4f, hr.y + 71f, lw + 4f, 14f), lv, 14, CrtStyle.Gold, a, TextAnchor.MiddleRight);
            }
            MiniScan(new Rect(hr.x - 4f, hr.y - 4f, hr.width + 8f, hr.height + 8f), a);

            // Teammates: one slim row each, stacked above your health
            float ty = hr.y - 36f;  // (clear of the brackets)
            foreach (var p in game.Players.Values)
            {
                if (p.Id == game.MyId)
                    continue;
                float pf = Mathf.Clamp01(p.Hp / Mathf.Max(1f, p.MaxHp));
                bool plow = !p.Downed && pf < MiniLow;
                Color pc = p.Downed || plow ? red : ink;
                var row = new Rect(hr.x, ty, hr.width, 22f);
                MiniText(new Rect(row.x, row.y, 110f, 16f), p.Name.ToUpperInvariant(), 16, dim, a);
                MiniText(new Rect(row.x + 110f, row.y, row.width - 110f, 16f),
                    p.Downed ? "DOWNED " + Mathf.RoundToInt(p.Revive * 100f) + "%" : Mathf.CeilToInt(p.Hp) + (p.OverHp > 0f ? "+" + Mathf.CeilToInt(p.OverHp) : "") + " / " + Mathf.CeilToInt(p.MaxHp), 16, pc, a, TextAnchor.MiddleRight);
                MiniCells(new Rect(row.x, row.y + 17f, row.width, 4f), p.Downed ? p.Revive : pf, 20, pc, plow ? MiniBlink(a) : a);
                MiniWoundCells(new Rect(row.x, row.y + 17f, row.width, 4f), p, 20, a);
                MiniBloodFloor(new Rect(row.x, row.y + 17f, row.width, 4f), p, 20, a);
                ty -= 28f;
            }

            // The keys: each keycap over its seconds left (RDY when ready), a six-cell recharge bar and the ability's name; the ultimate in
            // gold with its charge. A column is as wide as its name.
            if (!me.Downed)
            {
                const float minW = 62f, gap = 16f;
                const int nameSize = 14;
                float total = 0f;
                int shown = 0;
                for (int i = 0; i < Upgrades.SlotCount; i++)
                    if (me.Slots[i].Id != AbilityId.None)
                    {
                        total += Mathf.Max(minW, CrtUi.TitleWidth(MiniName(me, i), nameSize));
                        shown++;
                    }
                total += Mathf.Max(0, shown - 1) * gap;
                float x = W / 2f - total / 2f, y = H - 104f;
                for (int i = 0; i < Upgrades.SlotCount; i++)
                {
                    if (!MiniSlot(me, i, out bool ready, out float f))
                        continue;
                    string name = MiniName(me, i);
                    float nw = CrtUi.TitleWidth(name, nameSize), colW = Mathf.Max(minW, nw), barX = x + (colW - minW) / 2f;
                    bool ult = i == Upgrades.UltSlot;
                    bool full = ult ? me.Slots[i].Charges > 0 : ready;
                    Color c = ult ? CrtStyle.Gold : ready ? KeyGreen : dim;
                    float cx = x + colW / 2f;
                    float capW2 = MiniCap(cx, y, 20f, ult ? "ULT·" + SlotKeys[i] : SlotKeys[i], 18, c, full, ult && full ? 0.7f * a + 0.3f * a * Mathf.Sin(Time.unscaledTime * 4f) : a, true);
                    if (ult)
                        MiniGrazeFlash(cx, y, capW2, 20f, a);  // an enemy shot skimmed past you (HudTiles.cs)
                    string val = ult ? (full ? "READY" : Mathf.FloorToInt(f * 100f) + "%") : me.Slots[i].Id == AbilityId.TelekineticCrush && (me.CrushOn || !ready) ? Mathf.FloorToInt(f * 100f) + "%" : ready ? "RDY" : MiniSecs(me.Slots[i].Timer);
                    MiniText(new Rect(x - 10f, y + 24f, colW + 20f, 24f), val, 24, ult ? CrtStyle.Gold : ready ? KeyGreen : new Color(0.91f, 0.94f, 1f), a, TextAnchor.MiddleCenter);
                    MiniCells(new Rect(barX, y + 52f, minW, 4f), f, 6, ult ? CrtStyle.Gold : ready ? KeyGreen : ink, a);
                    MiniText(new Rect(x - 10f, y + 58f, colW + 20f, 16f), name, nameSize, ult ? CrtStyle.Gold : ready ? ink : dim, a, TextAnchor.MiddleCenter);
                    MiniMastery(me, i, new Rect(x, y, colW, 80f), y, a);  // cells, level and its moments (HudMastery.cs)
                    x += colW + gap;
                }
                MiniScan(new Rect(W / 2f - total / 2f - 12f, y - 2f, total + 24f, 84f), a);
            }

            // The wave and the enemies left
            var wr = new Rect(W - 240f, H - 110f, 200f, 76f);  // bottom right, level with the health: the minimap keeps the top right
            MiniBrackets(wr, ink, a);
            MiniText(new Rect(wr.x, wr.y, wr.width, 16f), MiniWave(), 16, dim, a, TextAnchor.MiddleRight);
            var myDrill = game.MyDrill;  // a drill counts its beacons instead (DrillGame.cs)
            MiniText(new Rect(wr.x, wr.y + 12f, wr.width, 50f), myDrill != null ? (Training.Drills[game.Drill].Steps - myDrill.Step).ToString() : game.Remaining.ToString(), 52, ink, a, TextAnchor.MiddleRight);
            MiniText(new Rect(wr.x, wr.y + 60f, wr.width, 16f), myDrill != null ? Training.Drills[game.Drill].StepNoun + " LEFT" : "ENEMIES LEFT", 16, dim, a, TextAnchor.MiddleRight);
            MiniScan(new Rect(wr.x - 4f, wr.y - 4f, wr.width + 8f, wr.height + 8f), a);

            // What you carry: one slim row each above the wave, as the teammates sit above your health (only what this run has)
            float ry = wr.y - 30f;  // (clear of the brackets)
            if (game.ExpandingOn)
            {
                bool vfull = me.Vectors >= me.VectorCap;
                Color vc = Color.Lerp(vfull ? CrtStyle.Gold : new Color(0.37f, 0.94f, 1f), Color.white, 0.6f * VectorChipFlash);  // (flashes as vectors come in: VectorGainLook.cs)
                if (vfull && vecFullNear)  // a pickup or Stash waiting by you: pulse amber
                    vc = vc.ScaleAlpha(0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f));
                MiniRow(wr, ry, "VECTORS", VectorChipCount(me) + " / " + me.VectorCap, vc, a, -1f);  // (the number only)
                vecChipRect = new Rect(wr.xMax - 60f, ry, 60f, 16f);  // where the shards land
                ry -= 22f;
            }
            if (me.Gold > 0 || game.CurrentPath == PathId.Bazaar)
            {
                MiniRow(wr, ry, "GOLD", me.Gold.ToString(), CrtStyle.Gold, a, -1f);
                Ui.Icon(new Rect(wr.xMax - CrtUi.TitleWidth(me.Gold.ToString(), 16) - 20f, ry + 1f, 14f, 14f), PixelArt.Get("ui_coin"), Color.white.ScaleAlpha(a));
                ry -= 22f;
            }
            if (game.LivesMax > 0)
            {
                int max = game.LivesMax, left = game.LivesLeft, n = Mathf.Min(max, 5);
                MiniText(new Rect(wr.x, ry, wr.width, 16f), "LIVES", 16, dim, a);
                var heart = PixelArt.Get("mod_secondwind");
                for (int i = 0; i < n; i++)
                    Ui.Icon(new Rect(wr.xMax - (n - i) * 15f, ry + 2f, 12f, 12f), heart, i < left ? LifeRed.ScaleAlpha(a) : new Color(1f, 1f, 1f, 0.18f * a));
                ry -= 22f;
            }
            if (me.Relic != RelicId.None)
            {
                MiniRow(wr, ry, "RELIC", Relics.Name(me.Relic).ToUpperInvariant(), RelicInk.ToColor(), a, -1f);
                ry -= 22f;
            }
            var path = Paths.Get(game.CurrentPath);
            if (path != null)
            {
                int pl = game.PathWavesLeft;
                MiniRow(wr, ry, path.Name.ToUpperInvariant(), game.OnPath ? pl + " LEFT" : "NEXT WAVE", path.Accent.ToColor(), a, -1f);
                ry -= 22f;
            }
            if (game.HuntMapOn && game.HuntRoute != null && game.Hunt != null)
            {
                MiniRow(wr, ry, "SECTOR", (game.HuntRoute.Step + 1) + " / " + game.HuntRoute.Length, ink, a, -1f);
                ry -= 22f;
            }
        }

        /// A slim right-column row: the label left, the value right in c, and (frac >= 0) a cell bar under it.
        void MiniRow(Rect col, float y, string label, string value, Color c, float a, float frac)
        {
            MiniText(new Rect(col.x, y, 110f, 16f), label, 16, CrtStyle.Dim, a);
            MiniText(new Rect(col.x + 60f, y, col.width - 60f, 16f), value, 16, c, a, TextAnchor.MiddleRight);
            if (frac >= 0f)
                MiniCells(new Rect(col.x, y + 17f, col.width, 4f), frac, 20, c, a);
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
            Color ink = CrtStyle.Ink, dim = CrtStyle.Dim, red = CrtStyle.Danger;
            V2 at = ToUi(me.Disp);
            float frac = Mathf.Clamp01(me.Hp / Mathf.Max(1f, me.MaxHp));
            bool low = frac < MiniLow;
            Color tone = low ? red : ink;

            // Where the tag goes: one row per ability under the health, wide enough for the longest name.
            const float off = 52f, rowH = 18f, nameSize = 14;
            int rows = 0;
            float nameW = 0f;
            for (int i = 0; i < Upgrades.SlotCount; i++)
                if (me.Slots[i].Id != AbilityId.None)
                {
                    rows++;
                    nameW = Mathf.Max(nameW, CrtUi.TitleWidth(MiniName(me, i), (int)nameSize));
                }
            const float capW = 44f, valW = 40f;
            float tw = Mathf.Max(176f, 16f + capW + 6f + nameW + 6f + valW), th = 38f + rows * rowH + 2f;
            float r = me.Def.BodyR * UiWorldScale;
            bool right = at.X - off - tw < 30f, below = at.Y - th - 30f < 70f;
            float tx = right ? at.X + off : at.X - off - tw, ty = below ? at.Y + 30f : at.Y - th - 30f;

            // The leader: a dot on your edge, a diagonal out to the tag's level, then a short run into its side.
            float sx = right ? 1f : -1f, sy = below ? 1f : -1f;
            var p0 = new Vector2(at.X + sx * r * 0.75f, at.Y + sy * r * 0.75f);
            float ly = below ? ty + 16f : ty + th - 16f, edge = right ? tx : tx + tw;
            var p1 = new Vector2(edge - sx * 14f, ly);
            var lc = tone.ScaleAlpha(0.8f * a);
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

            // The rows: the key (lit green when ready), the ability's name, then its seconds left (RDY when ready); the ultimate in gold
            // with its charge.
            float y = ty + 36f;
            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                if (!MiniSlot(me, i, out bool ready, out float f))
                    continue;
                bool ult = i == Upgrades.UltSlot;
                bool full = ult ? me.Slots[i].Charges > 0 : ready;
                Color c = ult ? CrtStyle.Gold : ready ? KeyGreen : dim;
                float ca = ult && full && !FxCalm ? a * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f)) : a;
                float kw = MiniCap(cx + capW / 2f, y, 16f, MiniShortKey(SlotKeys[i]), 15, c, full, ca, true);
                if (ult)
                    MiniGrazeFlash(cx + capW / 2f, y, kw, 16f, a);  // an enemy shot skimmed past you (HudTiles.cs)
                MiniText(new Rect(cx + capW + 6f, y, cw - capW - valW - 12f, 17f), MiniName(me, i), (int)nameSize, ult ? CrtStyle.Gold : ready ? ink : dim, a);
                string val = ult ? (full ? "ULT" : Mathf.FloorToInt(f * 100f) + "%") : me.Slots[i].Id == AbilityId.TelekineticCrush && (me.CrushOn || !ready) ? Mathf.FloorToInt(f * 100f) + "%" : ready ? "RDY" : Mathf.CeilToInt(me.Slots[i].Timer).ToString();
                MiniText(new Rect(cx + cw - valW, y, valW, 17f), val, 15, ult ? CrtStyle.Gold : ready ? KeyGreen : new Color(0.91f, 0.94f, 1f), a, TextAnchor.MiddleRight);
                y += rowH;
            }
            MiniScan(tr, a);
        }

        /// The wave line, top centre: WAVE 06/50  23 LEFT
        void MiniWaveLine(float a)
        {
            Color ink = CrtStyle.Ink, dim = CrtStyle.Dim;
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
