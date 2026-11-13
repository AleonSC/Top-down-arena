using System;
using System.Collections.Generic;

namespace Arena
{
    /// How a bullet looks (sent with its spawn): what it's going to do, at a glance.
    public enum ShotLook : byte
    {
        Round = 0,     // rings, sprays, spirals: round
        Dart = 1,      // aimed at a player: a dart pointing where it flies
        Crescent = 2,  // curving: a crescent bowed the way it turns
    }

    /// Keeping the regular waves readable rather than chaotic:
    ///   Taking turns: only so many regular shooters fire at once (ShooterLimit). Each gets a turn (ShooterTurn), then
    ///     rests (ShooterRest) while the one that has waited longest takes over; the rest reposition and hold fire.
    ///   A wind-up: a regular shooter glows for WindUp seconds before each volley (Enemy.Winding, replicated).
    ///   Shapes: an enemy bullet aimed at a player is a dart, a curving one a crescent, a ring's a round shot.
    /// Bosses aren't limited (their patterns are their fight); the ring attacks were made slower and sparser in their
    /// own code (Burster, Charger, Phantom, Mortar bombs, Volatile, Death Throes).
    public partial class Game
    {
        public const float ShooterTurn = 5f, ShooterRest = 1.5f, WindUp = 0.3f;
        /// How long a type glows and shows its aim before it fires (a Taser Guard charges longer: its bolt stuns).
        public static float WindUpOf(EnemyType t) => HiveKin.Of(t) == EnemyType.TaserGuard ? TaserWindUp : HiveKin.Of(t) == EnemyType.SecurityGuard ? SecurityWindUp : HiveKin.Of(t) == EnemyType.GasGrenadier ? GrenadierWindUp + GrenadierSwing : t == EnemyType.Lamplighter ? LampWind : t == EnemyType.Mourner ? MournWind
            : t == EnemyType.Orbiter ? OrbiterWindUp : t == EnemyType.Graviton ? GravitonWindUp : t == EnemyType.CoronaPriest ? PriestWindUp : t == EnemyType.Sickle ? SickleWindUp : WindUp;

        /// How many regular enemies may shoot at once: 4, one more every 6 waves up to 8, a quarter more for each extra
        /// player, times the difficulty's Shooters at once.
        public int ShooterLimit => Math.Max(1, (int)Math.Round(Math.Min(8f, 4f + Wave / 6f) * (1f + 0.25f * (Math.Max(1, Players.Count) - 1)) * D(DiffKnob.Shooters)));

        /// Does this enemy need a turn to shoot? Regular shooters do; bosses, melee and things that never fire don't.
        static bool TakesTurns(Enemy e) => e.Kin != EnemyType.SecurityGuard && !e.Def.IsBoss && !e.Def.Melee && e.FireT < 50f && !Challenges.Own(e.Type) && !Zombies.Is(e.Type);  // (a room's mini-boss and shadows fire freely; a Spitter's glob isn't a bullet)

        readonly List<Enemy> turnScratch = new List<Enemy>();

        /// Host, once a tick before the enemies act: turns end and pass to whoever has waited longest.
        void AssignShooterTurns(float dt)
        {
            int holding = 0;
            turnScratch.Clear();
            foreach (var e in Enemies.Values)
            {
                if (e.ShootT > 0f)
                {
                    e.ShootT -= dt;
                    // A turn ends on time, but not halfway through a Leader's aim.
                    bool busy = e.Type == EnemyType.Leader && e.Phase == 1;
                    if ((e.ShootT <= 0f && !busy) || e.Hp <= 0f || !TakesTurns(e))
                    {
                        e.ShootT = 0f;
                        e.ShootRest = ShooterRest;
                        e.ShootWait = 0f;
                    }
                    else
                    {
                        e.ShootT = Math.Max(e.ShootT, 0.01f);
                        holding++;
                        continue;
                    }
                }
                e.ShootRest -= dt;
                if (TakesTurns(e) && Targetable(e))
                {
                    e.ShootWait += dt;
                    if (e.ShootRest <= 0f)
                        turnScratch.Add(e);
                }
            }
            int free = ShooterLimit - holding;
            if (free <= 0 || turnScratch.Count == 0)
                return;
            turnScratch.Sort((a, b) => b.ShootWait.CompareTo(a.ShootWait));
            for (int i = 0; i < free && i < turnScratch.Count; i++)
                turnScratch[i].ShootT = ShooterTurn;
        }

        /// May this enemy fire its volley now?
        static bool HasTurn(Enemy e) => !TakesTurns(e) || e.ShootT > 0f;

        public const int RingVolley = 6;  // this many shots from one enemy in one tick is a ring or a spray: all round

        readonly List<Bullet> volley = new List<Bullet>();
        int volleyEnemy = -1, volleyTick = -1;

        /// Host: shots one enemy fires in the same tick. Once there are RingVolley of them, they're a ring or a spray, so
        /// every one is round, even those that happen to point at a player.
        void NoteVolley(Enemy e, Bullet b)
        {
            if (e.Id != volleyEnemy || tick != volleyTick)
            {
                volley.Clear();
                volleyEnemy = e.Id;
                volleyTick = tick;
            }
            volley.Add(b);
            if (volley.Count < RingVolley)
                return;
            foreach (var v in volley)
                if (v.Look == ShotLook.Dart)
                    v.Look = v.Sent.Look = ShotLook.Round;
        }

        /// The shape for an enemy shot: curving is a crescent; aimed within about 30° of its target, a dart; else round.
        ShotLook LookFor(Enemy e, float ang, float angVel)
        {
            if (angVel != 0f)
                return ShotLook.Crescent;
            Player p;
            if (!Players.TryGetValue(e.TargetId, out p))
                return ShotLook.Round;
            float off = Math.Abs(MathX.Fmod((p.Pos - e.Pos).Angle - ang + MathX.Pi, MathX.Tau) - MathX.Pi);
            return off < 0.5f ? ShotLook.Dart : ShotLook.Round;
        }
    }
}
