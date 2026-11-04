using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The in-match HUD (IMGUI): DrawHud, the wave plate, player cards, ability bar and mastery, banners. Split off:
    /// HudTab.cs (Tab menu), HudBossBars.cs (boss bars), HudPicker.cs (upgrade picker), HudSummary.cs (end of run).
    public partial class ArenaApp
    {
        /// The ability bar's key labels: your Keybinds (Settings), upper case.
        static string[] SlotKeys => Keybinds.SlotLabels;

        void DrawHud()
        {
            var me = game.Me;
            DrawJuiceHud();  // damage numbers and a boss's entrance card, under the rest of the HUD (Juice.cs)
            DrawAbilityCallouts();  // what each player just cast, over their head (AbilityLooks.cs)
            HudGroup(1, new Vector2(-150f, 30f), new Rect(SideL, SideY, SideW, SideH), DrawWaveCorner);
            HudGroup(3, new Vector2(0f, -80f), new Rect(W / 2f - 320f, 0f, 640f, 60f), () =>
            {
                if (game.Recorder != null)  // this match is being recorded (hosts only)
                    Ui.Label(new Rect(W - 70f, 34f, 62f, 16f), "● REC", 10, new Color(1f, 0.4f, 0.4f, 0.8f), TextAnchor.MiddleRight, true);
                DrawBossBars();
            });
            DrawStageBanner();  // a boss changing stage (StageShiftLook.cs)
            DrawChallengeHud(me);  // a challenge room's portal, title, objective and result (ChallengeLook.cs)
            DrawMiniBossBars(introHidHud ? 0f : 1f - hudT);  // (not while a boss's entrance card has the HUD off: they'd sit on the card's bars)  // the HUD hidden (H): a slim bar per boss stays at the top (HealthBars.cs)
            DrawWardenTags();   // "EXPOSED" / "STUNNED" over the Warden (WardenLook.cs)
            DrawUnlockToasts(W / 2f, 150f);  // SavesUi.cs
            if (aimDebug && me != null)
                DrawAimDebug(me);
            if (dpsMeter)
                DrawDpsMeter();  // F2 (DpsMeterUi.cs)

            // Names, level-up popups and downed labels over players (hidden while they're under
            // the between-waves banner, so they don't show on top of its text).
            bool banner = game.State == MatchState.Intermission && (me == null || me.Offer == null);
            foreach (var p in game.Players.Values)
            {
                float r = p.Def.BodyR;
                V2 at = ToUi(p.Disp);  // arena -> HUD coordinates (with the camera lean)
                r *= WorldToUi;
                if (banner && !LaunchHoldsBanner && at.X > BannerRect.x - 100f && at.X < BannerRect.xMax + 100f && at.Y > BannerRect.y - 20f && at.Y < BannerRect.yMax + r + 60f)
                    continue;
                if (LaunchHides(p))
                    continue;  // not landed yet (LaunchIntro.cs)
                DrawLaunchIcon(p, at, r);
                Ui.Shadowed(new Rect(at.X - 100f, at.Y - r - 30f, 200f, 18f), p.Name + (p.Id == game.MyId ? " (you)" : ""), 12,
                    ToColor(p.Def.Color.WithAlpha(0.95f)));
                if (p.LevelFlash > 0f)
                {
                    float rise = (1.5f - p.LevelFlash) * 22f;
                    Ui.Shadowed(new Rect(at.X - 100f, at.Y - r - 58f - rise * (Juicy ? 2f : 1f), 200f, 30f), "LEVEL UP!", Juicy ? 25 : 19,
                        new Color(1f, 0.88f, 0.35f, Mathf.Min(1f, p.LevelFlash)));
                }
                if (p.Downed)
                {
                    var tag = new Rect(at.X - 92f, at.Y + r + 10f, 184f, 34f);
                    Ui.HudPanel(tag, new Color(1f, 0.35f, 0.35f));
                    Ui.Label(new Rect(tag.x, tag.y + 3f, tag.width, 14f), "DOWNED — stand close to revive", 10, new Color(1f, 0.7f, 0.7f), TextAnchor.MiddleCenter, true);
                    Ui.Bar(new Rect(tag.x + 10f, tag.y + 21f, tag.width - 20f, 6f), p.Revive, Ui.Good);
                }
            }

            HudGroup(2, new Vector2(150f, 30f), new Rect(SideR, SideY, SideW, SideH), DrawPlayerCorner);
            if (me != null)
                HudGroup(0, new Vector2(0f, 110f), new Rect(W / 2f - 391f, H - 92f, 782f, 80f), () => DrawAbilityBar(me));
            DrawTreasureHud(me);  // your chest item's card, or what the wave is waiting for (PropsLook.cs)

            switch (game.State)
            {
                case MatchState.Intermission:
                    // While the pause menu is up these full-screen choices stay out of its way: they're drawn first, so their click
                    // areas would take the clicks that belong to the menu (Leave to lobby did nothing while a pick was on screen).
                    bool pauseUp = PauseMenuWanted;
                    if (me != null && me.Offer != null)
                    {
                        if (!tabHeld && !PickOutroPlaying && !pauseUp)
                            DrawUpgradePicker(me);  // holding Tab swaps the cards for the Tab menu; the next pick waits for the last one's outro (PickOutro.cs)
                        break;
                    }
                    if (game.PathOffer != null && !tabHeld)
                    {
                        if (!pauseUp)
                            DrawPathChoice(Time.unscaledTime);  // PathsUi.cs
                        break;
                    }
                    if (game.ShopOpen && me != null)
                    {
                        if (!tabHeld && !pauseUp)
                            DrawShop(me, Time.unscaledTime);  // ShopUi.cs
                        break;
                    }
                    var choosing = new List<string>();
                    foreach (var p in game.Players.Values)
                        if (p.PendingPicks + p.PendingLegendary + p.PendingMods + p.PrizePicks > 0 && p.Id != game.MyId)
                            choosing.Add(p.Name);
                    var pr = game.Practice;
                    int pNext = pr == null ? 0 : (game.PracticeIndex + 1) % Math.Max(1, pr.Waves.Count);
                    string sub = choosing.Count > 0 ? "Waiting for " + string.Join(" and ", choosing.ToArray()) + " to choose an upgrade…"
                        : game.AnyonePicking() ? ""
                        : pr != null ? "Practice wave " + (pNext + 1) + " starts in " + Mathf.CeilToInt(game.StateTimer)
                        : ApproachLine(game.Wave + 1) + "Wave " + (game.Wave + 1) + " starts in " + Mathf.CeilToInt(game.StateTimer);  // ApproachUi.cs
                    if (LaunchHoldsBanner)
                        break;  // a run launching: GET READY waits until the players have landed (LaunchIntro.cs)
                    if (game.Room == RoomPhase.Portal)
                        break;  // a portal is open: its own call instead (ChallengeLook.cs)
                    if (pr != null)
                        DrawBanner(game.PracticeIndex < 0 ? "PRACTICE RANGE" : "PRACTICE WAVE " + (game.PracticeIndex + 1) + " CLEARED", sub, Ui.Accent);
                    else
                        DrawBanner(game.Wave == 0 ? "GET READY" : "WAVE " + game.Wave + " CLEARED", sub, game.IsBossWave(game.Wave) ? Ui.Gold : Ui.Accent);
                    break;
                case MatchState.Victory:
                case MatchState.GameOver:
                    if (!pauseMenuOpen && !tabHeld && !RecallActive)
                        DrawRunSummary();
                    break;
            }
            if (tabHeld)
                DrawRunStatsScreen(me, false);  // RunStatsUi.cs
            else if (me != null && game.State != MatchState.GameOver && game.State != MatchState.Victory)
            {
                HudGroup(3, new Vector2(0f, -60f), new Rect(W - 290f, 10f, 274f, 22f), () =>
                {
                    var hint = new Rect(W - 290f, 10f, 274f, 22f);
                    Ui.Pill(hint, new Color(0f, 0f, 0f, 0.35f));
                    Ui.Label(hint, "Hold TAB  ·  run stats & build      H  ·  hide HUD", 11, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter, true);
                });
                // With the HUD hidden, a quiet reminder of how to get it back, so nobody's locked out of it.
                float back = Mathf.Clamp01((0.35f - hudT) / 0.35f);
                if (back > 0f && !LaunchActive && !RecallActive)  // (not while a run launches, the HUD on its way in, or beams out: Recall.cs)
                {
                    float breathe = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 2f);
                    Ui.Shadowed(new Rect(0f, H - 30f + 8f * (1f - back), W, 18f), "Press H to show the HUD", 12, new Color(1f, 1f, 1f, 0.38f * back * breathe));
                }
            }
        }

        // --- Hiding the HUD (H) ---------------------------------------------------------

        bool hudShown = true;
        float hudT = 1f;               // 0 hidden .. 1 shown, eased in Update
        const float HudFadeTime = 0.55f, HudStagger = 0.14f;
        const int HudGroups = 4;       // 0 ability bar, 1 wave panel, 2 player cards, 3 top row (boss bars, hints)

        /// One piece of the HUD, played in its turn: showing, the ability bar rises first, then the side panels slide
        /// in, then the top row drops; hiding runs the other way. Each piece slides, scales up from 90% and fades,
        /// overshooting a little as it lands (and pulling back before it leaves).
        void HudGroup(int order, Vector2 away, Rect pivot, Action draw)
        {
            float p = Mathf.Clamp01(hudT * (1f + HudStagger * (HudGroups - 1)) - HudStagger * order);
            if (p <= 0f)
                return;
            if (p >= 1f)
            {
                draw();
                return;
            }
            const float back = 1.70158f;  // ease-out-back
            float q = p - 1f, e = 1f + (back + 1f) * q * q * q + back * q * q;
            float k = 0.9f + 0.1f * e, a = p * p * (3f - 2f * p);
            Vector2 c = pivot.center;
            float ox = away.x * (1f - e) + c.x * (1f - k), oy = away.y * (1f - e) + c.y * (1f - k);
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX + ox * scale, offY + oy * scale, 0f), Quaternion.identity, new Vector3(scale * k, scale * k, 1f));
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * a);
            Ui.Alpha = a;
            draw();
            Ui.Alpha = 1f;
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        // --- Beside the hotbar ----------------------------------------------------------

        // The wave panel sits left of the ability bar and your card right of it, both 72 px tall and lined up with
        // the bar's bottom; the path and modifier chips stack up from the wave panel, teammates from your card.
        const float SideW = 229f, SideL = 12f, SideR = W - 12f - SideW, SideH = 72f, SideY = H - 12f - SideH;

        void DrawWaveCorner()
        {
            DrawWavePlate(new Rect(SideL, SideY, SideW, SideH));
            float y = SideY - 28f;
            y -= DrawApproachChip(SideL, y);  // ApproachUi.cs
            if (game.CurrentPath != PathId.None)
            {
                DrawPathChip(SideL, y);  // PathsUi.cs
                y -= 24f;
            }
            if (DrawGoldChip(SideL, y))  // ShopUi.cs
                y -= 24f;
            DrawActiveModifiers(DrawActiveDifficulty(SideL, y + 2f), y + 2f);  // DifficultyUi.cs, ModifiersUi.cs
        }

        void DrawPlayerCorner()
        {
            var ids = new List<int>(game.Players.Keys);
            ids.Sort((a, b) => a == game.MyId ? -1 : b == game.MyId ? 1 : a.CompareTo(b));
            if (ids.Count == 0)
                return;
            // Yours in full; teammates as slim rows above it. (A spectator-less game always has you first.)
            DrawPlayerHudCard(game.Players[ids[0]], new Rect(SideR, SideY, SideW, SideH), ids[0] == game.MyId);
            float y = SideY - 6f;
            for (int i = 1; i < ids.Count; i++)
            {
                y -= 30f;
                DrawTeammateRow(game.Players[ids[i]], new Rect(SideR, y, SideW, 30f));
                y -= 5f;
            }
        }

        /// A teammate as one slim row: name and class, level, and health (or how far their revive has got).
        void DrawTeammateRow(Player p, Rect r)
        {
            var role = ToColor(p.Def.Color);
            Ui.HudPanel(r, p.Downed ? new Color(1f, 0.3f, 0.3f) : (Color?)null);
            Ui.Pill(new Rect(r.x + 5f, r.y + 6f, 3f, r.height - 12f), p.Downed ? new Color(0.5f, 0.5f, 0.55f) : role);
            Ui.Label(new Rect(r.x + 14f, r.y + 2f, 82f, 14f), p.Name, 11, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 14f, r.y + 15f, 82f, 12f), Defs.KitName(p.Kit) + " " + p.Def.Name, 8, role, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.xMax - 40f, r.y + 2f, 34f, 14f), "LV " + p.Level, 9, Ui.Gold, TextAnchor.MiddleRight, true);
            var hp = new Rect(r.x + 98f, r.y + 17f, r.width - 106f, 7f);
            float frac = Mathf.Clamp01(p.Hp / Mathf.Max(1f, p.MaxHp));
            if (p.Downed)
            {
                Ui.Bar(hp, p.Revive, new Color(0.6f, 0.6f, 0.65f));
                Ui.Label(new Rect(hp.x, r.y + 2f, hp.width - 36f, 14f), "DOWNED", 9, new Color(1f, 0.6f, 0.6f), TextAnchor.MiddleLeft, true);
                return;
            }
            GhostBar(hp, frac, HealthColor(frac), PlayerKey(p.Id, true));
            if (p.ShieldHp > 0f)
                Ui.Bar(new Rect(hp.x + 1f, hp.yMax - 2f, (hp.width - 2f) * Mathf.Clamp01(p.ShieldHp / Mathf.Max(1f, p.MaxHp)), 2f), 1f, new Color(0.55f, 0.8f, 1f), new Color(0f, 0f, 0f, 0f));
            Ui.Label(new Rect(hp.x, r.y + 2f, hp.width - 36f, 14f), Mathf.CeilToInt(p.Hp) + " / " + Mathf.CeilToInt(p.MaxHp), 9, Ui.Muted, TextAnchor.MiddleLeft, true);
        }

        /// Green when healthy, through yellow, to red when low.
        static Color HealthColor(float frac) =>
            frac > 0.5f ? Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(0.35f, 0.95f, 0.45f), (frac - 0.5f) * 2f)
                : Color.Lerp(new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.3f), frac * 2f);

        // --- HUD pieces -----------------------------------------------------------

        int plateWave = -1, plateMax = 1;

        /// Left of the ability bar: the wave number, how many enemies are left (with a progress bar) and the score.
        void DrawWavePlate(Rect r)
        {
            if (game.Wave != plateWave)
            {
                plateWave = game.Wave;
                plateMax = 1;
            }
            plateMax = Math.Max(plateMax, game.Remaining);
            var practice = game.Practice;
            bool bossWave = practice != null ? BossUp() : game.IsBossWave(game.Wave);
            Ui.HudPanel(r, bossWave ? Ui.Gold : (Color?)null);
            string head = practice != null ? "PRACTICE  ·  LV " + game.Wave : game.Hunt != null ? (bossWave ? "HUNT  ·  BOSS" : "HUNT  ·  WAVE") : bossWave ? "BOSS WAVE" : "WAVE";
            Ui.Label(new Rect(r.x + 16f, r.y + 8f, 120f, 14f), head, 10, bossWave ? Ui.Gold : practice != null ? Ui.Accent : Ui.Muted, TextAnchor.MiddleLeft, true);
            string num = practice != null ? Math.Max(1, game.PracticeIndex + 1).ToString() : game.Hunt != null ? game.HuntWave.ToString() : game.Wave.ToString();
            Ui.Label(new Rect(r.x + 14f, r.y + 20f, 120f, 40f), num, 34, Color.white, TextAnchor.MiddleLeft, true);
            string of = practice != null ? (practice.Loop ? "/ " + practice.Waves.Count + "  LOOP" : "/ " + practice.Waves.Count) : game.Endless ? "ENDLESS" : game.Hunt != null ? "/ " + Hunts.Length : "/ " + Game.MaxWave;
            Ui.Label(new Rect(r.x + 18f + num.Length * 20f, r.y + 31f, 100f, 24f), of, 13,
                game.Endless || practice != null && practice.Loop ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.xMax - 140f, r.y + 8f, 124f, 14f), "ENEMIES LEFT", 10, Ui.Muted, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.xMax - 140f, r.y + 20f, 124f, 26f), game.Remaining.ToString(), 20, Color.white, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.xMax - 140f, r.y + 44f, 124f, 14f), "SCORE " + game.Score.ToString("N0"), 10, Ui.Gold, TextAnchor.MiddleRight, true);
            float progress = game.State == MatchState.Fighting ? 1f - (float)game.Remaining / Math.Max(1, plateMax) : 1f;
            Ui.Bar(new Rect(r.x + 14f, r.yMax - 12f, r.width - 28f, 5f), progress, bossWave ? Ui.Gold : Ui.Accent);
        }

        bool BossUp()
        {
            foreach (var e in game.Enemies.Values)
                if (e.Def.IsBoss)
                    return true;
            return false;
        }

        /// One player's card: role-colored accent, name and class, health (with shield) and XP.
        void DrawPlayerHudCard(Player p, Rect r, bool mine)
        {
            var role = ToColor(p.Def.Color);
            Ui.HudPanel(r, p.Downed ? new Color(1f, 0.3f, 0.3f) : mine ? role : (Color?)null);
            Ui.Pill(new Rect(r.x + 6f, r.y + 9f, 4f, r.height - 18f), p.Downed ? new Color(0.5f, 0.5f, 0.55f) : role);
            Ui.Label(new Rect(r.x + 18f, r.y + 6f, r.width - 70f, 18f), p.Name, 13, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 18f, r.y + 22f, r.width - 70f, 14f), Defs.KitName(p.Kit) + " " + p.Def.Name, 10, role, TextAnchor.MiddleLeft, true);
            var lv = new Rect(r.xMax - 52f, r.y + 8f, 42f, 18f);
            Ui.Pill(lv, new Color(1f, 0.8f, 0.35f, 0.22f));
            Ui.Label(lv, "LV " + p.Level, 10, Ui.Gold, TextAnchor.MiddleCenter, true);

            var hpBar = new Rect(r.x + 18f, r.y + 39f, r.width - 28f, 12f);
            float frac = Mathf.Clamp01(p.Hp / Mathf.Max(1f, p.MaxHp));
            Color hpCol = HealthColor(frac);
            if (p.Downed)
            {
                Ui.Bar(hpBar, p.Revive, new Color(0.6f, 0.6f, 0.65f));
                Ui.Label(hpBar, "DOWNED", 9, new Color(1f, 0.6f, 0.6f), TextAnchor.MiddleCenter, true);
            }
            else
            {
                GhostBar(hpBar, frac, hpCol, PlayerKey(p.Id, true));
                if (p.ShieldHp > 0f)  // Arcane Ward: a blue stripe along the bottom of the health bar
                    Ui.Bar(new Rect(hpBar.x + 2f, hpBar.yMax - 4f, (hpBar.width - 4f) * Mathf.Clamp01(p.ShieldHp / Mathf.Max(1f, p.MaxHp)), 3f), 1f, new Color(0.55f, 0.8f, 1f), new Color(0f, 0f, 0f, 0f));
                Ui.Shadowed(new Rect(hpBar.x, hpBar.y - 1f, hpBar.width, hpBar.height + 2f), Mathf.CeilToInt(p.Hp) + " / " + Mathf.CeilToInt(p.MaxHp), 10, Color.white);
            }
            Ui.Bar(new Rect(r.x + 18f, r.y + 55f, r.width - 28f, 4f), p.Xp / Defs.XpToNext(p.Level), new Color(1f, 0.82f, 0.3f), new Color(0f, 0f, 0f, 0.4f));
        }

        /// F3: the numbers behind the aim lines.
        void DrawAimDebug(Player me)
        {
            var r = new Rect(14f, 12f, 300f, 104f);
            Ui.HudPanel(r, new Color(0.3f, 0.9f, 1f));
            Func<float, string> deg = a => (MathX.Fmod(a * 180f / MathX.Pi + 360f, 360f)).ToString("0.00") + "°";
            float diff = MathX.Fmod(game.LastShotAngle - game.LastShotAim + MathX.Pi, MathX.Tau) - MathX.Pi;
            Ui.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 16f), "AIM DEBUG  (F3)", 11, new Color(0.3f, 0.9f, 1f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 24f, r.width - 24f, 18f), "Aim now", 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 12f, r.y + 24f, r.width - 24f, 18f), deg(me.Aim), 12, Color.white, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 42f, r.width - 24f, 18f), "Last shot", 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 12f, r.y + 42f, r.width - 24f, 18f), game.LastShotId != 0 ? deg(game.LastShotAngle) : "-", 12, new Color(1f, 0.85f, 0.2f), TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 60f, r.width - 24f, 18f), "Shot vs aim when it appeared", 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 12f, r.y + 60f, r.width - 24f, 18f), game.LastShotId != 0 ? (diff * 180f / MathX.Pi).ToString("+0.00;-0.00;0.00") + "°" : "-", 12, Color.white, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 78f, r.width - 24f, 18f), "Ping to host", 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 12f, r.y + 78f, r.width - 24f, 18f), session.IsHost ? "0 ms (you're the host)" : Mathf.RoundToInt(game.PingMs) + " ms", 12,
                session.IsHost || game.PingMs < 60f ? Ui.Good : game.PingMs < 120f ? Ui.Warn : new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleRight, true);
        }

        /// A centered announcement between waves.
        static readonly Rect BannerRect = new Rect(W / 2f - 280f, H / 2f - 92f, 560f, 96f);

        void DrawBanner(string title, string sub, Color accent)
        {
            var r = new Rect(BannerRect.x, BannerRect.y, BannerRect.width, sub.Length > 0 ? 96f : 70f);
            Ui.Card(r, accent, 0.8f);  // solid, so nothing in the arena shows through
            Ui.Fill(new Rect(r.x + 40f, r.y + 14f, r.width - 80f, 1f), new Color(accent.r, accent.g, accent.b, 0.35f));
            Ui.Title(new Rect(r.x, r.y + 16f, r.width, 40f), title, 30, Color.white);
            if (sub.Length > 0)
                Ui.Label(new Rect(r.x, r.y + 60f, r.width, 22f), sub, 15, Ui.Muted, TextAnchor.MiddleCenter, true);
        }

        /// XP bar plus the ability tiles (Space / Q / E / Shift / ultimate) along the bottom.
        void DrawAbilityBar(Player me)
        {
            const float tileW = 150f, tileH = 58f, gap = 8f;
            float total = tileW * Upgrades.SlotCount + gap * (Upgrades.SlotCount - 1);
            float x0 = (W - total) / 2f, y = H - tileH - 12f;

            // XP strip with the level badge.
            var lv = new Rect(x0, y - 22f, 52f, 16f);
            Ui.Pill(lv, new Color(1f, 0.8f, 0.35f, 0.25f));
            Ui.Label(lv, "LV " + me.Level, 10, Ui.Gold, TextAnchor.MiddleCenter, true);
            Ui.Bar(new Rect(x0 + 58f, y - 17f, total - 58f, 6f), me.Xp / Defs.XpToNext(me.Level), new Color(1f, 0.82f, 0.3f), new Color(0f, 0f, 0f, 0.5f));

            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                var r = new Rect(x0 + i * (tileW + gap), y, tileW, tileH);
                var look = TileLookFor(i, i == Upgrades.UltSlot);  // how the tile reacts this frame (HudTiles.cs)
                int slotIndex = i;
                TileTransformed(r.center, look.Scale, look.Dx, () => DrawAbilityTile(me, slotIndex, r, look));
            }
            DrawMasteryToasts(x0 + total / 2f, y - 64f);
        }

        /// One ability tile: key, name, READY or the timer, mastery and recharge lines, charge pips; `look` adds the
        /// tile's reactions (rim flashes, READY sliding in, a pip popping).
        void DrawAbilityTile(Player me, int i, Rect r, TileLook look)
        {
            var slot = me.Slots[i];
            bool ultimate = i == Upgrades.UltSlot;
            if (slot.Id == AbilityId.None)
            {
                Ui.HudPanel(r);
                DrawKeyCap(new Rect(r.x + 8f, r.y + 7f, 0f, 16f), SlotKeys[i], new Color(1f, 1f, 1f, 0.45f));
                Ui.Label(new Rect(r.x + 8f, r.y + 26f, r.width - 16f, 18f), "Empty", 12, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 8f, r.y + 40f, r.width - 16f, 14f), "from a very rare upgrade", 9, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
                return;
            }
            bool ready = slot.Charges > 0;
            float cd = game.Cooldown(me, i);
            float fill = slot.Charges >= slot.MaxCharges ? 1f : 1f - slot.Timer / Mathf.Max(0.01f, cd);
            Color accent = ultimate ? Ui.Gold : ToColor(me.Def.Color);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            Ui.HudPanel(r, ready ? new Color(accent.r, accent.g, accent.b, ultimate ? 0.6f + 0.4f * pulse : 1f) : (Color?)null);
            if (look.White > 0f)
                Ui.HudRim(r, new Color(1f, 1f, 1f, 0.9f * look.White));

            // Cooldown: a dark shade over the tile that drains downward as it recharges.
            if (!ready)
            {
                GUI.BeginGroup(new Rect(r.x, r.y, r.width, r.height * (1f - fill)));
                Ui.Pill(new Rect(0f, 0f, r.width, r.height), new Color(0f, 0f, 0f, 0.35f));
                GUI.EndGroup();
            }
            DrawKeyCap(new Rect(r.x + 8f, r.y + 7f, 0f, 16f), ultimate ? "ULT · " + SlotKeys[4] : SlotKeys[i], ready ? accent : new Color(1f, 1f, 1f, 0.6f));
            // Name: bright with a sheen when ready, dimmer while recharging.
            Color nameBottom = ready ? Color.Lerp(accent, Color.white, 0.25f) : new Color(0.62f, 0.66f, 0.78f, 0.9f);
            Ui.Fancy(new Rect(r.x + 8f, r.y + 24f, r.width - 16f, 22f), Upgrades.Ability(slot.Id).Name, 15, ready ? Color.white : new Color(0.9f, 0.92f, 1f, 0.9f), nameBottom,
                TextAnchor.MiddleLeft, ready ? accent : (Color?)null);
            if (ready)
            {
                Color hi = ultimate ? new Color(1f, 0.95f, 0.7f) : new Color(0.8f, 1f, 0.85f), lo = ultimate ? Ui.Gold : Ui.Good;
                float rin = look.ReadyIn;  // sliding in from the right as the tile pops
                if (rin > 0f)
                    Ui.Fancy(new Rect(r.x + 8f + 14f * (1f - rin), r.y + 5f, r.width - 16f, 20f), ultimate ? "READY!" : "READY", 11, Fade(hi, rin), Fade(lo, rin), TextAnchor.MiddleRight,
                        new Color(lo.r, lo.g, lo.b, (ultimate ? 0.6f + 0.4f * pulse : 0.8f) * rin));
            }
            else if (slot.Id == AbilityId.StunGun && me.StunShots > 0)  // the gun is out: its cooldown waits for the last shot
                Ui.Fancy(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 20f), me.StunShots + (me.StunShots == 1 ? " SHOT" : " SHOTS"), 12, Color.white, new Color(0.45f, 0.88f, 1f), TextAnchor.MiddleRight);
            else
            {
                // Pressed too early: the timer flashes red.
                float red = look.Red;
                Ui.Fancy(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 20f), ultimate ? Mathf.FloorToInt(fill * 100f) + "%" : slot.Timer.ToString(slot.Timer < 10f ? "0.0" : "0") + "s",
                    14, Color.Lerp(Color.white, new Color(1f, 0.55f, 0.55f), red), Color.Lerp(new Color(0.7f, 0.8f, 1f), new Color(1f, 0.3f, 0.3f), red), TextAnchor.MiddleRight,
                    red > 0f ? new Color(1f, 0.3f, 0.3f, 0.7f * red) : (Color?)null);
            }
            // Along the bottom: the mastery XP line (green, full once mastered) sitting right on top of the
            // recharge line, the pair one strip.
            float mxp;
            me.MasteryXp.TryGetValue(slot.Id, out mxp);
            var strip = new Rect(r.x + 8f, r.yMax - 13f, r.width - 16f, 7f);
            const float xpH = 3f;
            Ui.Pill(strip, new Color(0f, 0f, 0f, 0.45f));
            GUI.BeginGroup(new Rect(strip.x, strip.y, strip.width * Game.MasteryProgress(mxp), xpH));
            Ui.Pill(new Rect(0f, 0f, strip.width, strip.height), new Color(0.35f, 0.9f, 0.45f));
            GUI.EndGroup();
            GUI.BeginGroup(new Rect(strip.x, strip.y + xpH, strip.width * Mathf.Clamp01(fill), strip.height - xpH));
            Ui.Pill(new Rect(0f, -xpH, strip.width, strip.height), ready ? accent : new Color(0.5f, 0.6f, 0.8f));
            GUI.EndGroup();
            if (slot.MaxCharges > 1)
                for (int k = 0; k < slot.MaxCharges; k++)
                {
                    var pip = new Rect(r.xMax - 12f - k * 9f, r.y + 30f, 6f, 6f);
                    if (k == look.Pip)
                    {
                        // The charge that just came back: a fading halo spreading round it, and the pip popping.
                        float h = 3f + 6f * look.PipHalo;
                        Ui.Pill(new Rect(pip.x - h, pip.y - h, pip.width + 2f * h, pip.height + 2f * h), new Color(1f, 1f, 1f, 0.35f * (1f - look.PipHalo)));
                        float e = 3f * look.PipPop;
                        pip = new Rect(pip.x - e, pip.y - e, pip.width + 2f * e, pip.height + 2f * e);
                    }
                    Ui.Pill(pip, k < slot.Charges ? accent : new Color(1f, 1f, 1f, 0.15f));
                }
            if (look.Red > 0f)
                Ui.HudRim(r, new Color(1f, 0.35f, 0.35f, 0.85f * look.Red));
        }

        static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        /// Five small bars for an ability's mastery level; the next one fills as it earns XP.
        void DrawMasteryPips(Rect r, int level, float progress)
        {
            bool max = level >= Game.MasteryMax;
            Color on = max ? Ui.Gold : new Color(0.6f, 0.85f, 1f);
            for (int k = 0; k < Game.MasteryMax; k++)
            {
                var pip = new Rect(r.x + k * 13f, r.y, 10f, r.height);
                Ui.Fill(pip, new Color(1f, 1f, 1f, 0.22f));
                if (k < level)
                    Ui.Fill(pip, on);
                else if (k == level && progress > 0f)
                    Ui.Fill(new Rect(pip.x, pip.y, pip.width * progress, pip.height), new Color(on.r, on.g, on.b, 0.55f));
            }
        }

        /// "Barrier · Mastery 3" when one of your abilities levels up.
        void DrawMasteryToasts(float cx, float y)
        {
            var toasts = game.MasteryToasts;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                toasts[i].T -= Time.unscaledDeltaTime;
                if (toasts[i].T <= 0f)
                    toasts.RemoveAt(i);
            }
            for (int i = 0; i < toasts.Count && i < 3; i++)
            {
                var t = toasts[toasts.Count - 1 - i];
                bool max = t.Level >= Game.MasteryMax;
                float a = Mathf.Clamp01(t.T / 0.5f);
                string text = Upgrades.Ability(t.Ability).Name + (max ? "  MASTERED: " + Game.Capstone(t.Ability) : "  ·  Mastery " + t.Level);
                var c = max ? Ui.Gold : new Color(0.6f, 0.85f, 1f);
                Ui.Shadowed(new Rect(cx - 400f, y - i * 22f, 800f, 20f), text, max ? 15 : 14, new Color(c.r, c.g, c.b, a), TextAnchor.MiddleCenter);
            }
        }

        /// A small key label like [SPACE]; r.width is ignored (it's sized to the text).
        void DrawKeyCap(Rect r, string key, Color color)
        {
            float w = 12f + key.Length * 6.5f;
            var cap = new Rect(r.x, r.y, w, r.height);
            Ui.Pill(cap, new Color(color.r, color.g, color.b, 0.2f * color.a + 0.05f));
            Ui.Fancy(cap, key, 9, new Color(1f, 1f, 1f, 0.55f + 0.45f * color.a), new Color((color.r + 1f) / 2f, (color.g + 1f) / 2f, (color.b + 1f) / 2f, 0.5f + 0.5f * color.a), TextAnchor.MiddleCenter);
        }
    }
}
