using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The boss bars at the top of the HUD, with each boss's stage names and notes.
    public partial class ArenaApp
    {
        /// Name and health bar for each boss on the field, plus warnings for their mechanics: wide across the
        /// top middle, which the rest of the HUD leaves free.
        static string HeraldStageName(HeraldStage s) => s == HeraldStage.Gathering ? "GATHERING STORM" : s == HeraldStage.Gale ? "GALE"
            : s == HeraldStage.Supercell ? "SUPERCELL" : "LAST STAND";

        public static string EclipseStageName(EclipseStage s) => s == EclipseStage.Corona ? "CORONA" : s == EclipseStage.Penumbra ? "PENUMBRA"
            : s == EclipseStage.Umbra ? "UMBRA" : s == EclipseStage.Echoes ? "ECHOES" : s == EclipseStage.LastLight ? "LAST LIGHT" : "TOTALITY";

        /// The Eclipse's stage under its health bar, and what to watch for.
        string EclipseNote(EclipseState s, ref Color c)
        {
            if (s.TransT > 0f)
            {
                c = new Color(1f, 0.95f, 0.8f);
                return "CAN'T BE HURT — " + EclipseStageName(s.TransTo) + " is coming";
            }
            if (s.FlashT > 0f)
            {
                c = new Color(1f, 0.92f, 0.7f);
                return "HIDE IN THE MOON'S SHADOW — " + s.FlashT.ToString("0.0") + " s";
            }
            if (s.Stage == EclipseStage.Totality)
            {
                c = new Color(1f, 0.45f, 0.4f);
                return "TOTALITY — kill it before the nova: " + s.NovaT.ToString("0.0") + " s (it takes +50%)";
            }
            string stage = "STAGE " + ((int)s.Stage + 1) + " / 5 · " + EclipseStageName(s.Stage);
            switch (s.Turn)
            {
                case EclipseTurn.Flares: return stage + " — SOLAR FLARES: step out of the lines";
                case EclipseTurn.LunarWake: return stage + " — the moon soaks your shots";
                case EclipseTurn.Crescents: return stage + " — CRESCENTS: they come back";
                case EclipseTurn.ClosingUmbra: return stage + " — CLOSING UMBRA: find a gap";
                case EclipseTurn.DarkSpiral: return stage + " — stay in the light";
                case EclipseTurn.EchoColossus: return stage + " — ECHO OF THE COLOSSUS";
                case EclipseTurn.EchoLich: return stage + " — ECHO OF THE LICH: walk round with the beams";
                case EclipseTurn.EchoHerald: return stage + " — ECHO OF THE STORM HERALD";
                case EclipseTurn.EchoSingularity: return stage + " — ECHO OF THE SINGULARITY";
                case EclipseTurn.SolarStorm: return stage + " — SOLAR STORM";
            }
            if (s.Stage == EclipseStage.Umbra)
                return stage + " — the dark drains you: stay in the light";
            return stage;
        }

        /// The Storm Herald's stage under its health bar, and what to watch for.
        string HeraldNote(HeraldState s, ref Color c)
        {
            if (s.TransT > 0f)
            {
                c = new Color(0.9f, 0.92f, 1f);
                return "CAN'T BE HURT — " + HeraldStageName(s.TransTo) + " is coming";
            }
            if (s.CondId == game.MyId && s.CondId != 0)
            {
                c = new Color(0.83f, 0.6f, 1f);
                return "YOU'RE THE CONDUCTOR — get away from your allies!";
            }
            if (s.CondId != 0)
            {
                c = new Color(0.83f, 0.6f, 1f);
                return "CONDUCTOR — keep away from " + (game.Players.ContainsKey(s.CondId) ? game.Players[s.CondId].Name : "them") + "!";
            }
            switch (s.Turn)
            {
                case HeraldTurn.Rods: return "LIGHTNING RODS — stay off the lines between them";
                case HeraldTurn.RodCage:
                    c = new Color(0.83f, 0.6f, 1f);
                    return "ROD CAGE — get out through the open side!";
                case HeraldTurn.Crosswind: return "GALE — step into the wind to dodge";
                case HeraldTurn.BallLightning: return "BALL LIGHTNING — shoot them, keep out from between them";
                case HeraldTurn.Downburst: return "DOWNBURST — get out of the row";
                case HeraldTurn.Thunderclap: return "THUNDERCLAP — slip through the gaps";
                case HeraldTurn.StormFront: return "STORM FRONT — find the gap";
                case HeraldTurn.Eye: return "EYE OF THE STORM — walk round with the spokes";
            }
            return HeraldStageName(s.Stage);
        }

        static string DuelStageName(DuelStage s) => s == DuelStage.EnGarde ? "EN GARDE" : s == DuelStage.Masquerade ? "MASQUERADE" : s == DuelStage.ToTheDeath ? "TO THE DEATH" : "LAST STAND";

        /// The Duelist's line under its boss bar: what it's doing and what to do about it (Duelist.cs).
        static string DuelNote(DuelState s, ref Color col)
        {
            if (s.TransT > 0f)
            {
                col = new Color(0.9f, 0.92f, 1f);
                return "CAN'T BE HURT — " + DuelStageName(s.TransTo) + " is coming";
            }
            if (s.StaggerT > 0f)
            {
                col = new Color(1f, 0.8f, 0.35f);
                return "OPEN — it takes 50% more damage!";
            }
            if (s.SaluteT > 0f || s.Dash == DuelDash.Salute)
            {
                col = new Color(1f, 0.8f, 0.35f);
                return "SALUTE — perfect-parry the lunge to open it up";
            }
            if (s.Turn == DuelTurn.Guard)
            {
                col = new Color(0.81f, 0.84f, 1f);
                return "GUARD — its blade turns your shots back; get behind it";
            }
            if (s.Turn == DuelTurn.Masquerade)
            {
                col = new Color(0.71f, 0.61f, 1f);
                return "MASQUERADE — only the real one's shots are solid";
            }
            return DuelStageName(s.Stage);
        }

        static string LichStageName(LichStage s) => s == LichStage.Crypt ? "THE CRYPT" : s == LichStage.Harvest ? "SOUL HARVEST" : s == LichStage.Undeath ? "UNDEATH" : "THE FINAL RITE";
        static string SingStageName(SingStage s) => s == SingStage.EventHorizon ? "EVENT HORIZON" : s == SingStage.Accretion ? "ACCRETION"
            : s == SingStage.Spaghettify ? "SPAGHETTIFY" : "LAST STAND";

        static string ColStageName(ColStage s) => s == ColStage.Awakening ? "AWAKENING" : s == ColStage.Crumbling ? "CRUMBLING"
            : s == ColStage.MoltenCore ? "MOLTEN CORE" : "LAST STAND";

        /// The Colossus's stage under its health bar, and what to watch for.
        static string ColossusNote(ColossusState c, ref Color col)
        {
            if (c.TransT > 0f)
            {
                col = new Color(0.95f, 0.9f, 0.85f);
                return "CAN'T BE HURT — " + ColStageName(c.TransTo) + " is coming";
            }
            if (c.DazeT > 0f)
            {
                col = new Color(1f, 0.85f, 0.35f);
                return "DAZED — it takes 50% more damage!";
            }
            if (c.ChargeWarn > 0f)
            {
                col = new Color(1f, 0.45f, 0.4f);
                return "CHARGE — get off the red line (or put a plate in its way)";
            }
            if (c.Rings.Count > 0)
            {
                col = new Color(0.6f, 1f, 0.7f);
                return "STOMP — the rings shove you: slip through a green gap";
            }
            return ColStageName(c.Stage);
        }

        static string DevourStageName(DevourStage s) => s == DevourStage.Appetite ? "APPETITE" : s == DevourStage.Gorging ? "GORGING"
            : s == DevourStage.Ravenous ? "RAVENOUS" : "LAST MEAL";

        /// The Devourer's stage under its health bar, and what to watch for.
        string DevourNote(DevourState s, ref Color c)
        {
            var hungry = new Color(1f, 0.45f, 0.7f);
            if (s.TransT > 0f)
            {
                c = new Color(1f, 0.85f, 0.88f);
                return "CAN'T BE HURT — " + DevourStageName(s.TransTo) + " is coming";
            }
            if (s.SwallowId != 0 && s.SwallowId == game.MyId)
            {
                c = new Color(0.8f, 0.9f, 0.4f);
                return "SWALLOWED — hold on, your allies' hits get you out";
            }
            if (s.SwallowId != 0)
            {
                c = new Color(0.8f, 0.9f, 0.4f);
                return (game.Players.ContainsKey(s.SwallowId) ? game.Players[s.SwallowId].Name.ToUpper() : "AN ALLY") + " IS SWALLOWED — shoot it, double damage!";
            }
            if (s.BurrowT > 0f)
                return "BURROWING — it's coming up under you, keep moving";
            switch (s.Turn)
            {
                case DevourTurn.Hungry:
                    c = hungry;
                    return "HUNGRY — don't shoot it, shoot the morsels!";
                case DevourTurn.Snap:
                case DevourTurn.Frenzy: return "LUNGES — step off the red line";
                case DevourTurn.Inhale:
                    c = new Color(0.8f, 0.9f, 0.4f);
                    return "INHALE — get out of the cone!";
                case DevourTurn.Bile: return "BILE — keep out of the pools";
                case DevourTurn.Jaws: return "JAWS — dodge out to the side";
                case DevourTurn.Vomit: return "VOMIT — get behind it";
                case DevourTurn.LastMeal: return "LAST MEAL — keep walking out, mind the teeth";
            }
            if (s.Stage == DevourStage.Ravenous)
            {
                c = hungry;
                return "THE MAW — shots into its mouth heal it, hit its back";
            }
            return DevourStageName(s.Stage);
        }

        /// The Warden's stage under its health bar, and what to watch for.
        string WardenNote(WardenState w, ref Color c)
        {
            if (w.TransT > 0f && w.TransTo == WardenStage.LastStand)
            {
                c = new Color(0.7f, 0.82f, 1f);
                return "CAN'T BE HURT — he's putting on his armor";
            }
            if (w.TransT > 0f)
            {
                c = new Color(0.9f, 0.92f, 1f);
                return "CAN'T BE HURT — " + WardenStageName(w.TransTo) + " is coming";
            }
            if (w.Stage == WardenStage.LastStand && w.Armor > 0f)
            {
                c = new Color(0.7f, 0.82f, 1f);
                return "LAST STAND — ARMORED: break it to reach his health";
            }
            var me = game.Me;
            if (me != null && me.StunT > 0f)
            {
                c = new Color(0.45f, 0.88f, 1f);
                return WardenStageName(w.Stage) + " — STUNNED!";
            }
            foreach (var g in game.Enemies.Values)
                if (g.Type == EnemyType.Guard)
                {
                    c = new Color(0.45f, 0.65f, 1f);
                    return WardenStageName(w.Stage) + " — THE GUARDS ARE COMING FOR YOU";
                }
            if (w.Turn == WardenTurn.Rioters && w.RiotShieldMax > 0f)  // its riot shield: break it (Warden.cs)
            {
                if (w.RiotShield <= 0f)
                {
                    c = new Color(1f, 0.6f, 0.35f);
                    return "RIOT — SHIELD BROKEN: he takes 50% more";
                }
                c = new Color(0.8f, 0.88f, 1f);
                return "RIOT — break his shield (" + Mathf.CeilToInt(100f * w.RiotShield / w.RiotShieldMax) + "%)";
            }
            if (w.Turn == WardenTurn.Light && w.LightMode == WardenLight.Lock)
            {
                c = new Color(1f, 0.4f, 0.35f);
                return WardenStageName(w.Stage) + " — SPOTTED!";
            }
            c = w.Stage == WardenStage.Count ? new Color(1f, 0.92f, 0.65f) : w.Stage == WardenStage.Lockdown ? new Color(0.7f, 0.8f, 1f) : new Color(1f, 0.55f, 0.3f);
            return WardenStageName(w.Stage);
        }

        /// The Hive Queen's stage under her health bar, and what to watch for.
        string QueenNote(Enemy e, ref Color c)
        {
            var q = e.Queen;
            if (q.TransT > 0f)
            {
                c = new Color(0.9f, 0.92f, 1f);
                return "CAN'T BE HURT — " + QueenStageName(q.TransTo) + " is coming";
            }
            c = q.Stage == QueenStage.Swarm ? new Color(1f, 0.82f, 0.3f) : q.Stage == QueenStage.Honeycomb ? new Color(1f, 0.65f, 0.15f) : new Color(1f, 0.45f, 0.3f);
            if (e.Phase == 1)
                return "SWARM — SHIELDED, kill her drones!";
            if (q.Turn == QueenTurn.Eggs)
                return "HONEYCOMB — break the eggs before they hatch!";
            if (Game.QueenAir(q) > 0.5f)
                return "FRENZY — DIVING, get out from under her!";
            return QueenStageName(q.Stage);
        }

        static string QueenStageName(QueenStage s) =>
            s == QueenStage.Swarm ? "SWARM" : s == QueenStage.Honeycomb ? "HONEYCOMB" : s == QueenStage.Frenzy ? "FRENZY" : "LAST STAND";

        /// The Reapers' stage under their health bars, and what to do about the Dual Reaper.
        string ReaperNote(Enemy e, ReaperFight R, ref Color c)
        {
            if (R.Trans != 0)
            {
                c = new Color(0.9f, 0.92f, 1f);
                return R.Trans == 1 ? "CAN'T BE HURT — THEY'RE FUSING" : "CAN'T BE HURT — IT'S SPLITTING";
            }
            if (e.Type == EnemyType.TwinB)
                return null;  // Dusk's bar speaks for both
            if (R.Stage == ReaperStage.Twilight)
            {
                c = new Color(0.77f, 0.55f, 1f);
                return "TWILIGHT";
            }
            if (R.Stage == ReaperStage.Sundered)
            {
                c = new Color(1f, 0.4f, 0.4f);
                return "SUNDERED";
            }
            c = new Color(1f, 0.8f, 0.35f);
            if (R.Trapped)
                return R.ShieldFrac > 0f ? "TRAPPED — BREAK ITS SHIELD!" : "OPEN — HIT IT NOW!";
            if (R.Turn == ReaperTurn.Chase)
                return "ARMORED — LURE IT INTO A HOLE";
            if (R.Turn == ReaperTurn.Lasers)
                return "ARMORED — KILL THE PILLARS";
            return "THE DUAL REAPER";
        }

        static string WardenStageName(WardenStage s) =>
            s == WardenStage.Count ? "COUNT" : s == WardenStage.Lockdown ? "LOCKDOWN" : s == WardenStage.Riot ? "RIOT" : "LAST STAND";

        void DrawBossBars()
        {
            float panelW = 640f, y = 12f, x0 = W / 2f - panelW / 2f;
            foreach (var e in game.Enemies.Values)
            {
                if (!e.Def.IsBoss && !e.Def.Mini)  // (a challenge room's mini-boss gets one too)
                    continue;
                var col = ToColor(e.Def.Color);
                string note = null;
                Color noteColor = Ui.Warn;
                if (e.Type == EnemyType.HiveQueen && e.Queen != null)
                    note = QueenNote(e, ref noteColor);
                else if (e.Type == EnemyType.HiveQueen && e.Phase == 1)
                    note = "SHIELDED — kill her swarm!";
                else if (Game.IsTwin(e.Type) && e.Phase == 1)
                {
                    note = "ENRAGED";
                    noteColor = new Color(1f, 0.4f, 0.4f);
                }
                else if (Game.SoulBound(e))
                {
                    note = "SOUL BOUND — hurt the other Reaper!";
                    noteColor = new Color(0.85f, 0.88f, 1f);
                }
                else if (e.Type == EnemyType.Singularity && e.Sing != null && e.Sing.TransT > 0f)
                {
                    note = "CAN'T BE HURT — " + SingStageName(e.Sing.TransTo) + " is coming";
                    noteColor = new Color(0.9f, 0.92f, 1f);
                }
                else if (e.Type == EnemyType.Singularity && ImCaged())
                {
                    note = "CAGED — shoot the wells around you!";
                    noteColor = new Color(0.9f, 0.55f, 1f);
                }
                else if (e.Type == EnemyType.Singularity && e.Phase == Game.SingSplit)
                {
                    note = "SPLIT — shoot the shards, the core can't be hurt!";
                    noteColor = new Color(0.9f, 0.55f, 1f);
                }
                else if (e.Type == EnemyType.Singularity && e.Phase == Game.SingWhiteHole)
                {
                    note = "WHITE HOLE — it pushes, and every bullet turns round!";
                    noteColor = new Color(0.95f, 0.95f, 1f);
                }
                else if (e.Type == EnemyType.Singularity && AnyWells())
                    note = "The corner wells swallow your shots — kill them!";
                else if (e.Type == EnemyType.Singularity && e.Sing != null)
                    note = SingStageName(e.Sing.Stage);
                else if (e.Type == EnemyType.StormHerald && e.Herald != null)
                    note = HeraldNote(e.Herald, ref noteColor);
                else if (e.Type == EnemyType.Duelist && e.Duel != null)
                    note = DuelNote(e.Duel, ref noteColor);
                else if (e.Type == EnemyType.Colossus && e.Colossus != null)
                    note = ColossusNote(e.Colossus, ref noteColor);
                else if (e.Type == EnemyType.Lich && e.Lich != null && e.Lich.TransT > 0f)
                {
                    note = "CAN'T BE HURT — " + LichStageName(e.Lich.TransTo) + " is coming";
                    noteColor = new Color(0.9f, 0.92f, 1f);
                }
                else if (e.Type == EnemyType.Lich && e.Phase == Game.LichGuarded)
                    note = e.Lich != null && e.Lich.Stage == LichStage.Harvest ? "GUARDED — break all three phylacteries!" : "GUARDED — break the phylactery!";
                else if (e.Type == EnemyType.Lich && e.Lich != null && e.Lich.Window > 0f)
                {
                    note = "OPEN — " + Mathf.CeilToInt(e.Lich.Window) + " s before its phylacteries rise again";
                    noteColor = new Color(1f, 0.8f, 0.35f);
                }
                else if (e.Type == EnemyType.Lich && e.Lich != null)
                    note = LichStageName(e.Lich.Stage);
                else if (e.Type == EnemyType.Devourer && e.Devour != null)
                    note = DevourNote(e.Devour, ref noteColor);
                else if (Game.Hungry(e))
                {
                    note = "HUNGRY — stop shooting, your hits heal it!";
                    noteColor = new Color(1f, 0.45f, 0.7f);
                }
                else if (e.Type == EnemyType.Eclipse && e.Eclipse != null)
                    note = EclipseNote(e.Eclipse, ref noteColor);
                else if (e.Type == EnemyType.Boss && e.Warden != null)
                    note = WardenNote(e.Warden, ref noteColor);
                else if (Game.IsReaper(e.Type) && game.Reapers != null)
                    note = ReaperNote(e, game.Reapers, ref noteColor);
                else if (e.Type == EnemyType.Gatekeeper)
                    note = GatekeeperNote(e, ref noteColor);  // ChallengeLook.cs
                // The Warden's last-stand armor: a blue bar above its health, as long as it, that wears down first.
                bool armored = e.Type == EnemyType.Boss && e.Warden != null && e.Warden.Armor > 0f && e.Warden.ArmorMax > 0f;
                float armorH = armored ? 9f : 0f;
                float h = (note != null ? 62f : 46f) + armorH;
                var panel = new Rect(x0, y, panelW, h);
                Ui.HudPanel(panel, col);
                float frac = Mathf.Clamp01(e.Hp / e.MaxHp);
                // Name with small diamonds either side, and the health percentage.
                Ui.Pill(new Rect(panel.x + 14f, panel.y + 12f, 6f, 6f), col);
                Ui.Label(new Rect(panel.x + 26f, panel.y + 5f, panelW - 120f, 20f), e.Def.Name.ToUpperInvariant(), 14, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(panel.xMax - 90f, panel.y + 5f, 76f, 20f), Mathf.CeilToInt(frac * 100f) + "%", 13, Ui.Muted, TextAnchor.MiddleRight, true);
                if (armored)
                    GhostBar(new Rect(panel.x + 14f, panel.y + 26f, panelW - 28f, 6f), Mathf.Clamp01(e.Warden.Armor / e.Warden.ArmorMax),
                        new Color(0.45f, 0.65f, 1f), EnemyKey(e.Id) | 2L << 40);
                var bar = new Rect(panel.x + 14f, panel.y + 27f + armorH, panelW - 28f, 12f);
                GhostBar(bar, frac, col, EnemyKey(e.Id) | 1L << 40);  // with its damage ghost (HealthBars.cs)
                for (int k = 1; k < 10; k++)  // tick marks every 10%
                    Ui.Fill(new Rect(bar.x + bar.width * k / 10f, bar.y + 2f, 1f, bar.height - 4f), new Color(0f, 0f, 0f, 0.35f));
                if (note != null)
                    Ui.Label(new Rect(panel.x, panel.y + 41f + armorH, panelW, 18f), note, 12, noteColor, TextAnchor.MiddleCenter, true);
                y += h + 6f;
            }
            string warning = null;
            if (game.TwinReviveT > 0f)
                warning = "Kill the other Reaper in " + game.TwinReviveT.ToString("0.0") + "s or its twin revives!";
            else if (game.Reapers != null && game.Reapers.Suck > 0f)
                warning = "THE PILLARS STOOD — it's pulling you in!";
            else if (game.PullStrength > 0f && FindDevourer(game) != null)
                warning = "ITS LAST MEAL — it's pulling you in!";  // Devourer.cs
            else if (game.PullStrength > 0f)
                warning = "GRAVITY WELL — run!";
            if (warning != null)
            {
                var r = new Rect(x0 + panelW / 2f - 230f, y, 460f, 28f);
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f);
                Ui.Pill(r, new Color(0.45f, 0.06f, 0.1f, 0.75f));
                Ui.Label(r, warning, 14, new Color(1f, 0.75f, 0.75f, pulse), TextAnchor.MiddleCenter, true);
            }
        }
    }
}
