using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The lobby's Perks view, opened from the loadout's Perks key (Docs/RiotGear.md, look A of Docs/Mockups/riot-gear-perk-menu.html): your slots on the left, the perks
    /// as cards on the right. Click an unlocked card to equip it, an equipped card or a slot's "remove" to take it off; hover for the
    /// rules. Bosses whose perks aren't built yet are locked placeholders. Equipping saves the profile and tells the lobby
    /// (Session.ShareCosmetics), so everyone sees your perks on your seat.
    public partial class ArenaApp
    {
        const int PerkTab = 5;
        const int PerkSlots = 3;  // the most a player can unlock

        /// Bosses whose perks come later (Docs/BossHuntsAndPerks.md), shown as locked cards.
        static readonly string[] PerkBossesToCome = { "Hive Queen", "Twin Reapers", "Singularity", "Colossus", "Duelist", "Lich", "Storm Herald", "Devourer", "Eclipse" };

        string perkNote = "";
        float perkNoteUntil;

        /// The loadout's Perks key: "PERKS n/m" with the equipped perks' icons; a click opens the Perks view (lobbyTab = PerkTab).
        void DrawPerksKey(Rect title, bool calm)
        {
            var prof = session.Game.Profile;
            var equipped = Perks.Equipped(prof);
            string label = "PERKS  " + equipped.Count + "/" + Perks.Slots(prof);
            var kr = new Rect(title.xMax - 124f, title.y + 18f, 124f, 32f);  // right of the class name, under the preview
            if (LobbyKit.Key(kr, label, false, calm, equipped.Count > 0 ? PerkIcon(equipped[0]) : null, colorIcon: true, size: 13, minSize: 10))
                lobbyTab = PerkTab;
        }

        static Texture2D PerkIcon(PerkId id) => PixelArt.Get(id == PerkId.RiotGear ? "perk_riot" : id == PerkId.Contraband ? "perk_bag" : "ui_lock");

        void DrawPerks(Rect r, float t)
        {
            var prof = session.Game.Profile;
            bool canEdit = session.Resume == null;  // a continued run keeps the perks it was saved with
            int slots = Perks.Slots(prof);
            var equipped = Perks.Equipped(prof);
            LobbyEntry mine;
            if (!canEdit && session.Lobby.TryGetValue(session.MyId, out mine))
            {
                equipped.Clear();  // a continued run: what your seat was saved with, not what you have equipped now
                equipped.AddRange(mine.PerkIds);
                slots = Mathf.Max(1, equipped.Count);
            }
            LobbyKit.Panel(r);  // the lobby screen (LobbyKit.cs)
            if (LobbyKit.Key(new Rect(r.xMax - 128f, r.y + 8f, 110f, 26f), "< LOADOUT", false, fxReduced, size: 13, minSize: 10))
                lobbyTab = 0;  // back to the loadout (the Perks view opens from its Perks key)

            // --- Left: the slots
            float lx = r.x + 18f, lw = 236f;
            Ui.Heading(lx, r.y + 14f, lw, canEdit ? "Equipped  (" + equipped.Count + " of " + slots + (slots == 1 ? " slot)" : " slots)") : "Saved with this run");
            for (int i = 0; i < PerkSlots; i++)
            {
                var sr = new Rect(lx, r.y + 42f + i * 86f, lw, 78f);
                if (i < equipped.Count)
                    DrawPerkSlot(sr, Perks.Get(equipped[i]), canEdit, prof);
                else if (i < slots && canEdit)
                    DrawEmptyPerkSlot(sr);
                else if (canEdit)
                    DrawLockedPerkSlot(sr, prof);
            }
            float ny = r.y + 42f + PerkSlots * 86f + 4f;
            string blurb = canEdit
                ? "Perks bend the rules for a run: one per boss, a few slots. Win a Boss Hunt to unlock that boss's perks."
                : "You're continuing a saved run, so everyone keeps the perks they had when it was saved.";
            Ui.Label(new Rect(lx, ny, lw, 70f), blurb, 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
            if (session.PracticeMode && canEdit)  // you can set them up here, but the Practice Range plays without them
                Ui.Label(new Rect(lx, ny + 48f, lw, 36f), "Perks are off in the Practice Range: they apply to your next real run.", 12, Ui.Gold, TextAnchor.UpperLeft, true, true);
            if (Time.unscaledTime < perkNoteUntil)
                Ui.Label(new Rect(lx, ny + (session.PracticeMode ? 96f : 72f), lw, 36f), perkNote, 12, Ui.Gold, TextAnchor.UpperLeft, true, true);

            // --- Right: the perks
            float x = r.x + 274f, w = r.xMax - 18f - x;
            Ui.Heading(x, r.y + 14f, w, "Perks");
            const float gap = 8f;
            float top = r.y + 42f, cw = (w - gap) / 2f;
            int n = Perks.All.Length;
            int realRows = (n + 1) / 2;
            float bigH = 92f;
            for (int i = 0; i < n; i++)
                DrawPerkCard(new Rect(x + (i % 2) * (cw + gap), top + (i / 2) * (bigH + gap), cw, bigH), Perks.All[i], prof, canEdit, equipped);
            float top2 = top + realRows * (bigH + gap);
            int rows = (PerkBossesToCome.Length + 1) / 2;
            float ch = Mathf.Min(64f, (r.yMax - 18f - top2 - gap * (rows - 1)) / rows);
            for (int i = 0; i < PerkBossesToCome.Length; i++)
                DrawComingPerk(new Rect(x + (i % 2) * (cw + gap), top2 + (i / 2) * (ch + gap), cw, ch), PerkBossesToCome[i]);
        }

        void DrawPerkSlot(Rect r, PerkDef d, bool canEdit, Profile prof)
        {
            Ui.Pill(r, new Color(1f, 0.8f, 0.35f, 0.12f));
            Ui.Rim(r, Ui.Gold, 0.85f);
            Ui.Pill(new Rect(r.x + 9f, r.y + 9f, 60f, 60f), new Color(0.02f, 0.03f, 0.06f, 0.95f));
            Ui.Icon(new Rect(r.x + 15f, r.y + 15f, 48f, 48f), PerkIcon(d.Id), 1f);
            Ui.Label(new Rect(r.x + 78f, r.y + 10f, r.width - 120f, 20f), d.Name, 14, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 78f, r.y + 32f, r.width - 86f, 40f), d.Line, 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
            if (canEdit)
            {
                var rm = new Rect(r.xMax - 62f, r.y + 6f, 56f, 18f);
                bool hot = rm.Contains(Event.current.mousePosition);
                Ui.Label(rm, "remove", 11, hot ? Color.white : Ui.Muted, TextAnchor.MiddleRight);
                if (Ui.HitArea(rm))
                    TakePerkOff(prof, d.Id);
            }
        }

        void DrawEmptyPerkSlot(Rect r)
        {
            DashedCard(r);
            Ui.Label(new Rect(r.x + 14f, r.y + 14f, r.width - 28f, 22f), "Empty slot", 14, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 14f, r.y + 38f, r.width - 28f, 30f), "Pick a perk from the list", 11, Ui.Muted);
        }

        void DrawLockedPerkSlot(Rect r, Profile prof)
        {
            DashedCard(r);
            Ui.Icon(new Rect(r.x + 14f, r.y + 22f, 32f, 32f), PixelArt.Get("ui_lock"), new Color(0.55f, 0.6f, 0.72f, 0.7f));
            string rule = prof.RunsWon == 0 ? "Win a full run" : "Win 5 different Boss Hunts  (" + Mathf.Min(5, prof.HuntsWonCount) + " of 5)";
            Ui.Label(new Rect(r.x + 58f, r.y + 12f, r.width - 66f, 20f), "Locked slot", 13, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 58f, r.y + 34f, r.width - 66f, 38f), rule, 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
        }

        void DrawPerkCard(Rect r, PerkDef d, Profile prof, bool canEdit, List<PerkId> equipped)
        {
            bool owned = Perks.Unlocked(prof, d.Id), on = equipped.Contains(d.Id);
            bool hover = r.Contains(Event.current.mousePosition);
            if (owned)
            {
                Ui.Pill(r, on ? new Color(1f, 0.8f, 0.35f, 0.1f) : new Color(0.04f, 0.05f, 0.1f, 0.9f));
                Ui.Rim(r, Ui.Gold, on ? 1f : hover && canEdit ? 0.8f : 0.5f);
            }
            else
                DashedCard(r);
            float a = owned ? 1f : 0.55f;
            Ui.Pill(new Rect(r.x + 9f, r.y + 9f, r.height - 18f, r.height - 18f), new Color(0.02f, 0.03f, 0.06f, 0.95f));
            Ui.Icon(new Rect(r.x + 17f, r.y + 17f, r.height - 34f, r.height - 34f), PerkIcon(d.Id), a);
            float tx = r.x + r.height;
            Ui.Label(new Rect(tx, r.y + 8f, r.width - r.height - 8f, 14f), BossName(d.Boss).ToUpperInvariant(), 10, owned ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(tx, r.y + 22f, r.width - r.height - 8f, 22f), d.Name, 16, owned ? Color.white : Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(tx, r.y + 46f, r.width - r.height - 8f, 28f), d.Line, 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
            string status = on ? "Equipped" : owned ? (canEdit ? "Click to equip" : "") : d.Built ? d.UnlockText : "Coming soon";
            Ui.Label(new Rect(r.x + 8f, r.yMax - 20f, r.width - 16f, 16f), status, 10, on ? Ui.Gold : Ui.Muted, TextAnchor.MiddleRight, true);
            if (hover)
            {
                TipStart(r, d.Name, owned ? Ui.Gold : Ui.Muted);
                TipAdd(d.Tip, 12, Ui.Text);
                TipAdd(owned ? "" : "Locked: " + d.UnlockText, 11, Ui.Muted);
            }
            if (owned && canEdit && Ui.HitArea(r))
            {
                if (on)
                    TakePerkOff(prof, d.Id);
                else if (Perks.Equip(prof, d.Id))
                    PerksChanged(prof);
                else
                {
                    perkNote = "Every slot is full. Remove a perk first.";
                    perkNoteUntil = Time.unscaledTime + 3f;
                }
            }
        }

        void DrawComingPerk(Rect r, string boss)
        {
            DashedCard(r);
            Ui.Icon(new Rect(r.x + 12f, r.center.y - 10f, 20f, 20f), PixelArt.Get("ui_lock"), new Color(0.55f, 0.6f, 0.72f, 0.7f));
            Ui.Label(new Rect(r.x + 42f, r.y + 6f, r.width - 50f, 14f), boss.ToUpperInvariant(), 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 42f, r.y + 20f, r.width - 50f, r.height - 24f), "Locked", 13, Ui.Muted, TextAnchor.UpperLeft, true);
            if (r.Contains(Event.current.mousePosition))
            {
                TipStart(r, boss, Ui.Muted);
                TipAdd("This boss's perk comes after its Boss Hunt does.", 12, Ui.Text);
            }
        }

        void TakePerkOff(Profile prof, PerkId id)
        {
            if (Perks.Unequip(prof, id))
                PerksChanged(prof);
        }

        void PerksChanged(Profile prof)
        {
            prof.Save();
            session.ShareCosmetics();  // the lobby (and your seat) show the new set
        }

        /// Your perks on a party card: a small icon for each, right-aligned on the class line; hover for the name and what it does.
        void DrawSeatPerks(Rect r, LobbyEntry e)
        {
            for (int i = 0; i < e.PerkIds.Length; i++)
            {
                var d = Perks.Get(e.PerkIds[i]);
                if (d == null)
                    continue;
                var ir = new Rect(r.xMax - 9f - (e.PerkIds.Length - i) * 19f + 3f, r.y + 31f, 16f, 16f);
                Ui.Icon(ir, PerkIcon(d.Id), 1f);
                if (ir.Contains(Event.current.mousePosition))
                {
                    TipStart(ir, d.Name, Ui.Gold);
                    TipAdd(BossName(d.Boss) + " perk", 10, Ui.Muted, true);
                    TipAdd(d.Line, 12, Ui.Text);
                }
            }
        }
    }
}
