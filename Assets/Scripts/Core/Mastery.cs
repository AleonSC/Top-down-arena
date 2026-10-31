using System;
using System.Collections.Generic;

namespace Arena
{
    /// Ability mastery: every ability levels up (1 to 5) as you use it during a run. Each level adds
    /// +8% to what it does (damage, healing) and takes 4% off its cooldown; level 5 unlocks its capstone,
    /// a change to how it works (see AbilityDef.Capstone). Host-authoritative; XP is sent to everyone.
    public partial class Game
    {
        public const int MasteryMax = 5;

        /// Total XP needed for each level (index = level - 1). XP per cast is the ability's cooldown in
        /// seconds times how well it landed, so slow and fast abilities level at a similar pace:
        /// about level 3 by wave 8-10 and level 5 by wave 20-25 if you use it whenever it's ready.
        public static readonly float[] MasteryXpFor = { 0f, 100f, 300f, 550f, 850f };

        public static int MasteryLevelFor(float xp)
        {
            int level = 1;
            while (level < MasteryMax && xp >= MasteryXpFor[level])
                level++;
            return level;
        }

        /// 0..1 progress from this level to the next (1 at max).
        public static float MasteryProgress(float xp)
        {
            int level = MasteryLevelFor(xp);
            if (level >= MasteryMax)
                return 1f;
            return MathX.Clamp01((xp - MasteryXpFor[level - 1]) / (MasteryXpFor[level] - MasteryXpFor[level - 1]));
        }

        public static int MasteryLevel(Player p, AbilityId id)
        {
            float xp;
            return id != AbilityId.None && p.MasteryXp.TryGetValue(id, out xp) ? MasteryLevelFor(xp) : 1;
        }

        public static bool Mastered(Player p, AbilityId id) => MasteryLevel(p, id) >= MasteryMax;

        public static float MasteryPower(Player p, AbilityId id) => 1f + 0.08f * (MasteryLevel(p, id) - 1);

        public static float MasteryCooldown(Player p, AbilityId id) => 1f - 0.04f * (MasteryLevel(p, id) - 1);

        // --- During a cast -----------------------------------------------------------
        // While an ability's effect runs, damage and healing it does are scaled by its mastery and
        // counted, so we know how well it landed. Lasting effects (zones, minions, shots) carry the power.

        bool casting, castMastered;
        float castPower = 1f;
        int castHits;

        void BeginCast(Player p, AbilityId id)
        {
            casting = true;
            castPower = MasteryPower(p, id);
            castMastered = Mastered(p, id);
            castHits = 0;
        }

        /// Instant abilities earn XP by what they hit (0.5x for nothing, up to 1.5x); lasting ones
        /// (barriers, zones, summons, buffs) earn the full amount. No XP outside a fight with enemies around.
        void EndCast(Player p, int slot, AbilityId id, bool hadTargets)
        {
            casting = castMastered = false;
            castPower = 1f;
            if (!hadTargets)
                return;
            float eff = InstantAbility(id) ? 0.5f + Math.Min(1f, castHits / 3f) : 1f;
            GainMastery(p, id, Cooldown(p, slot) * eff);
        }

        static bool InstantAbility(AbilityId id) => Upgrades.Ability(id).Instant;

        public void GainMastery(Player p, AbilityId id, float xp)
        {
            if (!IsHost || id == AbilityId.None || xp <= 0f || ShowcaseMode)
                return;
            float was;
            p.MasteryXp.TryGetValue(id, out was);
            int before = MasteryLevelFor(was);
            if (Has(RunMod.Practiced))
                xp *= Modifiers.PracticedMastery;
            float now = Math.Min(MasteryXpFor[MasteryMax - 1], was + xp);
            p.MasteryXp[id] = now;
            int after = MasteryLevelFor(now);
            if (after > before)
            {
                Recalc(p);  // Dash's capstone adds a charge
                if (p.Id == MyId)
                    MasteryToasts.Add(new MasteryToast { Ability = id, Level = after, T = 3f });
                BroadcastFx(FxKind.Ring, p.Pos, after >= MasteryMax ? new Rgba(1f, 0.82f, 0.3f) : new Rgba(0.6f, 0.85f, 1f), 70f, 0.6f, Sfx.LevelUp);
                DoLog(p.Name + "'s " + Upgrades.Ability(id).Name + " reached level " + after);
            }
        }

        // --- Capstone helpers ----------------------------------------------------------

        /// Shoves enemies (not bosses or other heavies) out of a circle, and optionally slows them.
        void Knockback(V2 center, float range, float push, float slowTime)
        {
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || Heavy(e) || e.Pos.DistanceTo(center) > range + e.Radius)
                    continue;
                V2 away = (e.Pos - center).Normalized();
                e.Pos = ClampToArena(e.Pos + (away.IsZero ? V2.FromAngle(Rand(0f, MathX.Tau)) : away) * push, e.Radius);
                if (slowTime > 0f)
                {
                    e.SlowT = Math.Max(e.SlowT, slowTime);
                    e.SlowMult = Math.Min(e.SlowMult, 0.5f);
                }
            }
        }

        /// Freezes enemies (not bosses) in a circle solid for a while.
        void Freeze(V2 center, float range, float time)
        {
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && !e.Def.IsBoss && e.Pos.DistanceTo(center) < range + e.Radius)
                {
                    e.FrozenT = Math.Max(e.FrozenT, time);
                    e.SlowT = Math.Max(e.SlowT, time);
                }
        }

        /// Erases enemy bullets within `width` of a line segment.
        void EraseAlong(V2 from, V2 to, float width)
        {
            V2 path = to - from;
            float len = Math.Max(1f, path.Length);
            V2 dir = path / len;
            var erase = new List<int>();
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind))
                    continue;
                float along = Math.Max(0f, Math.Min(len, (b.Pos.X - from.X) * dir.X + (b.Pos.Y - from.Y) * dir.Y));
                if (b.Pos.DistanceTo(from + dir * along) < width)
                    erase.Add(b.Id);
            }
            foreach (int bid in erase)
            {
                Bullets.Remove(bid);
                pendingRemoved.Add(bid);
            }
            if (casting)
                castHits += (erase.Count + 4) / 5;
        }

        /// Dev menu: put every ability this player has at level 5.
        public void DevMasterAll(int id)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p))
                return;
            DevUsed(p.Name + ": every ability mastered");
            foreach (var s in p.Slots)
                if (s.Id != AbilityId.None)
                    GainMastery(p, s.Id, MasteryXpFor[MasteryMax - 1]);
        }

        // Snapshot: each slot's ability XP (everyone needs it for capstones like Dash's extra charge and the HUD).
        // known: false while reading a player this game doesn't have (the values are read and dropped).
        void SyncMastery(NetSync s, Player p, bool known)
        {
            bool levelChanged = false;
            for (int k = 0; k < Upgrades.SlotCount; k++)
            {
                var slotId = p.Slots[k].Id;
                float xp;
                p.MasteryXp.TryGetValue(slotId, out xp);
                int idByte = (int)slotId;
                s.Byte(ref idByte);
                s.Float(ref xp);
                var id = (AbilityId)idByte;
                if (!s.Reading || !known || id == AbilityId.None)
                    continue;
                float was;
                p.MasteryXp.TryGetValue(id, out was);
                if (MasteryLevelFor(was) != MasteryLevelFor(xp))
                {
                    levelChanged = true;
                    if (p.Id == MyId && MasteryLevelFor(xp) > MasteryLevelFor(was))
                        MasteryToasts.Add(new MasteryToast { Ability = id, Level = MasteryLevelFor(xp), T = 3f });
                }
                p.MasteryXp[id] = xp;
            }
            if (levelChanged)
                Recalc(p);
        }

        /// What each ability's level-5 capstone does.
        public static string Capstone(AbilityId id) => id == AbilityId.None ? "" : Upgrades.Ability(id).Capstone;

        /// "Barrier reached level 3" popups for your own abilities (the front end draws and ages them).
        public readonly List<MasteryToast> MasteryToasts = new List<MasteryToast>();
    }

    public class MasteryToast
    {
        public AbilityId Ability;
        public int Level;
        public float T;
    }
}
