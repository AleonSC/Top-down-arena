using System;
using System.Collections.Generic;

namespace Arena
{
    /// Lightning Rod: the Tank Elementalist's second ultimate (lobby key 1 of the Ultimate row; Glacial Tomb stays the
    /// default). Docs/Mockups/lightning-rod.html. Host only, except Player.RodT, which is replicated for the look
    /// (Game/LightningRodLook.cs).
    ///
    /// For RodTime (6 s, 8 s mastered) the tank crackles with charge: it takes RodCut (80%) less damage and every enemy,
    /// bosses too, goes for it (the taunt path: PickTarget and each enemy's TargetId, as Roar does, but without Roar's own
    /// damage cut). Every enemy hit that lands on it calls a bolt from the sky:
    ///   - an enemy bullet: on the enemy that fired it (Bullet.ShooterId, stamped by SpawnBullet while that enemy is acting;
    ///     a shot fired by nobody known strikes the enemy nearest to where it was fired from). If the shooter has died
    ///     since, the bolt still lands where it stood and arcs from there;
    ///   - a melee or contact hit (a body, the Swordsman's sword, a Reaver's swipe, a Riot Shield's bash, the Devourer's
    ///     bite, a Reaper's reap): on the enemy that hit.
    /// A bolt does RodBoltDamage (30) on the struck enemy, then arcs to the RodArcs (2, 3 mastered) nearest enemies within
    /// RodArcReach of it for RodArcDamage (15) each, all scaled by the tank's damage multiplier. It counts as lightning:
    /// wet enemies (Tsunami, Bullet Rain) take the usual extra arcs from where it lands (Tsunami.cs WetArcs).
    /// The cap: each enemy can call at most one bolt every RodBoltEvery (0.25 s), so a shotgun volley calls one, not five;
    /// the other pellets are still grounded (cut by 80%). Arcs are not capped.
    /// While the charge is up a shot that touches the tank during its hit grace is grounded too (it is used up and calls a
    /// bolt, but does no damage), so the more they shoot, the more bolts fall.
    /// It ends when the time runs out, or at once if the tank goes down.
    public partial class Game
    {
        public const float RodTime = 6f, RodTimeMastered = 8f;   // seconds of charge
        public const float RodCut = 0.8f;                         // damage you take while it's up is cut by this much
        public const float RodBoltDamage = 30f, RodArcDamage = 15f;  // the bolt on the shooter; each arc on from it
        public const int RodArcCount = 2, RodArcCountMastered = 3;
        public const float RodArcReach = 320f;                    // how far an arc can jump from the struck enemy
        public const float RodBoltEvery = 0.25f;                  // one bolt per enemy this often, at most
        public const float RodGuessReach = 160f;                  // a shot with no known shooter: the nearest enemy this close to where it was fired from
        public const float RodBoltFx = 0.55f, RodArcFx = 0.32f, RodGroundFx = 0.9f;  // how long each effect lives (the look reads its age)
        public const float RodDemoShotEvery = 0.42f;              // the lobby preview: a dummy fires at you this often while the charge is up (Showcase.cs)
        public const int RodBotShooters = 3;                      // a bot casts it when this many enemies have shots coming at it
        public static readonly Rgba RodBlue = new Rgba(0.59f, 0.78f, 1f);

        /// Lightning Rod's entry: key 1 of the tank Elementalist's Ultimate row (AbilityDef.UltKey). Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] SecondUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.LightningRod, Kit = Kit.Elementalist, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Lightning Rod", Cooldown = 70f,
                Desc = "For 6 s you crackle with charge: you take 80% less damage and every enemy, bosses too, goes for you. Each enemy hit on you calls a bolt from the sky on whoever landed it (30), which arcs on to the 2 enemies nearest it (15 each). Wet enemies take extra arcs.",
                Capstone = "Lasts 8 s and each bolt arcs to 3 enemies.",
                Bot = b => b.Shooters >= RodBotShooters,
            },
        };

        /// Host: enemy hits that landed on a charged player this tick; their bolts fall in TickRods (after the bullets and
        /// enemies have moved, so nothing is changed while those lists are being walked).
        struct RodCall
        {
            public int Player, Shooter;  // Shooter: the enemy's id, 0 when not known
            public V2 From;              // where the shooter stood (a bullet: when it fired)
            public float Taken;          // what the hit took off the player, for the grey number
            public bool Shot;            // a bullet (otherwise melee or contact)
        }
        readonly List<RodCall> rodCalls = new List<RodCall>();
        readonly Dictionary<int, float> rodCap = new Dictionary<int, float>();  // enemy id -> seconds until it can call another bolt
        readonly List<int> rodCapDone = new List<int>();
        Enemy actingEnemy;   // host: the enemy whose turn it is in TickEnemies (SpawnBullet stamps its shots with it)
        float lastTaken;     // host: what the last Damage call took off its player (0 when nothing got through)

        void CastLightningRod(Player p, bool mastered)
        {
            float time = mastered ? RodTimeMastered : RodTime;
            p.RodT = time;
            p.UltT = time;
            p.RodArcs = mastered ? RodArcCountMastered : RodArcCount;
            foreach (var e in Enemies.Values)  // the taunt: as Roar, everyone (bosses too) turns on you for the whole charge
            {
                e.TargetId = p.Id;
                e.RetargetT = time;
            }
            BroadcastFx(FxKind.Ring, p.Pos, RodBlue, 110f, 0.45f, Sfx.SpellZap);
        }

        /// Damage(): the cut while the charge is up.
        static float RodMult(Player p) => p.RodT > 0f ? 1f - RodCut : 1f;

        /// Combat.cs: an enemy bullet reached a charged player (taken: what it took off them, 0 in the hit grace).
        void RodShot(Player p, Bullet b, float taken)
        {
            rodCalls.Add(new RodCall { Player = p.Id, Shooter = b.ShooterId, From = b.ShooterPos, Taken = taken, Shot = true });
        }

        /// A melee or contact hit from e just landed on p (call after its Damage). Does nothing unless p is charged.
        void RodTouched(Player p, Enemy e)
        {
            if (p.RodT <= 0f || e == null)
                return;
            rodCalls.Add(new RodCall { Player = p.Id, Shooter = e.Id, From = e.Pos, Taken = lastTaken, Shot = false });
        }

        /// Host, every tick (after Collide): the charge running down, the cap clearing, and this tick's bolts falling.
        void TickRods(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.RodT <= 0f)
                    continue;
                p.RodT = p.Downed ? 0f : Math.Max(0f, p.RodT - dt);
                if (p.RodT <= 0f)
                {
                    if (p.Downed)
                        p.UltT = 0f;
                    foreach (var e in Enemies.Values)  // it's over: they pick their own targets again
                        if (e.TargetId == p.Id)
                            e.RetargetT = 0f;
                }
            }
            if (rodCap.Count > 0)
            {
                rodCapDone.Clear();
                var keys = new List<int>(rodCap.Keys);
                foreach (int k in keys)
                {
                    float left = rodCap[k] - dt;
                    if (left <= 0f)
                        rodCapDone.Add(k);
                    else
                        rodCap[k] = left;
                }
                foreach (int k in rodCapDone)
                    rodCap.Remove(k);
            }
            if (rodCalls.Count == 0)
                return;
            var calls = new List<RodCall>(rodCalls);
            rodCalls.Clear();
            foreach (var c in calls)
            {
                Player p;
                if (!Players.TryGetValue(c.Player, out p))
                    continue;
                BroadcastFx(FxKind.RodGround, p.Pos, new V2(p.Id, c.Taken), RodBlue, c.Shot ? 1f : 2f, RodGroundFx, Sfx.None);
                if (p.Downed)
                    continue;  // (the hit that put them down: the charge is gone with them)
                Enemy shooter = null;
                V2 at = c.From;
                if (c.Shooter != 0 && Enemies.TryGetValue(c.Shooter, out shooter) && shooter.Hp > 0f)
                    at = shooter.Pos;
                else if (c.Shooter == 0)
                {
                    shooter = NearestEnemy(c.From, RodGuessReach);  // fired by nobody known (a hazard, a ring off a bomb): the nearest enemy to where it came from (Minions.cs)
                    if (shooter != null && shooter.Hp > 0f)
                        at = shooter.Pos;
                    else
                        shooter = null;
                }
                else
                    shooter = null;  // it has died since it fired: the bolt lands where it stood
                int key = shooter != null ? shooter.Id : c.Shooter;
                if (key == 0 && shooter == null)
                    continue;  // nobody to strike
                if (rodCap.ContainsKey(key))
                    continue;  // it called one less than RodBoltEvery ago
                rodCap[key] = RodBoltEvery;
                RodBolt(p, shooter, at);
            }
        }

        /// One bolt from the sky at `at` (on `struck` when it's still alive), then its arcs.
        void RodBolt(Player p, Enemy struck, V2 at)
        {
            float mult = p.Stats.DmgMult;
            float bolt = RodBoltDamage * mult, arc = RodArcDamage * mult;
            bool alive = struck != null && Targetable(struck);
            BroadcastFx(FxKind.RodBolt, at, new V2(p.Id, bolt), RodBlue, alive ? struck.Radius : 0f, RodBoltFx, Sfx.SpellZap);
            var near = new List<Enemy>();
            foreach (var e in Enemies.Values)
                if (e != struck && Targetable(e) && !DevourUnder(e) && !GutwormUnder(e) && e.Pos.DistanceTo(at) < RodArcReach + e.Radius)
                    near.Add(e);
            near.Sort((a, b) => a.Pos.DistanceSq(at).CompareTo(b.Pos.DistanceSq(at)));
            if (alive)
                HurtEnemy(struck, bolt, p.Id);
            WetArcs(at, struck, bolt, p.Id);  // lightning: every wet enemy takes its share (Tsunami.cs)
            int arcs = Math.Max(RodArcCount, p.RodArcs);
            for (int i = 0; i < near.Count && i < arcs; i++)
            {
                if (near[i].Hp <= 0f)
                    continue;  // (a wet arc just finished it)
                BroadcastFx(FxKind.RodArc, at, near[i].Pos, RodBlue, arc, RodArcFx, i == 0 ? Sfx.SpellChainArc : Sfx.None);
                HurtEnemy(near[i], arc, p.Id);
            }
        }

        /// The bot's sense for it: how many different enemies have shots in flight coming at this player (within 520 px).
        int ShootersAt(Player me)
        {
            var seen = new HashSet<int>();
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind))
                    continue;
                V2 to = me.Pos - b.Pos;
                float d = to.Length;
                if (d > 520f || d < 1f || b.Vel.Length < 1f)
                    continue;
                if (to.Dot(b.Vel) / (d * b.Vel.Length) > 0.9f)
                    seen.Add(b.ShooterId != 0 ? b.ShooterId : -b.Id);
            }
            return seen.Count;
        }

        /// The lobby preview: dummy number k (in turn) fires one small shot at you, so a Lightning Rod demo has hits to ground.
        public void ShowcaseDummyShot(int k)
        {
            if (dummyIds.Count == 0 || Me == null)
                return;
            Enemy e;
            int id = dummyIds[((k % dummyIds.Count) + dummyIds.Count) % dummyIds.Count];
            if (id == 0 || !Enemies.TryGetValue(id, out e))
                return;
            V2 dir = (Me.Pos - e.Pos).Normalized();
            actingEnemy = e;
            SpawnBullet(Defs.SmallShot, e.Pos + dir * (e.Radius + 4f), dir * 260f, 0f, 3f, Defs.BulletDamage(Defs.SmallShot), 0).Source = e.Type;
            actingEnemy = null;
        }
    }
}
