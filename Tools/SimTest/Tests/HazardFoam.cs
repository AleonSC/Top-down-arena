using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Hazard Foam (HazardFoam.cs): the Healer Soldier's key 3 on the lobby's Ultimate row. A foam sprayer replaces the weapon for up to
// 12 s (85% speed); held Fire drains its 100 tank (mastered 130) at 14 a second and lobs globs at the cursor that set into lumps of
// foam (one zone each, up to 24 px). A lump is solid to players and enemies and stops every shot; enemy shots (10) and enemies
// pressed against it (8 a second) wear it down, and enemies touching it slow. Bosses plough through. Each lump lasts 60 s
// (mastered 90), shrinking over the last 6 s. It's seen on every screen.
static partial class Program
{
    static void HazardFoamTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);

        var def = Upgrades.Ability(AbilityId.HazardFoam);
        Check(def.Name == "Hazard Foam" && def.Cooldown == 100f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Role == Role.Healer && def.Kit == Kit.Soldier && def.Capstone != null,
            "Hazard Foam is the Healer Soldier's ultimate on key 3, 100 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Soldier, 1) == AbilityId.FieldHospital && Upgrades.Ultimate(Role.Healer, Kit.Soldier, 2) == AbilityId.SupplyDrop
            && Upgrades.Ultimate(Role.Healer, Kit.Soldier, 3) == AbilityId.HazardFoam && Upgrades.ClampUlt(Role.Healer, Kit.Soldier, 3) == 3
            && Upgrades.Ultimate(Role.Dps, Kit.Soldier, 3) != AbilityId.HazardFoam,
            "the Field Hospital and Supply Drop keep keys 1 and 2; key 3 is Hazard Foam, only for the Healer Soldier");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 6 }, port, false, "Medic", Role.Healer, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(3);
        settle();
        Check(host.Lobby[1].UltId == AbilityId.HazardFoam && client.Lobby[1].Ult == 3, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting, noPicks);
        var me = g.Players[1];
        var ally = g.Players[client.MyId];
        Check(me.Slots[Upgrades.UltSlot].Id == AbilityId.HazardFoam, "in the match the host's ultimate is Hazard Foam");
        g.DevSetGod(client.MyId, true);

        V2 meAt = new V2(500f, 500f), allyAt = new V2(300f, 250f), aim = new V2(700f, 500f);
        bool fire = false, pinMe = true;
        var pinned = new Dictionary<int, V2>();
        var free = new HashSet<int>();  // enemies kept but not held in place
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !pinned.ContainsKey(k)).ToList()) g.Enemies.Remove(id);
                foreach (var e in g.Enemies.Values) { if (!free.Contains(e.Id)) e.Pos = pinned[e.Id]; e.FireT = 99f; }
                g.State = MatchState.Fighting;
                if (pinMe) me.Pos = meAt;
                me.InvulnT = 1f; me.Hp = me.MaxHp; ally.Pos = allyAt;
                noPicks();
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire });
            }
        };
        step(0.2f);
        Func<V2, float, Enemy> enemy = (at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.SpawnT = 0f; e.Hp = e.MaxHp = hp; pinned[e.Id] = at;
            return e;
        };
        enemy(new V2(120f, 120f), 1e7f);  // a keeper far off, so the wave isn't over
        Action charge = () => { me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; };
        Func<IEnumerable<Zone>> lumps = () => g.Zones.Values.Where(q => q.Kind == ZoneKind.HazardFoam);

        // the sprayer
        float speedBefore = me.Speed;
        charge();
        hostAbility.Invoke(g, new object[] { me.Id, Upgrades.UltSlot, aim, false });
        Check(Game.FoamOn(me) && me.FoamT > Game.FoamTime - 0.1f && me.FoamTank == Game.FoamTankMax && me.UltT > 0f, "the sprayer goes on: 12 s and a full 100 tank");
        Check(Math.Abs(me.Speed - speedBefore * Game.FoamSlow) < 0.5f, "carrying it you move 85% as fast (" + me.Speed.ToString("0") + " of " + speedBefore.ToString("0") + ")");
        step(0.5f);
        Check(me.FoamTank == Game.FoamTankMax && !lumps().Any(), "without Fire nothing sprays and the tank stays full");

        // spraying: globs, lumps at the cursor, the tank draining, not the weapon
        fire = true;
        step(1f);
        fire = false;
        Check(me.FoamGlobs >= Game.FoamRate * 0.8f && me.FoamGlobs <= Game.FoamRate * 1.2f, "held Fire lobs 16 globs a second (" + me.FoamGlobs + ")");
        Check(Math.Abs(Game.FoamTankMax - Game.FoamDrain - me.FoamTank) < 1.5f, "and drains 14 of the tank a second (" + me.FoamTank.ToString("0.0") + " left)");
        Check(g.Bullets.Values.All(b => b.Kind == Defs.FoamGlob || b.Owner != me.Id), "and not the weapon");
        step(0.8f);
        var all = lumps().ToList();
        Check(all.Count >= 2 && all.All(z => z.Pos.CloserThan(aim, 70f)), "the globs set into lumps of foam round the cursor (" + all.Count + ")");
        Check(all.Any(z => z.Radius > Game.FoamR0 + 1f) && all.All(z => z.Radius <= Game.FoamMaxR + 0.01f), "lumps grow as foam piles on, up to 24 px");
        Check(all.All(z => Math.Abs(z.Duration - Game.FoamLife) < 0.01f), "each lasts 60 s");
        settle();
        Check(Game.FoamOn(cg.Players[me.Id]) && cg.Zones.Values.Count(q => q.Kind == ZoneKind.HazardFoam) == all.Count, "the other screen sees the sprayer and the foam");

        // it's solid: your shots, enemy shots, enemies and allies
        var lump = all.OrderByDescending(z => z.Radius).First();
        var behind = enemy(lump.Pos + new V2(120f, 0f), 1000f);
        shoot.Invoke(g, new object[] { Defs.DpsShot, lump.Pos - new V2(60f, 0f), new V2(700f, 0f), 0f, 1f, 50f, ally.Id, false, 0f });
        step(0.4f);
        Check(behind.Hp == 1000f, "a player's shot stops in the foam (the enemy behind it isn't touched)");
        float soak = lump.Soak;
        shoot.Invoke(g, new object[] { (byte)Defs.SmallShot, lump.Pos + new V2(80f, 0f), new V2(-500f, 0f), 0f, 1f, 10f, 0, false, 0f });
        step(0.4f);
        Check(!g.Bullets.Values.Any(b => Defs.IsEnemyShot(b.Kind)) && Math.Abs(soak - lump.Soak - Game.FoamShotWear) < 0.5f, "an enemy shot stops in it and wears it by 10 (" + (soak - lump.Soak).ToString("0.0") + ")");
        pinned.Remove(behind.Id); g.Enemies.Remove(behind.Id);

        var stuck = enemy(lump.Pos, 1000f);  // dropped in the middle: pushed out to the rim, slowed and chewing
        free.Add(stuck.Id);
        step(dt * 2f);
        Check(stuck.Pos.DistanceTo(lump.Pos) >= Game.FoamR(lump) + stuck.Radius * 0.8f - 0.5f, "an enemy can't stand in the foam: it's pushed out to the rim");
        Check(stuck.FoamT > 0f, "and is slowed against it");
        free.Remove(stuck.Id);
        pinned[stuck.Id] = stuck.Pos + (lump.Pos - stuck.Pos).Normalized() * 3f;  // pressed against it for a second
        soak = lump.Soak;
        step(1f);
        Check(soak - lump.Soak > Game.FoamChew * 0.6f && soak - lump.Soak < Game.FoamChew * 1.6f, "an enemy pressed against it chews 8 a second (" + (soak - lump.Soak).ToString("0.0") + ")");
        pinned.Remove(stuck.Id); g.Enemies.Remove(stuck.Id);

        pinMe = false;
        me.Pos = lump.Pos;
        step(dt * 2f);
        Check(me.Pos.DistanceTo(lump.Pos) >= Game.FoamR(lump) + me.Def.HitR - 0.5f, "you can't walk through it either");
        pinMe = true;

        // worn to nothing, it breaks
        soak = lump.Soak;
        int lumpId = lump.Id;
        typeof(Game).GetMethod("WearFoam", flags).Invoke(g, new object[] { lump, soak + 1f });
        step(dt * 2f);
        Check(!g.Zones.ContainsKey(lumpId), "a lump worn to nothing breaks apart");

        // it dissolves: shrinking over the last 6 s, gone at 60 s
        var old = lumps().First();
        float full = Game.FoamR(old);
        old.T = Game.FoamFizz * 0.5f;
        Check(Game.FoamR(old) < full * 0.7f && Game.FoamR(old) > full * 0.3f, "over its last 6 s a lump shrinks as it fizzes away");
        old.T = dt;
        step(dt * 2f);
        Check(!g.Zones.ContainsKey(old.Id), "and then it's gone");

        // the tank runs dry: the sprayer comes off
        fire = true;
        step(me.FoamTank / Game.FoamDrain + 0.3f);
        fire = false;
        Check(!Game.FoamOn(me) && me.FoamT == 0f && me.UltT == 0f, "an empty tank takes the sprayer off");
        settle();
        Check(!Game.FoamOn(cg.Players[me.Id]), "on the other screen too");
        Check(lumps().Count() <= Game.FoamCap, "never more than 80 lumps (" + lumps().Count() + ")");

        // the time runs out
        charge();
        hostAbility.Invoke(g, new object[] { me.Id, Upgrades.UltSlot, aim, false });
        step(Game.FoamTime + 0.2f);
        Check(!Game.FoamOn(me) && me.FoamTank == 0f, "unused, the sprayer comes off after 12 s");

        // mastered: a bigger tank and longer-lasting foam; going down takes it off
        foreach (var z in lumps().ToList()) g.Zones.Remove(z.Id);
        me.MasteryXp[AbilityId.HazardFoam] = 1e6f;
        charge();
        hostAbility.Invoke(g, new object[] { me.Id, Upgrades.UltSlot, aim, false });
        Check(me.FoamTank == Game.FoamTankMastered, "mastered, a 130 tank");
        fire = true;
        step(0.5f);
        fire = false;
        step(0.6f);
        Check(lumps().Any() && lumps().All(z => Math.Abs(z.Duration - Game.FoamLifeMastered) < 0.01f), "and the foam lasts 90 s");
        me.InvulnT = 0f;
        me.Downed = true;
        g.FixedStep(dt, new PlayerInput { AimAt = aim });
        Check(me.FoamT == 0f, "going down takes the sprayer off");
        me.Downed = false; me.Hp = me.MaxHp;

        host.Leave(""); client.Leave("");
    }
}
