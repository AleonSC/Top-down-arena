using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Rodent Minigun (RodentMinigun.cs): the DPS Summoner's key 1 on the lobby's Ultimate row (the pick reaching the run on F),
// the portal's timeline (open, spin-up, full fire, wind-down, close) and how many rodents it spits, the weapon put away and the
// slow walk, the portal dragging after the aim at 240°/s, rodents hurting what they hit, the mix of mice, rats and hamsters
// (from the bullet id), the rat's shove (not heavies), hamsters going through one enemy, weapon mods not applying, Plague Rats,
// the client seeing the portal, the end on time, going down, mastered.
static partial class Program
{
    static void RodentMinigunTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var rodentHit = typeof(Game).GetMethod("RodentHit", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby -----------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.RodentMinigun);
        Check(def.Name == "Rodent Minigun" && def.Cooldown == 70f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Kit == Kit.Summoner && def.Role == Role.Dps && def.Capstone != null,
            "Rodent Minigun is the DPS Summoner's ultimate on key 1, 70 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Summoner) == AbilityId.Legion && Upgrades.Ultimate(Role.Dps, Kit.Summoner, 1) == AbilityId.RodentMinigun
            && Upgrades.Ultimate(Role.Tank, Kit.Summoner, 1) == AbilityId.BonePhalanx,
            "Legion stays the default; key 1 is Rodent Minigun; the Tank Summoner's key 1 is still Bone Phalanx");
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Plague Rats");
        Check(card != null && card.RequiresAbility == AbilityId.RodentMinigun && card.Rarity == Rarity.Rare, "its Rare card is Plague Rats");

        // --- The mix: which rodent comes from the bullet id -----------------------------------------------------------------
        int[] n = new int[3], plague = new int[3];
        for (int id = 1; id <= 20000; id++)
        {
            n[(int)Game.RodentOf(id, Game.RmRatShare)]++;
            plague[(int)Game.RodentOf(id, Game.RmRatShare + Game.RmPlagueShare)]++;
        }
        Console.WriteLine("  rodents: mice {0:0.0}%, rats {1:0.0}%, hamsters {2:0.0}%; with Plague Rats rats {3:0.0}%", n[0] / 200f, n[1] / 200f, n[2] / 200f, plague[1] / 200f);
        Check(Math.Abs(n[0] / 20000f - 0.74f) < 0.02f && Math.Abs(n[1] / 20000f - 0.16f) < 0.02f && Math.Abs(n[2] / 20000f - 0.10f) < 0.02f, "74% mice, 16% rats, 10% hamsters");
        Check(Math.Abs(plague[1] / 20000f - 0.30f) < 0.02f && Math.Abs(plague[2] / 20000f - 0.10f) < 0.02f, "Plague Rats makes 30% rats, hamsters unchanged");

        // --- Into a run -----------------------------------------------------------------------------------------------------
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 3 }, port, false, "Ratking", Role.Dps, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Summoner);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(1);
        settle();
        Check(host.Lobby[1].Ult == 1 && client.Lobby[1].Ult == 1 && host.Lobby[1].UltId == AbilityId.RodentMinigun, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        int U = Upgrades.UltSlot;
        Check(me.Slots[U].Id == AbilityId.RodentMinigun && client.Game.Players[1].Slots[U].Id == AbilityId.RodentMinigun, "in the match the host's F is Rodent Minigun, on every game");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        var spots = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, Enemy> pin = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; spots[e] = at; return e;
        };
        V2 mePos = new V2(300f, 450f), aim = new V2(900f, 450f);
        bool fire = true, hold = true;
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !spots.Keys.Any(e => e.Id == k)).ToList()) g.Enemies.Remove(id);
                foreach (var kv in spots) { kv.Key.Pos = kv.Value; kv.Key.KnockLeft = V2.Zero; }
                if (hold) me.Pos = mePos;
                me.Hp = me.MaxHp;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire });
            }
        };
        Action reset = () =>
        {
            g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); spots.Clear();
            me.RmT = 0f; me.UltT = 0f; me.Downed = false; me.RodentsFired = 0; me.Pos = mePos; me.Aim = 0f;
            me.Slots[U].Charges = 1; me.Slots[U].Timer = 0f;
            masteredField.SetValue(g, false);
            aim = new V2(900f, 450f); fire = true; hold = true;
            g.Players[2].Pos = new V2(300f, 900f);
            g.State = MatchState.Fighting;
        };

        // --- The cast and the timeline ---------------------------------------------------------------------------------------
        reset();
        var target = pin(new V2(620f, 450f), EnemyType.Grunt);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        float total = Game.RmTotal(Game.RmFull);
        Check(Game.RodentOn(me) && me.RmLen == Game.RmFull && Math.Abs(me.UltT - total) < 1e-3f, "casting opens the portal: 0.4 + 1 + 5 + 0.6 + 0.4 s");
        Check(g.Sounds.Any(c => c.Sfx == Sfx.RodentPortal), "the portal's sound goes with the cast");
        Check(Math.Abs(Game.RodentSpeed(me) - Game.RmSlow) < 1e-4f, "you walk at 75% speed");
        step(Game.RmOpen - 0.05f);
        Check(me.RodentsFired == 0, "nothing comes out while the portal opens");
        step(Game.RmSpin);
        int afterSpin = me.RodentsFired;
        step(Game.RmFull);
        int afterFull = me.RodentsFired;
        int weaponShots = g.Bullets.Values.Count(b => b.Owner == 1 && b.Kind != Defs.RodentShot);
        step(total - Game.RmOpen - Game.RmSpin - Game.RmFull + 0.1f);
        float lost = target.MaxHp - target.Hp;
        Console.WriteLine("  rodent minigun: {0} in the spin-up, {1} at full fire, {2} in all; the grunt ahead took {3:0}", afterSpin, afterFull - afterSpin, me.RodentsFired, lost);
        Check(Math.Abs(afterSpin - 16) <= 2, "the spin-up spits about 16 (6 a second rising to 36)");
        Check(Math.Abs(afterFull - afterSpin - 180) <= 2, "full fire spits 36 a second for 5 s");
        Check(Math.Abs(me.RodentsFired - 207) <= 4, "about 207 rodents a cast");
        Check(weaponShots == 0, "Fire held: the weapon is put away while the portal is open");
        Check(lost > 600f * me.Stats.DmgMult && lost < 2000f * me.Stats.DmgMult, "the grunt in front is chewed up (" + lost.ToString("0") + ")");
        Check(!Game.RodentOn(me) && me.RmT == 0f && me.UltT == 0f, "the portal closes on time");

        // --- The barrel drags after the aim ----------------------------------------------------------------------------------
        reset();
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        step(0.5f);
        aim = mePos + new V2(0f, 300f);  // a quarter turn
        step(0.25f);
        float turned = Math.Abs(MathX.WrapPi(client.Game.Players[1].RmAng)) * 180f / MathX.Pi, hostTurn = Math.Abs(MathX.WrapPi(me.RmAng)) * 180f / MathX.Pi;
        Console.WriteLine("  rodent minigun: 0.25 s after a 90° turn the portal has turned {0:0}° (the client sees {1:0}°)", hostTurn, turned);
        Check(hostTurn > 50f && hostTurn < 70f, "it turns 240° a second, so it lags the aim");
        Check(client.Game.Players[1].RmT > 0f && Math.Abs(turned - hostTurn) < 20f, "the client sees the portal and its heading");
        step(0.5f);
        Check(Math.Abs(MathX.WrapPi(me.RmAng) - MathX.Pi / 2f) < 0.02f, "then it catches up");

        // --- Going down ends it ----------------------------------------------------------------------------------------------
        me.Downed = true;
        step(dt * 2);
        Check(me.RmT == 0f && me.UltT == 0f, "going down shuts the portal");
        me.Downed = false;
        me.Hp = me.MaxHp;

        // --- Mastered: 2 s longer --------------------------------------------------------------------------------------------
        reset();
        masteredField.SetValue(g, true);
        cast.Invoke(g, new object[] { me, U, aim });
        Check(me.RmLen == Game.RmFullMastered && Math.Abs(me.UltT - Game.RmTotal(Game.RmFullMastered)) < 1e-3f, "mastered: 7 s of full fire");
        masteredField.SetValue(g, false);

        // --- Each rodent's damage, and no weapon mods ------------------------------------------------------------------------
        me.Stats.Pierce += 2;
        step(Game.RmOpen + Game.RmSpin);
        var rodents = g.Bullets.Values.Where(b => b.Kind == Defs.RodentShot && b.Owner == 1).ToList();
        float dm = me.Stats.DmgMult;
        bool dmgOk = rodents.Count > 0, pierceOk = rodents.Count > 0;
        foreach (var b in rodents)
        {
            var kind = Game.RodentOf(b.Id, Game.RatShareOf(me));
            float want = (kind == Game.Rodent.Rat ? Game.RmRatDmg : kind == Game.Rodent.Hamster ? Game.RmHamsterDmg : Game.RmMouseDmg) * dm;
            dmgOk &= Math.Abs(b.Dmg - want) < 0.01f;
            pierceOk &= b.Pierce == (kind == Game.Rodent.Hamster ? 1 : 0);
        }
        Check(dmgOk, "mice hit for 5, rats 9, hamsters 6 (times your damage)");
        Check(pierceOk, "hamsters go through one enemy; Piercing Rounds don't add to any of them");
        me.Stats.Pierce -= 2;

        // --- The rat's shove, and Plague Rats ---------------------------------------------------------------------------------
        reset();
        int ratId = Enumerable.Range(1, 1000).First(i => Game.RodentOf(i, Game.RmRatShare) == Game.Rodent.Rat);
        int mouseId = Enumerable.Range(1, 1000).First(i => Game.RodentOf(i, Game.RmRatShare) == Game.Rodent.Mouse);
        var grunt = pin(new V2(600f, 300f), EnemyType.Grunt);
        var heavyType = Enum.GetValues(typeof(EnemyType)).Cast<EnemyType>().First(t => (int)t < 250 && Defs.Get(t).Heavy && !Defs.Get(t).IsBoss && Defs.Get(t).Speed > 0f);
        var heavy = pin(new V2(600f, 600f), heavyType);
        Func<int, Bullet> rodent = id => new Bullet { Id = id, Kind = Defs.RodentShot, Owner = 1, Vel = new V2(600f, 0f), Dmg = 9f };
        rodentHit.Invoke(g, new object[] { rodent(mouseId), grunt });
        Check(grunt.KnockLeft.Length < 0.01f, "a mouse doesn't shove");
        rodentHit.Invoke(g, new object[] { rodent(ratId), grunt });
        rodentHit.Invoke(g, new object[] { rodent(ratId), heavy });
        Check(Math.Abs(grunt.KnockLeft.Length - Game.RmRatShove) < 0.5f && grunt.KnockLeft.X > 0f && heavy.KnockLeft.Length < 0.01f, "a rat shoves a grunt 20 px along its flight; a heavy doesn't budge");
        Check(grunt.BleedT <= 0f, "without Plague Rats nothing bleeds");
        card.Apply(me.Stats);
        rodentHit.Invoke(g, new object[] { rodent(ratId), grunt });
        Check(grunt.BleedT > 0f && grunt.BleedDps >= Game.RmPlagueBleed - 0.01f && Math.Abs(Game.RatShareOf(me) - 0.30f) < 1e-4f, "Plague Rats: rats make what they bite bleed, and 30% of rodents are rats");

    }
}
