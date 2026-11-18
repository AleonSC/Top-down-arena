using UnityEngine;

namespace Arena
{
    /// Mastery on the corner HUD's keys: four cells under each name (one per level-up, the current one filling; green,
    /// gold once mastered) with the level digit beside them ("M" at 5). Moments: "+24" rises off the keycap as XP comes
    /// in; a level-up flashes brackets round the key, pops "LV 3" over it and puts a line above the keys
    /// ("SHOCKWAVE · LV 3" / "+8% POWER -4% COOLDOWN", or "MASTERED" and the capstone). Preview `MXFX=gain|level,<s>,<xp>[,slot]`.
    public partial class ArenaApp
    {
        const float MxGainTime = 0.9f, MxLevelTime = 2.8f;
        static readonly Color MxGreen = new Color(0.42f, 1f, 0.6f);

        readonly float[] mxSeen = new float[Upgrades.SlotCount], mxGainAmt = new float[Upgrades.SlotCount];
        readonly float[] mxGainAgo = Filled(TileNever), mxLevelAgo = Filled(TileNever);
        readonly AbilityId[] mxIds = new AbilityId[Upgrades.SlotCount];
        int mxFor = -1;

        /// Each frame (TickTiles): age the moments and catch XP gained in your slots.
        void TickMasteryTiles(Player me, float dt)
        {
            bool fresh = me == null || mxFor != me.Id;
            mxFor = me != null ? me.Id : -1;
            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                mxGainAgo[i] += dt;
                mxLevelAgo[i] += dt;
                if (me == null)
                    continue;
                var id = me.Slots[i].Id;
                me.MasteryXp.TryGetValue(id, out float xp);
                if (!fresh && id == mxIds[i] && id != AbilityId.None && xp > mxSeen[i])
                {
                    mxGainAmt[i] = (mxGainAgo[i] < MxGainTime ? mxGainAmt[i] : 0f) + xp - mxSeen[i];
                    mxGainAgo[i] = 0f;
                    if (Game.MasteryLevelFor(xp) > Game.MasteryLevelFor(mxSeen[i]))
                        mxLevelAgo[i] = 0f;
                }
                mxSeen[i] = xp;
                mxIds[i] = id;
            }
        }

        /// For the preview tool: slot `slot` `ago` seconds into a moment ("gain" of `amt` XP, or "level").
        void PreviewMasteryMoment(string moment, float ago, float amt, int slot)
        {
            mxGainAmt[slot] = amt;
            mxGainAgo[slot] = ago;
            if (moment == "level")
                mxLevelAgo[slot] = ago;
        }

        /// Slot i's mastery in the corner HUD's key column (col: the column, y the keycap's top).
        void MiniMastery(Player me, int i, Rect col, float y, float a)
        {
            var id = me.Slots[i].Id;
            me.MasteryXp.TryGetValue(id, out float xp);
            int lv = Game.MasteryLevelFor(xp);
            bool max = lv >= Game.MasteryMax;
            float prog = max ? 1f : Game.MasteryProgress(xp);
            Color on = max ? Ui.Gold : MxGreen;
            float cx = col.center.x;
            float flash = mxLevelAgo[i] < 0.6f ? 1f - mxLevelAgo[i] / 0.6f : 0f;

            // Four cells, one per level-up, and the level beside them.
            const float cw = 62f;
            int segs = Game.MasteryMax - 1;
            float pos = (lv - 1 + (max ? 0f : prog)) / segs;
            var cells = new Rect(cx - cw / 2f, y + 78f, cw, 4f);
            float sw = (cw - 3f * (segs - 1)) / segs;
            for (int k = 0; k < segs; k++)
            {
                var c = new Rect(cells.x + k * (sw + 3f), cells.y, sw, cells.height);
                Ui.Fill(c, MiniA(Color.white, 0.14f * a));
                float f = Mathf.Clamp01(pos * segs - k);
                if (f > 0f)
                    Ui.Fill(new Rect(c.x, c.y, sw * f, c.height), MiniA(Color.Lerp(on, Color.white, k == lv - 2 ? flash : 0f), a));
            }
            MiniText(new Rect(cells.xMax + 4f, cells.y - 6f, 30f, 16f), max ? "M" : lv.ToString(), 14, Color.Lerp(on, Color.white, flash), a);

            // XP coming in: "+24" rising off the keycap.
            if (mxGainAgo[i] < MxGainTime && mxGainAmt[i] > 0f && mxLevelAgo[i] >= MxLevelTime)  // (a level-up says it instead)
            {
                float k = mxGainAgo[i] / MxGainTime;
                float rise = 20f * (1f - (1f - k) * (1f - k));
                MiniText(new Rect(cx - 40f, y - 20f - rise, 80f, 18f), "+" + Mathf.RoundToInt(mxGainAmt[i]), 18, on, a * Mathf.Clamp01((1f - k) * 1.8f), TextAnchor.MiddleCenter);
            }
            // A level-up: brackets flash round the key, "LV 3" pops over it, and the line above the keys says what it gave.
            if (mxLevelAgo[i] < MxLevelTime)
            {
                float t = mxLevelAgo[i];
                float la = Mathf.Min(Mathf.Clamp01(t / 0.15f), Mathf.Clamp01((MxLevelTime - t) / 0.5f));
                if (flash > 0f)
                    MiniBrackets(new Rect(col.x - 8f, y - 6f, col.width + 16f, 92f), on, a * flash);
                if (t < 1.4f)
                {
                    float pop = 1f + 0.5f * Mathf.Clamp01(1f - t / 0.25f);
                    float pa = a * Mathf.Clamp01((1.4f - t) / 0.4f);
                    MiniText(new Rect(cx - 60f, y - 30f, 120f, 26f), max ? "MAX" : "LV " + lv, Mathf.RoundToInt(22f * pop), on, pa, TextAnchor.MiddleCenter);
                }
                string head = Upgrades.Ability(id).Name.ToUpperInvariant() + (max ? "  MASTERED" : "  ·  LV " + lv);
                string body = max ? Game.Capstone(id) : "+8% POWER   -4% COOLDOWN";
                MiniText(new Rect(W / 2f - 400f, y - 66f, 800f, 20f), head, 20, on, a * la, TextAnchor.MiddleCenter);
                MiniText(new Rect(W / 2f - 400f, y - 46f, 800f, 16f), body.ToUpperInvariant(), 14, CrtUi.Ink, a * la, TextAnchor.MiddleCenter);
            }
        }
    }
}
