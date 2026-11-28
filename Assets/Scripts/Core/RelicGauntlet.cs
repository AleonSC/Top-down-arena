using System;
using System.IO;
using System.Collections.Generic;

namespace Arena
{
    /// A laser beam, as a segment in world px with a half width (the red line a gauntlet's emitters throw).
    public struct RelicBeam
    {
        public V2 A, B;
        public float Half;
    }

    /// The Relic Gauntlet (Docs/MysteryDoors.md, owner Nov 21): a mystery door's 3 cell hallway. Its middle cell holds a faction relic on a
    /// pedestal, its two outer cells are hazard cells (void pits, room turrets and moving lasers), its far end holds a chest. The room is
    /// marked by `Surprise == 1 + SurpriseShape.Gauntlet`, so every machine derives the pits, turrets and lasers from the room alone (no wire data,
    /// but a clock: `Game.RelicGauntletClock`). Lasers hurt whoever touches them (`TickRelicGauntlets`, host). Look: Game/GauntletLook.cs.
    /// Layout in the room's own units: u along the hallway (0-1800), v across it (0-600); the near cell is 0-600, the relic cell 600-1200, the far cell 1200-1800.
    public static class RelicGauntlet
    {
        public const int Length = 1800, Width = 600;
        public const float SweepPeriod = 4.4f, SpinPeriod = 7f;  // a sweep rail's there-and-back, the spinner's full turn (s)
        public const float BeamHalf = 6f, SpinArm = 270f;
        public const float HurtEvery = 0.7f;                     // a player touched by a beam is hurt at most this often
        public static float LaserDamage(int wave) => 10f + 0.6f * Math.Max(0, wave);

        public static byte Mark => (byte)(1 + (int)SurpriseShape.Gauntlet);

        public static bool Is(ExpansionRoom r) => r.Surprise == Mark && !r.Floating && Math.Max(r.W, r.H) == Length && Math.Min(r.W, r.H) == Width;

        // The void pits (u, v, length, width): the near cell keeps a middle lane for the lasers, the far cell is a slalom round the spinner.
        static readonly int[][] PitBoxes =
        {
            new[] { 150, 50, 150, 150 }, new[] { 300, 400, 150, 150 }, new[] { 450, 50, 100, 100 }, new[] { 50, 450, 100, 100 },  // (on the 50 px floor grid)
            new[] { 1250, 450, 150, 100 }, new[] { 1500, 50, 150, 100 }, new[] { 1650, 400, 100, 150 }, new[] { 1250, 50, 100, 100 },
        };

        // The turret posts (u, v): wall mounts in the outer cells.
        static readonly int[][] TurretPosts = { new[] { 400, 36 }, new[] { 200, 564 }, new[] { 1400, 36 }, new[] { 1560, 564 } };

        static bool Horizontal(ExpansionRoom r) => r.W > r.H;

        /// Room units to world px.
        public static V2 ToWorld(ExpansionRoom r, float u, float v) => Horizontal(r) ? new V2(r.X + u, r.Y + v) : new V2(r.X + v, r.Y + u);

        /// A world point in the room's units.
        public static V2 ToLocal(ExpansionRoom r, V2 p) => Horizontal(r) ? new V2(p.X - r.X, p.Y - r.Y) : new V2(p.Y - r.Y, p.X - r.X);

        /// The relic's spot: the middle of the middle cell.
        public static V2 RelicSpot(ExpansionRoom r) => ToWorld(r, Length / 2f, Width / 2f);

        /// The relic a gauntlet holds: any faction relic, seeded from its spot (the same on every machine).
        public static RelicId RelicFor(int seed, ExpansionRoom r)
        {
            int n = (int)RelicId.Thorns - Relics.Classic;
            return (RelicId)(Relics.Classic + new DetRandom(seed * 6151 + r.X * 31 + r.Y * 17 + 5).Int(n));
        }

        public static void AddPits(List<Solid> to, ExpansionRoom r)
        {
            foreach (var p in PitBoxes)
            {
                V2 a = ToWorld(r, p[0], p[1]), b = ToWorld(r, p[0] + p[2], p[1] + p[3]);
                to.Add(World.Box(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
            }
        }

        public static void AddTurrets(List<RoomTrap> to, ExpansionRoom r)
        {
            foreach (var p in TurretPosts)
                to.Add(new RoomTrap { Pos = ToWorld(r, p[0], p[1]), Kind = TrapKind.Turret });
        }

        static float Tri(float t, float period)
        {
            float k = MathX.Fmod(t, period) / period;
            return k < 0.5f ? k * 2f : 2f - k * 2f;
        }

        /// The beams of one gauntlet at clock time t: two sweep rails in anti-phase across the near cell, a two armed spinner in the far cell.
        public static void Beams(ExpansionRoom r, float t, List<RelicBeam> to)
        {
            float a = 90f + 420f * Tri(t, SweepPeriod), b = 600f - a;
            to.Add(new RelicBeam { A = ToWorld(r, a, 0f), B = ToWorld(r, a, Width), Half = BeamHalf });
            to.Add(new RelicBeam { A = ToWorld(r, b, 0f), B = ToWorld(r, b, Width), Half = BeamHalf });
            float ang = t / SpinPeriod * MathX.Tau;
            V2 c = ToWorld(r, 1500f, 300f);
            V2 arm = new V2((float)Math.Cos(ang), (float)Math.Sin(ang)) * SpinArm;
            to.Add(new RelicBeam { A = c - arm, B = c + arm, Half = BeamHalf });
        }

        /// The rail posts and the spinner's hub, for the look: (a, b) pairs of emitter ends and the hub.
        public static V2 Hub(ExpansionRoom r) => ToWorld(r, 1500f, 300f);

    }

    public partial class Game
    {
        float relicGauntletOffset;                       // a client's clock minus the host's (set from Msg.ExpandState)
        readonly Dictionary<int, float> relicGauntletCd = new Dictionary<int, float>();
        readonly List<RelicBeam> beamBuf = new List<RelicBeam>();

        /// The lasers' clock: the host's, kept on every machine (a client's own clock plus the offset the last map message gave it).
        public float RelicGauntletClock => IsHost ? hostClock : AnimT + relicGauntletOffset;

        /// The gauntlets on the map.
        public IEnumerable<ExpansionRoom> RelicGauntletRooms()
        {
            foreach (var r in Expanded)
                if (RelicGauntlet.Is(r))
                    yield return r;
        }

        void WriteRelicGauntletClock(BinaryWriter w) => w.Write(hostClock);

        void ReadRelicGauntletClock(BinaryReader r)
        {
            float host = r.ReadSingle();
            if (!IsHost)
                relicGauntletOffset = host - AnimT;
        }

        /// Host, every tick: a player touching a laser is hurt (at most every RelicGauntlet.HurtEvery). A dash's i-frames are the damage rules' own.
        void TickRelicGauntlets(float dt)
        {
            if (!IsHost || !ExpandingOn || Expanded.Count == 0)
                return;
            var ids = new List<int>(relicGauntletCd.Keys);
            foreach (int id in ids)
                relicGauntletCd[id] = Math.Max(0f, relicGauntletCd[id] - dt);
            foreach (var room in RelicGauntletRooms())
            {
                beamBuf.Clear();
                RelicGauntlet.Beams(room, RelicGauntletClock, beamBuf);
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.Pos.X < room.X || p.Pos.X > room.X1 || p.Pos.Y < room.Y || p.Pos.Y > room.Y1)
                        continue;
                    relicGauntletCd.TryGetValue(p.Id, out float cd);
                    if (cd > 0f)
                        continue;
                    foreach (var b in beamBuf)
                        if (p.Pos.DistanceToSegment(b.A, b.B) < b.Half + p.Def.HitR * 0.5f)
                        {
                            Damage(p, RelicGauntlet.LaserDamage(Wave), "Laser");
                            relicGauntletCd[p.Id] = RelicGauntlet.HurtEvery;
                            break;
                        }
                }
            }
        }
    }
}
