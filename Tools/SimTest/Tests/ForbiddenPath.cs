using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Forbidden Path (ForbiddenPath.cs): the DPS Ninja's key 1 on the lobby's Ultimate row (the pick reaching the run on F), the shed
// (rooted, untouchable), the beast's speed, the lock-on (the healthiest in reach, a new one when it dies), Fire held running the
// five stages back to back (Pounce lands you beside the target, every stage hurts, the Spiral pulls, the shades, the Gate's slam
// and the brands bursting), the weapon put away, the health drain (never below 1), a kill's heal, the client seeing the beast,
// its lock and the brands, the end on time, going down, mastered, Forbidden Hunger.
static partial class Program
{
    static void ForbiddenPathTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby -----------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.ForbiddenPath);
        Check(def.Name == "Forbidden Path" && def.Cooldown == 75f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Kit == Kit.Ninja && def.Role == Role.Dps && def.Capstone != null,
            "Forbidden Path is the DPS Ninja's ultimate on key 1, 75 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Ninja) == AbilityId.DeathLotus && Upgrades.Ultimate(Role.Dps, Kit.Ninja, 1) == AbilityId.ForbiddenPath
            && Upgrades.Ultimate(Role.Tank, Kit.Ninja, 1) == AbilityId.DragonsDogma,
            "Death Lotus stays the default; key 1 is Forbidden Path; the Tank Ninja's key 1 is still Dragon's Dogma");
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Forbidden Hunger");
        Check(card != null && card.RequiresAbility == AbilityId.ForbiddenPath && card.Rarity == Rarity.Rare, "its Rare card is Forbidden Hunger");

        // --- Into a run -----------------------------------------------------------------------------------------------------
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 3 }, port, false, "Beast", Role.Dps, 0, 0, Kit.Ninja);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Ninja);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(1);
        settle();
        Check(host.Lobby[1].Ult == 1 && client.Lobby[1].Ult == 1 && host.Lobby[1].UltId == AbilityId.ForbiddenPath, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        int U = Upgrades.UltSlot;
        Check(me.Slots[U].Id == AbilityId.ForbiddenPath && client.Game.Players[1].Slots[U].Id == AbilityId.ForbiddenPath, "in the match the host's F is Forbidden Path, on every game");
        g.DevSetGod(2, true);

        var spots = new Dictionary<Enemy, V2>();
        Func<V2, float, Enemy> pin = (at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.FireT = 99f; spots[e] = at; return e;
        };
        V2 home = new V2(300f, 450f), aim = new V2(900f, 450f);
        bool fire = true, keepHp = true, freeze = true;
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !spots.Keys.Any(e => e.Id == k)).ToList()) g.Enemies.Remove(id);
                foreach (var kv in spots.ToList())
                {
                    kv.Key.FireT = 99f;
                    if (freeze) kv.Key.Pos = kv.Value; else spots[kv.Key] = kv.Key.Pos;
                    kv.Key.KnockLeft = V2.Zero;
                }
                if (keepHp) me.Hp = me.MaxHp;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire });
            }
        };
        Action reset = () =>
        {
            g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); spots.Clear();
            me.FpT = 0f; me.FpUp = false; me.FpStage = 0; me.UltT = 0f; me.Downed = false; me.InvulnT = 0f;
            me.FpHits = 0; me.FpKills = 0; me.FpStages = 0; me.Pos = home; me.Aim = 0f; me.Hp = me.MaxHp;
            me.Slots[U].Charges = 1; me.Slots[U].Timer = 0f;
            masteredField.SetValue(g, false);
            fire = false; keepHp = true; freeze = true;
            g.Players[2].Pos = new V2(300f, 900f);
            g.State = MatchState.Fighting;
        };

        // --- The cast: the shed, then a fast beast -------------------------------------------------------------------------
        reset();
        var target = pin(new V2(560f, 450f), 1e6f);  // the healthiest in reach: the lock
        var ring = new[] { pin(new V2(560f, 380f), 1e5f), pin(new V2(620f, 470f), 1e5f), pin(new V2(520f, 520f), 1e5f) };
        var far = pin(new V2(1250f, 450f), 2e6f);  // healthier, but out of reach
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        Check(Game.FpOn(me) && Math.Abs(me.UltT - (Game.FpShed + Game.FpTime)) < 1e-3f && me.InvulnT >= Game.FpShed - 1e-3f,
            "casting starts the shed: 0.6 s untouchable, then 8 s of beast");
        Check(Game.FpSpeed(me) == 0f, "shedding roots you");
        step(Game.FpShed + 0.05f);
        Check(Math.Abs(Game.FpSpeed(me) - Game.FpSpeedMult) < 1e-4f, "then the beast runs at 135% speed");
        Check(me.FpTarget == target.Id, "it locks the healthiest enemy within 420 px, not the healthier one out of reach");
        Check(me.FpStage == 0 && me.Pos.DistanceTo(home) < 1f, "without Fire nothing happens");

        // --- Fire held: the whole combo ------------------------------------------------------------------------------------
        fire = true;
        step(Game.FpLeap + 0.02f);
        float besides = me.Pos.DistanceTo(target.Pos);
        Check(me.FpStage == 1 && besides < target.Radius + me.Def.BodyR + 10f, "the Pounce lands you beside the target (" + besides.ToString("0") + " px off)");
        Check(target.Hp < target.MaxHp && ring.All(e => e.Hp < e.MaxHp) && far.Hp == far.MaxHp, "its burst hurts the target and the ones round it, not the far one");
        Check(ring.All(e => e.FpBrands >= 1) && target.FpBrands >= 1, "each one hit is branded");
        int weaponShots = g.Bullets.Values.Count(b => b.Owner == 1);
        var ringStart = ring.Select(e => e.Pos.DistanceTo(target.Pos)).ToArray();
        freeze = false;
        step(Game.FpPounceTime - Game.FpLeap + Game.FpRakeTime + 0.05f);
        Check(me.FpStages == 3 && me.FpStage == 3, "the Rake runs, then the Spiral starts at once");
        step(Game.FpSpiralTime);
        var ringEnd = ring.Select(e => e.Pos.DistanceTo(target.Pos)).ToArray();
        Check(ringEnd.Zip(ringStart, (a, b) => a < b - 5f || a < 60f).All(x => x), "the Spiral pulls the others in");
        Check(target.FpBrands == Game.FpBrandMax, "the bites pile brands on the target, up to 5");
        freeze = true;
        step(Game.FpRendTime + 0.05f);
        Check(me.FpStages == 5 && me.FpStage == 5, "the Rend, then the Gate");
        int brandsBefore = ring.Sum(e => e.FpBrands) + target.FpBrands;
        float hpBeforeSlam = target.Hp;
        step(Game.FpGateTime);
        Console.WriteLine("  forbidden path: {0} hits, the target lost {1:0} (the slam and its brands {2:0}); brands before the Gate {3}", me.FpHits, target.MaxHp - target.Hp, hpBeforeSlam - target.Hp, brandsBefore);
        Check(target.FpBrands == 0 && ring.All(e => e.FpBrands == 0), "the Gate bursts every brand");
        float want = (Game.FpGateDmg + Game.FpBrandDmg * Game.FpBrandMax) * me.Stats.DmgMult;
        Check(Math.Abs(hpBeforeSlam - target.Hp - want) < 1f, "the slam hits for 90 and the five brands for 50 (times your damage)");
        Check(g.Bullets.Values.Count(b => b.Owner == 1) == weaponShots && weaponShots == 0, "Fire held: the weapon is put away");
        Check(me.FpNext == 1, "the combo goes round to the Pounce again");

        // --- The client sees it -------------------------------------------------------------------------------------------
        step(Game.FpPounceTime * 0.5f);
        settle();
        var cme = client.Game.Players[1];
        Enemy ct;
        Check(cme.FpT > 0f && cme.FpTarget == target.Id && cme.FpStage == me.FpStage, "the client sees the beast, its lock and its stage");
        Check(client.Game.Enemies.TryGetValue(target.Id, out ct) && ct.FpBrands == target.FpBrands && ct.FpBrands > 0, "and the brands");

        // --- A new lock when the target dies, and a kill's heal -----------------------------------------------------------
        fire = false;
        step(Game.FpPounceTime + 0.05f);
        keepHp = false;
        me.Hp = me.MaxHp * 0.5f;
        target.Hp = 1f;
        float hpBefore = me.Hp;
        fire = true;
        step(Game.FpLeap + 0.02f);
        Check(me.FpKills >= 1 && me.Hp > hpBefore + me.MaxHp * Game.FpKillHeal * 0.8f, "a kill heals 5% of your max health");
        spots.Remove(target);
        step(dt * 3);
        Check(me.FpTarget != target.Id && me.FpTarget >= 0 && me.FpTarget != far.Id, "the dead target's lock moves to the next in reach");

        // --- The drain ------------------------------------------------------------------------------------------------------
        fire = false;
        step(Game.FpTime);  // (let any stage finish)
        reset();
        pin(new V2(560f, 450f), 1e6f);
        cast.Invoke(g, new object[] { me, U, aim });
        step(Game.FpShed + 0.02f);
        keepHp = false;
        float h0 = me.Hp;
        step(1f);
        Check(Math.Abs(h0 - me.Hp - me.MaxHp * Game.FpDrain) < me.MaxHp * 0.005f, "the beast drains 3% of your max health a second");
        me.Hp = 1.5f;
        step(1f);
        Check(me.Hp >= 1f && !me.Downed, "the drain never takes you below 1");

        // --- The end on time ------------------------------------------------------------------------------------------------
        keepHp = true;
        step(Game.FpTime);
        Check(!Game.FpOn(me) && me.FpT == 0f && me.UltT == 0f && Math.Abs(Game.FpSpeed(me) - 1f) < 1e-4f, "it ends on time, at normal speed");

        // --- Going down ends it ---------------------------------------------------------------------------------------------
        reset();
        cast.Invoke(g, new object[] { me, U, aim });
        me.Downed = true;
        step(dt * 2);
        Check(me.FpT == 0f && me.UltT == 0f, "going down ends it");
        me.Downed = false;
        me.Hp = me.MaxHp;

        // --- Mastered, and Forbidden Hunger ---------------------------------------------------------------------------------
        reset();
        masteredField.SetValue(g, true);
        cast.Invoke(g, new object[] { me, U, aim });
        Check(Math.Abs(me.UltT - (Game.FpShed + Game.FpTimeMastered)) < 1e-3f, "mastered: 11 s of beast");
        masteredField.SetValue(g, false);
        reset();
        card.Apply(me.Stats);
        Check(Math.Abs(me.Stats.FpDmgMult - 1.2f) < 1e-4f && Math.Abs(me.Stats.FpKillHealMult - 2f) < 1e-4f, "Forbidden Hunger: 20% harder, kills heal twice as much");
        var lone = pin(new V2(560f, 450f), 1e6f);
        cast.Invoke(g, new object[] { me, U, aim });
        step(Game.FpShed + 0.02f);
        fire = true;
        step(Game.FpLeap + 0.02f);
        Check(Math.Abs(lone.MaxHp - lone.Hp - Game.FpPounceDmg * me.Stats.DmgMult * 1.2f) < 0.5f, "and the Pounce hits for 36 (times your damage)");
    }
}
