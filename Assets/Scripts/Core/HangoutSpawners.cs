using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Arena
{
    /// The Hangout's enemy spawners (Docs/Hangout.md): twelve pads round the room's edges, four along the top and the bottom and two
    /// down each side. Each holds one of the regular enemies (every type the Practice picker offers but the bosses). Stand on one and
    /// press Interact (F) to turn it on or off; while you stand on it, the mouse wheel or the discard key (B) changes its enemy. While
    /// it's on it trickles its enemy into the room (one every SpawnerEvery, at most SpawnerMax of its own alive), and they fight like
    /// a wave's. Turning it off takes its enemies away. The host runs them; `Msg.HangoutSpawner` is a player's press,
    /// `Msg.HangoutSpawners` every pad's enemy and switch (sent on a change, and every SpawnerResend for anyone who missed one).
    public partial class Game
    {
        public const int SpawnerCount = 12, SpawnerMax = 5;
        public const float SpawnerEvery = 3f, SpawnerFirst = 0.6f, SpawnerReach = 64f, SpawnerResend = 2f;

        /// What a spawner can hold: the Practice picker's enemies, bosses left out.
        public static EnemyType[] SpawnerChoices => spawnerChoices ?? (spawnerChoices = PracticePlan.Choices.Where(t => !Defs.Get(t).IsBoss).ToArray());
        static EnemyType[] spawnerChoices;  // (built on first use: Game's statics come up before Defs')

        /// What the twelve hold when the Hangout opens: a mix of melee and ranged (any missing from SpawnerChoices falls back to its slot's).
        static readonly EnemyType[] SpawnerStart =
        {
            EnemyType.Grunt, EnemyType.Sniper, EnemyType.Brute, EnemyType.Spinner, EnemyType.Lunger, EnemyType.Mortar,
            EnemyType.Shielder, EnemyType.Swarmer, EnemyType.Duelist, EnemyType.Spitter, EnemyType.Runner, EnemyType.TaserGuard,
        };

        public sealed class HangoutSpawner
        {
            public V2 Pos, Inward;  // its middle; the way into the room
            public int Pick;        // index in SpawnerChoices
            public bool On;
            public float T;         // host: until the next one
            public readonly List<int> Alive = new List<int>();  // host: its enemies still in the room
            public EnemyType Type => SpawnerChoices[Pick];
        }

        public readonly HangoutSpawner[] Spawners = MakeSpawners();
        float spawnerResendT;

        static HangoutSpawner[] MakeSpawners()
        {
            var list = new List<HangoutSpawner>();
            const float edge = 58f, top = 140f, bottom = 195f;  // (the rows clear the HUD: the mode banner at the top, the health, ability and vector panels at the bottom)
            for (int i = 0; i < 4; i++)
            {
                float x = Width * (0.14f + 0.24f * i);
                list.Add(new HangoutSpawner { Pos = new V2(x, top), Inward = new V2(0f, 1f) });
                list.Add(new HangoutSpawner { Pos = new V2(x, Height - bottom), Inward = new V2(0f, -1f) });
            }
            for (int i = 0; i < 2; i++)
            {
                float y = Height * (0.36f + 0.28f * i);
                list.Add(new HangoutSpawner { Pos = new V2(edge, y), Inward = new V2(1f, 0f) });
                list.Add(new HangoutSpawner { Pos = new V2(Width - edge, y), Inward = new V2(-1f, 0f) });
            }
            return list.ToArray();
        }

        /// Every machine, as the Hangout starts: all off, back to their starting enemies.
        void ResetSpawners()
        {
            for (int i = 0; i < Spawners.Length; i++)
            {
                var s = Spawners[i];
                int k = Array.IndexOf(SpawnerChoices, SpawnerStart[i]);
                s.Pick = k >= 0 ? k : i % SpawnerChoices.Length;
                s.On = false;
                s.T = 0f;
                s.Alive.Clear();
            }
            spawnerResendT = 0f;
        }

        /// The spawner you're standing on, or -1 (any machine; the Hangout only).
        public int SpawnerNear(V2 pos)
        {
            if (!HangoutOn)
                return -1;
            int best = -1;
            float bestD = SpawnerReach;
            for (int i = 0; i < Spawners.Length; i++)
            {
                float d = pos.DistanceTo(Spawners[i].Pos);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        /// Your own game: a press on spawner i: 0 turns it on or off, +1 / -1 steps its enemy.
        public void UseSpawner(int i, int action)
        {
            if (i < 0 || i >= Spawners.Length || !HangoutOn)
                return;
            if (IsHost)
            {
                SetSpawner(i, action);
                return;
            }
            var w = Proto.Begin(Msg.HangoutSpawner);
            w.Write((byte)i);
            w.Write((sbyte)Math.Sign(action));
            session.SendToHost(Proto.End(w));
        }

        /// Host: a client's press (only from someone standing by it).
        public void OnHangoutSpawner(int peer, BinaryReader r)
        {
            int i = r.ReadByte();
            int action = r.ReadSByte();
            Player p;
            if (!IsHost || !HangoutOn || i >= Spawners.Length || !Players.TryGetValue(peer, out p) || p.Pos.FartherThan(Spawners[i].Pos, SpawnerReach * 1.5f))
                return;
            SetSpawner(i, action);
        }

        void SetSpawner(int i, int action)
        {
            var s = Spawners[i];
            if (action == 0)
            {
                s.On = !s.On;
                s.T = SpawnerFirst;
                if (!s.On)
                    ClearSpawned(s);
                BroadcastFx(FxKind.Ring, s.Pos, s.On ? new Rgba(1f, 0.3f, 0.3f) : new Rgba(0.6f, 0.6f, 0.65f), 46f, 0.35f, s.On ? Sfx.Pickup : Sfx.None);
            }
            else
            {
                int n = SpawnerChoices.Length;
                s.Pick = ((s.Pick + Math.Sign(action)) % n + n) % n;
                ClearSpawned(s);  // (what it made before goes with the old enemy)
                s.T = SpawnerFirst;
            }
            SendSpawners();
        }

        void ClearSpawned(HangoutSpawner s)
        {
            foreach (int id in s.Alive)
            {
                Enemy e;
                if (Enemies.TryGetValue(id, out e))
                {
                    BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, e.Radius + 10f, 0.3f, Sfx.None);
                    Enemies.Remove(id);
                }
            }
            s.Alive.Clear();
        }

        /// Host, every Hangout tick: each one that's on sends its next enemy in.
        void TickSpawners(float dt)
        {
            if ((spawnerResendT -= dt) <= 0f)
                SendSpawners();
            foreach (var s in Spawners)
            {
                s.Alive.RemoveAll(id => !Enemies.ContainsKey(id));
                if (!s.On || (s.T -= dt) > 0f)
                    continue;
                s.T = SpawnerEvery;
                if (s.Alive.Count >= SpawnerMax)
                    continue;
                int first = nextEnemyId;
                var d = Defs.Get(s.Type);
                SpawnEnemyAt(s.Type, ClampToArena(s.Pos + s.Inward * (40f + d.Radius), d.Radius + 10f), 0.8f, 0);
                for (int id = first; id < nextEnemyId; id++)
                    if (Enemies.ContainsKey(id))
                        s.Alive.Add(id);  // (with any partner it brings: Bound Wraiths and Pylons come in pairs)
            }
        }

        void SendSpawners()
        {
            spawnerResendT = SpawnerResend;
            var w = Proto.Begin(Msg.HangoutSpawners);
            w.Write((byte)Spawners.Length);
            foreach (var s in Spawners)
            {
                w.Write((byte)s.Pick);
                w.Write(s.On);
            }
            session.Broadcast(Proto.End(w));
        }

        /// Clients: every pad's enemy and switch.
        void ReadSpawners(BinaryReader r)
        {
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                int pick = r.ReadByte();
                bool on = r.ReadBoolean();
                if (IsHost || i >= Spawners.Length)
                    continue;
                Spawners[i].Pick = Math.Min(pick, SpawnerChoices.Length - 1);
                Spawners[i].On = on;
            }
        }
    }
}
