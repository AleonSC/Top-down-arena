using System;
using System.Collections.Generic;

namespace Arena
{
    /// Static Bubble: the Healer Elementalist's second ultimate (lobby key 1 of the Ultimate row; Sanctum stays the default).
    /// From its mockup (claude.ai artifact "Static Bubble"):
    ///   - A BubRadius px bubble of static wraps you for up to BubTime s and moves with you.
    ///   - Enemies (heavies and bosses too) can't be inside it: anything touching it is put out at its edge and shoved
    ///     BubShove px on; each touch hurts BubTouchDamage and stuns BubTouchStun s (not bosses), and feeds it BubTouchFeed
    ///     (once per BubFeedGap s an enemy).
    ///   - Enemy shots that touch it are eaten, each feeding it BubShotFeed. Allies inside it can't be hurt.
    ///   - It grows from BubRadius to BubRadiusFull px as it fills to BubFull. Full, it bursts (BubBlast px: BubBlastDamage,
    ///     BubBlastStun s, not bosses, shoved BubBlastShove px, shots erased) and gives you and every ally in the blast
    ///     Static Rush: StaticRushTime s of StaticRushMove speed and StaticRushFire fire rate.
    ///   - Time runs out first: it pops smaller, its blast, damage and Static Rush scaled by how full it was (at least BubFizzleMin).
    /// Ends quietly when you go down, the wave ends or the slot changes. Mastered: the burst reaches BubBlastMastered px
    /// and Static Rush lasts StaticRushTimeMastered s. Damage scales by your damage.
    /// Replicated: Player.BubT (time left, 0 = off), BubR (its size) and BubCharge on the caster; RushT and RushLen on everyone it
    /// reached. FxKind.StaticBurst is the burst. Drawn by Game/StaticBubbleLook.cs.
    public partial class Game
    {
        public const float BubTime = 10f, BubRadius = 44f, BubRadiusFull = 118f, BubGrow = 8f;
        public const float BubFull = 40f, BubShotFeed = 1f, BubTouchFeed = 3f, BubFeedGap = 0.6f;
        public const float BubShove = 60f, BubTouchDamage = 4f, BubTouchStun = 0.35f;
        public const float BubBlast = 190f, BubBlastMastered = 240f, BubBlastDamage = 70f, BubBlastStun = 0.6f, BubBlastShove = 110f;
        public const float BubFizzleMin = 0.25f, BubBurstFx = 0.7f, BubZapGap = 0.2f;
        public const int BubArcsPerTick = 4;  // feed arcs drawn per tick at most (the rest just feed)
        public const float StaticRushTime = 6f, StaticRushTimeMastered = 8f, StaticRushMove = 1.4f, StaticRushFire = 1.6f;

        public static readonly Rgba BubCyan = new Rgba(0.37f, 0.85f, 1f), BubViolet = new Rgba(0.87f, 0.8f, 1f), StaticRushGold = new Rgba(0.95f, 0.76f, 0.31f);

        /// Static Bubble's entry: key 1 of the Healer Elementalist's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] StaticBubbleUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.StaticBubble, Kit = Kit.Elementalist, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Static Bubble", Cooldown = 80f,
                Desc = "A bubble of static round you shoves enemies out, eats their shots and shields allies inside. What it touches feeds it until it bursts: Static Rush for everyone in the blast.",
                Capstone = "The burst reaches 240 px, and Static Rush lasts 8 s.",
                Bot = b => b.Danger || b.Crowd >= JuggBotCrowd,
            },
        };

        /// The bubble is up.
        public static bool BubOn(Player p) => p.BubT > 0f && !p.Downed;
        /// How full it is, 0..1.
        public static float BubFill(Player p) => Math.Min(1f, p.BubCharge / BubFull);
        /// Its size for a fill (it grows fast at first, then slower).
        public static float BubSize(float fill) => BubRadius + (BubRadiusFull - BubRadius) * (float)Math.Sqrt(Math.Max(0f, fill));
        /// Static Rush's share of your speed (Player.Speed) and fire rate (FireRate).
        public static float StaticRushSpeed(Player p) => p.RushT > 0f ? StaticRushMove : 1f;

        /// Is this point inside a standing Static Bubble? (Allies in one can't be hurt: Combat.cs Damage.)
        public bool InStaticBubble(V2 pos)
        {
            foreach (var p in Players.Values)
                if (p.BubUp && BubOn(p) && p.Pos.CloserThan(pos, p.BubR))
                    return true;
            return false;
        }

        void CastStaticBubble(Player p, bool mastered)
        {
            p.BubT = BubTime;
            p.BubR = BubRadius;
            p.BubCharge = 0f;
            p.BubMastered = mastered;
            p.BubUp = true;
            p.BubZapT = 0f;
            p.UltT = p.BubT;
            BroadcastFx(FxKind.Ring, p.Pos, BubCyan, BubRadius + 20f, 0.35f, Sfx.SpellChainArc);
        }

        /// It goes: `burst` sets it off (full, or a smaller pop when time ran out); otherwise it just fades (down, wave over, slot changed).
        void EndStaticBubble(Player p, bool burst)
        {
            if (!p.BubUp)
                return;
            p.BubUp = false;
            p.BubT = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.StaticBubble)
                p.UltT = 0f;
            if (!burst)
            {
                BroadcastFx(FxKind.Ring, p.Pos, BubCyan, p.BubR, 0.3f, Sfx.None);
                return;
            }
            float fill = BubFill(p);
            bool full = fill >= 1f;
            float k = full ? 1f : Math.Max(BubFizzleMin, fill);
            float reach = full ? (p.BubMastered ? BubBlastMastered : BubBlast) : p.BubR + (BubBlast - p.BubR) * fill;
            float dmg = BubBlastDamage * k * p.Stats.DmgMult;
            int arcs = 0;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.SpawnT > 0f || e.Pos.FartherOrAt(p.Pos, reach + e.Radius))
                    continue;
                HurtEnemy(e, dmg, p.Id);
                if (!e.Def.IsBoss)
                    Stun(e, BubBlastStun * k);
                ShoveFrom(e, p.Pos, BubBlastShove * k);
                if (arcs++ < 10)
                    BroadcastFx(FxKind.RodArc, p.Pos, e.Pos, BubViolet, dmg, 0.3f, Sfx.None);
            }
            bubGone.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && b.Pos.CloserThan(p.Pos, reach))
                    bubGone.Add(b.Id);
            foreach (int id in bubGone)
                KillBullet(id);
            float rush = (p.BubMastered ? StaticRushTimeMastered : StaticRushTime) * k;
            foreach (var q in Players.Values)
                if (q == p || !q.Downed && q.Pos.CloserThan(p.Pos, reach + q.Def.BodyR))
                {
                    q.RushT = Math.Max(q.RushT, rush);
                    q.RushLen = q.RushT;
                    p.BubRushed++;
                }
            if (full)
                p.BubBursts++;
            else
                p.BubFizzles++;
            BroadcastFx(FxKind.StaticBurst, p.Pos, new V2(p.Id, full ? 1f : 0f), full ? BubViolet : BubCyan, reach, BubBurstFx, full ? Sfx.Shockwave : Sfx.SpellZap);
        }

        readonly List<int> bubGone = new List<int>();

        /// Host, every tick after the enemies have moved (with the other keep-out zones): the clock, the growth, enemies put out
        /// and shoved, shots eaten, the burst.
        void TickStaticBubble(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (!p.BubUp)
                    continue;
                p.BubT = Math.Max(0f, p.BubT - dt);
                if (p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.StaticBubble)
                {
                    EndStaticBubble(p, false);
                    continue;
                }
                p.BubR += (BubSize(BubFill(p)) - p.BubR) * Math.Min(1f, BubGrow * dt);
                p.BubZapT = Math.Max(0f, p.BubZapT - dt);
                int arcs = 0;
                bool fed = false;
                // enemies: put out at the edge and shoved on; a touch feeds it once per BubFeedGap
                foreach (var e in AllFoes(by: p.Id))
                {
                    if (e.SpawnT > 0f || DevourUnder(e) || GutwormUnder(e))
                        continue;
                    float lim = p.BubR + e.Radius;
                    V2 off = e.Pos - p.Pos;
                    float d = off.Length;
                    if (d >= lim)
                        continue;
                    V2 dir = d > 0.01f ? off * (1f / d) : V2.FromAngle(p.Aim);
                    e.Pos = ClampToArena(p.Pos + dir * (lim + 1f), e.Radius);
                    if (e.T - e.BubFedAt < BubFeedGap)
                        continue;
                    e.BubFedAt = e.T;
                    KnockEnemy(e, dir * BubShove);
                    HurtEnemy(e, BubTouchDamage * p.Stats.DmgMult, p.Id);
                    if (!e.Def.IsBoss)
                        Stun(e, BubTouchStun);
                    p.BubCharge += BubTouchFeed;
                    p.BubFed++;
                    fed = true;
                    if (arcs++ < BubArcsPerTick)
                        BroadcastFx(FxKind.RodArc, p.Pos + dir * (p.BubR * 0.3f), e.Pos, BubViolet, BubTouchDamage, 0.22f, Sfx.None);
                }
                // enemy shots: eaten
                bubGone.Clear();
                foreach (var b in Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && b.Pos.CloserThan(p.Pos, p.BubR + b.Radius))
                        bubGone.Add(b.Id);
                foreach (int id in bubGone)
                {
                    Bullet b;
                    if (!Bullets.TryGetValue(id, out b))
                        continue;
                    if (arcs++ < BubArcsPerTick)
                    {
                        V2 dir = (b.Pos - p.Pos).Normalized();
                        BroadcastFx(FxKind.RodArc, p.Pos + dir * (p.BubR * 0.45f), p.Pos + dir * p.BubR, BubCyan, 0f, 0.14f, Sfx.None);
                    }
                    KillBullet(id);
                    p.BubCharge += BubShotFeed;
                    p.BubEaten++;
                    fed = true;
                }
                if (fed && p.BubZapT <= 0f)
                {
                    p.BubZapT = BubZapGap;
                    BroadcastSound(Sfx.SpellZap);
                }
                if (p.BubCharge >= BubFull || p.BubT <= 0f)
                    EndStaticBubble(p, true);
            }
        }

        // --- Network: the player's Static Bubble fields (SyncPlayer) ---

        void SyncStaticBubble(NetSync s, Player p)
        {
            s.Float(ref p.BubT);  // Static Bubble
            if (p.BubT > 0f)
            {
                s.Float(ref p.BubR);
                s.Float(ref p.BubCharge);
            }
            s.Float(ref p.RushT);
            if (p.RushT > 0f)
                s.Float(ref p.RushLen);
        }
    }
}
