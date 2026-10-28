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
            Recalc(p);
            BroadcastDevStats(p);
        }

        public void DevSetGod(int id, bool on)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p))
                return;
            p.DevGod = on;
            BroadcastDevStats(p);
        }

        public void DevHeal(int id)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p))
                return;
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
            p.Level++;
            p.PendingPicks++;
            if (State == MatchState.Intermission && p.Offer == null)
                NextOffer(p);
        }

        /// Clear the arena and start the given wave after a short countdown.
        public void DevJumpToWave(int wave)
        {
            if (!IsHost || State == MatchState.Idle)
                return;
            wave = Math.Max(1, Math.Min(Endless ? 999 : MaxWave, wave));
            if (State == MatchState.Fighting)
                Recorder?.WaveEnd(this);
            spawnQueue.Clear();
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
            Wave = wave - 1;
            State = MatchState.Intermission;
            StateTimer = 2f;
            DoLog("dev: jumping to wave " + wave);
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
