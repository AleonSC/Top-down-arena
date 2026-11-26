using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Arena
{
    /// How hard a run is: a preset (Easy / Normal / Hard / Nightmare) or Custom, where the host sets every knob
    /// with a slider or by typing a number (the lobby's Difficulty tab, DifficultyUi.cs). The host picks; it's sent
    /// in the lobby message, kept in run saves and written in session recordings.
    public enum DifficultyLevel : byte { Easy, Normal, Hard, Nightmare, Custom }

    public enum DiffKnob
    {
        EnemyHp,        // x: every enemy's and boss's health
        BossHp,         // x: bosses' health, on top of Enemy health
        SoloHp,         // share: enemies' health when you play alone
        EnemyDamage,    // x: everything that hurts players (bullets, contact, hazards)
        DamagePerWave,  // share per wave: enemy damage grows this much each wave (wave 1 = no growth)
        EnemyCount,     // x: enemies in a regular wave
        FireRate,       // x: how fast enemies and bosses shoot
        EnemySpeed,     // x: how fast enemies move
        EliteChance,    // x: how often regular enemies come as elites
        BulletBudget,   // x: the cap on regular enemies' bullets on screen
        BossBullets,    // share: the part of the bullet budget other enemies get while a boss is up
        Regen,          // HP/s: everyone's passive regeneration
        OrbChance,      // share: small health orb drop chance per kill
        ReviveTime,     // s: holding still by a downed ally to get them up
        HitGrace,       // s: how long you can't be hit again after a hit
        Shooters,       // x: how many regular enemies may shoot at once (Readability.cs)
        ApproachShare,  // share: the themed share of the last wave before each boss (Approaches.cs); 0 = off
        ApproachRamp,   // on/off: the four approach waves rise to that share (on) or all use it (off)
        ArenaLayouts,   // on/off: each boss cycle gets a fixed floor plan (Layouts.cs); off = always the open arena
        ArenaRooms,     // retired (Nov 18): was the Rooms arena (the Wings map); ignored, kept so the knob order stays the same
        ArenaExpanding, // on/off: the Arena setting, Expanding (on: starts as the Classic box and grows by rooms the team buys, ExpansionGame.cs); wins over Rooms
        Campaign,       // on/off: every wave is themed enemies, the last boss's fading out and the next one's building up (Approaches.cs, Docs/Campaign.md); off = the Boss approach setting
        BuildVote,      // retired (Nov 18): was Team vote building; ignored, kept so the knob order stays the same
        BuildPurse,     // retired (Nov 18): was the Team purse; ignored, kept so the knob order stays the same
        Lives,          // count: retries from the last boss's checkpoint when everyone goes down (Checkpoints.cs)
        RoomCards,   // retired (Nov 18): was Room cards; ignored, kept so the knob order stays the same
        GridRooms,      // retired (Nov 18): was Grid rooms (now always on); ignored, kept so the knob order stays the same
        Mechanics,      // tier: which mechanics the run uses (MechTier: Easy..Nightmare; Docs/DifficultyTiers.md)
    }

    /// The mechanics tier: which fights, enemies and rules a run uses (Docs/DifficultyTiers.md). A preset sets it to its own
    /// name; Custom lets the host pick it (DiffKnob.Mechanics). Normal plays as the game is balanced; Hard and Nightmare can add
    /// mechanics and Easy can take some away. Read it with `Game.MechTier`, `MechAtLeast`, `MechOk`.
    public enum MechTier : byte { Easy, Normal, Hard, Nightmare }

    public enum KnobUnit { Times, Percent, PercentPerWave, Seconds, HpPerSecond, OnOff, Count, Tier }

    public class KnobDef
    {
        public DiffKnob Knob;
        public string Name, Desc;
        public KnobUnit Unit;
        public float Min, Max, Step;
        public bool HarderUp = true;  // a higher value makes the run harder (shown in red; lower in green)
        public bool Flavour;          // changes how a run feels, not how hard it is (never shown in red or green)
        public float[] Presets;       // Easy, Normal, Hard, Nightmare

        public float Normal => Presets[(int)DifficultyLevel.Normal];

        /// The value as people read it: "1.25x", "12%", "+3.0% / wave", "4 s", "0.25 HP/s".
        public string Format(float v)
        {
            switch (Unit)
            {
                case KnobUnit.Times: return v.ToString("0.##", CultureInfo.InvariantCulture) + "x";
                case KnobUnit.Percent: return Math.Round(v * 100f).ToString(CultureInfo.InvariantCulture) + "%";
                case KnobUnit.PercentPerWave: return "+" + (v * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "% / wave";
                case KnobUnit.Seconds: return v.ToString("0.##", CultureInfo.InvariantCulture) + " s";
                case KnobUnit.OnOff: return v >= 0.5f ? "On" : "Off";
                case KnobUnit.Count: return Math.Round(v).ToString(CultureInfo.InvariantCulture);
                case KnobUnit.Tier: return Difficulty.TierName(v);
                default: return v.ToString("0.##", CultureInfo.InvariantCulture) + " HP/s";
            }
        }

        /// What goes in its text box: the number alone, in the unit shown (percent knobs as 12, not 0.12).
        public string Edit(float v)
        {
            if (Unit == KnobUnit.OnOff)
                return v >= 0.5f ? "on" : "off";
            if (Unit == KnobUnit.Count)
                return Math.Round(v).ToString(CultureInfo.InvariantCulture);
            if (Unit == KnobUnit.Tier)
                return Difficulty.TierName(v);
            bool pct = Unit == KnobUnit.Percent || Unit == KnobUnit.PercentPerWave;
            return (pct ? v * 100f : v).ToString(Unit == KnobUnit.PercentPerWave ? "0.0" : "0.##", CultureInfo.InvariantCulture);
        }

        /// Reads a typed number (with or without its unit: "1.5", "1.5x", "12%"); false if it isn't one.
        public bool TryParse(string text, out float v)
        {
            v = 0f;
            if (Unit == KnobUnit.OnOff)
            {
                string o = (text ?? "").Trim().ToLowerInvariant();
                if (o == "on" || o == "1" || o == "yes") { v = 1f; return true; }
                if (o == "off" || o == "0" || o == "no") { v = 0f; return true; }
                return false;
            }
            if (Unit == KnobUnit.Tier)
            {
                string o = (text ?? "").Trim();
                for (int i = 0; i < TierCount; i++)
                    if (string.Equals(o, Difficulty.Names[i], StringComparison.OrdinalIgnoreCase))
                    {
                        v = i;
                        return true;
                    }
            }
            string t = (text ?? "").Trim().TrimEnd('x', 'X', '%', 's', ' ').Replace(',', '.');
            if (t.StartsWith("+", StringComparison.Ordinal))
                t = t.Substring(1);
            if (!float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v) || float.IsNaN(v) || float.IsInfinity(v))
                return false;
            if (Unit == KnobUnit.Percent || Unit == KnobUnit.PercentPerWave)
                v /= 100f;
            return true;
        }

        public float Clamp(float v)
        {
            v = Math.Max(Min, Math.Min(Max, v));
            return Unit == KnobUnit.Tier ? (float)Math.Round(v) : v;  // a tier is a whole step
        }

        const int TierCount = 4;  // MechTier's values

        /// Where v sits between Min and Max (the slider), and back (snapped to Step).
        public float ToSlider(float v) => (Clamp(v) - Min) / (Max - Min);
        public float FromSlider(float s) => Clamp(Min + (float)Math.Round(Math.Max(0f, Math.Min(1f, s)) * (Max - Min) / Step) * Step);
    }

    public class Difficulty
    {
        public DifficultyLevel Level = DifficultyLevel.Normal;
        public readonly float[] Values = new float[Knobs.Length];

        public float this[DiffKnob k] => Values[(int)k];

        public Difficulty()
        {
            for (int i = 0; i < Knobs.Length; i++)
                Values[i] = Knobs[i].Normal;  // the flavour knobs start here; presets leave them be
            SetLevel(DifficultyLevel.Normal);
        }

        public static Difficulty Of(DifficultyLevel level)
        {
            var d = new Difficulty();
            d.SetLevel(level);
            return d;
        }

        /// A preset copies its numbers in; Custom keeps the numbers you have (so it starts from the last preset).
        /// Flavour knobs (the boss approach) aren't part of a preset: they keep what they're set to.
        public void SetLevel(DifficultyLevel level)
        {
            Level = level;
            if (level == DifficultyLevel.Custom)
                return;
            for (int i = 0; i < Knobs.Length; i++)
                if (!Knobs[i].Flavour)
                    Values[i] = Knobs[i].Presets[(int)level];
        }

        /// Changing a number makes it a Custom difficulty (a flavour knob doesn't: it isn't about how hard it is).
        public void Set(DiffKnob k, float v)
        {
            Values[(int)k] = Knobs[(int)k].Clamp(v);
            if (!Knobs[(int)k].Flavour)
                Level = DifficultyLevel.Custom;
        }

        public Difficulty Copy()
        {
            var d = new Difficulty { Level = Level };
            Array.Copy(Values, d.Values, Values.Length);
            return d;
        }

        public bool SameAs(Difficulty o)
        {
            if (o == null || o.Level != Level)
                return false;
            for (int i = 0; i < Values.Length; i++)
                if (Math.Abs(Values[i] - o.Values[i]) > 1e-5f)
                    return false;
            return true;
        }

        public string Name => Names[(int)Level];

        /// The mechanics tier (DiffKnob.Mechanics).
        public MechTier Tier => (MechTier)Math.Max(0, Math.Min(3, (int)Math.Round(this[DiffKnob.Mechanics])));

        /// A tier's name ("Hard"), for the Mechanics knob.
        public static string TierName(float v) => Names[Math.Max(0, Math.Min(3, (int)Math.Round(v)))];

        /// The knobs that differ from Normal, for summaries ("Enemy damage 1.5x, Revive time 6 s").
        public List<string> Changes()
        {
            var list = new List<string>();
            foreach (var k in Knobs)
                if (Math.Abs(this[k.Knob] - k.Normal) > 1e-5f)
                    list.Add(k.Name + " " + k.Format(this[k.Knob]));
            return list;
        }

        // --- Network and saves ---------------------------------------------------------------------------------------

        public void Write(BinaryWriter w)
        {
            w.Write((byte)Level);
            w.Write((byte)Values.Length);
            foreach (var v in Values)
                w.Write(v);
        }

        public static Difficulty Read(BinaryReader r)
        {
            var d = new Difficulty { Level = (DifficultyLevel)r.ReadByte() };
            int n = r.ReadByte();
            if (d.Level != DifficultyLevel.Custom && (int)d.Level < Names.Length)
                for (int i = n; i < d.Values.Length; i++)
                    if (!Knobs[i].Flavour)
                        d.Values[i] = Knobs[i].Presets[(int)d.Level];  // older data without a knob (Mechanics): the preset's own (Custom: Normal)
            for (int i = 0; i < n; i++)
            {
                float v = r.ReadSingle();
                if (i < d.Values.Length)
                    d.Values[i] = v;
            }
            return d;
        }

        /// "Hard" for a preset (plus any changed flavour knob: "Hard ApproachShare=1"); "Custom EnemyHp=1.5 Regen=0 ..." (every knob by name) for a custom one.
        public string ToText()
        {
            var sb = new StringBuilder(Level.ToString());
            foreach (var k in Knobs)
                if (Level == DifficultyLevel.Custom || k.Flavour && Math.Abs(this[k.Knob] - k.Normal) > 1e-5f)  // a preset adds its flavour knobs only when they're changed
                    sb.Append(' ').Append(k.Knob).Append('=').Append(this[k.Knob].ToString("R", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// Reads ToText; unknown knobs are skipped and missing ones stay Normal.
        public static Difficulty Parse(string text) => ParseKnobs(text) ?? new Difficulty();

        static Difficulty ParseKnobs(string text)
        {
            var parts = (text ?? "").Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            DifficultyLevel level;
            if (parts.Length == 0 || !Enum.TryParse(parts[0], out level) || !Enum.IsDefined(typeof(DifficultyLevel), level))
                return null;
            var d = Of(level == DifficultyLevel.Custom ? DifficultyLevel.Normal : level);
            d.Level = level;
            for (int i = 1; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                DiffKnob k;
                float v;
                if (eq > 0 && Enum.TryParse(parts[i].Substring(0, eq), out k) && Enum.IsDefined(typeof(DiffKnob), k)
                    && float.TryParse(parts[i].Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    d.Values[(int)k] = Knobs[(int)k].Clamp(v);
            }
            return d;
        }

        // --- The presets ---------------------------------------------------------------------------------------------

        public static readonly string[] Names = { "Easy", "Normal", "Hard", "Nightmare", "Custom" };

        public static readonly string[] Blurbs =
        {
            "Softer hits, more healing, fewer enemies, no boss last stands. For a relaxed run.",
            "The game as it's balanced: enemy damage grows each wave, healing is scarce.",
            "Tougher, faster, harder-hitting enemies, more elites, slower revives.",
            "Everything turned up and no regeneration. Bring a plan.",
            "Set every number yourself with the sliders, or type one in.",
        };

        /// Every knob, in the order the lobby shows them (the same order as DiffKnob).
        public static readonly KnobDef[] Knobs =
        {
            new KnobDef { Knob = DiffKnob.EnemyHp, Name = "Enemy health", Desc = "Every enemy's and boss's health.",
                Unit = KnobUnit.Times, Min = 0.25f, Max = 4f, Step = 0.05f, Presets = new[] { 0.75f, 1f, 1.25f, 1.5f } },
            new KnobDef { Knob = DiffKnob.BossHp, Name = "Boss health", Desc = "Bosses' health, on top of enemy health.",
                Unit = KnobUnit.Times, Min = 0.25f, Max = 4f, Step = 0.05f, Presets = new[] { 0.8f, 1f, 1.2f, 1.4f } },
            new KnobDef { Knob = DiffKnob.SoloHp, Name = "Solo enemy health", Desc = "Enemies' health when you play alone (two players: 100%).",
                Unit = KnobUnit.Percent, Min = 0.4f, Max = 1.5f, Step = 0.05f, Presets = new[] { 0.7f, 0.85f, 1f, 1.15f } },
            new KnobDef { Knob = DiffKnob.EnemyDamage, Name = "Enemy damage", Desc = "Everything that hurts you: bullets, touching enemies, hazards.",
                Unit = KnobUnit.Times, Min = 0.25f, Max = 4f, Step = 0.05f, Presets = new[] { 0.75f, 1f, 1.25f, 1.5f } },
            new KnobDef { Knob = DiffKnob.DamagePerWave, Name = "Damage growth", Desc = "Enemy damage grows this much every wave (Normal: 2.5x by wave 50).",
                Unit = KnobUnit.PercentPerWave, Min = 0f, Max = 0.1f, Step = 0.005f, Presets = new[] { 0.015f, 0.03f, 0.04f, 0.05f } },
            new KnobDef { Knob = DiffKnob.EnemyCount, Name = "Enemy count", Desc = "How many enemies come in a regular wave.",
                Unit = KnobUnit.Times, Min = 0.5f, Max = 2.5f, Step = 0.05f, Presets = new[] { 0.85f, 1f, 1.15f, 1.3f } },
            new KnobDef { Knob = DiffKnob.FireRate, Name = "Enemy fire rate", Desc = "How fast enemies and bosses shoot.",
                Unit = KnobUnit.Times, Min = 0.5f, Max = 2f, Step = 0.05f, Presets = new[] { 0.9f, 1f, 1.1f, 1.2f } },
            new KnobDef { Knob = DiffKnob.EnemySpeed, Name = "Enemy speed", Desc = "How fast enemies move.",
                Unit = KnobUnit.Times, Min = 0.5f, Max = 2f, Step = 0.05f, Presets = new[] { 1.1f, 1.1f, 1.21f, 1.32f } },
            new KnobDef { Knob = DiffKnob.EliteChance, Name = "Elites", Desc = "How often enemies come as elites (from wave 12; Normal: up to 35%).",
                Unit = KnobUnit.Times, Min = 0f, Max = 3f, Step = 0.1f, Presets = new[] { 0.5f, 1f, 1.5f, 2f } },
            new KnobDef { Knob = DiffKnob.BulletBudget, Name = "Bullet budget", Desc = "The cap on regular enemies' bullets on screen.",
                Unit = KnobUnit.Times, Min = 0.5f, Max = 2f, Step = 0.05f, Presets = new[] { 0.85f, 1f, 1.15f, 1.3f } },
            new KnobDef { Knob = DiffKnob.BossBullets, Name = "Adds' fire in boss fights", Desc = "The share of the bullet budget other enemies get while a boss is up.",
                Unit = KnobUnit.Percent, Min = 0f, Max = 1f, Step = 0.05f, Presets = new[] { 0.4f, 0.6f, 0.75f, 0.9f } },
            new KnobDef { Knob = DiffKnob.Regen, Name = "Regeneration", Desc = "Everyone's passive healing (upgrades add to it).", HarderUp = false,
                Unit = KnobUnit.HpPerSecond, Min = 0f, Max = 3f, Step = 0.05f, Presets = new[] { 0f, 0f, 0f, 0f } },  // (owner Nov 20: no passive regen by default; upgrades give it)
            new KnobDef { Knob = DiffKnob.OrbChance, Name = "Health orb drops", Desc = "The chance a kill drops a small health orb.", HarderUp = false,
                Unit = KnobUnit.Percent, Min = 0f, Max = 0.5f, Step = 0.01f, Presets = new[] { 0.18f, 0.12f, 0.09f, 0.06f } },
            new KnobDef { Knob = DiffKnob.ReviveTime, Name = "Revive time", Desc = "How long it takes to get a downed ally up.",
                Unit = KnobUnit.Seconds, Min = 1f, Max = 10f, Step = 0.5f, Presets = new[] { 3f, 4f, 5f, 6f } },
            new KnobDef { Knob = DiffKnob.HitGrace, Name = "Hit grace", Desc = "How long you can't be hit again after taking a hit.", HarderUp = false,
                Unit = KnobUnit.Seconds, Min = 0.05f, Max = 1f, Step = 0.05f, Presets = new[] { 0.35f, 0.25f, 0.2f, 0.15f } },
            new KnobDef { Knob = DiffKnob.Shooters, Name = "Shooters at once", Desc = "How many regular enemies take turns shooting at once (Normal: 4, up to 8 by wave 24; more with more players).",
                Unit = KnobUnit.Times, Min = 0.25f, Max = 3f, Step = 0.05f, Presets = new[] { 0.75f, 1f, 1.25f, 1.5f } },
            // Boss approaches (Approaches.cs): flavour, the same on every preset. Blend = 85% with the ramp; Fully themed = 100% without.
            new KnobDef { Knob = DiffKnob.ApproachShare, Name = "Boss approach", Flavour = true,
                Desc = "How much of the last wave before each boss is that boss's own kind of enemy (0%: the usual random waves).",
                Unit = KnobUnit.Percent, Min = 0f, Max = 1f, Step = 0.05f, Presets = new[] { 0.85f, 0.85f, 0.85f, 0.85f } },
            new KnobDef { Knob = DiffKnob.ApproachRamp, Name = "Approach ramp", Flavour = true,
                Desc = "On: the four waves before a boss build up to that share (40%, 60%, 80%, all). Off: every one of them uses it.",
                Unit = KnobUnit.OnOff, Min = 0f, Max = 1f, Step = 1f, Presets = new[] { 1f, 1f, 1f, 1f } },
            // Arena layouts (Layouts.cs): flavour too. Off keeps the open arena (and the usual enemy count) all run.
            new KnobDef { Knob = DiffKnob.ArenaLayouts, Name = "Arena layouts", Flavour = true,
                Desc = "On: each boss cycle gets a fixed floor plan with walls, pillars and edge gates (and 30% more enemies). Off: always the open arena.",
                Unit = KnobUnit.OnOff, Min = 0f, Max = 1f, Step = 1f, Presets = new[] { 1f, 1f, 1f, 1f } },
            // Retired (Nov 18): was "Rooms arena" (the Wings map); ignored, kept so the knob order stays the same.
            new KnobDef { Knob = DiffKnob.ArenaRooms, Name = "Rooms arena (retired)", Flavour = true,
                Desc = "Retired (Nov 18): ignored; the Rooms map is gone.",
                Unit = KnobUnit.OnOff, Min = 0f, Max = 1f, Step = 1f, Presets = new[] { 0f, 0f, 0f, 0f } },
            // The Expanding arena (ExpansionGame.cs, Docs/ExpandingArena.md): flavour. Starts as the Classic box; after each boss the team votes on rooms to buy.
            new KnobDef { Knob = DiffKnob.ArenaExpanding, Name = "Expanding arena", Flavour = true,
                Desc = "On: the arena starts as the Classic box and grows: after each boss the team votes on a new room to buy with a shared purse (paths are off). Off: the Classic arena.",
                Unit = KnobUnit.OnOff, Min = 0f, Max = 1f, Step = 1f, Presets = new[] { 0f, 0f, 0f, 0f } },
            // The campaign buildup (Approaches.cs, Docs/Campaign.md): flavour. Every wave is faction enemies; Boss approach and its ramp only apply when this is off.
            new KnobDef { Knob = DiffKnob.Campaign, Name = "Campaign buildup", Flavour = true,
                Desc = "On: every wave is made of boss-themed enemies: the last boss's phase out while the next boss's build up. Off: the usual random waves with the Boss approach share.",
                Unit = KnobUnit.OnOff, Min = 0f, Max = 1f, Step = 1f, Presets = new[] { 1f, 1f, 1f, 1f } },
            // Retired (Nov 18): was "Team vote building"; ignored, kept so the knob order stays the same.
            new KnobDef { Knob = DiffKnob.BuildVote, Name = "Team vote (retired)", Flavour = true,
                Desc = "Retired (Nov 18): ignored; every build goes up at once.",
                Unit = KnobUnit.OnOff, Min = 0f, Max = 1f, Step = 1f, Presets = new[] { 0f, 0f, 0f, 0f } },
            new KnobDef { Knob = DiffKnob.BuildPurse, Name = "Team purse (retired)", Flavour = true,
                Desc = "Retired (Nov 18): ignored; you only spend your own vectors.",
                Unit = KnobUnit.OnOff, Min = 0f, Max = 1f, Step = 1f, Presets = new[] { 0f, 0f, 0f, 0f } },
            // Lives (Checkpoints.cs, Docs/Checkpoints.md): retries from the last boss's checkpoint when everyone goes down.
            new KnobDef { Knob = DiffKnob.Lives, Name = "Lives", HarderUp = false,
                Desc = "Retries from the last boss's checkpoint when everyone goes down: the run comes back as it was there, and its score drops by a quarter each time.",
                Unit = KnobUnit.Count, Min = 0f, Max = 5f, Step = 1f, Presets = new[] { 3f, 2f, 1f, 0f } },
            // Retired (Nov 18): were Room cards and Grid rooms; the Expanding arena always builds on the grid now. Ignored, kept so the knob order stays the same.
            new KnobDef { Knob = DiffKnob.RoomCards, Name = "Room cards (retired)", Flavour = true,
                Desc = "Retired (Nov 18): ignored; rooms are grid pieces.",
                Unit = KnobUnit.OnOff, Min = 0f, Max = 1f, Step = 1f, Presets = new[] { 0f, 0f, 0f, 0f } },
            new KnobDef { Knob = DiffKnob.GridRooms, Name = "Grid rooms (retired)", Flavour = true,
                Desc = "Retired (Nov 18): ignored; the Expanding arena always builds on the grid.",
                Unit = KnobUnit.OnOff, Min = 0f, Max = 1f, Step = 1f, Presets = new[] { 0f, 0f, 0f, 0f } },
            // The mechanics tier (Docs/DifficultyTiers.md): not a number but which fights and enemies a run uses.
            new KnobDef { Knob = DiffKnob.Mechanics, Name = "Mechanics",
                Desc = "Hard and Nightmare add boss stages, attacks and enemy variants; Easy takes some away.",
                Unit = KnobUnit.Tier, Min = 0f, Max = 3f, Step = 1f, Presets = new[] { 0f, 1f, 2f, 3f } },
        };
    }

    public partial class Game
    {
        /// The run's difficulty (host: copied from the lobby when the run starts; clients: told by the host).
        public Difficulty Diff = new Difficulty();

        public float D(DiffKnob k) => Diff[k];

        /// Enemy damage at this wave: the difficulty's multiplier, growing each wave.
        public float EnemyDamageScale(int wave) => D(DiffKnob.EnemyDamage) * (1f + D(DiffKnob.DamagePerWave) * Math.Max(0, StrengthWave(wave) - 1)) * ClueDamage;

        /// A hunt map's damage clues: enemies hit softer during the boss fight (HuntMap.ClueDamageScale).
        float ClueDamage => HuntRoute != null && Hunt != null && BossNow ? HuntRoute.ClueDamageScale : 1f;

        public float PassiveRegen => D(DiffKnob.Regen);

        /// The run's mechanics tier (Docs/DifficultyTiers.md): Normal plays as balanced, Hard and Nightmare add, Easy takes away.
        public MechTier MechTier => Diff.Tier;

        /// True on this tier or a harder one ("from Hard on").
        public bool MechAtLeast(MechTier t) => MechTier >= t;

        /// True when the run's tier is between min and max (both included): the filter every tier-gated row uses.
        public bool MechOk(MechTier min, MechTier max) => MechTier >= min && MechTier <= max;
    }
}
