using UnityEngine;

namespace Arena
{
    /// The Look tab's Weapon skin section (CosmeticsUi.cs opens it as section 7): the lobby's class, a key per weapon of its role,
    /// that weapon's skins to equip (Expertise.cs; one per weapon) and your expertise in the class. Equipping saves the profile.
    public partial class ArenaApp
    {
        const int SkinSection = 7;  // the Look tab's Weapon skin option
        string SkinIcon => "class_" + kitChoice.ToString().ToLowerInvariant();
        int skinWeapon = -1;  // the weapon key picked in the section (-1: the lobby's weapon, set when it opens)

        const string SkinSub = "Your weapon's shots and muzzle flash. One per weapon, earned with expertise in the class you pick in the lobby.";

        /// The lobby's weapons for your role and class.
        static WeaponDef[] LobbyWeapons(Role role, Kit kit)
        {
            int n = Defs.WeaponCount(role, kit);
            var l = new WeaponDef[n];
            for (int i = 0; i < n; i++)
                l[i] = Defs.Weapon(role, kit, i);
            return l;
        }

        /// The Look tab's line for the section: the class's expertise level.
        string SkinLine(Profile p) => Defs.KitName(kitChoice) + " expertise " + p.ExpertiseLevel(kitChoice);

        /// A short stream of shots from the hand along the aim, in a skin's tint (model space).
        void DrawSkinShots(Rgba tint, float aim, float reach, float t, bool flash)
        {
            var dir = new V2(Mathf.Cos(aim), Mathf.Sin(aim));
            var side = new V2(-dir.Y, dir.X);
            var from = dir * 30f + side * -7f;
            for (int i = 0; i < 5; i++)
            {
                float f = ((i + t * 2.3f) % 5f) / 5f;
                var at = from + dir * (reach * f);
                GlowDisc(at, 8f, tint, 0.45f);
                Line(at - dir * 7f, at + dir * 3f, 3f, tint);
            }
            if (flash)
                GlowDisc(from + dir * 3f, 11f, tint, 0.6f);
        }

        void DrawSkinWindow(Profile p, float t)
        {
            OpenSectionPanel("WEAPON SKIN", CrtStyle.Ink, SkinSub, 1f);
            float px = DrawerPanel.x + 28f, top = DrawerPanel.y + 102f;
            var ink = CrtStyle.Ink;
            var ev = Event.current;
            var m = ev.mousePosition;
            Kit kit = kitChoice;
            var weapons = LobbyWeapons(hostRole, kit);
            if (skinWeapon < 0 || skinWeapon >= weapons.Length)
                skinWeapon = Mathf.Clamp(weaponChoice, 0, Mathf.Max(0, weapons.Length - 1));
            var weapon = weapons.Length > 0 ? weapons[skinWeapon] : null;
            var skins = weapon != null ? Expertise.For(weapon.Id) : new System.Collections.Generic.List<WeaponSkinDef>();
            skins.Insert(0, null);  // null: the standard look
            var mi = Expertise.Of(p.ExpertiseXp(kit));
            string equipped = weapon != null ? p.SkinOf(weapon.Id) : "";

            // Right: the weapon keys, then its skins.
            float lx = px + 364f, lw = DrawerPanel.xMax - 28f - lx;
            const float rowH = 52f, step = 56f;
            const int fit = 7;
            Ui.Label(new Rect(lx, top, lw, 18f), "WEAPON  ·  " + Defs.KitName(kit).ToUpperInvariant() + " " + Defs.Get(hostRole).Name.ToUpperInvariant(), 12, CabSoft, TextAnchor.MiddleLeft, true);
            float kw = (lw - 6f * Mathf.Max(0, weapons.Length - 1)) / Mathf.Max(1, weapons.Length);
            for (int i = 0; i < weapons.Length; i++)
                if (DkKey(new Rect(lx + i * (kw + 6f), top + 22f, kw, 32f), weapons[i].Name.ToUpperInvariant(), i == skinWeapon, FxCalm, size: 13, minSize: 9))
                    skinWeapon = i;
            float ly = top + 72f;
            Ui.Label(new Rect(lx, ly, 200f, 18f), "CHOOSE", 12, CabSoft, TextAnchor.MiddleLeft, true);
            var view = new Rect(lx, ly + 22f, lw, fit * step - 4f);
            int hover = -1;
            if (GUI.enabled && view.Contains(m))
            {
                int i = (int)((m.y - view.y) / step);
                if (i < skins.Count && i < fit && (m.y - view.y) - i * step < rowH)
                    hover = i;
            }
            for (int i = 0; i < Mathf.Min(skins.Count, fit); i++)
            {
                var c = skins[i];
                string id = c == null ? "" : c.Id;
                bool ok = c == null || p.OwnsSkin(c), on = equipped == id;
                var rr = new Rect(lx, view.y + i * step, lw, rowH);
                Ui.Fill(rr, on ? CrtStyle.Gold.WithAlpha(0.08f) : ink.WithAlpha(i == hover ? 0.12f : 0.04f));
                CrtKit.Frame(rr, 1f, on ? CrtStyle.Gold : ink.WithAlpha(0.25f));
                var th = new Rect(rr.x + 8f, rr.y + (rowH - 40f) / 2f, 64f, 40f);
                Ui.Pill(th, new Color(0.05f, 0.07f, 0.12f, 0.95f));
                if (BeginUiGl())
                {
                    UiModelAt(new V2(th.x + 4f, th.center.y + 2f), 0.5f);
                    var tint = c == null ? Expertise.StandardTint : c.Tint;
                    DrawSkinShots(ok ? tint : tint.WithAlpha(0.35f), 0f, 70f, 0.4f, true);
                    EndUiGl();
                }
                CrtKit.Frame(th, 1f, ink.WithAlpha(ok ? 0.5f : 0.25f));
                float nx = rr.x + 82f, nw = rr.width - 82f - 106f;
                CrtUi.Text(new Rect(nx, rr.y + 5f, nw, 24f), c == null ? "Standard issue" : c.Name, 15, ok ? CrtStyle.Ink : CrtStyle.Dim, TextAnchor.MiddleLeft, true, FxCalm);
                string sub = c == null ? "default" : ok ? "unlocked" : "locked: " + Defs.KitName(kit) + " expertise " + c.Level;
                Ui.Label(new Rect(nx, rr.y + 31f, nw, 18f), sub, 13, c != null && ok ? Ui.Good : CrtStyle.Dim, TextAnchor.MiddleLeft);
                var kr = new Rect(rr.xMax - 8f - 92f, rr.y + (rowH - 32f) / 2f, 92f, 32f);
                if (!ok)
                {
                    CrtKit.Dashed(kr, 2f, CrtStyle.Dim.WithAlpha(0.8f));
                    Ui.Icon(new Rect(kr.x + 7f, kr.center.y - 8f, 16f, 16f), PixelArt.Get("ui_lock"), CrtStyle.Dim);
                    Ui.Label(new Rect(kr.x + 26f, kr.y, kr.width - 28f, kr.height), "LV " + c.Level, 13, CrtStyle.Dim, TextAnchor.MiddleLeft, true);
                }
                else if (on)
                {
                    CrtKit.SoftBox(kr, CrtStyle.Gold, true, false, false);
                    Ui.Label(kr, "EQUIPPED", 13, CrtKit.SoftText(CrtStyle.Gold, true), TextAnchor.MiddleCenter, true);
                }
                else if (DkKey(kr, "EQUIP", false, FxCalm, size: 16) && weapon != null && p.EquipSkin(weapon.Id, id))
                    p.Save();
            }
            if (skins.Count == 1 && weapon != null)
                Ui.Label(new Rect(lx, view.y + step + 4f, lw, 40f), "No skins for the " + weapon.Name + " yet. They will unlock here as your " + Defs.KitName(kit) + " expertise rises.", 13, CabSoft, TextAnchor.UpperLeft, false, true);

            // Left: the preview, the hovered skin tried on.
            var shown = hover >= 0 ? skins[hover] : Expertise.Skin(equipped);
            Ui.Label(new Rect(px, top, 340f, 18f), "TRY IT ON", 12, CabSoft, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(px + 80f, top, 260f, 18f), (shown == null ? "Standard issue" : shown.Name) + (shown != null && !p.OwnsSkin(shown) ? "  (expertise " + shown.Level + ")" : ""), 12, CrtStyle.Gold, TextAnchor.MiddleRight, true);
            var pv = new Rect(px, top + 22f, 340f, 250f);
            Ui.Fill(pv, new Color(0.04f, 0.02f, 0.07f, 1f));
            if (BeginUiGl())
            {
                BackgroundThumb(pv, CrtStyle.Ink, 0.4f);
                UiClip(pv);  // (EndUiGl lifts the clip)
                UiModelAt(new V2(pv.x + 90f, pv.center.y), 1.4f);
                float aim = -0.5f + 0.9f * Mathf.Sin(t * 0.7f);  // (DrawGenericPlayer's aim)
                DrawSkinShots(shown == null ? Expertise.StandardTint : shown.Tint, aim, 150f, t, true);
                DrawGenericPlayer(t);
                EndUiGl();
            }
            CrtKit.Frame(pv, 1f, ink.WithAlpha(0.5f));

            // Under it: your expertise in the class.
            Ui.Label(new Rect(px, pv.yMax + 10f, 340f, 18f), Defs.KitName(kit).ToUpperInvariant() + " EXPERTISE", 12, CabSoft, TextAnchor.MiddleLeft, true);
            var mc = new Rect(px, pv.yMax + 32f, 340f, 88f);
            DkSub(mc);
            CrtKit.Frame(mc, 1f, ink.WithAlpha(0.5f));
            Ui.Label(new Rect(mc.x + 14f, mc.y + 8f, 100f, 34f), mi.Maxed ? "MAX" : "LV " + mi.Level, 26, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < Expertise.MaxLevel; i++)
            {
                var pip = new Rect(mc.x + 112f + i * 21f, mc.y + 18f, 15f, 15f);
                if (i < mi.Level)
                    Ui.Fill(pip, CrtStyle.Gold);
                else
                    CrtKit.Frame(pip, 1f, ink.WithAlpha(0.4f));
            }
            var bar = new Rect(mc.x + 14f, mc.y + 50f, mc.width - 28f, 6f);
            Ui.Fill(bar, ink.WithAlpha(0.12f));
            Ui.Fill(new Rect(bar.x, bar.y, bar.width * mi.Fraction, bar.height), CrtStyle.Gold.WithAlpha(0.85f));
            var next = Expertise.NextFor(kit, mi.Level);
            string nextLine = mi.Maxed ? "Top level reached" : next != null ? "Next at LV " + next.Level + ": " + next.Name : "Next: LV " + (mi.Level + 1);
            Ui.Label(new Rect(mc.x + 14f, mc.y + 60f, mc.width - 28f, 20f), nextLine, 13, CabSoft, TextAnchor.MiddleLeft);
            if (!mi.Maxed)
                Ui.Label(new Rect(mc.x + 14f, mc.y + 60f, mc.width - 28f, 20f), mi.Into.ToString("N0") + " / " + mi.Need.ToString("N0") + " XP", 13, CabSoft, TextAnchor.MiddleRight);
            Ui.Label(new Rect(px, pv.yMax + 132f, 340f, 36f), "Every run's XP also goes to the class you played.", 13, CabSoft, TextAnchor.UpperLeft, false, true);
        }
    }
}
