using UnityEngine;

namespace Arena
{
    /// The HUD modes on H: 0 full (Hud.cs), 1 corner text, 2 on the player, 3 hidden. The two partial modes keep only
    /// what you act on mid-fight: health, ability cooldown numbers and enemies left (a boss's slim bar stays, as when
    /// hidden). Looks: Docs/Mockups/hud-modes.html (A and C). Local to each player: no protocol change.
    public partial class ArenaApp
    {
        public const int HudModeCount = 4;
        static readonly string[] HudModeNames = { "Full", "Corner text", "On the player", "Hidden" };
        int hudMode;          // 0 full, 1 corner text, 2 on the player, 3 hidden
        float miniT;          // 0..1: the partial HUD easing in and out

        void SetHudMode(int mode)
        {
            hudMode = ((mode % HudModeCount) + HudModeCount) % HudModeCount;
            hudShown = hudMode == 0;  // the full console only shows in mode 0 (Hud.cs)
        }

        /// Eases the partial HUD in or out (called every frame from Update).
        void TickMiniHud()
        {
            bool want = (hudMode == 1 || hudMode == 2) && !LaunchHoldsHud && !BossIntroHoldsHud && !RecallActive;
            miniT = MathX.MoveTowards(miniT, want ? 1f : 0f, Time.unscaledDeltaTime / HudFadeTime);
        }

        static readonly Color MiniDim = new Color(0.62f, 0.69f, 0.74f);

        void DrawMiniHud(Player me)
        {
            if (me == null || miniT <= 0f)
                return;
            float a = miniT * miniT * (3f - 2f * miniT);
            if (hudMode == 2)
                MiniOnPlayer(me, a);
            else if (hudMode == 1)
                MiniCorners(me, a);
            MiniEnemiesLeft(a, hudMode == 2);
        }

        void MiniEnemiesLeft(float a, bool top)
        {
            string n = game.Remaining.ToString();
            if (top)  // centre top, small
            {
                Ui.Shadowed(new Rect(W / 2f - 60f, 10f, 120f, 34f), n, 30, Fade(Color.white, a));
                Ui.Shadowed(new Rect(W / 2f - 60f, 40f, 120f, 14f), "LEFT", 11, Fade(MiniDim, a));
                return;
            }
            Ui.Shadowed(new Rect(W - 212f, 12f, 200f, 40f), n, 38, Fade(Color.white, a), TextAnchor.MiddleRight);
            Ui.Shadowed(new Rect(W - 212f, 50f, 200f, 14f), "ENEMIES LEFT", 11, Fade(MiniDim, a), TextAnchor.MiddleRight);
            string wave = game.Practice != null ? "PRACTICE" : game.Hunt != null ? "HUNT WAVE " + game.HuntWave : "WAVE " + game.Wave;
            Ui.Shadowed(new Rect(W - 212f, 65f, 200f, 14f), wave, 11, Fade(new Color(MiniDim.r, MiniDim.g, MiniDim.b, 0.65f), a), TextAnchor.MiddleRight);
        }

        /// Cooldown text for slot i: "" for none, the key alone when ready, else the seconds left.
        bool MiniSlot(Player me, int i, out string key, out string cd)
        {
            key = SlotKeys[i];
            cd = "";
            var slot = me.Slots[i];
            if (slot.Id == AbilityId.None)
                return false;
            if (slot.Charges <= 0)
                cd = slot.Timer.ToString(slot.Timer < 10f ? "0.0" : "0");
            return true;
        }

        // A: text in the corners, no boxes.
        void MiniCorners(Player me, float a)
        {
            float frac = Mathf.Clamp01(me.Hp / Mathf.Max(1f, me.MaxHp));
            if (me.Downed)
            {
                Ui.Shadowed(new Rect(20f, H - 62f, 260f, 30f), "DOWNED", 26, Fade(new Color(1f, 0.45f, 0.45f), a), TextAnchor.MiddleLeft);
                Ui.Shadowed(new Rect(22f, H - 34f, 260f, 16f), Mathf.RoundToInt(me.Revive * 100f) + "% revived", 12, Fade(MiniDim, a), TextAnchor.MiddleLeft);
            }
            else
            {
                string hp = Mathf.CeilToInt(me.Hp).ToString();
                Ui.Shadowed(new Rect(20f, H - 62f, 130f, 36f), hp, 32, Fade(HealthColor(frac), a), TextAnchor.MiddleLeft);
                float w = Ui.TextWidth(hp, 32, true);
                Ui.Shadowed(new Rect(24f + w, H - 52f, 120f, 22f), "/ " + Mathf.CeilToInt(me.MaxHp), 14, Fade(MiniDim, a), TextAnchor.MiddleLeft);
                Ui.Shadowed(new Rect(22f, H - 30f, 120f, 14f), "HEALTH", 11, Fade(MiniDim, a), TextAnchor.MiddleLeft);
            }
            int shown = 0;
            for (int i = 0; i < Upgrades.SlotCount; i++)
                if (me.Slots[i].Id != AbilityId.None)
                    shown++;
            float step = 56f, x0 = W / 2f - step * (shown - 1) / 2f;
            int n = 0;
            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                if (!MiniSlot(me, i, out string key, out string cd))
                    continue;
                float cx = x0 + step * n++;
                bool ready = cd.Length == 0;
                Color accent = i == Upgrades.UltSlot ? Ui.Gold : ToColor(me.Def.Color);
                Ui.Shadowed(new Rect(cx - 26f, H - 58f, 52f, 16f), key, 13, Fade(ready ? accent : new Color(0.45f, 0.5f, 0.55f), a));
                Ui.Shadowed(new Rect(cx - 26f, H - 42f, 52f, 26f), ready ? "·" : cd, ready ? 22 : 20, Fade(ready ? accent : Color.white, a));
            }
        }

        // C: the info rides on your character.
        void MiniOnPlayer(Player me, float a)
        {
            V2 at = ToUi(me.Disp);
            float r = me.Def.BodyR * WorldToUi, ar = r + 11f;
            float frac = Mathf.Clamp01(me.Hp / Mathf.Max(1f, me.MaxHp));
            const float start = Mathf.PI * 0.75f, sweep = Mathf.PI * 1.5f;  // a three-quarter arc, gap at the top
            Color track = new Color(0f, 0f, 0f, 0.5f * a), fill = Fade(me.Downed ? new Color(0.6f, 0.6f, 0.65f) : HealthColor(frac), a);
            float f = me.Downed ? me.Revive : frac;
            int steps = Mathf.Max(24, Mathf.RoundToInt(ar * 1.2f));
            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps, ang = start + sweep * t;
                var p = new Rect(at.X + Mathf.Cos(ang) * ar - 2f, at.Y + Mathf.Sin(ang) * ar - 2f, 4f, 4f);
                Ui.Fill(p, t <= f ? fill : track);
            }
            string hp = me.Downed ? "DOWNED" : Mathf.CeilToInt(me.Hp).ToString();
            Ui.Shadowed(new Rect(at.X - 50f, at.Y - ar - 22f, 100f, 16f), hp, 13, Fade(me.Downed ? new Color(1f, 0.6f, 0.6f) : Color.white, a));
            if (me.Downed)
                return;
            int shown = 0;
            for (int i = 0; i < Upgrades.SlotCount; i++)
                if (me.Slots[i].Id != AbilityId.None)
                    shown++;
            float step = 28f, x0 = at.X - step * (shown - 1) / 2f, y = at.Y + ar + 10f;
            int n = 0;
            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                if (!MiniSlot(me, i, out string key, out string cd))
                    continue;
                float cx = x0 + step * n++;
                bool ready = cd.Length == 0;
                Color accent = i == Upgrades.UltSlot ? Ui.Gold : ToColor(me.Def.Color);
                Ui.Shadowed(new Rect(cx - 14f, y, 28f, 18f), ready ? key : cd, ready ? 13 : 15, Fade(ready ? accent : Color.white, a));
                float cdMax = Mathf.Max(0.01f, game.Cooldown(me, i));
                float prog = ready ? 1f : Mathf.Clamp01(1f - me.Slots[i].Timer / cdMax);
                Ui.Fill(new Rect(cx - 8f, y + 19f, 16f, 2f), new Color(1f, 1f, 1f, 0.2f * a));
                Ui.Fill(new Rect(cx - 8f, y + 19f, 16f * prog, 2f), Fade(ready ? accent : new Color(1f, 1f, 1f, 0.6f), a));
            }
        }
    }
}
