using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Wolf Whistle's pack (the DPS Summoner's weapon; the darts and the takedown are in KitWeapons.cs).
    ///   Growing: you start with one wolf (plus one per extra projectile). Every PackKillsPerWolf of your kills, a wolf
    ///     joins, up to PackMax slots. With every slot taken, the new wolf replaces your most wounded one instead
    ///     (or, with no wolf hurt but the alpha, the alpha gets a wolf's health back). Player.PackKills is the count
    ///     towards the next one (replicated, for the pips under your feet).
    ///   Soaking: PackShare of every hit you take goes to the pack instead (PackSoak, from Damage), always the most
    ///     wounded wolf first, so they fall one at a time. A wolf at 0 health is gone. That is the price of the pack.
    ///     A wolf's health grows with the enemies' damage each wave (EnemyDamageScale), so a wolf soaks the same share
    ///     of hits all run. Pack Bond makes it tougher.
    ///   Alpha: Alt (Msg.WeaponDash, WeaponAlt.Pack) merges every small wolf into one big alpha that takes a single slot.
    ///     Its size n is the wolves in it (AlphaMax at most; Alt again folds new wolves into it). It bites
    ///     n x AlphaBonus as hard, runs AlphaSpeedPer faster per extra wolf, is drawn bigger, and has their health summed.
    ///     Minion.Alpha is n on the host; screens read it back from Scale (AlphaSize).
    public partial class Game
    {
        public const int PackMax = 5, PackKillsPerWolf = 10, AlphaMax = 8;
        public const float PackShare = 0.1f, WolfHp = 20f;
        public const float AlphaScalePer = 0.15f, AlphaSpeedPer = 0.08f, AlphaBonus = 1.15f, PackMergeCd = 0.5f;

        float packLocalCd;  // your own game: Alt can't merge again for this long

        /// One of the Wolf Whistle's pack (not Alpha Call's or Legion's short-lived wolves).
        static bool PackWolf(Minion m, int owner) => m.Owner == owner && m.Kind == MinionKind.Wolf && m.Companion;

        /// How many wolves a pack wolf is worth: 1, or an alpha's size. Works on every screen (from the replicated Scale).
        public static int AlphaSize(Minion m) => m.Scale <= 1.01f ? 1 : 1 + (int)Math.Round((m.Scale - 1f) / AlphaScalePer);

        /// Companions a weapon keeps that aren't the weapon's own (legendaries and Force Pact: they stay).
        static bool OtherCompanion(Minion m) =>
            m.Kind == MinionKind.WardenGuard || m.Kind == MinionKind.Thrall || m.Kind == MinionKind.HiveDrone || m.Kind == MinionKind.Scythe;

        /// A pack wolf's full health this wave, for `size` wolves.
        float WolfMaxHp(Player p, int size) => WolfHp * size * EnemyDamageScale(Math.Max(1, Wave)) * p.Stats.MinionToughMult;

        readonly List<Minion> packScratch = new List<Minion>();
        readonly List<int> packGone = new List<int>();

        /// The player's pack, alpha first.
        List<Minion> PackOf(Player p)
        {
            packScratch.Clear();
            foreach (var m in Minions.Values)
                if (PackWolf(m, p.Id))
                    packScratch.Add(m);
            packScratch.Sort((a, b) => b.Alpha != a.Alpha ? b.Alpha.CompareTo(a.Alpha) : a.Id.CompareTo(b.Id));
            return packScratch;
        }

        /// Host, every tick, for a player holding the Wolf Whistle (instead of the usual companion top-up).
        void TickWolfPack(Player p)
        {
            var gone = packGone;
            gone.Clear();
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Companion && m.Kind != MinionKind.Wolf && !OtherCompanion(m))
                    gone.Add(m.Id);  // the last weapon's companions leave
            foreach (int id in gone)
                Minions.Remove(id);
            var pack = PackOf(p);
            if (!p.PackStarted)
            {
                p.PackStarted = true;
                for (int i = pack.Count, n = Math.Min(PackMax, 1 + ExtraShots(p)); i < n; i++)
                    AddWolf(p);
                pack = PackOf(p);
            }
            foreach (var m in pack)  // health keeps up with the wave (the same fraction of a bigger pool)
            {
                float max = WolfMaxHp(p, Math.Max(1, m.Alpha));
                if (Math.Abs(max - m.MaxHp) > 0.01f)
                {
                    m.Hp = m.MaxHp > 0f ? m.Hp * max / m.MaxHp : max;
                    m.MaxHp = max;
                }
            }
        }

        Minion AddWolf(Player p, V2? at = null)
        {
            var m = SpawnMinion(MinionKind.Wolf, p, at ?? p.Pos + RandDir() * 30f, -1f, true);
            m.MaxHp = m.Hp = WolfMaxHp(p, 1);
            return m;
        }

        /// Host: `killer` got a kill. With the Wolf Whistle, every PackKillsPerWolf a wolf joins (or replaces one).
        void PackKill(Player killer)
        {
            if (killer == null || killer.Weapon.Id != WeaponId.WolfWhistle || killer.Downed)
                return;
            var pack = PackOf(killer);
            Minion worst = null;
            foreach (var m in pack)
                if (m.Alpha == 0 && m.Hp < m.MaxHp - 0.01f && (worst == null || m.Hp < worst.Hp))
                    worst = m;
            Minion alpha = pack.Count > 0 && pack[0].Alpha > 0 ? pack[0] : null;
            bool full = pack.Count >= PackMax;
            if (full && worst == null && (alpha == null || alpha.Hp >= alpha.MaxHp - 0.01f))
            {
                killer.PackKills = (byte)PackKillsPerWolf;  // a fresh pack: the next wolf waits, ready
                return;
            }
            if (++killer.PackKills < PackKillsPerWolf)
                return;
            killer.PackKills = 0;
            if (!full)
            {
                var w = AddWolf(killer);
                BroadcastFx(FxKind.Ring, w.Pos, w.Def.Color, 22f, 0.3f, Sfx.Summon);
            }
            else if (worst != null)
            {
                V2 at = worst.Pos;
                Minions.Remove(worst.Id);  // a fresh wolf takes the wounded one's place
                var w = AddWolf(killer, at);
                BroadcastFx(FxKind.Ring, at, w.Def.Color, 22f, 0.3f, Sfx.Summon);
            }
            else
            {
                alpha.Hp = Math.Min(alpha.MaxHp, alpha.Hp + WolfMaxHp(killer, 1));
                BroadcastFx(FxKind.Ring, alpha.Pos, alpha.Def.Color, 30f * alpha.Scale, 0.3f, Sfx.Summon);
            }
        }

        /// Host, from Damage: the pack takes PackShare of the hit, most wounded wolf first. Returns what's left for you.
        float PackSoak(Player p, float amount)
        {
            if (amount <= 0f || p.Weapon.Id != WeaponId.WolfWhistle)
                return amount;
            float share = amount * PackShare, left = share;
            while (left > 0.0001f)
            {
                Minion worst = null;
                foreach (var m in Minions.Values)
                    if (PackWolf(m, p.Id) && m.Hp > 0f && (worst == null || m.Hp < worst.Hp))
                        worst = m;
                if (worst == null)
                    break;
                float take = Math.Min(left, worst.Hp);
                worst.Hp -= take;
                left -= take;
                if (worst.Hp <= 0.001f)
                {
                    BroadcastFx(FxKind.Boom, worst.Pos, worst.Def.Color, 16f * worst.Scale, 0.3f, Sfx.Hurt);
                    Minions.Remove(worst.Id);
                }
            }
            return amount - (share - left);
        }

        /// Host: Alt with the Wolf Whistle. Every small wolf merges into one alpha (or into the alpha you have).
        void HostPackMerge(Player p)
        {
            if (p.Weapon.Id != WeaponId.WolfWhistle || p.StunT > 0f || State == MatchState.GameOver)
                return;
            var pack = PackOf(p);
            Minion alpha = pack.Count > 0 && pack[0].Alpha > 0 ? pack[0] : null;
            int size = alpha != null ? alpha.Alpha : 0;
            var small = new List<Minion>();
            foreach (var m in pack)
                if (m.Alpha == 0 && size + small.Count < AlphaMax)
                    small.Add(m);
            if (small.Count == 0 || alpha == null && small.Count < 2)
                return;  // nothing to merge
            float hp = alpha != null ? alpha.Hp : 0f;
            V2 sum = default(V2);
            foreach (var m in small)
            {
                hp += m.Hp;
                sum += m.Pos;
            }
            if (alpha == null)
            {
                alpha = small[0];
                small.RemoveAt(0);
                alpha.Alpha = 1;
                alpha.Pos = sum * (1f / (small.Count + 1));
            }
            foreach (var m in small)
            {
                BroadcastFx(FxKind.Link, m.Pos, alpha.Pos, m.Def.Color, 0f, 0.3f, Sfx.None);
                Minions.Remove(m.Id);
            }
            alpha.Alpha += small.Count;
            SetAlpha(p, alpha);
            alpha.Hp = Math.Min(alpha.MaxHp, hp);
            BroadcastFx(FxKind.Ring, alpha.Pos, alpha.Def.Color, 34f * alpha.Scale, 0.4f, Sfx.Roar);
        }

        /// An alpha's size, bite and health pool for its Alpha wolves.
        void SetAlpha(Player p, Minion m)
        {
            int n = Math.Max(1, m.Alpha);
            m.Scale = 1f + AlphaScalePer * (n - 1);
            m.Power = p.Stats.MinionDmgMult * n * (n > 1 ? AlphaBonus : 1f) / m.Scale;  // (Scale also multiplies a bite: TickMinions)
            m.MaxHp = WolfMaxHp(p, n);
        }

        /// How much faster a pack wolf runs: an alpha AlphaSpeedPer per extra wolf in it.
        static float PackSpeed(Minion m) => m.Kind == MinionKind.Wolf && m.Alpha > 1 ? 1f + AlphaSpeedPer * (m.Alpha - 1) : 1f;

        /// Your own game: Alt with the Wolf Whistle. True when it sent something.
        bool LocalPackMerge(Player me, bool pressed, V2 aimAt, float dt)
        {
            packLocalCd = Math.Max(0f, packLocalCd - dt);
            if (me.Weapon.Id != WeaponId.WolfWhistle || !pressed || packLocalCd > 0f)
                return false;
            packLocalCd = PackMergeCd;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.Pack, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.Pack);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }
    }
}
