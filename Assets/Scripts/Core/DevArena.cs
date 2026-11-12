using System;
using System.Collections.Generic;

namespace Arena
{
    /// The F1 dock's Dev Arena: a sandbox match (no waves arrive, the wave never clears, everyone in god mode) where you
    /// spawn enemies by hand and can take one over ("play as") to try its attacks yourself: WASD moves it, the mouse is
    /// what it goes for (its own AI aims, lunges and charges at the cursor), Fire attacks on its real cadence, Parry
    /// attacks at once (skipping the cooldown), the role ability lets go. Host only and local: the possessed enemy syncs
    /// like any enemy, so nothing new goes over the network. Hooked into TickEnemies (EnemyAI.cs) and the Fighting
    /// spawner (Waves.cs); drawn by Game/DevArenaUi.cs.
    public partial class Game
    {
        /// Host: the sandbox is on (set after the match starts; HostStart's reset turns it off).
        public bool DevArena;

        /// Host: the enemy being played (its id), or -1.
        public int PossessId = -1;

        V2 possessMove, possessAim;
        bool possessAttack, possessNow;

        /// The enemy being played, or null (a dead one lets go by itself).
        public Enemy Possessed
        {
            get
            {
                Enemy e;
                if (PossessId < 0)
                    return null;
                if (!Enemies.TryGetValue(PossessId, out e) || e.Hp <= 0f)
                {
                    PossessId = -1;
                    return null;
                }
                return e;
            }
        }

        public void DevSetArena(bool on)
        {
            if (!IsHost || State == MatchState.Idle || DevArena == on)
                return;
            DevArena = on;
            DevUsed("dev arena " + (on ? "on" : "off"));
            HeroDummies.Clear();
            if (!on)
            {
                PossessId = -1;
                return;
            }
            DevFreeze = false;
            DevClearArena();
            foreach (var p in Players.Values)
                DevSetGod(p.Id, true);
        }

        /// Dev Arena: every enemy, boss, hazard and enemy shot gone; downed players back up.
        public void DevClearArena()
        {
            if (!IsHost || State == MatchState.Idle)
                return;
            PossessId = -1;
            spawnQueue.Clear();
            squadQueue.Clear();
            Enemies.Clear();
            ResetBossState();
            Zones.Clear();
            Hazards.Clear();
            Cages.Clear();
            ClearEnemyBullets();
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            foreach (var p in Players.Values)
                if (p.Downed)
                    RevivePlayer(p, 1f);
        }

        /// Dev Arena: spawn one of this enemy in front of player `id` and play as it. True when it worked.
        public bool DevPossess(int id, EnemyType type)
        {
            if (!IsHost || !DevArena || State != MatchState.Fighting)
                return false;
            var had = new System.Collections.Generic.HashSet<int>(Enemies.Keys);
            if (DevSpawnEnemy(id, type, 1, false) == 0)
                return false;
            Enemy pick = null;
            foreach (var e in Enemies.Values)
                if (!had.Contains(e.Id) && e.Type == type)
                {
                    pick = e;
                    break;
                }
            if (pick == null)
                return false;
            PossessId = pick.Id;
            pick.SpawnT = Math.Min(pick.SpawnT, 0.3f);
            possessAim = pick.Pos;
            possessMove = V2.Zero;
            possessAttack = possessNow = false;
            DevUsed("playing as " + pick.Def.Name);
            return true;
        }

        public void DevRelease()
        {
            if (PossessId >= 0)
                DevUsed("let go of the enemy");
            PossessId = -1;
        }

        /// The host's controls for the enemy being played, once a fixed step (ArenaApp.FixedUpdate). `now` is a press.
        public void SetPossessInput(V2 move, V2 aimAt, bool attack, bool now)
        {
            possessMove = move.LimitLength(1f);
            possessAim = aimAt;
            possessAttack = attack;
            possessNow |= now;
        }

        // --- Hero dummies and Can't die -------------------------------------------------------------------------

        /// Dev Arena: the enemy you play can't die (its health refills; a killing blow leaves it at 1). On by default.
        public bool PossessUnkillable = true;

        /// Dev Arena: hero dummies draw every other enemy's aim (else enemies go for the players as usual).
        public bool DummiesDrawAim = true;

        public const int MaxHeroDummies = 6;
        public const float HeroDummyR = 18f;         // its body, about a hero's hit size
        const float HeroDummyContactEvery = 0.5f;    // one touch per enemy body this often, like a hero's hit grace
        const float HeroDummyDpsWindow = 3f;

        /// A stand-in hero for the Dev Arena (host only, not sent: only the host sees them). Enemy shots and touches land on it
        /// and are counted at what an unbuffed hero would take; other enemies aim at the nearest one.
        public class HeroDummy
        {
            public V2 Pos;
            public float Taken, Last, Flash, ContactT;
            public int Hits;
            public readonly List<KeyValuePair<float, float>> Recent = new List<KeyValuePair<float, float>>();  // (clock, damage)
        }

        public readonly List<HeroDummy> HeroDummies = new List<HeroDummy>();
        public float DevClock;

        /// Damage per second over the last few seconds.
        public float HeroDummyDps(HeroDummy d)
        {
            float sum = 0f;
            foreach (var kv in d.Recent)
                sum += kv.Value;
            return sum / HeroDummyDpsWindow;
        }

        public bool DevAddHeroDummy(V2 at)
        {
            if (!IsHost || !DevArena || HeroDummies.Count >= MaxHeroDummies)
                return false;
            HeroDummies.Add(new HeroDummy { Pos = ClampToArena(at, HeroDummyR + 10f) });
            AddFx(FxKind.Ring, HeroDummies[HeroDummies.Count - 1].Pos, new Rgba(1f, 0.85f, 0.4f), 40f, 0.35f);
            return true;
        }

        /// Takes away the dummy nearest `at` (within reach), or every dummy when `all`.
        public void DevRemoveHeroDummy(V2 at, bool all = false)
        {
            if (all)
            {
                HeroDummies.Clear();
                return;
            }
            int best = -1;
            float bestD = 120f * 120f;
            for (int i = 0; i < HeroDummies.Count; i++)
            {
                float d = HeroDummies[i].Pos.DistanceSq(at);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            if (best >= 0)
                HeroDummies.RemoveAt(best);
        }

        /// The start of TickEnemies, in the Dev Arena: the clock, the dummies' flashes and DPS windows, and the enemy you play
        /// healed up when it can't die.
        void TickDevArena(float dt)
        {
            if (!DevArena)
                return;
            DevClock += dt;
            foreach (var d in HeroDummies)
            {
                d.Flash = Math.Max(0f, d.Flash - dt);
                d.ContactT = Math.Max(0f, d.ContactT - dt);
                d.Recent.RemoveAll(kv => kv.Key < DevClock - HeroDummyDpsWindow);
            }
            var e = Possessed;
            if (e != null && PossessUnkillable)
                e.Hp = e.MaxHp;
        }

        void HeroDummyHit(HeroDummy d, float dmg)
        {
            float amount = dmg * EnemyDamageScale(Math.Max(1, Wave)) * RoomsDamageMult;  // what an unbuffed hero would take (Damage in Combat.cs)
            d.Taken += amount;
            d.Last = amount;
            d.Hits++;
            d.Flash = 0.18f;
            d.Recent.Add(new KeyValuePair<float, float>(DevClock, amount));
        }

        /// MoveBullets: an enemy shot reaching a hero dummy lands on it (and is used up). True when it hit one.
        bool HeroDummyShot(Bullet b)
        {
            if (!DevArena || HeroDummies.Count == 0)
                return false;
            foreach (var d in HeroDummies)
            {
                float r = HeroDummyR + b.Radius;
                if (b.Pos.DistanceSq(d.Pos) < r * r)
                {
                    HeroDummyHit(d, b.Dmg);
                    return true;
                }
            }
            return false;
        }

        /// TickEnemies: an enemy's body touching a hero dummy hurts it, at most once per HeroDummyContactEvery.
        void HeroDummyContact(Enemy e, float contact)
        {
            if (!DevArena || HeroDummies.Count == 0 || contact <= 0f)
                return;
            foreach (var d in HeroDummies)
                if (d.ContactT <= 0f && d.Pos.DistanceTo(e.Pos) < e.Radius + HeroDummyR)
                {
                    d.ContactT = HeroDummyContactEvery;
                    HeroDummyHit(d, contact);
                }
        }

        /// TickEnemies: where an enemy (not the one you play) aims when hero dummies draw its aim.
        bool HeroDummyAim(Enemy e, ref V2 tpos)
        {
            if (!DevArena || !DummiesDrawAim || HeroDummies.Count == 0)
                return false;
            HeroDummy best = null;
            foreach (var d in HeroDummies)
                if (best == null || d.Pos.DistanceSq(e.Pos) < best.Pos.DistanceSq(e.Pos))
                    best = d;
            tpos = best.Pos;
            return true;
        }

        /// HurtEnemy: a killing blow on the enemy you play leaves it at 1 when it can't die.
        void PossessSurvives(Enemy e)
        {
            if (e.Hp <= 0f && PossessUnkillable && DevArena && e.Id == PossessId)
                e.Hp = 1f;
        }

        /// TickEnemies: is this the enemy being played?
        bool IsPossessed(Enemy e) => e.Id == PossessId && DevArena;

        /// TickEnemies, for the enemy being played: its AI attacks this tick (held Fire, or an "attack now" press).
        bool PossessAttacking => possessAttack || possessNow;

        /// Moves the enemy being played: your keys when you steer; otherwise its own movement (a lunge, a charge,
        /// keeping its range from the cursor) while it attacks; otherwise it stands still.
        void PossessMoveEnemy(Enemy e, V2 tpos, float dt)
        {
            if (possessMove.Length > 0.05f)
            {
                float speed = Math.Max(60f, e.Speed * BruteSpeedMult(e));
                if (e.SlowT > 0f && !Heavy(e))
                    speed *= e.SlowMult;
                e.Pos = ClampToArena(e.Pos + possessMove * speed * dt, e.Radius);
                if ((Props.Count > 0 || LayoutOn) && !e.Def.IsBoss)
                    e.Pos = PushOutOfProps(e.Pos, e.Radius * 0.8f);
            }
            else if (PossessAttacking)
                MoveEnemy(e, tpos, dt);
        }

        /// TickEnemies, before the enemy being played counts down to its next attack: an "attack now" press skips the
        /// cooldown (and a melee enemy's rest between lunges).
        void PossessFireNow(Enemy e)
        {
            if (!possessNow)
                return;
            possessNow = false;
            e.FireT = Math.Min(e.FireT, 0f);
            if (e.Phase == 0)
                e.PhaseT = Math.Min(e.PhaseT, 0f);
        }
    }
}
