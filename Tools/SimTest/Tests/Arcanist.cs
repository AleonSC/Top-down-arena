using System;
using System.Linq;
using Arena;

// The Arcanist (Arcanist.cs, Docs/Arcanist.md): one weapon, 1st ability and ultimate a role, standing in for the locked lobby places.
// Tank Orbit Shards / Kinetic Grasp / Grand Sigil, Healer Glyph Stylus / Arcane Tether / Sanctum Sigil, DPS Dancing Blades /
// Telekinetic Crush / Runic Array, and the Kinetic Field passive.
static partial class Program
{
    static void ArcanistTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var hostDash = typeof(Game).GetMethod("HostWeaponDash", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        var shotSpeed = typeof(Game).GetMethod("ArcShotSpeed", flags);
        var prepare = typeof(Game).GetMethod("PrepareArcBullets", flags);

        // The table.
        Check(Defs.Weapon(Role.Tank, Kit.Arcanist, 0).Id == WeaponId.OrbitShards && Defs.Weapon(Role.Healer, Kit.Arcanist, 0).Id == WeaponId.GlyphStylus
            && Defs.Weapon(Role.Dps, Kit.Arcanist, 0).Id == WeaponId.DancingBlades && Defs.WeaponGap(Role.Dps, Kit.Arcanist, 1) && Defs.WeaponGap(Role.Tank, Kit.Arcanist, 2),
            "one weapon a role: Orbit Shards, Glyph Stylus, Dancing Blades (the other places locked)");
        Check(Upgrades.FirstAbility(Role.Tank, Kit.Arcanist, 0) == AbilityId.KineticGrasp && Upgrades.Ultimate(Role.Tank, Kit.Arcanist) == AbilityId.GrandSigil
            && Upgrades.FirstAbility(Role.Healer, Kit.Arcanist, 0) == AbilityId.ArcaneTether && Upgrades.Ultimate(Role.Healer, Kit.Arcanist) == AbilityId.SanctumSigil
            && Upgrades.FirstAbility(Role.Dps, Kit.Arcanist, 0) == AbilityId.TelekineticCrush && Upgrades.Ultimate(Role.Dps, Kit.Arcanist) == AbilityId.RunicArray
            && Upgrades.AbilityGap(Role.Dps, Kit.Arcanist, 1), "Kinetic Grasp / Grand Sigil, Arcane Tether / Sanctum Sigil, Telekinetic Crush / Runic Array");
        Check(Defs.KitName(Kit.Arcanist) == "Arcanist" && Math.Abs(Defs.KitHpMult(Kit.Arcanist) - 0.9f) < 0.001f && !Defs.KitLocked(Role.Tank, Kit.Arcanist)
            && !Defs.KitLocked(Role.Healer, Kit.Arcanist), "the Arcanist: 90% health, open to every role");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Arc", Role.Dps, 0, 0, Kit.Arcanist);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0, 0, Kit.Arcanist);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var tank = g.Players[2];
        Check(me.Kit == Kit.Arcanist && me.Weapon.Id == WeaponId.DancingBlades && me.Slots[0].Id == AbilityId.TelekineticCrush && me.Slots[Upgrades.UltSlot].Id == AbilityId.RunicArray,
            "a DPS Arcanist starts with the Dancing Blades, Telekinetic Crush and Runic Array");
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        V2 mePos = new V2(500f, 450f), tankPos = new V2(500f, 800f), aim = new V2(800f, 450f);
        bool fire = false, alt = false;
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = mePos;
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire, WeaponAction = alt });
            }
        };
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 5000f; e.SpawnT = 0f; e.FireT = 99f; e.Pos = e.Disp = at;
            return e;
        };
        Func<V2, V2, Bullet> enemyShot = (at, vel) => (Bullet)spawnBullet.Invoke(g, new object[] { Defs.SmallShot, at, vel, 0f, 3f, 10f, 0, false, 0f });
        Action<int> ready = slot => { me.Slots[slot].Timer = 0f; me.Slots[slot].Charges = 1; };

        // Dancing Blades: three hover behind you; a click sends one to dance on the enemy at the cursor.
        g.Enemies.Clear();
        make(EnemyType.Grunt, new V2(1500f, 880f));
        var target = make(EnemyType.Grunt, aim);
        step(0.6f);
        var blades = g.Minions.Values.Where(m => m.Owner == 1 && m.Kind == MinionKind.ArcBlade).ToList();
        Check(blades.Count == 3 && blades.All(b => !b.Sent), "three Dancing Blades wait behind you");
        float hp0 = target.Hp;
        fire = true; step(0.05f); fire = false;
        Check(blades.Count(b => b.Sent) == 1, "a click launches one blade");
        step(0.8f);
        Check(target.Hp < hp0 - 2f * me.Weapon.Damage && blades.Any(b => b.Sent && b.Wind >= 1f), "it reaches the enemy and dances on it, slashing (" + (hp0 - target.Hp) + " damage)");

        // The dance's look, Crossing Cuts (Game.BladeDanceAt): through the middle on each slash tick, a turn on each time, inside the
        // slash circle, with no jumps.
        {
            V2 at = new V2(300f, 200f);
            const float dIn = 0.7f;
            bool mids = true, inside = true, smooth = true;
            float h, hPrev, maxStep = 0f, maxTurn = 0f;
            V2 prev = Game.BladeDanceAt(at, dIn, 0f, out hPrev);
            for (int k = 0; k <= 4; k++)
            {
                V2 p = Game.BladeDanceAt(at, dIn, k / 4f, out h);
                mids &= p.DistanceTo(at) < 0.01f && Math.Abs(MathX.WrapPi(h - (dIn + k * Game.BladeCutTurn))) < 0.01f;
            }
            for (int i = 1; i <= 240; i++)
            {
                V2 p = Game.BladeDanceAt(at, dIn, i / 240f, out h);
                inside &= p.DistanceTo(at) <= Game.BladeSlashR + 0.5f;
                maxStep = Math.Max(maxStep, p.DistanceTo(prev));
                maxTurn = Math.Max(maxTurn, Math.Abs(MathX.WrapPi(h - hPrev)));
                prev = p;
                hPrev = h;
            }
            smooth = maxStep < 6f && maxTurn < 0.6f;
            Check(mids && inside && smooth, "Crossing Cuts: through the middle on each tick, each cut a turn on, within the slash circle, no jumps (step "
                + maxStep.ToString("0.0") + " px, turn " + maxTurn.ToString("0.00") + " rad at 240 fps)");
        }
        hostDash.Invoke(g, new object[] { 1, 54, aim });
        Check(blades.Where(b => b.Sent).All(b => b.Returning), "Alt calls the blades home");
        step(1f);
        Check(blades.All(b => !b.Sent), "and they come back to wait behind you");

        // Runic Array: a rift behind you pours blades on its own, each curving in on an enemy near the cursor.
        g.Enemies.Clear();
        var away = make(EnemyType.Brute, new V2(1500f, 880f));
        var t1 = make(EnemyType.Brute, aim + new V2(0f, -60f));
        var t2 = make(EnemyType.Brute, aim + new V2(40f, 70f));
        float awayHp = away.Hp, t1Hp = t1.Hp, t2Hp = t2.Hp;
        ready(Upgrades.UltSlot);
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, aim, false });
        Check(me.ArrayT > 5f, "Runic Array opens its rift for 6 s");
        step(0.3f);
        Check(g.Bullets.Values.All(b => b.Kind != Defs.SpectralBlade), "no blades while the rift is still opening (0.45 s)");
        step(0.5f);
        var flying = g.Bullets.Values.Where(b => b.Kind == Defs.SpectralBlade).ToList();
        Check(flying.Count >= 2 && flying.All(b => b.ArrayBlade && (b.HomeOn == t1.Id || b.HomeOn == t2.Id)), "with no Fire it sends blades, each homing on an enemy near the cursor ("
            + flying.Count + ")");
        Check(flying.Any(b => b.HomeOn == t1.Id) && flying.Any(b => b.HomeOn == t2.Id), "taking the enemies there in turn");
        step(1.5f);
        Check(t1.Hp < t1Hp && t2Hp > t2.Hp, "the blades curve in and hit both");
        Check(away.Hp == awayHp, "and leave an enemy away from the cursor alone");
        Check(client.Game.Players[1].ArrayT > 0f, "the client sees the Array (for its rift)");
        step(4f);
        Check(me.ArrayT == 0f, "it shuts after 6 s");
        int left = g.Bullets.Values.Count(b => b.Kind == Defs.SpectralBlade);
        step(2f);
        Check(g.Bullets.Values.Count(b => b.Kind == Defs.SpectralBlade) == 0, "and sends no more blades (" + left + " were still out)");
        me.ArrayT = 0f;
        step(1.2f);

        // Telekinetic Crush (TelekineticCrush.cs): a toggle grip on a force meter that carries enemies round; let go to slam or fling.
        V2 aimWas = aim;
        g.Enemies.Clear();
        make(EnemyType.Grunt, new V2(1500f, 880f));
        var a1 = make(EnemyType.Grunt, aim + new V2(-30f, 0f));
        var a2 = make(EnemyType.Grunt, aim + new V2(30f, 20f));
        me.CrushMeter = 1f; me.CrushLockT = 0f;
        step(0.05f);
        Check(me.Slots[0].Charges == 1 && !me.CrushOn, "Telekinetic Crush is ready with a full meter");
        hostAbility.Invoke(g, new object[] { 1, 0, aim, false });
        step(0.4f);
        var grip = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.ArcCrush && z.Owner == 1);
        Check(me.CrushOn && grip != null && g.CrushHolding(me) == 2, "pressed, a grip opens at the cursor and lifts the two grunts in it");
        Check(me.CrushMeter < 0.95f && me.CrushMeter > 0.8f, "the meter drains while it's open (" + me.CrushMeter.ToString("0.00") + ")");
        Check(client.Game.Players[1].CrushOn && client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.ArcCrush), "the client sees the grip");
        aim = new V2(800f, 300f);
        step(0.8f);
        Check(grip.Pos.DistanceTo(aim) < 30f && a1.Pos.CloserThan(grip.Pos, grip.Radius) && a2.Pos.CloserThan(grip.Pos, grip.Radius),
            "it follows the cursor, carrying them along");
        var caughtShot = enemyShot(grip.Pos + new V2(-120f, 0f), new V2(500f, 0f));
        step(0.4f);
        Check(!g.Bullets.ContainsKey(caughtShot.Id) && grip.Aim >= 1f, "an enemy shot that enters the grip is caught");
        float h1 = a1.Hp;
        int gripBlades0 = g.Bullets.Values.Count(b => b.Kind == Defs.SpectralBlade);
        hostAbility.Invoke(g, new object[] { 1, 0, aim, false });
        step(0.05f);
        float slam = Game.CrushSlamDmg + Game.CrushSlamPer;
        Check(!me.CrushOn && h1 - a1.Hp >= slam * 0.9f && a1.StunT > 0.3f, "let go with the grip still: a slam, 30 +10 for the other one (" + (h1 - a1.Hp) + ")");
        Check(g.Bullets.Values.Count(b => b.Kind == Defs.SpectralBlade) > gripBlades0, "and the caught shot flies back out as a spectral blade");
        float closedAt = me.CrushMeter;
        step(0.6f);
        Check(me.CrushMeter == closedAt, "closed, the meter waits a moment...");
        step(0.8f);
        Check(me.CrushMeter > closedAt + 0.05f, "...then refills (" + closedAt.ToString("0.00") + " to " + me.CrushMeter.ToString("0.00") + ")");

        // Fling: whip the grip toward a wall and let go on the move.
        g.Enemies.Clear();
        make(EnemyType.Grunt, new V2(1500f, 880f));
        aim = new V2(780f, 450f);
        var f1 = make(EnemyType.Grunt, aim);
        me.CrushMeter = 1f;
        hostAbility.Invoke(g, new object[] { 1, 0, aim, false });
        step(0.4f);
        Check(g.CrushHolding(me) == 1, "a new grip lifts the grunt");
        aim = new V2(500f, 0f);
        float fh = f1.Hp;
        step(0.12f);
        float whip = me.CrushVel.Length;
        hostAbility.Invoke(g, new object[] { 1, 0, aim, false });
        step(1.2f);
        Check(whip >= Game.CrushFlingMin && fh - f1.Hp >= Game.CrushImpact * 0.9f && fh - f1.Hp < slam,
            "let go on the move: it's flung on and slams into the wall for 25 (" + (fh - f1.Hp) + " at " + whip.ToString("0") + " px/s)");

        // Heavies are only slowed; running the meter dry burns it out.
        g.Enemies.Clear();
        aim = new V2(780f, 450f);
        var brute = make(EnemyType.RiotCaptain, aim);  // (a heavy mini-boss)
        me.CrushMeter = 0.3f;
        hostAbility.Invoke(g, new object[] { 1, 0, aim, false });
        step(0.3f);
        Check(me.CrushOn && brute.Def.Heavy && g.CrushHolding(me) == 0 && brute.SlowT > 0f, "a heavy enemy isn't lifted, only slowed");
        step(1.5f);
        Check(!me.CrushOn && me.CrushMeter == 0f && me.Slots[0].Charges == 0, "run dry, the grip lets go on its own");
        hostAbility.Invoke(g, new object[] { 1, 0, aim, false });
        step(0.05f);
        Check(!me.CrushOn, "and it's burnt out: it won't open again yet");
        me.CrushMeter = 1f; me.CrushLockT = 0f;
        aim = aimWas;
        step(0.2f);

        // Kinetic Field: enemy shots near an Arcanist slow down.
        g.Enemies.Clear();
        var near = enemyShot(mePos + new V2(60f, 0f), new V2(0f, 100f));
        var far = enemyShot(mePos + new V2(400f, 0f), new V2(0f, 100f));
        prepare.Invoke(g, null);
        Check((float)shotSpeed.Invoke(g, new object[] { near }) == Game.FieldSlow && (float)shotSpeed.Invoke(g, new object[] { far }) == 1f,
            "Kinetic Field: an enemy shot within 90 px flies at 75% speed, one farther off at full");
        g.Bullets.Remove(near.Id); g.Bullets.Remove(far.Id);

        // --- The Tank (the client's seat): Orbit Shards, Kinetic Grasp, Grand Sigil.
        Action<float> tstep = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = mePos;
                tank.Pos = tankPos;
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim });
            }
        };
        tstep(0.6f);
        var shards = g.Minions.Values.Where(m => m.Owner == 2 && m.Kind == MinionKind.ArcShard).ToList();
        Check(shards.Count == 4 && client.Game.Minions.Values.Count(m => m.Kind == MinionKind.ArcShard) == 4, "four Orbit Shards circle the Tank (and the client sees them)");
        var cut = enemyShot(shards[0].Pos + new V2(0f, -1f), new V2(0f, 10f));
        tstep(0.05f);
        Check(!g.Bullets.ContainsKey(cut.Id), "an orbiting shard cuts a small shot");
        hostDash.Invoke(g, new object[] { 2, 52, aim });
        Check(tank.ShardGuard && tank.Speed < Defs.Get(Role.Tank).Speed * Defs.KitSpeedMult(Kit.Arcanist) * 0.6f, "Alt held: Shard Guard, and the Tank slows to 55%");
        var blocked = enemyShot(tankPos + new V2(Game.ShardGuardR - 4f, 0f), new V2(-10f, 0f));
        tstep(0.05f);
        Check(!g.Bullets.ContainsKey(blocked.Id), "Shard Guard stops a shot reaching its ring");
        hostDash.Invoke(g, new object[] { 2, 53, aim });
        Check(!tank.ShardGuard, "letting go of Alt drops the guard");

        // Kinetic Grasp: lift the biggest enemy near the cursor, hold it, throw it on the second press.
        g.Enemies.Clear();
        make(EnemyType.Grunt, new V2(1500f, 880f));
        var small = make(EnemyType.Grunt, tankPos + new V2(250f, 0f));
        var big = make(EnemyType.Grunt, tankPos + new V2(270f, 40f));
        big.MaxHp = big.Hp = 9000f;
        var victim = make(EnemyType.Grunt, tankPos + new V2(-80f, -200f));
        tank.Slots[0].Timer = 0f; tank.Slots[0].Charges = 1;
        hostAbility.Invoke(g, new object[] { 2, 0, tankPos + new V2(260f, 20f), false });
        Check(tank.KgHeld == big.Id, "Kinetic Grasp lifts the biggest enemy near the cursor");
        tank.Aim = (victim.Pos - tankPos).Angle;
        tstep(0.6f);
        Check(big.Pos.DistanceTo(Game.GraspHoldPoint(tank, big.Radius)) < 20f && big.StunT > 0f, "and holds it in front of you, helpless");
        Check(client.Game.Players[2].KgHeld == big.Id, "the client knows what is held (for the look and the second press)");
        float bigHp = big.Hp, vHp = victim.Hp;
        tank.Aim = (victim.Pos - big.Pos).Angle;  // (the client's own aim comes in with its input)
        hostAbility.Invoke(g, new object[] { 2, 0, aim, false });
        Check(tank.KgHeld == 0, "the second press throws it (no cooldown spent)");
        tstep(0.6f);
        Check(victim.Hp < vHp - 30f && big.Hp < bigHp - 30f, "the thrown enemy and the one it hits both take the blow");

        // Grand Sigil: enemy shots freeze on its rim and fly back out as spectral blades when it ends.
        g.Enemies.Clear();
        make(EnemyType.Grunt, new V2(1500f, 880f));
        tank.Slots[Upgrades.UltSlot].Timer = 0f; tank.Slots[Upgrades.UltSlot].Charges = 1;
        hostAbility.Invoke(g, new object[] { 2, Upgrades.UltSlot, aim, false });
        var sigil = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.GrandSigil);
        Check(sigil != null && sigil.Radius == Game.SigilRadius, "Grand Sigil draws a 200 px circle round the Tank");
        var frozen = enemyShot(tankPos + new V2(Game.SigilRadius + 60f, 0f), new V2(-300f, 0f));
        tstep(0.6f);
        V2 stopAt = frozen.Pos;
        tstep(0.3f);
        Check(g.Bullets.ContainsKey(frozen.Id) && frozen.Pos.DistanceTo(stopAt) < 0.5f && frozen.Pos.DistanceTo(tankPos) <= Game.SigilRadius + 8f, "a shot reaching its rim freezes there");
        int blades0 = g.Bullets.Values.Count(b => b.Kind == Defs.SpectralBlade);
        sigil.T = 0.01f;
        tstep(0.05f);
        Check(!g.Bullets.ContainsKey(frozen.Id) && g.Bullets.Values.Count(b => b.Kind == Defs.SpectralBlade) > blades0, "as it ends the frozen shot flies back out as a spectral blade");

        host.Leave(""); client.Leave("");
        ArcanistHealerTest(dt);
    }

    /// The Healer Arcanist: Glyph Stylus heals an ally it passes, Arcane Tether pulls one in, Sanctum Sigil's runes burst.
    static void ArcanistHealerTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Healer", Role.Healer, 0, 0, Kit.Arcanist);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        Check(me.Weapon.Id == WeaponId.GlyphStylus && me.Slots[0].Id == AbilityId.ArcaneTether && me.Slots[Upgrades.UltSlot].Id == AbilityId.SanctumSigil,
            "a Healer Arcanist starts with the Glyph Stylus, Arcane Tether and Sanctum Sigil");
        g.Enemies.Clear();
        V2 mePos = new V2(500f, 450f), allyPos = new V2(700f, 450f);
        bool fire = false, holdAlly = true;
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = mePos;
                if (holdAlly) ally.Pos = allyPos;
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = allyPos, Fire = fire });
            }
        };
        step(0.2f);
        ally.Hp = ally.MaxHp * 0.3f;
        float hp0 = ally.Hp;
        fire = true; step(0.5f); fire = false;
        Check(ally.Hp > hp0 + 5f, "a Glyph Stylus bolt through an ally heals them (" + (ally.Hp - hp0) + ")");

        // Arcane Tether: the ally is pulled to your side and healed.
        allyPos = new V2(850f, 600f);
        step(0.1f);
        holdAlly = false;
        ally.Hp = ally.MaxHp * 0.3f;
        hp0 = ally.Hp;
        me.Slots[0].Timer = 0f; me.Slots[0].Charges = 1;
        hostAbility.Invoke(g, new object[] { 1, 0, allyPos, false });
        Check(ally.Hp >= hp0 + Game.TetherHeal - 0.5f && ally.InvulnT > 0.3f, "Arcane Tether heals the ally and makes them untouchable on the way");
        Run(s, dt, 1, () => ally.Pos.DistanceTo(mePos) < 60f);
        Check(client.Game.Me.Pos.DistanceTo(mePos) < 80f, "and pulls them to your side (their own game moves them)");

        // Sanctum Sigil: heals inside; a rune bursts on the first enemy to touch it.
        holdAlly = true;
        allyPos = mePos + new V2(40f, 0f);
        me.Slots[Upgrades.UltSlot].Timer = 0f; me.Slots[Upgrades.UltSlot].Charges = 1;
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        var z = g.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.SanctumSigil);
        Check(z != null && (int)z.Aim == (1 << Game.SanctumRunes) - 1 && z.Follow, "Sanctum Sigil follows you with all six runes up");
        ally.Hp = ally.MaxHp * 0.3f;
        hp0 = ally.Hp;
        step(1f);
        Check(ally.Hp > hp0 + 15f, "allies inside heal (" + (ally.Hp - hp0) + " in a second)");
        V2 rune = Game.SanctumRune(z, 0);
        var e1 = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, rune, 0f, 0, false });
        e1.Hp = e1.MaxHp = 5000f; e1.SpawnT = 0f; e1.Pos = e1.Disp = Game.SanctumRune(z, 0);
        float eh = e1.Hp;
        step(0.05f);
        Check(e1.Hp < eh - 30f && (int)z.Aim != (1 << Game.SanctumRunes) - 1, "a rune bursts on the enemy that touches it, and is spent");
        host.Leave(""); client.Leave("");
    }
}
