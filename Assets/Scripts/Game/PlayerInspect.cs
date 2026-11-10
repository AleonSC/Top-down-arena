using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Click a party card in the lobby: your own opens your profile (OpenOwnProfile), a teammate's opens their holo card, large
    /// (Docs/Mockups/lobby-profile-inspect.html, look A): their frame as the border, card background, name effect and trail,
    /// rank badge, class, XP, three records and the names of what they have equipped. Read-only. Esc, the close key or a click
    /// outside closes it; while it is open the lobby underneath is only drawn.
    public partial class ArenaApp
    {
        int inspectId = -1;      // lobby id of the player on show, -1 = none
        float inspectAt;         // when it opened (the pop-in)
        bool profileFromCard;    // the profile was opened from your party card: Back leaves the lobby menu too

        const float InspectW = 460f, InspectH = 620f;

        void OpenInspect(int id)
        {
            inspectId = id;
            inspectAt = Time.unscaledTime;
            GUIUtility.keyboardControl = 0;
        }

        void CloseInspect() => inspectId = -1;

        /// Your own party card: the profile (the lobby menu's Profile row, without the Look tab).
        void OpenOwnProfile()
        {
            OpenLobbyMenu();
            pauseRecords = true;
            profileFromCard = true;
        }

        /// A party card's click: yours opens your profile, anyone else's their holo card. `area` is the part of the card that takes it.
        void PartyCardClick(Rect area, LobbyEntry e, Color tier, float t)
        {
            bool hover = area.Contains(Event.current.mousePosition);
            if (hover)
                Ui.Rim(area, tier, 0.9f);
            if (!Ui.HitArea(area))
                return;
            if (e.Id == session.MyId)
                OpenOwnProfile();
            else
                OpenInspect(e.Id);
        }

        /// Over the lobby (ArenaApp.cs, after the CRT glass): the holo card.
        void DrawInspect()
        {
            LobbyEntry e;
            if (inspectId < 0 || !session.Lobby.TryGetValue(inspectId, out e))
            {
                inspectId = -1;
                return;
            }
            float t = Time.unscaledTime;
            float pop = fxReduced ? 1f : Mathf.Clamp01((t - inspectAt) / 0.28f);
            pop = 1f - (1f - pop) * (1f - pop) * (1f - pop);
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.02f, 0.06f, 0.74f * pop));
            var card = new Rect((W - InspectW) / 2f, 50f + (1f - pop) * 24f, InspectW, InspectH);
            var tier = ToColor(Rank.TierColor(e.Rank));
            var roleCol = ToColor(Defs.Get(e.Role).Color);
            if (pop < 1f)
                GUI.color = new Color(1f, 1f, 1f, pop);

            // The click catcher: outside the card closes it.
            if (Ui.HitArea(new Rect(0, 0, W, H)) && !card.Contains(Event.current.mousePosition))
                CloseInspect();

            // Frame and background (their equipped ones; else the class colour).
            if (e.Frame > 0)
                DrawFrameCard(card, e.Frame, t, true, e.CardBg);
            else
            {
                Ui.Card(card, roleCol, 0.7f);
                DrawCardBackground(card, e.CardBg, t);
            }
            var tint = new Rect(card.x + 5f, card.y + 5f, card.width - 10f, card.height - 10f);
            Ui.Fill(tint, new Color(0.03f, 0.04f, 0.12f, e.CardBg > 0 ? 0.35f : 0.8f));
            if (!fxReduced)  // the scan band sweeping down
            {
                float by = card.y + ((t * 0.19f) % 1f) * (card.height + 90f) - 90f;
                float y0 = Mathf.Max(by, card.y + 5f), y1 = Mathf.Min(by + 90f, card.yMax - 5f);
                if (y1 > y0)
                    Ui.Fill(new Rect(card.x + 5f, y0, card.width - 10f, y1 - y0), new Color(tier.r, tier.g, tier.b, 0.1f));
            }

            float cx = card.center.x, y = card.y;
            Ui.Label(new Rect(card.x, y + 18f, card.width, 18f), (Rank.Title(e.Rank) + "  ·  RANK " + e.Rank).ToUpperInvariant(), 12, tier, TextAnchor.MiddleCenter, true);
            var badge = new Rect(cx - 65f, y + 40f, 130f, 130f);
            Ui.Glow(new Rect(badge.x + 10f, badge.y + 10f, badge.width - 20f, badge.height - 20f), tier, 0.5f);
            RankLook.Draw(badge, e.Rank);

            DrawName(new Rect(card.x + 20f, y + 176f, card.width - 40f, 44f), e.Name, 36, e.NameFx, t, Color.white, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(card.x, y + 220f, card.width, 26f), Defs.KitName(e.Kit) + " " + Defs.Get(e.Role).Name, 20, roleCol, TextAnchor.MiddleCenter, true);

            // The art box: their character, aiming around.
            var art = new Rect(cx - 115f, y + 252f, 230f, 160f);
            Ui.Pill(art, new Color(roleCol.r * 0.22f, roleCol.g * 0.22f, roleCol.b * 0.22f, 0.95f));
            Ui.Rim(art, roleCol, 0.5f);
            if (BeginUiGl())
            {
                UiModelAt(new V2(art.center.x, art.center.y), 2.6f);
                DrawPreviewPlayer(e.Role, e.Kit, e.Weapon, t);
                EndUiGl();
            }

            // Their trail, flowing off to the left.
            var strip = new Rect(card.x + 30f, y + 424f, card.width - 60f, 26f);
            if (e.Trail > 0 && BeginUiGl())
            {
                UiModelAt(new V2(strip.xMax - 6f, strip.center.y), 0.6f);
                DrawTrailSample(e.Trail, (strip.width - 12f) / 0.6f, t, 6f);
                EndUiGl();
            }
            else if (e.Trail == 0)
                Ui.Label(strip, "NO TRAIL", 10, Ui.Muted, TextAnchor.MiddleCenter, true);

            // XP towards the next rank.
            var xp = new Rect(card.x + 30f, y + 460f, card.width - 60f, 8f);
            Ui.Pill(xp, new Color(0.04f, 0.05f, 0.13f, 0.95f));
            if (e.XpPct > 0)
                Ui.Pill(new Rect(xp.x, xp.y, Mathf.Max(8f, xp.width * e.XpPct / 100f), xp.height), tier);

            // Three records.
            string[] vals = { e.Kills.ToString("N0"), e.BestWave > 0 ? e.BestWave.ToString() : "-", e.BossKills.ToString("N0") };
            string[] labs = { "KILLS", "BEST WAVE", "BOSSES" };
            float sw = (card.width - 60f - 16f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var s = new Rect(card.x + 30f + i * (sw + 8f), y + 480f, sw, 54f);
                Ui.Fill(s, new Color(1f, 1f, 1f, 0.06f));
                CrtKit.Frame(s, 1f, new Color(1f, 1f, 1f, 0.12f));
                Ui.Label(new Rect(s.x, s.y + 6f, s.width, 28f), vals[i], 22, Color.white, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(s.x, s.y + 34f, s.width, 14f), labs[i], 10, Ui.Muted, TextAnchor.MiddleCenter, true);
            }

            // What they have equipped, as chips.
            var chips = new List<string>();
            AddChip(chips, "Frame", CosmeticKind.Frame, e.Frame);
            AddChip(chips, "Trail", CosmeticKind.Trail, e.Trail);
            AddChip(chips, "Card", CosmeticKind.CardBackground, e.CardBg);
            AddChip(chips, "Name", CosmeticKind.NameEffect, e.NameFx);
            float chipX = card.x + 30f, chipY = y + 546f, right = card.xMax - 30f;
            foreach (var c in chips)
            {
                float cw = Ui.TextWidth(c, 11, true) + 18f;
                if (chipX + cw > right)
                {
                    chipX = card.x + 30f;
                    chipY += 26f;
                }
                var cr = new Rect(chipX, chipY, cw, 22f);
                Ui.Pill(cr, new Color(0.04f, 0.05f, 0.13f, 0.9f));
                Ui.Rim(cr, tier, 0.6f);
                Ui.Label(cr, c, 11, new Color(0.86f, 0.9f, 1f), TextAnchor.MiddleCenter, true);
                chipX += cw + 8f;
            }
            if (chips.Count == 0)
                Ui.Label(new Rect(card.x, chipY, card.width, 22f), "NOTHING EQUIPPED", 10, Ui.Muted, TextAnchor.MiddleCenter, true);

            // Close key.
            var x = new Rect(card.xMax - 112f, card.y + 12f, 98f, 24f);
            Ui.Pill(x, new Color(0.11f, 0.15f, 0.31f, 0.95f));
            Ui.Rim(x, new Color(0.29f, 0.36f, 0.65f), 0.9f);
            Ui.Label(x, "CLOSE · ESC", 11, new Color(0.9f, 0.94f, 1f), TextAnchor.MiddleCenter, true);
            if (Ui.HitArea(x))
                CloseInspect();
            GUI.color = Color.white;
        }

        static void AddChip(List<string> chips, string kind, CosmeticKind k, int index)
        {
            if (index <= 0)
                return;
            var def = Rank.Cosmetic(Rank.IdOf(k, index));
            if (def != null)
                chips.Add(kind + ": " + def.Name.Replace(" frame", "").Replace(" trail", "").Replace(" card", "").Replace(" name", ""));
        }
    }
}
