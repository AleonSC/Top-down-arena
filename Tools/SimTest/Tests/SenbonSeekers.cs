using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Senbon Seekers (SenbonSeekers.cs), the DPS Ninja's 4th choice on the lobby's 4th key: the table, the pick reaching the run,
// three needles a cast up to 9 (the key doing nothing at 9), no timer, a needle launching only at an enemy within 200 px, the hit
// (20 and a poison stack, the needle used up), at most two on one enemy, poison stacking to 5, a needle whose enemy dies flying
// back into the ring, other screens seeing the ring and the needles, going down losing them, Hidden Needles and mastery (12).
static partial class Program
{
    static void SenbonTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        var def = Upgrades.Ability(AbilityId.SenbonSeekers);
        Check(def.Name == "Senbon Seekers" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Ninja && def.Role == Role.Dps && def.Cooldown == 6f,
            "Senbon Seekers: a 6 s DPS Ninja 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Dps, Kit.Ninja) == 4 && Upgrades.FirstAbility(Role.Dps, Kit.Ninja, 3) == AbilityId.SenbonSeekers
            && Upgrades.FirstAbility(Role.Dps, Kit.Ninja, 0) == AbilityId.ShadowStep && Upgrades.FirstAbility(Role.Dps, Kit.Ninja, 2) == AbilityId.BladeFan,
            "the DPS Ninja's 4th key is Senbon Seekers; its first three are unchanged");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.SenbonSeekers) && Upgrades.All.Any(u => u.Name == "Hidden Needles" && u.RequiresAbility == AbilityId.SenbonSeekers && u.Rarity == Rarity.Rare),
            "Senbon Seekers is a very rare card for DPS Ninjas who picked another, and Hidden Needles a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Senbon", Role.Dps, 0, 3, Kit.Ninja);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.SenbonSeekers && client.Game.Players[1].Slots[0].Id == AbilityId.SenbonSeekers, "the 4th pick reaches the run, on both screens");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 mePos = new V2(500f, 450f);
        var mine = new HashSet<int>();
        var pins = new Dictionary<int, V2>();  // enemies held where they were made
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; mine.Add(e.Id); pins[e.Id] = at; return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(b => b.Kind != Defs.Senbon).Select(b => b.Id).ToList()) g.Bullets.Remove(id);
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; V2 at; if (pins.TryGetValue(e.Id, out at)) e.Pos = at; }
                me.Pos = mePos;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = mePos + new V2(300f, 0f) });
            }
        };
        Action reset = () =>
        {
            mine.Clear(); pins.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            me.Pos = mePos; me.SbN = 0; me.SbCap = false;
            me.MasteryXp.Remove(AbilityId.SenbonSeekers);
            make(EnemyType.Grunt, new V2(1500f, 120f));  // far off: the wave goes on
        };
        Action cast = () => { me.Slots[0].Charges = 1; hostAbility.Invoke(g, new object[] { 1, 0, mePos, false }); };
        Func<int> flying = () => g.Bullets.Values.Count(b => b.Kind == Defs.Senbon && b.Owner == 1);

        // Three a cast, up to 9; no timer.
        reset();
        cast();
        Check(me.SbN == 3, "a cast: three needles");
        cast(); cast();
        Check(me.SbN == 9 && Game.SbFull(me), "three casts: nine");
        cast();
        Check(me.SbN == 9, "a fourth cast adds none (the key does nothing at 9)");
        step(10f);
        Check(me.SbN == 9 && flying() == 0, "no enemy near: all nine stay after 10 s");
        Check(client.Game.Players[1].SbN == 9, "other screens see the ring");
        float a0, r0;
        Game.SbSlot(0, 9, me.SbClock, out a0, out r0);
        Check(r0 >= Game.SbOrbitR - Game.SbWobble - 0.1f && r0 <= Game.SbOrbitR + Game.SbWobble + 0.1f, "they school 36-52 px round you");

        // Out of range: nothing goes.
        var far = make(EnemyType.Grunt, mePos + new V2(260f, 0f));
        step(1f);
        Check(me.SbN == 9 && flying() == 0 && far.Hp == far.MaxHp, "an enemy 260 px off draws no needle");

        // In range: one needle at a time (at most two on it), each hit 20 and a poison stack, used up.
        reset();
        cast();
        var near = make(EnemyType.Grunt, mePos + new V2(150f, 0f));
        step(0.5f);
        int launched = 3 - me.SbN;
        Check(launched >= 1 && client.Game.Bullets.Values.Any(b => b.Kind == Defs.Senbon) || near.PoisonT > 0f, "an enemy 150 px off: a needle leaves the ring (other screens see it fly)");
        int maxFlying = 0;
        float hp0 = near.MaxHp;
        for (int i = 0; i < 40; i++) { step(0.05f); maxFlying = Math.Max(maxFlying, flying()); }
        Check(me.SbN == 0 && flying() == 0, "all three flew and were used up");
        Check(maxFlying <= Game.SbPerFoe, "at most two needles on one enemy at once (" + maxFlying + ")");
        float lost = hp0 - near.Hp;
        Check(near.PoisonT > 0f && Math.Abs(near.PoisonDps - 15f) < 0.5f, "three hits: three poison stacks, 15/s (" + near.PoisonDps + ")");
        Check(lost > 60f - 0.5f && lost < 60f + 15f * 2f, "three hits of 20, plus poison (" + lost + ")");

        // Poison stacks to 5.
        reset();
        cast(); cast(); cast();
        var bag = make(EnemyType.Grunt, mePos + new V2(120f, 30f));
        for (int i = 0; i < 80; i++) { step(0.05f); }
        Check(me.SbN == 0 && Math.Abs(bag.PoisonDps - 25f) < 0.5f && bag.PoisonT > 0f, "nine hits: poison stops at 5 stacks, 25/s (" + bag.PoisonDps + ")");

        // Its enemy dies on the way: it takes another, or flies back into the ring.
        reset();
        cast();
        var gone = make(EnemyType.Grunt, mePos + new V2(190f, 0f));
        for (int i = 0; i < 90 && flying() == 0; i++) step(dt);
        Check(flying() >= 1, "a needle is on its way");
        g.Enemies.Remove(gone.Id); mine.Remove(gone.Id);
        step(1.2f);
        Check(flying() == 0 && me.SbN == 3, "its enemy gone and none in range: it flies back into the ring (" + me.SbN + ")");

        // Going down loses them.
        reset();
        cast();
        me.Downed = true;
        step(dt);
        me.Downed = false;
        Check(me.SbN == 0, "going down loses the needles");

        // Hidden Needles: 30% harder, poison stacks 2 higher.
        reset();
        me.Stats.SbDmgMult = 1.3f; me.Stats.SbStackBonus = 2f;
        cast();
        var hn = make(EnemyType.Grunt, mePos + new V2(100f, 0f));
        Bullet first = null;
        for (int i = 0; i < 30 && first == null; i++) { step(dt); first = g.Bullets.Values.FirstOrDefault(b => b.Kind == Defs.Senbon); }
        Check(first != null && Math.Abs(first.Dmg - 26f) < 0.5f, "Hidden Needles: a needle hits for 26");
        me.Stats.SbDmgMult = 1f; me.Stats.SbStackBonus = 0f;

        // Mastery: holds 12.
        reset();
        me.MasteryXp[AbilityId.SenbonSeekers] = 1e6f;
        for (int k = 0; k < 5; k++) cast();
        Check(me.SbN == 12 && client.Game.Players[1].SbN <= 12, "mastered: holds 12");
        step(0.2f);
        Check(client.Game.Players[1].SbN == 12 && Game.SbFull(client.Game.Players[1]), "and other screens know the cap");
        me.MasteryXp.Remove(AbilityId.SenbonSeekers);
    }
}
