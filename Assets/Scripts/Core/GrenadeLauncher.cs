using System;

namespace Arena
{
    /// The Tank Soldier's Grenade Launcher (the 4th weapon key; Docs/Mockups/grenade-launcher.html): a revolving drum launcher.
    ///   HE drum (Player.GlMode 0): each click lobs a grenade (Defs.GrenadeShot, Bullet.GlNade) over enemies to the cursor (GlMin to
    ///     GlRange px), GlGap apart. It flies GlFlight(distance), hops GlHop px in GlHopTime, then bursts: WeaponDef damage in GlRadius,
    ///     half at the edge, and a small shove (ExplodeGrenade in Combat.cs calls GlBlast). GlDrum rounds (Heavy Drum: more), then a
    ///     reload of GlReloadTime (Player.GlReload counts it down, speeding up with fire rate). Player.GlUsed counts the rounds spent.
    ///   Alt (Msg.WeaponDash, WeaponAlt.Drum): lobs a smoke canister to the cursor (like the Riot Repeater's Tear Gas), GlSmokeGap apart, while the
    ///     HE drum keeps firing. Player.GlMode and GlSwap stay 0 (the old drum swap, kept in the snapshot). The canister (ZoneKind.SmokeShell) flies like a grenade, bounces
    ///     twice (GlSmokeHops) and opens into a cloud GlSmokeRadius round for GlSmokeLife. Regular enemies in it are slowed GlSmokeSlow
    ///     and can't attack (Enemy.SmokeT, read with the daze in EnemyAI.cs); bosses and mini-bosses are only slowed GlSmokeBossSlow.
    ///     GlSmokeMax canisters (Player.GlSmokeUsed counts the spent ones), one back every GlSmokeRegen s in either drum (Player.GlCharge).
    ///   Put away, it is ready again: full drum, full smoke, HE drum. Zone.GuardFrac on a smoke zone is the canister's flight distance
    ///   (muzzle to cursor), for drawing its arc (Game/GrenadeLauncherLook.cs).
    public partial class Game
    {
        public const int GlDrum = 6, GlSmokeMax = 3;
        public const float GlGap = 0.42f, GlReloadTime = 1.5f, GlRadius = 72f, GlRange = 380f, GlMin = 60f, GlEdge = 0.5f, GlShove = 22f;
        public const float GlHop = 12f, GlHopTime = 0.16f, GlSwapTime = 0.55f, GlSmokeGap = 0.55f, GlSwapFlip = 0.42f, GlMuzzle = 30f;
        public const float GlSmokeRadius = 105f, GlSmokeLife = 6f, GlSmokeRegen = 6f, GlSmokeSlow = 0.45f, GlSmokeBossSlow = 0.3f, GlSmokeGrow = 0.7f;
        public const float GlSmokeHop1 = 16f, GlSmokeHop2 = 6f, GlSmokeHopT1 = 0.16f, GlSmokeHopT2 = 0.1f;
        public static readonly Rgba GlBoomColor = new Rgba(1f, 0.62f, 0.27f);  // a Grenade Launcher blast's Boom (nothing else uses it)

        /// Seconds a grenade or canister flies (before its hops) to land `dist` px out.
        public static float GlFlight(float dist) => 0.3f + dist / 950f;

        /// How high the arc of a `dist` px lob peaks, px.
        public static float GlPeak(float dist) => 22f + dist * 0.13f;

        /// Seconds from launch until a smoke canister flown `dist` px opens.
        public static float GlSmokeLand(float dist) => GlFlight(dist) + GlSmokeHopT1 + GlSmokeHopT2;

        /// HE rounds in this player's drum (Heavy Drum: more).
        public static int GlDrumOf(Player p) => GlDrum + (p != null ? p.Stats.GlDrumBonus : 0);

        /// Is a smoke zone still flying in or bouncing (not a cloud yet)?
        public static bool GlSmokeInFlight(Zone z) => z.Duration - z.T < GlSmokeLand(z.GuardFrac);

        /// How far a smoke cloud reaches now: it billows out over GlSmokeGrow after it opens, 0 before.
        public static float GlSmokeReach(Zone z)
        {
            float since = z.Duration - z.T - GlSmokeLand(z.GuardFrac);
            if (since < 0f)
                return 0f;
            float k = Math.Min(1f, since / GlSmokeGrow);
            return z.Radius * (1f - (1f - k) * (1f - k) * (1f - k));
        }

        /// Is enemy `e` jammed by a smoke cloud right now (inside one, and not a boss)? Every game can tell, from the zones.
        public bool InGlSmoke(Enemy e, bool countBosses = false)
        {
            if (!countBosses && (e.Def.IsBoss || e.Def.Mini))
                return false;
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.SmokeShell && z.T > 0.3f && e.Pos.CloserThan(z.Pos, GlSmokeReach(z) + e.Radius * 0.3f))
                    return true;
            return false;
        }

        float glLocalCd;  // your own game: until Alt lobs again

        /// Host, every tick the launcher is out: the swap, the reload, the smoke coming back, and the trigger.
        void TickGrenadeLauncher(Player p, float dt)
        {
            if (p.GlSmokeUsed > 0)
            {
                p.GlCharge += dt / GlSmokeRegen;
                if (p.GlCharge >= 1f)
                {
                    p.GlCharge -= 1f;
                    p.GlSmokeUsed--;
                    BroadcastFx(FxKind.Ring, p.Pos, GlSmokeColor, 24f, 0.25f, Sfx.TankShellLoad);
                }
            }
            else
                p.GlCharge = 0f;
            if (p.GlReload > 0f)
            {
                p.GlReload -= dt * p.Stats.FireRateMult;
                if (p.GlReload <= 0f)
                {
                    p.GlReload = 0f;
                    p.GlUsed = 0;
                    Sounds.Add(Sfx.TankShellLoad);
                }
            }
            else if (p.GlUsed >= GlDrumOf(p))
                p.GlReload = GlReloadTime;
            if (!p.Firing || p.ParryActive || p.FireCd > 0f || State == MatchState.GameOver)
                return;
            if (p.GlReload > 0f || p.GlUsed >= GlDrumOf(p))
                return;
            p.FireCd = GlGap / FireRate(p);
            FirePlayer(p);  // FireVolley: LaunchGrenade (and Barrage's copies)
            p.GlUsed++;
            p.GlShot = (byte)(p.GlShot + 1);
            if (p.GlUsed >= GlDrumOf(p))
                p.GlReload = GlReloadTime;
        }

        /// Host, every tick: a launcher put away is ready again when it comes back out.
        void ResetGrenadeLauncher(Player p)
        {
            p.GlMode = 0;
            p.GlUsed = 0;
            p.GlSmokeUsed = 0;
            p.GlCharge = p.GlReload = p.GlSwap = 0f;
        }

        /// Where a lob from `p` along `aim` lands: the cursor's distance (GlMin to GlRange), inside the arena. `from` is the muzzle.
        V2 GlTarget(Player p, float aim, out V2 from, V2? at = null)
        {
            V2 dir = V2.FromAngle(aim);
            from = p.Pos + dir * GlMuzzle;
            float dist = Math.Max(GlMin, Math.Min(GlRange * p.Stats.ShotSpeedMult, (at ?? p.AimAt).DistanceTo(p.Pos)));
            return ClampToArena(p.Pos + dir * dist, 12f);
        }

        /// One HE grenade (FireVolley's case): flies to the cursor, hops, then bursts where its flight ends (Combat.cs: GlBlast).
        void LaunchGrenade(Player p, float aim, float dmg, int extra)
        {
            for (int k = 0; k <= extra; k++)  // each extra projectile: one more grenade, fanned out to alternating sides
                LaunchOneGrenade(p, aim + (k == 0 ? 0f : (k % 2 == 1 ? 1f : -1f) * 0.12f * ((k + 1) / 2)), dmg);
        }

        void LaunchOneGrenade(Player p, float aim, float dmg)
        {
            V2 from, at = GlTarget(p, aim, out from);
            V2 dir = (at - from).Length > 1f ? (at - from).Normalized() : V2.FromAngle(aim);
            float dist = at.DistanceTo(from), life = GlFlight(dist) + GlHopTime;
            V2 end = at + dir * GlHop;
            var b = SpawnBullet(Defs.GrenadeShot, from, (end - from) / life, 0f, life, p.Weapon.Damage * dmg, p.Id);
            b.Splash = GlRadius * AreaMult(p);
            b.GlNade = true;
            b.Burst = 1;  // (marks it as landing where its flight ends: MoveBullets)
        }

        /// Host: the HE burst (from ExplodeGrenade): full damage at the middle, GlEdge of it at the rim, a small shove outward.
        void GlBlast(Bullet b)
        {
            foreach (var e in FoesInCircle(b.Pos, b.Splash, by: b.Owner))
            {
                if (BlocksShot(e, b.Pos))
                    continue;
                float d = e.Pos.DistanceTo(b.Pos), k = Math.Min(1f, d / Math.Max(1f, b.Splash));
                HurtEnemy(e, b.Dmg * (1f - (1f - GlEdge) * k), b.Owner);
                if (!e.Def.IsBoss && !e.Def.Mini && d > 0.5f)
                    KnockEnemy(e, (e.Pos - b.Pos) * (GlShove * (1f - 0.5f * k) / d));
            }
            BroadcastFx(FxKind.Boom, b.Pos, GlBoomColor, b.Splash, 0.6f, Sfx.Explode);
            PropBlast(b.Pos, b.Splash, b.Owner);
            GateLockBlast(b.Pos, b.Splash, b.Owner);
        }

        public static readonly Rgba GlSmokeColor = new Rgba(0.37f, 0.83f, 0.74f);

        /// Host: a smoke canister to the cursor (the smoke drum's shot).
        void LaunchSmoke(Player p, V2 aimAt)
        {
            V2 toward = aimAt - p.Pos;
            float aim = toward.Length > 1f ? toward.Angle : p.Aim;
            V2 from, at = GlTarget(p, aim, out from, aimAt);
            V2 dir = (at - from).Length > 1f ? (at - from).Normalized() : V2.FromAngle(aim);
            float dist = at.DistanceTo(from);
            V2 rest = ClampToArena(at + dir * (GlSmokeHop1 + GlSmokeHop2), 12f);
            var z = AddZone(ZoneKind.SmokeShell, p, rest, GlSmokeRadius * AreaMult(p) * p.Stats.GlCloudMult, GlSmokeLand(dist) + GlSmokeLife);
            z.Aim = dir.Angle;
            z.GuardFrac = dist;
            p.GlSmokeUsed++;
            p.GlShot = (byte)(p.GlShot + 1);
            Sounds.Add(Sfx.TankGasLaunch);
        }

        /// Host: Alt. Lob a smoke canister to the cursor (if one is loaded).
        void HostSmokeLob(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.GrenadeLauncher || p.StunT > 0f || p.GasCd > 0f || p.GlSmokeUsed >= GlSmokeMax || State == MatchState.GameOver)
                return;
            p.GasCd = GlSmokeGap;
            LaunchSmoke(p, aimAt);
        }

        /// Host, every tick: smoke clouds land, then slow and jam what's in them; the launcher refills when put away.
        void TickSmokeShells(float dt)
        {
            foreach (var p in Players.Values)
                if (p.Weapon.Id != WeaponId.GrenadeLauncher)
                    ResetGrenadeLauncher(p);
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.SmokeShell || GlSmokeInFlight(z))
                    continue;
                if (!z.Landed)
                {
                    z.Landed = true;  // it pops open and hisses
                    BroadcastFx(FxKind.Spark, z.Pos, GlSmokeColor, 6f, 0.1f, Sfx.TankGasHiss);
                }
                float reach = GlSmokeReach(z);
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e) || e.Pos.FartherThan(z.Pos, reach + e.Radius * 0.3f))
                        continue;
                    bool big = e.Def.IsBoss || e.Def.Mini;
                    e.WadeT = Math.Max(e.WadeT, 0.2f);  // the wade slow (as a Bloodbath's): it reaches Heavy enemies and bosses too, with no frost look
                    e.WadeMult = Math.Min(e.WadeMult, 1f - (big ? GlSmokeBossSlow : GlSmokeSlow));
                    if (!big)
                        e.SmokeT = 0.2f;  // jammed: no attacks while it lasts (EnemyAI.cs)
                }
            }
        }

        /// Your own game: Alt with the launcher out. True when it sent the smoke lob.
        bool LocalDrumSwap(Player me, V2 aimAt, bool pressed, float dt)
        {
            glLocalCd = Math.Max(0f, glLocalCd - dt);
            if (!pressed || me.Weapon.Id != WeaponId.GrenadeLauncher || glLocalCd > 0f)
                return false;
            glLocalCd = GlSmokeGap;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.Drum, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.Drum);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        // --- Network: the player's Grenade Launcher fields (SyncPlayer) ---

        void SyncGrenadeLauncher(NetSync s, Player p)
        {
            s.Byte(ref p.GlMode);  // the Grenade Launcher
            s.Byte(ref p.GlUsed);
            s.Byte(ref p.GlSmokeUsed);
            s.Byte(ref p.GlShot);
            s.Float(ref p.GlReload);
            s.Float(ref p.GlSwap);
            s.Float(ref p.GlCharge);
        }

        /// WeaponDef.BotAlt (BotWeaponAlt.cs): a smoke canister when a pack closes in
        static bool GrenadeLauncherBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            return !late && me.GlSmokeUsed < GlSmokeMax && g.Tap(g.EnemiesNear(me.Pos, 220f) >= 3, 2f);
        }
    }
}
