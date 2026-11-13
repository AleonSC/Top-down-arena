using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Arena
{
    /// Developer tools (the F1 menu): jump to a wave, edit players' stats by hand, god mode.
    /// Everything that changes the match runs on the host; stat edits are sent to everyone so each
    /// peer's stats (recomputed from upgrades plus these overrides) still agree.
    public partial class Game
    {
        /// Every editable stat, in declaration order (the index is what goes over the network).
        public static readonly FieldInfo[] StatFields = SortedStatFields();

        static FieldInfo[] SortedStatFields()
        {
            var f = typeof(Stats).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(f, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            return f;
        }

        public static float StatValue(Stats s, int field)
        {
            object v = StatFields[field].GetValue(s);
            if (v is bool) return (bool)v ? 1f : 0f;
            if (v is int) return (int)v;
            return (float)v;
        }

        static void SetStat(Stats s, int field, float value)
        {
            var f = StatFields[field];
            if (f.FieldType == typeof(bool)) f.SetValue(s, value != 0f);
            else if (f.FieldType == typeof(int)) f.SetValue(s, (int)Math.Round(value));
            else f.SetValue(s, value);
        }

        void ApplyStatOverrides(Player p)
        {
            foreach (var kv in p.StatOverrides)
                SetStat(p.Stats, kv.Key, kv.Value);
        }

        // --- Host commands --------------------------------------------------------

        public void DevSetStat(int id, int field, float value)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p) || field < 0 || field >= StatFields.Length)
                return;
            p.StatOverrides[field] = value;
            DevUsed(p.Name + ": " + StatFields[field].Name + " set to " + value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            Recalc(p);
            BroadcastDevStats(p);
        }

        /// Back to what their upgrades give.
        public void DevResetStats(int id)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p))
                return;
            p.StatOverrides.Clear();
            DevUsed(p.Name + ": stats reset");
            Recalc(p);
            BroadcastDevStats(p);
        }

        public void DevSetGod(int id, bool on)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p))
                return;
            if (p.DevGod != on)
                DevUsed(p.Name + ": god mode " + (on ? "on" : "off"));
            p.DevGod = on;
            BroadcastDevStats(p);
        }

        /// Dev: put `count` of an enemy in a row in front of this player (a boss comes in the middle of the arena, as on its wave; the Twin
        /// Reapers both). Host only, while a wave is on; returns how many were spawned. They count toward the wave like any enemy.
        public int DevSpawnEnemy(int id, EnemyType type, int count, bool elite)
        {
            Player p;
            if (!IsHost || State != MatchState.Fighting || !Players.TryGetValue(id, out p))
                return 0;
            var d = Defs.Get(type);
            count = Math.Max(1, Math.Min(count, 20));
            if (d.IsBoss)
            {
                for (int i = 0; i < (type == EnemyType.TwinA ? 1 : count); i++)
                {
                    SpawnEnemy(type);
                    if (type == EnemyType.TwinA)
                        SpawnEnemy(EnemyType.TwinB);
                }
                DevUsed(p.Name + ": spawned " + d.Name);
                return type == EnemyType.TwinA ? 2 : count;
            }
            V2 fwd = V2.FromAngle(p.Aim);
            V2 side = new V2(-fwd.Y, fwd.X);
            float gap = d.Radius * 2f + 16f;
            for (int i = 0; i < count; i++)
            {
                int row = i / 6, inRow = Math.Min(6, count - row * 6);
                V2 at = p.Pos + fwd * (300f + row * gap) + side * ((i % 6 - (inRow - 1) / 2f) * gap);
                SpawnEnemyAt(type, ClampToArena(at, d.Radius + 20f), 0.9f, 0, elite);
            }
            DevUsed(p.Name + ": spawned " + count + " " + d.Name);
            return count;
        }

        /// Dev: drop an upgrade a few steps in front of this player, exactly as a treasure chest drops theirs (a Loot only they see:
        /// walk onto it, then take it (G) or discard it (X)). Host only, and only an upgrade that could be offered to their build
        /// (their class, abilities and weapon); true when it was dropped. Marks the run as a dev run.
        public bool DevSpawnLoot(int id, int upgrade)
        {
            Player p;
            if (!IsHost || State == MatchState.Idle || upgrade < 0 || upgrade >= Upgrades.All.Length || !Players.TryGetValue(id, out p))
                return false;
            if (!Upgrades.Available(upgrade, OfferFor(p)))
                return false;
            int n = 0;
            foreach (var l in Loots.Values)
                if (l.Owner == id)
                    n++;
            V2 fwd = V2.FromAngle(p.Aim);
            V2 side = new V2(-fwd.Y, fwd.X);
            float shift = ((n + 1) / 2) * 44f * (n % 2 == 1 ? 1f : -1f);
            var loot = new Loot { Id = nextLootId++, Owner = id, Upgrade = upgrade, Pos = ClampToArena(p.Pos + fwd * 90f + side * shift, 20f) };
            Loots[loot.Id] = loot;
            BroadcastFx(FxKind.Ring, loot.Pos, Upgrades.RarityColor(Upgrades.All[upgrade].Rarity), 40f, 0.4f, Sfx.None);
            DevUsed(p.Name + ": spawned " + Upgrades.All[upgrade].Name);
            SendSnapshot();
            return true;
        }

        public void DevHeal(int id)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p))
                return;
            DevUsed(p.Name + ": healed");
            if (p.Downed)
                RevivePlayer(p, 1f);
            p.Hp = p.MaxHp;
            p.ParryHp = ParryMax(p);
            p.ParryBroken = 0f;
        }

        public void DevLevelUp(int id)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p))
                return;
            DevUsed(p.Name + ": level up");
            LevelUpPlayer(p);  // same path as XP: milestone levels queue their pick too
            if (State == MatchState.Intermission && p.Offer == null)
                NextOffer(p);
        }

        /// Kill every enemy on the field as this player's kills: XP, score, drops, gold and kill credit as usual (the
        /// next collision pass handles each death). Shields, bubbles and a boss's immunity don't save them.
        /// Returns how many died.
        public int DevKillAll(int id)
        {
            if (!IsHost || State == MatchState.Idle)
                return 0;
            Player p;
            Players.TryGetValue(id, out p);
            int n = 0;
            foreach (var e in Enemies.Values)
            {
                if (e.Hp <= 0f || e.Dummy)
                    continue;
                if (p != null)
                {
                    p.Run.DamageDealt += e.Hp;
                    if (e.Def.IsBoss)
                        p.Run.BossDamage += e.Hp;
                }
                Recorder?.DamageDealt(id, e, e.Hp);
                e.LastHitBy = id;
                e.BubbleHp = 0f;
                e.ShieldHp = 0f;
                e.Hp = 0f;
                n++;
            }
            DpsSkip();  // (not damage the DPS meter should count)
            if (n > 0)
                DevUsed((p != null ? p.Name : "Host") + ": killed all " + n + " enemies");
            return n;
        }

        /// F1 "Freeze enemies": enemies stop moving and shooting and no new ones spawn (host only; they can still be
        /// killed). Checked in TickEnemies (EnemyAI.cs) and the Fighting spawner (Waves.cs).
        public bool DevFreeze;

        public void DevSetFreeze(bool on)
        {
            if (!IsHost)
                return;
            if (DevFreeze != on)
                DevUsed("enemies " + (on ? "frozen" : "unfrozen"));
            DevFreeze = on;
        }

        /// Clear the arena and start the given wave after a short countdown.
        public void DevJumpToWave(int wave, bool boss = false)
        {
            if (!IsHost || State == MatchState.Idle)
                return;
            wave = Math.Max(1, Math.Min(Endless ? 999 : MaxWave, wave));
            DevUsed((boss ? "jumped to the boss after wave " : "jumped to wave ") + wave);
            if (State == MatchState.Fighting)
                Recorder?.WaveEnd(this);
            spawnQueue.Clear();
            squadQueue.Clear();
            Enemies.Clear();
            ResetBossState();
            Zones.Clear();
            Hazards.Clear();
            Cages.Clear();
            RemoveTemporaryMinions();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            foreach (var p in Players.Values)
                if (p.Downed)
                    RevivePlayer(p, 1f);
            Wave = boss ? wave : wave - 1;
            Leg = Wave > 0 ? 1 : 0;  // (a Campaign: the jump lands on the first leg of wave n)
            BossSlot = !boss && Bosses.Slotted && IsBossWave(Wave);  // (jumping to wave n lands on regular wave n, not the boss before it; boss: the boss after wave n)
            State = MatchState.Intermission;
            StateTimer = 2f;
            DoLog("dev: jumping to " + (boss ? "the boss after " : "") + "wave " + wave);
        }

        void BroadcastDevStats(Player p)
        {
            var w = Proto.Begin(Msg.DevStats);
            w.Write(p.Id);
            w.Write(p.DevGod);
            w.Write((byte)p.StatOverrides.Count);
            foreach (var kv in p.StatOverrides)
            {
                w.Write((byte)kv.Key);
                w.Write(kv.Value);
            }
            session.Broadcast(Proto.End(w));
        }

        void ReadDevStats(BinaryReader r)
        {
            int id = r.ReadInt32();
            bool god = r.ReadBoolean();
            int n = r.ReadByte();
            var overrides = new Dictionary<int, float>();
            for (int i = 0; i < n; i++)
            {
                int field = r.ReadByte();
                overrides[field] = r.ReadSingle();
            }
            Player p;
            if (!Players.TryGetValue(id, out p))
                return;
            p.DevGod = god;
            p.StatOverrides = overrides;
            Recalc(p);
        }
    }
}
