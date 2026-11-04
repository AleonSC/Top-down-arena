using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// What one player did during a run, shown on the end-of-run screen.
    public class RunStats
    {
        public float DamageDealt, DamageTaken, HealingDone, BiggestHit;
        public float BossDamage;  // host only (Boss Lab)
        public int Kills, EliteKills, BossKills, Downs, Revives, ShotsFired, ShotsHit, AbilitiesUsed;

        public float Accuracy => ShotsFired > 0 ? Math.Min(1f, (float)ShotsHit / ShotsFired) : 0f;
    }

    /// The host keeps the tallies and sends them to everyone when the run ends.
    public partial class Game
    {
        public float RunTime;          // seconds of play (pauses excluded)
        public bool RunStatsReady;     // the end-of-run numbers have arrived

        /// Heals a player and credits whoever caused it. Returns the HP actually restored. what names the source
        /// for session recordings when there's no player behind it (a path's pools, Blood Moon).
        float Heal(Player target, float amount, Player source, string what = null)
        {
            if (target.Downed || amount <= 0f)
                return 0f;
            if (casting)
                amount *= castPower;
            float applied = Math.Min(amount, target.MaxHp - target.Hp);
            if (amount > applied && source != null && source.Role == Role.Healer && what != AuraSource)
                OverhealShield(target, amount - applied);
            if (applied <= 0f)
                return 0f;
            target.Hp += applied;
            if (casting)
                castHits++;
            if (source != null)
                source.Run.HealingDone += applied;
            Recorder?.Healed(source, applied);
            Recorder?.HealReceived(target, applied, what ?? (source == null ? "other" : source == target ? "own abilities" : "allies"));
            return applied;
        }

        /// A Healer's heal past full health: a short shield, up to 20% of max health (a bigger shield already
        /// up, like Energy Ward's, is kept).
        void OverhealShield(Player target, float excess)
        {
            float cap = target.MaxHp * OverhealCap;
            if (target.ShieldHp >= cap)
                return;
            target.ShieldHp = Math.Min(cap, target.ShieldHp + excess);
            target.ShieldT = Math.Max(target.ShieldT, OverhealTime);
        }

        Player PlayerOrNull(int id)
        {
            Player p;
            return Players.TryGetValue(id, out p) ? p : null;
        }

        void BroadcastRunStats()
        {
            RunStatsReady = true;
            session.Broadcast(RunStatsMessage(Msg.RunStats));
        }

        const float LiveStatsEvery = 1f;  // seconds between the live tallies the host sends (Msg.RunStatsLive)
        float liveStatsT;

        /// The host sends everyone the tallies so far, about once a second and when the game pauses, so the
        /// Run stats screen (Tab, or the pause menu) shows the whole party's numbers on every player's game.
        void TickLiveStats(float dt, bool now = false)
        {
            if (!IsHost || ShowcaseMode || Players.Count < 2)
                return;
            liveStatsT -= dt;
            if (!now && liveStatsT > 0f)
                return;
            liveStatsT = LiveStatsEvery;
            session.Broadcast(RunStatsMessage(Msg.RunStatsLive));
        }

        byte[] RunStatsMessage(Msg kind)
        {
            var w = Proto.Begin(kind);
            w.Write(RunTime);
            w.Write((byte)Players.Count);
            foreach (var p in Players.Values)
            {
                var s = p.Run;
                w.Write(p.Id);
                w.Write(s.DamageDealt);
                w.Write(s.DamageTaken);
                w.Write(s.HealingDone);
                w.Write(s.BiggestHit);
                w.Write(s.Kills);
                w.Write(s.EliteKills);
                w.Write(s.BossKills);
                w.Write(s.Downs);
                w.Write(s.Revives);
                w.Write(s.ShotsFired);
                w.Write(s.ShotsHit);
                w.Write(s.AbilitiesUsed);
            }
            return Proto.End(w);
        }

        /// The end-of-run tallies (final) or the live ones (Msg.RunStatsLive, which leaves RunStatsReady alone).
        void ReadRunStats(BinaryReader r, bool final = true)
        {
            RunTime = r.ReadSingle();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                var s = new RunStats();
                int id = r.ReadInt32();
                s.DamageDealt = r.ReadSingle();
                s.DamageTaken = r.ReadSingle();
                s.HealingDone = r.ReadSingle();
                s.BiggestHit = r.ReadSingle();
                s.Kills = r.ReadInt32();
                s.EliteKills = r.ReadInt32();
                s.BossKills = r.ReadInt32();
                s.Downs = r.ReadInt32();
                s.Revives = r.ReadInt32();
                s.ShotsFired = r.ReadInt32();
                s.ShotsHit = r.ReadInt32();
                s.AbilitiesUsed = r.ReadInt32();
                Player p;
                if (Players.TryGetValue(id, out p))
                    p.Run = s;
            }
            if (final)
                RunStatsReady = true;
        }
    }
}
