using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Juggernaut Suit (Tank Soldier, the second ultimate: `AbilityId.JuggernautSuit`, picked on the lobby's Ultimate row), from the
    /// second Tank > Soldier card of Docs/Mockups/ultimate-ideas.html. Steel plates clamp onto you (the first JuggClamp s are the look's
    /// bolting-on; the armour is up from the press) and for JuggTime s you carry JuggArmor armour (mastered: JuggArmorMastered). The
    /// armour soaks damage before your shields and health (`JuggSoak`, from `Damage` in Combat.cs); you move JuggSlow slower; and every
    /// enemy, bosses too, goes for you (`PickTarget` in EnemyAI.cs, as Roar does, without Roar's damage cut).
    /// Each enemy bullet the armour takes throws a flak shard straight back at whoever fired it (`Bullet.Shooter`, or the enemy nearest
    /// the line it came along): JuggShard damage (mastered JuggShardMastered) times your damage multiplier, dealt at once on the host
    /// (not a bullet), with a short tracer (`FxKind.JuggShard`: Pos = where the shot struck, Pos2 = the enemy, Radius = the wearer's id).
    /// A bullet the armour soaks whole gives no hit grace, so a stream of shots keeps feeding the flak.
    /// It ends when the time runs out, the armour is gone, you go down, or the wave is over (as Last Stand does), and the plates fall
    /// off. `Player.JuggT` (time left, 0 = off), `JuggArmor` and `JuggMax` are replicated; Game/JuggernautLook.cs draws it.
    public partial class Game
    {
        public const float JuggTime = 10f;             // how long the suit stays on
        public const float JuggClamp = 0.6f;           // the plates bolting on (drawing; the armour is up from the press)
        public const float JuggArmor = 300f, JuggArmorMastered = 450f;
        public const float JuggSlow = 0.75f;           // your speed while you wear it (25% slower)
        public const float JuggShard = 12f, JuggShardMastered = 18f;  // a flak shard's damage, times your damage multiplier
        public const float JuggShardFx = 0.3f;         // how long a shard's tracer lasts (flight, then a pop)
        public const float JuggShooterReach = 1400f;   // a shard looks this far back along the shot for whoever fired it
        public const float JuggBotRange = 260f;        // a bot puts it on with JuggBotCrowd enemies this close
        public const int JuggBotCrowd = 4;

        /// Wearing the suit.
        public static bool JuggOn(Player p) => p.JuggT > 0f && !p.Downed;

        /// Seconds since the suit went on (for the look), from the time left.
        public static float JuggSince(Player p) => JuggTime - p.JuggT;

        /// Host: the suit goes on. Every enemy turns on you straight away.
        void CastJugg(Player p, bool mastered)
        {
            p.JuggT = JuggTime;
            p.JuggMax = p.JuggArmor = mastered ? JuggArmorMastered : JuggArmor;
            p.JuggMastered = mastered;
            p.UltT = JuggTime;
            foreach (var e in Enemies.Values)
            {
                e.TargetId = p.Id;
                e.RetargetT = Math.Max(e.RetargetT, 1f);
            }
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.72f, 0.76f, 0.82f), 90f, JuggClamp, Sfx.Clang);
        }

        /// The suit comes off (time, armour gone, down, or the wave over).
        void EndJugg(Player p)
        {
            if (p.JuggT <= 0f && p.JuggArmor <= 0f)
                return;
            p.JuggT = 0f;
            p.JuggArmor = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.JuggernautSuit)
                p.UltT = 0f;
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.72f, 0.76f, 0.82f), 60f, 0.35f, Sfx.Clang);
        }

        /// Host, every tick.
        void TickJugg(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.JuggT <= 0f)
                    continue;
                p.JuggT = Math.Max(0f, p.JuggT - dt);
                if (p.JuggT <= 0f || p.JuggArmor <= 0f || p.Downed || State != MatchState.Fighting || p.Slots[Upgrades.UltSlot].Id != AbilityId.JuggernautSuit)
                    EndJugg(p);
            }
        }

        /// From Damage: the armour takes what it can; returns what is left for shields and health.
        float JuggSoak(Player p, float amount)
        {
            if (!JuggOn(p) || p.JuggArmor <= 0f || amount <= 0f)
                return amount;
            float soak = Math.Min(p.JuggArmor, amount);
            p.JuggArmor -= soak;
            return amount - soak;
        }

        /// An enemy bullet struck a suited player (Combat.cs, in place of a plain Damage): the armour takes it, and if it took any, a
        /// flak shard goes back at the shooter. A shot the armour took whole leaves no hit grace behind.
        void JuggShot(Player p, Bullet b)
        {
            float armour = p.JuggArmor, hp = p.Hp, grace = p.InvulnT;
            if (p.DevGod)
                JuggSoak(p, b.Dmg);  // (god mode, the lobby preview: the plates still wear, health is untouched)
            else
                Damage(p, b.Dmg, ShotSource(b));
            if (p.JuggArmor < armour || (armour > 0f && p.JuggArmor <= 0f))
                JuggFlak(p, b);
            if (!p.Downed && p.Hp >= hp)
                p.InvulnT = grace;  // all on the plates
            if (p.JuggArmor <= 0f)
                EndJugg(p);
        }

        /// Who fired this bullet: its Shooter if still alive, else the living enemy nearest the line it came along.
        Enemy JuggShooter(Bullet b)
        {
            Enemy e;
            if (b.Shooter > 0 && Enemies.TryGetValue(b.Shooter, out e) && Targetable(e))
                return e;
            V2 back = b.Vel.IsZero ? V2.Zero : -b.Vel.Normalized();
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var c in Enemies.Values)
            {
                if (!Targetable(c) || c.Dummy)
                    continue;
                V2 to = c.Pos - b.Pos;
                float along = back.IsZero ? to.Length : to.X * back.X + to.Y * back.Y;
                if (along < -c.Radius || along > JuggShooterReach)
                    continue;
                float off = back.IsZero ? 0f : Math.Abs(to.X * back.Y - to.Y * back.X);
                float score = off * 3f + along * 0.1f;  // nearest the line first, then nearest
                if (score < bestD)
                {
                    bestD = score;
                    best = c;
                }
            }
            return best;
        }

        /// One flak shard: dealt at once, with a tracer for everyone to see.
        void JuggFlak(Player p, Bullet b)
        {
            var e = JuggShooter(b);
            if (e == null)
                return;
            p.JuggShards++;
            float dmg = (p.JuggMastered ? JuggShardMastered : JuggShard) * p.Stats.DmgMult;
            HurtEnemy(e, dmg, p.Id);
            BroadcastFx(FxKind.JuggShard, b.Pos, e.Pos, new Rgba(1f, 0.82f, 0.45f), p.Id, JuggShardFx, Sfx.None);
        }

        /// The lobby preview: while you wear the suit, the dummies take turns shooting at you so the flak shows (Showcase.cs).
        void JuggShowcaseShots(List<int> dummies, float dt)
        {
            var me = Me;
            if (me == null || !JuggOn(me) || (juggDemoT -= dt) > 0f)
                return;
            juggDemoT = JuggDemoShot;
            for (int k = 0; k < dummies.Count; k++)
            {
                Enemy e;
                if (!Enemies.TryGetValue(dummies[(juggDemoN + k) % dummies.Count], out e) || e.SpawnT > 0f)
                    continue;
                EnemyShot(e, (me.Pos - e.Pos).Angle, 330f, Defs.SmallShot);
                juggDemoN = (juggDemoN + k + 1) % dummies.Count;
                return;
            }
        }
        float juggDemoT;
        int juggDemoN;
        const float JuggDemoShot = 0.28f;

        /// For the bot's rule (BotSense.Crowd): how many enemies are within JuggBotRange of you.
        int CrowdNear(Player me)
        {
            int n = 0;
            foreach (var e in Enemies.Values)
                if (Targetable(e) && !e.Dummy && e.Pos.DistanceSq(me.Pos) < JuggBotRange * JuggBotRange)
                    n++;
            return n;
        }
    }
}
