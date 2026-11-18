using UnityEngine;

namespace Arena
{
    /// Mastery on the ability tiles (prototype looks for picking one; `MasteryLook` 0 keeps today's thin line):
    ///   1 Segments  four segments along the tile's foot (one per level-up), the current one filling, "LV3" by the key cap
    ///   2 Badge     a level badge on the name row that fills bottom-up with XP, gold once mastered
    /// Both share the moments: XP gained shows as a white chunk on the bar and a "+12" rising off it; a level-up flashes
    /// the tile's rim, pops the level and raises a card over the tile ("LV 2 > 3 · +8% POWER · -4% COOLDOWN", or the
    /// capstone when mastered) in place of the centre toast.
    public partial class ArenaApp
    {
        internal static int MasteryLook;  // (preview: MASTERYLOOK=n)
        const float MxGainTime = 0.9f, MxLevelTime = 2.6f;
        static readonly Color MxGreen = new Color(0.35f, 0.9f, 0.45f);

        readonly float[] mxSeen = new float[Upgrades.SlotCount];
        readonly float[] mxGainFrom = new float[Upgrades.SlotCount], mxGainAmt = new float[Upgrades.SlotCount];
        readonly float[] mxGainAgo = Filled(TileNever), mxLevelAgo = Filled(TileNever);

        /// Where `xp` sits along the whole track, 0..1 (level 1 empty, mastered full).
        static float MxPos(float xp) => (Game.MasteryLevelFor(xp) - 1 + (Game.MasteryLevelFor(xp) >= Game.MasteryMax ? 0f : Game.MasteryProgress(xp))) / (Game.MasteryMax - 1);

        /// Each frame, after TickTiles: age the moments and catch XP gained in your slots.
        void TickMasteryTiles(Player me, bool fresh, float dt)
        {
            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                mxGainAgo[i] += dt;
                mxLevelAgo[i] += dt;
                if (me == null)
                    continue;
                var id = me.Slots[i].Id;
                float xp;
                me.MasteryXp.TryGetValue(id, out xp);
                if (!fresh && id == tileIds[i] && id != AbilityId.None && xp > mxSeen[i])
                {
                    mxGainFrom[i] = mxGainAgo[i] < MxGainTime ? Mathf.Min(mxGainFrom[i], MxPos(mxSeen[i])) : MxPos(mxSeen[i]);
                    mxGainAmt[i] = (mxGainAgo[i] < MxGainTime ? mxGainAmt[i] : 0f) + xp - mxSeen[i];
                    mxGainAgo[i] = 0f;
                    if (Game.MasteryLevelFor(xp) > Game.MasteryLevelFor(mxSeen[i]))
                        mxLevelAgo[i] = 0f;
                }
                mxSeen[i] = xp;
            }
        }

        /// For the preview tool: one slot `ago` seconds into a moment ("gain" of `amt` XP, or "level").
        void PreviewMasteryMoment(string moment, float ago, float amt, int slot)
        {
            var me = game.Me;
            for (int i = slot; i <= slot; i++)
            {
                float xp;
                me.MasteryXp.TryGetValue(me.Slots[i].Id, out xp);
                mxGainAmt[i] = amt;
                mxGainFrom[i] = MxPos(Mathf.Max(0f, xp - amt));
                mxGainAgo[i] = ago;
                if (moment == "level")
                    mxLevelAgo[i] = ago;
            }
        }

        /// The tile's foot: mastery and recharge (looks 1 and 2), the badge (2), and the moments.
        void DrawMasteryFoot(Player me, int i, Rect r, Color keyCol, float fill)
        {
            var id = me.Slots[i].Id;
            float xp;
            me.MasteryXp.TryGetValue(id, out xp);
            int lv = Game.MasteryLevelFor(xp);
            bool max = lv >= Game.MasteryMax;
            Color on = max ? Ui.Gold : MxGreen;
            float pos = MxPos(xp);
            float gainK = mxGainAgo[i] < MxGainTime ? mxGainAgo[i] / MxGainTime : 1f;
            float lvK = mxLevelAgo[i] < MxLevelTime ? mxLevelAgo[i] / MxLevelTime : 1f;
            float flash = mxLevelAgo[i] < 0.5f ? 1f - mxLevelAgo[i] / 0.5f : 0f;

            var foot = new Rect(r.x + 8f, r.yMax - 14f, r.width - 16f, 8f);
            float barX = foot.x, barW = foot.width, barY = foot.y, barH = 4f;
            if (MasteryLook == 1)
            {
                // Four segments, one per level-up.
                const float gap = 3f;
                int segs = Game.MasteryMax - 1;
                float sw = (foot.width - gap * (segs - 1)) / segs;
                for (int k = 0; k < segs; k++)
                {
                    var seg = new Rect(foot.x + k * (sw + gap), foot.y, sw, barH);
                    Ui.Fill(seg, new Color(1f, 1f, 1f, 0.1f));
                    float f = Mathf.Clamp01(pos * segs - k);
                    if (f > 0f)
                        Ui.Fill(new Rect(seg.x, seg.y, sw * f, barH), on);
                    if (flash > 0f && k == lv - 2)  // the segment just finished
                    {
                        float e = 2f * flash;
                        Ui.Fill(new Rect(seg.x - e, seg.y - e, sw + 2f * e, barH + 2f * e), new Color(1f, 1f, 1f, 0.85f * flash));
                    }
                }
                // "LV3" beside the key cap.
                float capW = 12f + (i == Upgrades.UltSlot ? ("ULT · " + SlotKeys[4]).Length : SlotKeys[i].Length) * 7f;
                float pop = 1f + 0.6f * flash;
                int size = Mathf.RoundToInt(15f * pop);
                var tag = new Rect(r.x + 8f + capW + 6f, r.y + 7f + 8f - 10f * pop, 60f, 20f * pop);
                DkTitle(tag, max ? "MAX" : "LV" + lv, size, Color.Lerp(on, Color.white, flash), TextAnchor.MiddleLeft);
            }
            else
            {
                // A plain XP bar, and the badge.
                barH = 3f;
                Ui.Fill(new Rect(barX, barY, barW, barH), new Color(1f, 1f, 1f, 0.1f));
                Ui.Fill(new Rect(barX, barY, barW * pos, barH), on);
                for (int k = 1; k < Game.MasteryMax - 1; k++)  // a notch at each level
                    Ui.Fill(new Rect(barX + barW * k / (Game.MasteryMax - 1f) - 1f, barY, 2f, barH), new Color(0.01f, 0.03f, 0.08f, 0.9f));
                float pop = 1f + 0.35f * flash;
                var b = new Rect(r.xMax - 30f, r.y + 25f, 22f, 19f);
                b = new Rect(b.center.x - b.width * pop / 2f, b.center.y - b.height * pop / 2f, b.width * pop, b.height * pop);
                Ui.Fill(b, new Color(0.01f, 0.03f, 0.08f, 0.95f));
                float prog = max ? 1f : Game.MasteryProgress(xp);
                Ui.Fill(new Rect(b.x, b.yMax - b.height * prog, b.width, b.height * prog), new Color(on.r, on.g, on.b, max ? 0.9f : 0.4f));
                CrtKit.Frame(b, 1f, Color.Lerp(on, Color.white, flash));
                DkTitle(b, lv.ToString(), Mathf.RoundToInt(16f * pop), max ? new Color(0.1f, 0.07f, 0.01f) : Color.Lerp(Color.white, on, 0.3f), TextAnchor.MiddleCenter);
            }
            // The recharge line under it.
            Ui.Fill(new Rect(foot.x, foot.y + barH + 1f, foot.width, 2f), new Color(1f, 1f, 1f, 0.06f));
            Ui.Fill(new Rect(foot.x, foot.y + barH + 1f, foot.width * Mathf.Clamp01(fill), 2f), keyCol);

            // XP just gained: a white chunk fading into the bar, a "+12" rising off its end.
            if (gainK < 1f && mxGainAmt[i] > 0f)
            {
                float a = 1f - gainK;
                float from = Mathf.Min(mxGainFrom[i], pos);
                Ui.Fill(new Rect(barX + barW * from, barY - 1f, Mathf.Max(2f, barW * (pos - from)), barH + 2f), new Color(1f, 1f, 1f, 0.9f * a));
                float rise = 16f * (1f - (1f - gainK) * (1f - gainK));
                var t = new Rect(barX + barW * pos - 32f, r.y - 20f - rise, 64f, 18f);
                float ta = Mathf.Clamp01(a * 1.6f);
                Ui.Fill(t, new Color(0.01f, 0.03f, 0.08f, 0.85f * ta));
                CrtKit.Frame(t, 1f, new Color(on.r, on.g, on.b, 0.8f * ta));
                DkTitle(t, "+" + Mathf.RoundToInt(mxGainAmt[i]) + " XP", 16, new Color(on.r, on.g, on.b, ta), TextAnchor.MiddleCenter);
            }
            // Levelled up: the rim flashes in the mastery colour.
            if (flash > 0f)
                CrtKit.Frame(r, 3f, new Color(on.r, on.g, on.b, flash));
            if (lvK < 1f)
                mxCard.Add(new MxCard { Tile = r, Id = id, Level = lv, Ago = mxLevelAgo[i] });
        }

        struct MxCard
        {
            public Rect Tile;
            public AbilityId Id;
            public int Level;
            public float Ago;
        }

        readonly System.Collections.Generic.List<MxCard> mxCard = new System.Collections.Generic.List<MxCard>();

        /// The level-up cards over their tiles (drawn after every tile so they sit on top).
        void DrawMasteryCards()
        {
            foreach (var c in mxCard)
            {
                bool max = c.Level >= Game.MasteryMax;
                Color on = max ? Ui.Gold : MxGreen;
                float inK = Mathf.Clamp01(c.Ago / 0.22f), outK = Mathf.Clamp01((MxLevelTime - c.Ago) / 0.4f);
                float a = Mathf.Min(inK, outK);
                float w = Mathf.Max(c.Tile.width, max ? 300f : 230f), h = max ? 50f : 44f;
                float y = c.Tile.y - h - 8f - 10f * (1f - EaseOut3(inK));
                var card = new Rect(Mathf.Clamp(c.Tile.center.x - w / 2f, 8f, W - 8f - w), y, w, h);
                Ui.Fill(card, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.94f * a));
                CrtKit.Frame(card, 2f, new Color(on.r, on.g, on.b, a));
                Ui.Fill(new Rect(card.x, card.y, 4f, card.height), new Color(on.r, on.g, on.b, a));
                // A little tail down to the tile.
                Ui.Fill(new Rect(c.Tile.center.x - 5f, card.yMax, 10f, 4f), new Color(on.r, on.g, on.b, a));
                DkTitle(new Rect(card.x + 12f, card.y + 4f, w - 20f, 20f), max ? "MASTERED" : "LEVEL UP", 15, new Color(on.r, on.g, on.b, a), TextAnchor.MiddleLeft);
                DkTitle(new Rect(card.x + 12f, card.y + 4f, w - 20f, 20f), max ? Upgrades.Ability(c.Id).Name.ToUpperInvariant() : "LV " + (c.Level - 1) + " > " + c.Level, 15, new Color(1f, 1f, 1f, a), TextAnchor.MiddleRight);
                string line = max ? Game.Capstone(c.Id) : "+8% POWER  ·  -4% COOLDOWN";
                Ui.Label(new Rect(card.x + 12f, card.y + 23f, w - 20f, h - 26f), line, max ? 12 : 13, new Color(0.85f, 0.9f, 1f, a), TextAnchor.UpperLeft, false);
            }
            mxCard.Clear();
        }
    }
}
