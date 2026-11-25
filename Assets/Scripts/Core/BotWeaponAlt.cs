using System;

namespace Arena
{
    /// The smart bot's weapon Alt (the weapon-action key, PlayerInput.WeaponAction; LocalWeaponDash in Ninja.cs reads it):
    /// one rule per weapon with a secondary, saying when a person would press it, hold it or let go. A tap is one tick
    /// down and at least one up; holds keep it down until the rule lets go. Cooldowns the host keeps are timed here
    /// with the bot's own clock (botAltCd). Weapons with no rule (or whose Alt needs a drawn route: the Shuriken's
    /// Great Star) never press it.
    public partial class Game
    {
        bool botAltHeld, botAltWas;
        float botAltCd, botAltHoldT, botRapierT;
        public int BotAltPresses;  // presses (tests)

        /// What the rules see this tick.
        struct AltView
        {
            public Player Me;
            public Enemy Target, ShootAt;
            public bool InReach;
            public float ShootD;   // distance to what it shoots at (9999 without)
            public int Near;       // enemies within BotNearReach
            public int Incoming;   // noticed enemy shots on course to hit within half a second
            public Player Downed, Lowest;
        }

        bool BotWeaponAlt(Player me, float dt, Enemy target, Enemy shootAt, bool inReach, Player downed, Player lowest, ref bool firing, ref V2 aimAt)
        {
            botAltCd -= dt;
            var v = new AltView
            {
                Me = me, Target = target, ShootAt = shootAt, InReach = inReach, Downed = downed, Lowest = lowest,
                ShootD = shootAt != null ? shootAt.Pos.DistanceTo(me.Pos) : 9999f,
                Near = EnemiesNear(me.Pos, BotNearReach),
                Incoming = IncomingShots(me, 0.5f),
            };
            bool down = me.StunT <= 0f && AltRule(v, dt, ref firing, ref aimAt);
            if (down && !botAltWas)
                BotAltPresses++;
            botAltHoldT = down ? botAltHoldT + dt : 0f;
            botAltWas = down;
            botAltHeld = down && HoldAlt(me.Weapon.Id);
            return down;
        }

        /// Alts that are held down rather than tapped (the parry stays down while one is held: it would let go of it).
        static bool HoldAlt(WeaponId w) => w == WeaponId.Kusarigama || w == WeaponId.BulwarkBarrier || w == WeaponId.Halberd || w == WeaponId.HoneybeeStaff
            || w == WeaponId.RepulsionCannon || w == WeaponId.BloodLash || w == WeaponId.Quarterstaff;

        /// A press: down for one tick when `want` and the bot's own cooldown allow, then up (the key reads edges).
        bool Tap(bool want, float cd)
        {
            if (!want || botAltWas || botAltCd > 0f)
                return false;
            botAltCd = cd;
            return true;
        }

        /// Noticed enemy shots on course to hit the bot within `look` s.
        int IncomingShots(Player me, float look)
        {
            int n = 0;
            float hitR = me.Def.HitR;
            foreach (var b in botThreats)
            {
                if (!Bullets.ContainsKey(b.Id))
                    continue;
                V2 rel = b.Pos - me.Pos;
                float vv = b.Vel.LengthSq;
                if (vv < 1f)
                    continue;
                float t = -rel.Dot(b.Vel) / vv;
                if (t >= 0f && t <= look && (rel + b.Vel * t).Length <= hitR + b.Radius + 12f)
                    n++;
            }
            return n;
        }

        /// Enemies within `r` of `at` and in front of the bot (within `half` radians of its aim).
        int EnemiesAhead(Player me, float r, float half)
        {
            int n = 0;
            foreach (var e in Enemies.Values)
            {
                if (IsSpawning(e) || SoulBound(e) || e.Hp <= 0f)
                    continue;
                V2 d = e.Pos - me.Pos;
                if (d.Length - e.Radius < r && Math.Abs(MathX.WrapPi(d.Angle - me.Aim)) < half)
                    n++;
            }
            return n;
        }

        bool AltRule(AltView v, float dt, ref bool firing, ref V2 aimAt)
        {
            bool late = DashT > 0f;  // the Alts read after a dash's check would lose the press (LocalWeaponDash)
            // Each class's weapons answer in turn; null: not one of theirs.
            return SoldierAlt(v, dt, late, ref firing, ref aimAt) ?? ElementalistAlt(v, dt, late, ref firing, ref aimAt)
                ?? SummonerAlt(v, dt, late, ref firing, ref aimAt) ?? KnightAlt(v, dt, late, ref firing, ref aimAt)
                ?? NinjaAlt(v, dt, late, ref firing, ref aimAt) ?? HemomancerAlt(v, dt, late, ref firing, ref aimAt)
                ?? FourthAlt(v, dt, late, ref firing, ref aimAt) ?? FourthMoreAlt(v, dt, late, ref firing, ref aimAt) ?? false;
        }

        /// The Soldier's weapons. Whether the bot presses (or holds) its Alt; null for a weapon that isn't theirs.
        bool? SoldierAlt(AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            var e = v.ShootAt;
            switch (me.Weapon.Id)
            {
                case WeaponId.Scattergun:  // Double Barrel point-blank (its kick also gets you out)
                    return !late && Tap(me.ScatShells > 0 && (e != null && v.ShootD < 150f || BotDanger > 2f && v.Near > 0), 1f);
                case WeaponId.FlakCannon:  // a proximity mine where the enemies are coming from
                    return !late && e != null && v.ShootD < 320f && Tap(MineLocalCd <= 0f, 2f);
                case WeaponId.RiotRepeater:  // tear gas on a crowd or the boss
                    return !late && e != null && v.InReach && Tap(GasLocalCd <= 0f && (e.Def.IsBoss || EnemiesNear(e.Pos, 100f) >= 3), 2f);
                case WeaponId.Railgun:  // Heat Dump on a crowd in front once it's hot, or on anything in its face
                    return Tap(me.RailHeat >= 60 && EnemiesAhead(me, DumpReach(me.RailHeat) * 0.8f, DumpHalf) >= 2 || me.RailHeat >= DumpMin && EnemiesAhead(me, 110f, DumpHalf) >= 1, DumpCd);
                case WeaponId.TwinRifle:  // a loaded grenade at a crowd or the boss
                    return !late && e != null && v.InReach && Tap(me.TwinNades > 0 && (e.Def.IsBoss || EnemiesNear(e.Pos, 90f) >= 2), 0.6f);
                case WeaponId.TwinPistols:  // Laser Sights on the boss or a crowd in reach
                    return !late && e != null && v.InReach && me.TpCd <= 0f && Tap(e.Def.IsBoss || EnemiesNear(e.Pos, 140f) >= 3, 1f);
                case WeaponId.MendersBow:  // Scatter: a crowd in front, not mid-draw
                    return !late && e != null && v.ShootD < ScatterReach * 0.8f && bowDraw < 0f && Tap(EnemiesAhead(me, ScatterReach * 0.8f, ScatterFan * 0.5f) >= 3, ScatterCd);
                case WeaponId.Revolver:  // the Fan stance with something in your face, the Gunslinger otherwise (PistolWhip.cs)
                    return !late && Tap(me.RevStance == 1 ? e == null || v.ShootD > 220f : e != null && v.ShootD < 130f, 0.8f);  // (in close, out past 220 px)
                case WeaponId.RepulsionCannon:  // Undertow: pull a spread-out group in, then let go and blast
                    if (botAltWas)
                        return botAltHoldT < 2f && me.RepelMeter > 5 && EnemiesAhead(me, 70f, 1f) == 0 && v.Incoming < 3;
                    return Tap(me.RepelMeter >= 30 && EnemiesAhead(me, 300f, 0.6f) >= 2 && EnemiesAhead(me, 90f, 0.6f) == 0, 3f);
            }
            return null;
        }

        /// The Elementalist's weapons. Whether the bot presses (or holds) its Alt; null for a weapon that isn't theirs.
        bool? ElementalistAlt(AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            var e = v.ShootAt;
            switch (me.Weapon.Id)
            {
                case WeaponId.FrostLance:  // Absolute Zero round you, with nothing about to hit during the charge
                    return Tap(ZeroLocalCd <= 0f && me.FlZeroT <= 0f && EnemiesNear(me.Pos, 250f) >= 3 && IncomingShots(me, 0.8f) == 0, 1f);
                case WeaponId.StormHammer:  // the slam on a group in front
                    return !late && Tap(EnemiesAhead(me, 250f, 1.2f) >= 2 || e != null && e.Def.IsBoss && v.ShootD < 220f, 6f);
                case WeaponId.Nimbus:  // Eye of the Storm on a crowd
                    return e != null && v.InReach && Tap(StormLocalCd <= 0f && (EnemiesNear(e.Pos, 160f) >= 2 || e.Def.IsBoss), 2f);
                case WeaponId.ArcLightning:  // Forked Volley whenever it's up
                    return e != null && v.InReach && Tap(ForkLocalCd <= 0f, 0.5f);
            }
            return null;
        }

        /// The Summoner's weapons. Whether the bot presses (or holds) its Alt; null for a weapon that isn't theirs.
        bool? SummonerAlt(AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            var e = v.ShootAt;
            switch (me.Weapon.Id)
            {
                case WeaponId.BoneGuard:  // an Ossuary Wall in the way of a group coming in
                    if (late || e == null || v.ShootD > 360f || !Tap(BoneWallLocalCd <= 0f && (EnemiesNear(me.Pos, 260f) >= 3 || v.Incoming >= 3), 2f))
                        return false;
                    aimAt = me.Pos + (e.Pos - me.Pos) * 0.5f;
                    return true;
            }
            return null;
        }

        /// The Knight's weapons. Whether the bot presses (or holds) its Alt; null for a weapon that isn't theirs.
        bool? KnightAlt(AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            var e = v.ShootAt;
            switch (me.Weapon.Id)
            {
                case WeaponId.Lance:  // Hook & Haul an enemy just out of reach, then Fire is the Breaching Thrust
                    return e != null && !e.Def.IsBoss && v.ShootD > 110f && v.ShootD < HookRange - 10f && Tap(true, HookCooldown + 0.2f);
                case WeaponId.Quarterstaff:  // jab while stacks last, at something close or shots coming in
                    return me.StaffStacks > 0 && !late && (EnemiesAhead(me, 170f, 0.8f) > 0 || v.Incoming >= 2);
                case WeaponId.Lightbringer:  // the Sunlance through a downed or hurt ally
                {
                    Player ally = v.Downed ?? (v.Lowest != null && v.Lowest.Hp < v.Lowest.MaxHp * 0.6f ? v.Lowest : null);
                    if (ally == null || me.Light < LanceNeed || SunlanceCd > 0f || ally.Pos.FartherThan(me.Pos, 440f) || !BotSees(me.Pos, ally.Pos))
                        return false;
                    if (SunlanceTarget(me, ally.Pos) != ally || !Tap(true, 1f))
                        return false;
                    aimAt = ally.Pos;
                    return true;
                }
                case WeaponId.Censer:  // the Cast on a crowd once a few Embers are lit (sooner with an ally to heal)
                    if (e == null || v.ShootD > 270f || me.Embers == 0 || CenserCastCd > 0f)
                        return false;
                    if (!Tap(me.Embers >= 3 || v.Downed != null || v.Lowest != null && v.Lowest.Hp < v.Lowest.MaxHp * 0.6f, 1f))
                        return false;
                    aimAt = e.Pos;
                    return true;
                case WeaponId.TwinBlades:  // Twin Fang at an enemy out of the chain's reach
                case WeaponId.Greatsword:  // the Moonwheel
                {
                    // The nearest enemy it can see 150-400 px off: thrown while closing in, or into a crowd.
                    Enemy far = null;
                    foreach (var en in Enemies.Values)
                    {
                        float d = en.Pos.DistanceTo(me.Pos);
                        if (!IsSpawning(en) && !SoulBound(en) && en.Hp > 0f && d >= 150f && d <= 400f && (far == null || d < far.Pos.DistanceTo(me.Pos)) && BotSees(me.Pos, en.Pos))
                            far = en;
                    }
                    if (far == null || me.FangOut != 0 || TwinFangCd > 0f || !Tap(!v.InReach || EnemiesNear(me.Pos, 400f) >= 4, 1f))
                        return false;
                    aimAt = far.Pos;
                }
                    return true;
                case WeaponId.Rapier:  // Riposte: raise it into shots or toward an enemy to lunge at, click once to lunge
                    if (!me.RapierGuard)
                    {
                        botRapierT = 0f;
                        return e != null && Tap(v.Incoming >= 2 || v.ShootD > 200f && v.ShootD < 600f, 2f);
                    }
                    botRapierT += dt;
                    firing = e != null && botRapierT >= 0.6f && botRapierT < 0.6f + dt * 1.5f;  // one click (Fire was up till now): the lunge
                    if (firing)
                        aimAt = e.Pos;
                    return botRapierT > 2.5f && Tap(true, 2f);  // no lunge came of it: drop the stance
            }
            return null;
        }

        /// The Ninja's weapons. Whether the bot presses (or holds) its Alt; null for a weapon that isn't theirs.
        bool? NinjaAlt(AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            var e = v.ShootAt;
            switch (me.Weapon.Id)
            {
                case WeaponId.Kusarigama:  // whirl a crowd in, let go for the snap once it's close
                    if (botAltWas)
                        return botAltHoldT < 2.5f && (botAltHoldT < 0.4f || EnemiesNear(me.Pos, 100f) < 3);
                    return !me.KusariWhirl && Tap(EnemiesNear(me.Pos, 200f) >= 3, 3.5f);
                case WeaponId.BoStaff:  // shove off something hugging you
                    return !late && Tap(EnemiesAhead(me, 80f, 0.9f) > 0 && (me.Hp < me.MaxHp * 0.6f || v.Near >= 2), 1.2f);
                case WeaponId.FumaShuriken:  // Orbit up close or under small shots, Throw at range
                {
                    bool orbit = v.ShootD < 190f || v.Incoming >= 3;
                    return Tap((me.FumaStance == 1) != orbit && (orbit || v.Incoming == 0), 1.5f);
                }
                case WeaponId.Katana:  // store a finisher, pair both finishers, cancel for opening moves (BotKatana.cs)
                    return BotKatana(v, dt, late, ref firing);
                case WeaponId.Kunai:  // blink to a mark near the target when it's safe there, or away from danger
                    return !late && me.KunaiMarkT > 0f && Tap(e != null && me.KunaiMark.CloserThan(e.Pos, 140f) && BotDanger < 1f || BotDanger > 2.5f && me.KunaiMark.FartherThan(me.Pos, 120f), 0.5f);
            }
            return null;
        }

        /// The Hemomancer's weapons. Whether the bot presses (or holds) its Alt; null for a weapon that isn't theirs.
        bool? HemomancerAlt(AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            switch (me.Weapon.Id)
            {
                case WeaponId.BloodLash:  // Martyr's Leash on a chained pack while healthy (it bleeds you)
                    if (botAltWas)
                        return botAltHoldT < 2f && me.Hp > me.MaxHp * 0.4f;
                    return !me.LeashOn && Tap(me.Chains.Count >= 2 && me.Hp > me.MaxHp * 0.5f && (BotDanger > 1f || v.Downed != null || v.Lowest != null && v.Lowest.Hp < v.Lowest.MaxHp * 0.5f), 6f);
                case WeaponId.BloodBrawler:  // Sympathy when the mark has company
                {
                    if (late || me.BsTarget == 0 || me.SympT > 0f || !Enemies.TryGetValue(me.BsTarget, out var mark))
                        return false;
                    return Tap(EnemiesNear(mark.Pos, 170f) >= 2, 8.5f);
                }
            }
            return null;
        }

        /// The fourth weapons, first half. Whether the bot presses (or holds) its Alt; null for a weapon that isn't theirs.
        bool? FourthAlt(AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            var e = v.ShootAt;
            switch (me.Weapon.Id)
            {
                case WeaponId.ArcKnuckles:  // Alt swaps stance: between combos it takes turns, Rocket to dash in on a target a way off, Brawl again once the chain is done
                    return !late && !me.Parrying && e != null && Tap(me.KnuckleStance == 0 ? me.KnuckleMove == 0 && v.ShootD > 90f : me.KnuckleChain == 0, 5f);
                case WeaponId.TwinRifleAuto:  // lock on the target (and let go of a lock that's lost)
                {
                    bool lost = me.TwinLock != 0 && !Enemies.ContainsKey(me.TwinLock);
                    if (me.TwinLock == 0 && e != null && v.InReach)
                    {
                        if (!Tap(true, 1f))
                            return false;
                        aimAt = e.Pos;
                        return true;
                    }
                    return !late && Tap(lost, 1f);
                }
                case WeaponId.OutOfAmmo:  // the spin when shots or bodies are right on you
                    return !late && me.AmmoSpin <= 0f && Tap(v.Incoming >= 3 || EnemiesNear(me.Pos, 90f) >= 2, 7.4f);
                case WeaponId.DoubleBarrel:  // both barrels point-blank
                    if (late || e == null || v.ShootD > 150f || me.DbUsed != 0 || me.DbReload > 0f)
                        return false;
                    if (Tap(true, 0.3f))
                    {
                        firing = false;
                        return true;
                    }
                    return false;
                case WeaponId.Stonespear:  // the volley at the boss or out of reach, with a few spears up
                    return !late && e != null && CountBits(me.SsMask) >= 2 && Tap(e.Def.IsBoss || !v.InReach || EnemiesNear(e.Pos, 120f) >= 2, 2f);
                case WeaponId.GrenadeLauncher:  // a smoke canister when a pack closes in
                    return !late && me.GlSmokeUsed < GlSmokeMax && Tap(EnemiesNear(me.Pos, 220f) >= 3, 2f);
            }
            return null;
        }

        /// The fourth weapons, second half. Whether the bot presses (or holds) its Alt; null for a weapon that isn't theirs.
        bool? FourthMoreAlt(AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            var e = v.ShootAt;
            switch (me.Weapon.Id)
            {
                case WeaponId.HoneybeeStaff:  // spray a group a little way off while the meter lasts
                    if (botAltWas)
                        return me.HoneyFill > 10f && e != null && v.ShootD < 260f;
                    if (e == null || v.ShootD < 100f || v.ShootD > 250f || me.HoneyFill < 40f || !Tap(EnemiesNear(e.Pos, 120f) >= 2, 1f))
                        return false;
                    aimAt = e.Pos;
                    return true;
                case WeaponId.Nunchaku:  // the guard under small shots, the Counter once it has some, off when it's quiet
                    if (me.NunGuard)
                    {
                        if (me.NunSwats >= 3 && e != null && v.ShootD < 170f)
                            firing = true;  // the Twin Counter
                        return Tap(v.Incoming == 0 && BotDanger < 0.5f, 1f);
                    }
                    return Tap(v.Incoming >= 3, 1f);
                case WeaponId.AcupunctureNeedles:  // Bloodletting at what it shoots, in reach (AcupunctureNeedles.cs)
                    if (late || e == null || v.ShootD >= BloodRange || !Tap(BloodletLocalCd <= 0f, 1f))
                        return false;
                    aimAt = e.Pos;  // (Fire may be aimed at a hurt teammate)
                    return true;
                case WeaponId.Ofuda:  // a Blast Ofuda at what it shoots, in reach (Ofuda.cs)
                    return !late && e != null && v.ShootD < BlastRange && Tap(OfudaLocalCd <= 0f, 1f);
                case WeaponId.StaticShock:  // the Arc Cannon into a crowd or the boss (StaticShock.cs)
                    return !late && e != null && v.ShootD < CannonReach && Tap(ArcLocalCd <= 0f && (e.Def.IsBoss || EnemiesNear(e.Pos, CannonBurstR) >= 3), 1f);
                case WeaponId.WindClash:  // the Tempest into a barrage or a crowd
                    return Tap(TempestLocalCd <= 0f && me.WcTempestT <= 0f && (v.Incoming >= 3 || EnemiesNear(me.Pos, 120f) >= 3), 1f);
                case WeaponId.WyvernStaff:  // the Wyrm Slam on a group
                    return Tap(WyvernSlamLocalCd <= 0f && EnemiesNear(me.Pos, 200f) >= 3, 1f);
                case WeaponId.NeedleSwarm:  // a fan of needles at a pack ahead, while the swarm is thick enough to spare them
                    return !late && e != null && v.ShootD < 320f && me.NsBurstCd <= 0f && me.NsCount > NsMax(me) * 0.6f && Tap(EnemiesNear(e.Pos, 120f) >= 2 || e.Def.IsBoss, 1f);
                case WeaponId.EmbolusSpears:  // burst the spears where they sit among enemies
                    if (late || !(EsOut(me.EsSt[0]) || EsOut(me.EsSt[1])))
                        return false;
                    foreach (var en in Enemies.Values)
                        if (!IsSpawning(en) && en.Hp > 0f && EnemiesNear(en.Pos, 90f) >= 2 && EsNear(me, en.Pos, 90f))
                            return Tap(true, 1f);
                    return false;
                case WeaponId.Halberd:  // set the pike while a pack closes in; lift it once they've been kept off or it has held a while
                    if (botAltWas)
                        return botAltHoldT < 3.5f && EnemiesNear(me.Pos, PikeScan) >= 1 && (v.Near >= 2 || botAltHoldT < 1f);
                    return Tap(v.Near >= 3 || EnemiesNear(me.Pos, 150f) >= 2, 1.2f);
                case WeaponId.BulwarkBarrier:  // Brace into a barrage; bash with it once Ram has built
                    if (botAltWas)
                    {
                        if (me.BbRam >= 60f && e != null && v.ShootD < 260f)
                            firing = true;  // the Shield Bash
                        return botAltHoldT < 4f && (v.Incoming >= 1 || BotDanger > 0.8f);
                    }
                    return Tap(v.Incoming >= 3 || EnemiesAhead(me, 70f, 1.2f) >= 2 || v.Near >= 3, 0.8f);  // (or a crowd close round it)
            }
            return null;
        }

        /// Is one of the bot's thrown Embolus Spears within `r` of `at`?
        static bool EsNear(Player me, V2 at, float r)
        {
            for (int i = 0; i < 2; i++)
                if (EsOut(me.EsSt[i]) && me.EsPos[i].CloserThan(at, r))
                    return true;
            return false;
        }

        static int CountBits(int m)
        {
            int n = 0;
            for (; m != 0; m &= m - 1)
                n++;
            return n;
        }
    }
}
