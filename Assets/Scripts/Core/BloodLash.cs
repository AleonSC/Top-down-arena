using System;
using System.Collections.Generic;

namespace Arena
{
    /// The tank Hemomancer's Blood Lash, the chain combo (Docs/BloodLashChains.md; mockup Docs/Mockups/blood-lash-chains.html).
    ///   - Fire is a four-step string: Crack (a 110 degree sweep), Backhand (the same sweep back), Snap (a narrow lash out to
    ///     LashSnapReach) and Reap. A press within LashChainWindow of the last step ending moves the string on; later, it starts
    ///     over at Crack. The step and a count of steps share Player.Combo with the Twin Blades (low 3 bits: the step, 1-4).
    ///   - Every lash that lands hooks the enemy with a blood chain: +1 link (+2 from the cracking tip, +2 from the Snap), up to
    ///     LashMaxLinks per enemy (Crimson Whip: one more). The chains are the player's: `Player.Chains` (enemy ids) and
    ///     `Player.ChainLinks`, both replicated; `ChainT` (host) holds them LashLinkHold s after the enemy's last hit, then a
    ///     link drops every LashLinkFade s. A chain snaps past LashBreak px.
    ///   - Reap yanks every chained enemy within LashReapRange in to touching you over LashReapTime: LashReapBase + LashReapPer a
    ///     link when it lands, stunned LashReapStun. The links spent become Clot (1 a link, up to ClotMax: 4% less damage taken
    ///     each, Hemomancer.cs). Heavy enemies and bosses aren't moved, but are still hit.
    ///   - Alt (the weapon-action key), Martyr's Leash: hold (up to LeashMax s, LeashDrain of your max health a second). Every
    ///     chained enemy is taunted, held (its chain doesn't fade) and dragged back in if it's farther than LeashLength; LeashShare
    ///     of each hit you take is taken off and runs down the chains into them, LeashReturn times over, split by their links.
    ///     LeashCooldown s after it ends before it can start again. Your game sends Msg.WeaponDash (WeaponAlt.PullOn on press,
    ///     WeaponAlt.PullOff on release, the Repulsion Cannon's kinds); the host does the rest (TickBloodLash).
    public partial class Game
    {
        public const float LashChainWindow = 0.8f;
        public const float LashCrackTime = 0.32f, LashSnapTime = 0.4f, LashReapTime = 0.3f, LashReapRecover = 0.25f;
        public const float LashSnapReach = 190f, LashSnapArc = 36f, LashSnapMult = 1.5f;
        public const int LashMaxLinks = 4, LashSnapLinks = 2;
        public const float LashLinkHold = 3f, LashLinkFade = 0.5f, LashBreak = 400f;
        public const float LashReapRange = 340f, LashReapBase = 20f, LashReapPer = 12f, LashReapStun = 0.6f;
        public const float LeashMax = 3f, LeashDrain = 0.02f, LeashLength = 160f, LeashPull = 220f, LeashShare = 0.4f, LeashReturn = 3f, LeashCooldown = 3f;
        public const float LeashTaunt = 0.3f;

        class ReapRun
        {
            public int Owner, Target, Links;
            public V2 From;
            public float T, Dmg;
            public bool Moves;
        }

        readonly List<ReapRun> reapRuns = new List<ReapRun>();

        /// The Blood Lash step in `combo` (1 Crack, 2 Backhand, 3 Snap, 4 Reap; 0 none yet).
        public static int LashStepOf(int combo) => combo & 7;

        /// The most links one enemy can hold for `p` (Crimson Whip: one more a card; each extra projectile: one more, as it raises ClotMax).
        public static int LashLinkCap(Player p) => LashMaxLinks + p.Stats.LashLinkBonus + Math.Max(0, p.ClotMax - ClotMaxBase);

        /// The links `p` has on enemy `id` (0: none).
        public static int LinksOn(Player p, int id)
        {
            int k = p.Chains.IndexOf(id);
            return k < 0 ? 0 : p.ChainLinks[k];
        }

        /// Every link `p` has out.
        public static int LinksOut(Player p)
        {
            int n = 0;
            foreach (var l in p.ChainLinks)
                n += l;
            return n;
        }

        /// Host: one press of fire with the Blood Lash (from FireBloodWeapon): the next step of the string.
        void FireBloodLash(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            p.ClotMax = ClotMaxBase + extra;
            int last = LashStepOf(p.Combo);
            int step = last >= 1 && last < 4 && p.ComboT > 0f ? last + 1 : 1;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = (count << 3) | step;
            float dur = step == 3 ? LashSnapTime : step == 4 ? LashReapTime + LashReapRecover : LashCrackTime;
            p.ComboT = dur + LashChainWindow;
            p.FireCd = dur / FireRate(p);
            float reach = (w.Reach + w.Grow * extra) * p.Stats.LashReachMult;  // (Crimson Whip: farther)
            switch (step)
            {
                case 1:
                case 2:
                {
                    var s = NewSwing(p, aim, reach, 110f, w.Damage * dmg);
                    s.NoCut = true;  // a whip of blood, not a blade
                    s.Tip = s.Reach * LashTipFrom;
                    s.Links = 1;
                    QueueSwing(s);
                    break;
                }
                case 3:
                {
                    var s = NewSwing(p, aim, LashSnapReach / Defs.WeaponOf(WeaponId.BloodLash).Reach * reach, LashSnapArc, w.Damage * LashSnapMult * dmg);
                    s.NoCut = true;
                    s.Taunt = LashTaunt;  // the Snap always lands with the tip: it taunts (no double damage)
                    s.Links = LashSnapLinks;
                    s.Delay = LashSnapTime * 0.3f;
                    QueueSwing(s);
                    break;
                }
                default:
                    Reap(p, dmg);
                    break;
            }
        }

        /// Host: a Blood Lash swing hit `e` (from LandSwing): the tip taunts, and the hit hooks a chain or adds links to it.
        void LashLanded(Player p, Enemy e, Swing s, bool crack)
        {
            if (crack)
            {
                TauntEnemy(p, e, LashTaunt);
                BroadcastFx(FxKind.Ring, e.Pos, LashRed, e.Radius + 10f, 0.18f, Sfx.None);
            }
            else if (s.Taunt > 0f)
                TauntEnemy(p, e, s.Taunt);
            AddLinks(p, e, s.Links + (crack ? 1 : 0));
        }

        void AddLinks(Player p, Enemy e, int n)
        {
            if (n <= 0 || e.Hp <= 0f)
                return;
            int k = p.Chains.IndexOf(e.Id);
            if (k < 0)
            {
                if (p.Chains.Count >= 255)
                    return;
                p.Chains.Add(e.Id);
                p.ChainLinks.Add(0);
                p.ChainT.Add(0f);
                k = p.Chains.Count - 1;
            }
            p.ChainLinks[k] = (byte)Math.Min(LashLinkCap(p), p.ChainLinks[k] + n);
            p.ChainT[k] = LashLinkHold;
        }

        /// Raising the parry starts the string over at Crack; a Snap still winding up never lands (the chains stay).
        void ResetLashChain(Player p)
        {
            p.Combo &= ~7;
            p.ComboT = 0f;
            p.FireCd = 0f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Links > 0 && s.Delay > 0f);
        }

        void DropChain(Player p, int k)
        {
            p.Chains.RemoveAt(k);
            p.ChainLinks.RemoveAt(k);
            p.ChainT.RemoveAt(k);
        }

        void ClearChains(Player p)
        {
            p.Chains.Clear();
            p.ChainLinks.Clear();
            p.ChainT.Clear();
        }

        /// The fourth step: every chained enemy in range is yanked in (TickBloodLash lands it), its links spent as Clot.
        void Reap(Player p, float dmg)
        {
            int spent = 0, pulled = 0;
            for (int k = p.Chains.Count - 1; k >= 0; k--)
            {
                Enemy e;
                if (!TryFoe(p.Chains[k], out e) || !Targetable(e) || e.Pos.DistanceTo(p.Pos) > LashReapRange + e.Radius)
                    continue;
                int links = p.ChainLinks[k];
                reapRuns.Add(new ReapRun { Owner = p.Id, Target = e.Id, Links = links, From = e.Pos, Dmg = (LashReapBase + LashReapPer * links) * dmg, Moves = !Heavy(e) });
                if (!Heavy(e))
                    Stun(e, LashReapTime + LashReapStun);
                BroadcastFx(FxKind.Drain, e.Pos, p.Pos, LashRed, 2f + links, LashReapTime, Sfx.None);
                spent += links;
                pulled++;
                DropChain(p, k);
            }
            BroadcastFx(FxKind.Ring, p.Pos, LashRed, p.Def.BodyR + 16f, 0.3f, pulled > 0 ? Sfx.SpellArcane : Sfx.SwingLight);
            if (spent > 0)
                AddClot(p, spent);
        }

        /// Host: a player pressed (on) or let go of the weapon-action key with the Blood Lash: Martyr's Leash.
        void HostLeash(Player p, bool on)
        {
            if (!on)
            {
                EndLeash(p);
                return;
            }
            if (p.LeashOn || p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.LeashCd > 0f || State == MatchState.GameOver)
                return;
            p.LeashOn = true;
            p.LeashT = 0f;
            BroadcastSound(Sfx.SpellPullStart);
        }

        void EndLeash(Player p)
        {
            if (!p.LeashOn)
                return;
            p.LeashOn = false;
            p.LeashCd = LeashCooldown;
            BroadcastSound(Sfx.SpellPullStop);
        }

        /// Host, from Damage: with the Leash up, LeashShare of a hit is taken off and runs down the chains. Returns what's left.
        float LeashSoak(Player p, float amount)
        {
            if (!p.LeashOn || amount <= 0f || p.Chains.Count == 0)
                return amount;
            int total = LinksOut(p);
            if (total <= 0)
                return amount;
            float back = amount * LeashShare;
            for (int k = p.Chains.Count - 1; k >= 0; k--)
            {
                Enemy e;
                if (!TryFoe(p.Chains[k], out e) || !Targetable(e))
                    continue;
                float d = back * LeashReturn * p.ChainLinks[k] / total * p.Stats.DmgMult;
                BroadcastFx(FxKind.Drain, p.Pos, e.Pos, LashRed, 2f, 0.25f, Sfx.None);
                HurtEnemy(e, d, p.Id);
            }
            return amount - back;
        }

        /// Host, every tick: chains fading and snapping, Reap's yanks landing, the Leash holding and draining.
        void TickBloodLash(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.LeashCd = Math.Max(0f, p.LeashCd - dt);
                if (p.Weapon.Id != WeaponId.BloodLash || p.Downed)
                {
                    if (p.Chains.Count > 0)
                        ClearChains(p);
                    EndLeash(p);
                    continue;
                }
                if (LashStepOf(p.Combo) != 0 && (p.ComboT -= dt) <= 0f)
                    p.Combo &= ~7;  // back to the Crack (the swing count stays)
                if (p.LeashOn)
                {
                    p.LeashT += dt;
                    PayHealth(p, p.MaxHp * LeashDrain * dt, "Martyr's Leash");
                    if (p.LeashT >= LeashMax || p.StunT > 0f || p.SwallowT > 0f || State == MatchState.GameOver)
                        EndLeash(p);
                }
                for (int k = p.Chains.Count - 1; k >= 0; k--)
                {
                    Enemy e;
                    if (!TryFoe(p.Chains[k], out e) || e.Hp <= 0f || e.Pos.DistanceTo(p.Pos) > LashBreak + e.Radius)
                    {
                        DropChain(p, k);
                        continue;
                    }
                    if (p.LeashOn)
                    {
                        p.ChainT[k] = Math.Max(p.ChainT[k], LashLinkFade);  // held while leashed
                        TauntEnemy(p, e, LeashTaunt);
                        float d = e.Pos.DistanceTo(p.Pos), len = LeashLength + e.Radius + p.Def.BodyR;
                        if (d > len && !Heavy(e))
                            e.Pos = ClampToArena(e.Pos - (e.Pos - p.Pos) / d * Math.Min(d - len, LeashPull * dt), e.Radius);
                        continue;
                    }
                    if ((p.ChainT[k] -= dt) > 0f)
                        continue;
                    if (p.ChainLinks[k] <= 1)
                        DropChain(p, k);
                    else
                    {
                        p.ChainLinks[k]--;
                        p.ChainT[k] = LashLinkFade;
                    }
                }
            }
            for (int i = reapRuns.Count - 1; i >= 0; i--)
            {
                var r = reapRuns[i];
                Player p;
                Enemy e;
                r.T += dt;
                if (!Players.TryGetValue(r.Owner, out p) || p.Downed || !TryFoe(r.Target, out e) || e.Hp <= 0f)
                {
                    reapRuns.RemoveAt(i);
                    continue;
                }
                V2 away = (r.From - p.Pos).Normalized();
                if (away.IsZero)
                    away = V2.FromAngle(p.Aim);
                V2 end = ClampToArena(p.Pos + away * (e.Radius + p.Def.BodyR + 4f), e.Radius);
                float k = Math.Min(1f, r.T / LashReapTime);
                if (r.Moves)
                    e.Pos = V2.Lerp(r.From, end, k * k);
                if (r.T < LashReapTime)
                    continue;
                reapRuns.RemoveAt(i);
                string was = CreditTo("weapon");
                HurtEnemy(e, r.Dmg, p.Id);
                ModHit(e, p.Id, r.Dmg, Defs.KnightSlash, away * -1f);
                dmgSrc = was;
                BroadcastFx(FxKind.Ring, e.Pos, LashRed, e.Radius + 12f, 0.25f, Sfx.None);
            }
        }

        // --- Network: the player's Blood Lash fields (SyncPlayer) ---

        void SyncBloodLash(NetSync s, Player p)
        {
            int chained = Math.Min(255, p.Chains.Count);  // the Blood Lash's chains
            s.Byte(ref chained);
            if (s.Reading)
            {
                p.Chains.Clear();
                p.ChainLinks.Clear();
            }
            for (int k = 0; k < chained; k++)
            {
                int chainId = s.Reading ? 0 : p.Chains[k];
                byte links = s.Reading ? (byte)0 : p.ChainLinks[k];
                s.Int(ref chainId);
                s.Byte(ref links);
                if (s.Reading)
                {
                    p.Chains.Add(chainId);
                    p.ChainLinks.Add(links);
                }
            }
            s.Bool(ref p.LeashOn);  // Martyr's Leash
            s.Float(ref p.LeashT);
        }

        /// WeaponDef.BotAlt (BotWeaponAlt.cs): Martyr's Leash on a chained pack while healthy (it bleeds you; held)
        static bool BloodLashBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            if (g.botAltWas)
                return g.botAltHoldT < 2f && me.Hp > me.MaxHp * 0.4f;
            return !me.LeashOn && g.Tap(me.Chains.Count >= 2 && me.Hp > me.MaxHp * 0.5f && (g.BotDanger > 1f || v.Downed != null || v.Lowest != null && v.Lowest.Hp < v.Lowest.MaxHp * 0.5f), 6f);
        }
    }
}
