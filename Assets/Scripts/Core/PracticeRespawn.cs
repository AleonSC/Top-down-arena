using System.IO;

namespace Arena
{
    /// The Practice Range has no game over: a downed player can respawn where they fell (full health, the usual 1.5 s of
    /// invulnerability, the wave keeps running), and the host can restart the wave. The screen is Game/PracticeDownedUi.cs.
    /// A client asks with Msg.Respawn; everything that changes (Downed, Hp) already rides the snapshot.
    public partial class Game
    {
        public const byte RespawnHere = 0, RespawnRestart = 1;

        /// You pressed Respawn here (kind 0) or Restart wave (kind 1, the host only). Does nothing outside the Practice Range.
        public void RequestRespawn(byte kind)
        {
            if (Practice == null || Me == null)
                return;
            if (IsHost)
            {
                HostRespawn(MyId, kind);
                return;
            }
            if (kind != RespawnHere)
                return;  // only the host restarts the wave
            var w = Proto.Begin(Msg.Respawn);
            w.Write(kind);
            session.SendToHost(Proto.End(w));
        }

        public void OnRespawn(int peer, BinaryReader r) => HostRespawn(peer, r.ReadByte());

        void HostRespawn(int id, byte kind)
        {
            if (Practice == null)
                return;
            Player p;
            if (kind == RespawnHere)
            {
                if (Players.TryGetValue(id, out p) && p.Downed)
                    RevivePlayer(p, 1f);
            }
            else if (kind == RespawnRestart && id == MyId && IsHost)
                RestartPracticeWave();
        }

        /// Host: throw away this practice wave and start it again after a short countdown. Everyone is back on their feet at full health.
        void RestartPracticeWave()
        {
            if (Practice == null || Practice.Waves.Count == 0 || State != MatchState.Fighting)
                return;
            Recorder?.WaveEnd(this);
            EndRoomsWave();
            spawnQueue.Clear();
            squadQueue.Clear();
            designQueue.Clear();
            Enemies.Clear();
            ResetBossState();
            ResetVariety(false);
            ClearPods();
            ClearRisers();
            Zones.Clear();
            Hazards.Clear();
            Cages.Clear();
            RemoveTemporaryMinions();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    RevivePlayer(p, 1f);
                p.Hp = p.MaxHp;
            }
            PracticeIndex = (PracticeIndex - 1 + Practice.Waves.Count) % Practice.Waves.Count;  // the next wave to start is this one again
            State = MatchState.Intermission;
            StateTimer = 2f;
        }
    }
}
