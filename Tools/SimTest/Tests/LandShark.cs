using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Land Shark (LandShark.cs), the DPS Summoner's 4th choice on the lobby's 4th key: the table, the pick reaching the run, the puddle
// at the cursor (clamped to 420 px), nothing hit before the bite, the bite (120 once and a 0.5 s stun to everything in the puddle,
// heavies too; enemies outside untouched), other screens seeing it, the puddle draining away, Great White and mastery (130 px, 160).
static partial class Program
{
    static void LandSharkTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        var def = Upgrades.Ability(AbilityId.LandShark);
        Check(def.Name == "Land Shark" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Summoner && def.Role == Role.Dps && def.Cooldown == 14f,
            "Land Shark: a 14 s DPS Summoner 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Dps, Kit.Summoner) == 4 && Upgrades.FirstAbility(Role.Dps, Kit.Summoner, 3) == AbilityId.LandShark
            && Upgrades.FirstAbility(Role.Dps, Kit.Summoner, 0) == AbilityId.BatSwarm && Upgrades.FirstAbility(Role.Dps, Kit.Summoner, 2) == AbilityId.AlphaCall,
            "the DPS Summoner's 4th key is Land Shark; its first three are unchanged");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.LandShark) && Upgrades.All.Any(u => u.Name == "Great White" && u.RequiresAbility == AbilityId.LandShark && u.Rarity == Rarity.Rare),
            "Land Shark is a very rare card for DPS Summoners who picked another, and Great White a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Shark", Role.Dps, 0, 3, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.LandShark && client.Game.Players[1].Slots[0].Id == AbilityId.LandShark, "the 4th pick reaches the run, on both screens");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 mePos = new V2(400f, 450f);
        var mine = new HashSet<int>();
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; mine.Add(e.Id); return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                g.Bullets.Clear(); g.Minions.Clear();
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                me.Pos = mePos;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = mePos + new V2(300f, 0f) });
            }
        };
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Zones.Clear();
            me.Pos = mePos;
            me.MasteryXp.Remove(AbilityId.LandShark);
            make(EnemyType.Grunt, new V2(1500f, 120f));  // far off: the wave goes on
        };
        Func<Zone> shark = () => g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.LandShark);
        Action<V2> cast = at => { me.Slots[0].Charges = 1; hostAbility.Invoke(g, new object[] { 1, 0, at, false }); };
        V2 spot = mePos + new V2(300f, 0f);

        // The puddle opens at the cursor; the bite lands once, on everything in it.
        reset();
        var inside = make(EnemyType.Grunt, spot + new V2(30f, 20f));
        var heavy = make(EnemyType.Thunderhead, spot + new V2(-40f, -10f));
        var outside = make(EnemyType.Grunt, spot + new V2(200f, 0f));
        float hi = inside.Hp, hh = heavy.Hp, ho = outside.Hp;
        cast(spot);
        var z0 = shark();
        Check(z0 != null && z0.Pos.DistanceTo(spot) < 1f && Math.Abs(z0.Radius - 100f) < 0.5f && Math.Abs(z0.Aim) < 0.05f && Math.Abs(z0.T - Game.LandSharkLife) < 0.02f,
            "the puddle opens at the cursor, 100 px, heading along the aim");
        step(Game.LandSharkBiteAt - 0.1f);
        Check(inside.Hp == hi && heavy.Hp == hh, "nothing is bitten before the shark reaches the top of its jump");
        Check(client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.LandShark && z.Pos.DistanceTo(spot) < 2f), "other screens see the puddle");
        bool stunned = false;
        for (int i = 0; i < 6; i++) { step(0.05f); stunned |= inside.StunT > 0f; }
        Check(hi - inside.Hp > 115f && hi - inside.Hp < 125f && stunned, "the bite: 120 and a stun to the grunt in the puddle");
        Check(hh - heavy.Hp > 115f && hh - heavy.Hp < 125f, "a heavy enemy is bitten too");
        Check(outside.Hp == ho, "an enemy outside the puddle is left alone");
        float after = inside.Hp;
        step(Game.LandSharkLife - Game.LandSharkBiteAt + 0.2f);
        Check(inside.Hp == after, "it bites only once");
        Check(shark() == null && client.Game.Zones.Values.All(z => z.Kind != ZoneKind.LandShark), "the puddle drains away, on every screen");

        // Out of reach: the puddle opens 420 px out along the aim.
        reset();
        cast(mePos + new V2(900f, 0f));
        Check(shark() != null && Math.Abs(shark().Pos.DistanceTo(mePos) - Game.LandSharkRange) < 2f, "a far cursor: the puddle opens 420 px out");

        // Great White.
        reset();
        me.Stats.LandSharkDmgMult = 1.4f;
        var strong = make(EnemyType.Grunt, spot);
        float sl = strong.Hp;
        cast(spot);
        step(Game.LandSharkBiteAt + 0.1f);
        Check(sl - strong.Hp > 162f && sl - strong.Hp < 174f, "Great White: 40% harder (168)");
        me.Stats.LandSharkDmgMult = 1f;

        // Mastery: a 130 px puddle and a 160 bite.
        reset();
        me.MasteryXp[AbilityId.LandShark] = 1e6f;
        var rim = make(EnemyType.Grunt, spot + new V2(118f, 0f));
        float rl = rim.Hp;
        cast(spot);
        Check(shark() != null && Math.Abs(shark().Radius - 130f) < 0.5f, "mastered: a 130 px puddle");
        step(Game.LandSharkBiteAt + 0.1f);
        float want = 160f * Game.MasteryPower(me, AbilityId.LandShark);  // (mastery levels add their usual power on top)
        Check(Math.Abs(rl - rim.Hp - want) < 5f, "and the bite hits for 160, out to its wider rim");
        me.MasteryXp.Remove(AbilityId.LandShark);
    }
}
