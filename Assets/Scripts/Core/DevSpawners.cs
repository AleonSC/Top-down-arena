using System;
using System.Collections.Generic;

namespace Arena
{
    /// A dev spawner (F1 dock, Spawn-an-enemy sheet with Spawner on): a pad on the floor that brings in one enemy type every few
    /// seconds while a wave is under way, never more than its cap alive at once. Placed and run by the host; sent in the snapshot
    /// so every screen draws it (Game/DevSpawnerLook.cs).
    public class DevSpawner
    {
        public int Id;
        public EnemyType Type;
        public V2 Pos;
        public bool Elite;
        public float Every;   // seconds between spawns
        public int Cap;       // most of its enemies alive at once
        public float T;       // seconds to the next spawn
        public int Alive;     // its enemies alive (host counts; sent for the look)
    }

    public partial class Game
    {
        public readonly Dictionary<int, DevSpawner> DevSpawners = new Dictionary<int, DevSpawner>();
        int nextDevSpawnerId = 1;
        public const int DevSpawnerMax = 12;  // spawners on the map at once
        public static readonly float[] DevSpawnerEvery = { 1f, 2f, 4f, 8f };
        public static readonly int[] DevSpawnerCaps = { 1, 3, 5, 10 };

        /// Dev: put a spawner of this enemy a little in front of this player. Host only, in a match, not a boss; returns its id (0: none placed).
        public int DevPlaceSpawner(int id, EnemyType type, float every, int cap, bool elite)
        {
            Player p;
            if (!IsHost || State == MatchState.Idle || !Players.TryGetValue(id, out p) || Defs.Get(type).IsBoss || DevSpawners.Count >= DevSpawnerMax)
                return 0;
            var s = new DevSpawner
            {
                Id = nextDevSpawnerId++, Type = type, Elite = elite,
                Every = Math.Max(0.5f, Math.Min(every, 30f)), Cap = Math.Max(1, Math.Min(cap, 20)),
                Pos = ClampToArena(p.Pos + V2.FromAngle(p.Aim) * 220f, 40f),
            };
            s.T = Math.Min(1f, s.Every);
            DevSpawners[s.Id] = s;
            BroadcastFx(FxKind.Ring, s.Pos, new Rgba(1f, 0.35f, 0.3f), 60f, 0.5f, Sfx.None);
            DevUsed(p.Name + ": placed a spawner of " + Defs.Get(type).Name + " (every " + s.Every + " s, up to " + s.Cap + ")");
            SendSnapshot();
            return s.Id;
        }

        /// Dev: take one spawner away (its enemies stay). Host only.
        public bool DevRemoveSpawner(int spawnerId)
        {
            if (!IsHost || !DevSpawners.Remove(spawnerId))
                return false;
            SendSnapshot();
            return true;
        }

        /// Dev: take every spawner away. Host only.
        public void DevClearSpawners()
        {
            if (!IsHost || DevSpawners.Count == 0)
                return;
            DevSpawners.Clear();
            DevUsed("spawners cleared");
            SendSnapshot();
        }

        /// Host, each step: while a wave is on, each spawner brings in its enemy on its timer, up to its cap alive.
        void TickDevSpawners(float dt)
        {
            if (DevSpawners.Count == 0)
                return;
            foreach (var s in DevSpawners.Values)
                s.Alive = 0;
            foreach (var e in Enemies.Values)
            {
                DevSpawner s;
                if (e.DevSpawner != 0 && e.Hp > 0f && DevSpawners.TryGetValue(e.DevSpawner, out s))
                    s.Alive++;
            }
            if (State != MatchState.Fighting)
                return;
            foreach (var s in DevSpawners.Values)
            {
                if (s.Alive >= s.Cap)
                {
                    s.T = Math.Max(s.T, 0.25f);  // full: the next one comes a moment after one dies
                    continue;
                }
                s.T -= dt;
                if (s.T > 0f)
                    continue;
                s.T = s.Every;
                var d = Defs.Get(s.Type);
                V2 at = ClampToArena(s.Pos + V2.FromAngle((float)rng.NextDouble() * MathX.Tau) * (d.Radius + 6f), d.Radius + 20f);
                var e = SpawnEnemyAt(s.Type, at, 0.6f, 0, s.Elite);
                e.DevSpawner = s.Id;
                s.Alive++;
                BroadcastFx(FxKind.Ring, s.Pos, new Rgba(1f, 0.35f, 0.3f), 34f, 0.3f, Sfx.None);
            }
        }

        // --- Snapshot ------------------------------------------------------------------------------------------------

        void SyncDevSpawners(NetSync s)
        {
            int count = DevSpawners.Count;
            s.Byte(ref count);
            if (s.Reading)
            {
                snapSeen.Clear();
                for (int i = 0; i < count; i++)
                    snapSeen.Add(SyncDevSpawner(s, null));
                RemoveUnseen(DevSpawners);
            }
            else
                foreach (var sp in DevSpawners.Values)
                    SyncDevSpawner(s, sp);
        }

        int SyncDevSpawner(NetSync s, DevSpawner sp)
        {
            int id = sp != null ? sp.Id : 0, cap = sp != null ? sp.Cap : 0, alive = sp != null ? sp.Alive : 0;
            byte type = sp != null ? (byte)sp.Type : (byte)0;
            bool elite = sp != null && sp.Elite;
            V2 pos = sp != null ? sp.Pos : default(V2);
            float every = sp != null ? sp.Every : 0f, t = sp != null ? sp.T : 0f;
            s.Int(ref id);
            s.Byte(ref type);
            s.Bool(ref elite);
            s.Vec(ref pos);
            s.Float(ref every);
            s.Float(ref t);
            s.Byte(ref cap);
            s.Byte(ref alive);
            if (s.Reading)
            {
                if (!DevSpawners.TryGetValue(id, out sp))
                    DevSpawners[id] = sp = new DevSpawner { Id = id };
                sp.Type = (EnemyType)type;
                sp.Elite = elite;
                sp.Pos = pos;
                sp.Every = every;
                sp.T = t;
                sp.Cap = cap;
                sp.Alive = alive;
            }
            return id;
        }
    }
}
