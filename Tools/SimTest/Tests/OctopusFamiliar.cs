using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Octopus Familiar (OctopusFamiliar.cs), the Healer Summoner's 4th choice on the lobby's 4th key: the table and cards, the pick
// (press, then click a player; the key again cancels; nothing spent until the click), the octopus on the clicked player on
// both screens, the slaps (8 enemies at once in reach of that player, 16 each, a heal for them, nothing outside), its health
// (shots stop on it, at 0 it bursts), its 60 s, one per caster, Three Hearts and mastery.
static partial class Program
{
    static void OctopusFamiliarTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurtGuard = typeof(Game).GetMethod("HurtGuard", flags);

        var def = Upgrades.Ability(AbilityId.OctopusFamiliar);
        Check(def.Name == "Octopus Familiar" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Summoner && def.Role == Role.Healer && def.Cooldown == 40f && def.Icon == "octopus",
            "Octopus Familiar: a 40 s Healer Summoner 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Healer, Kit.Summoner) == 4 && Upgrades.FirstAbility(Role.Healer, Kit.Summoner, 3) == AbilityId.OctopusFamiliar
            && Upgrades.FirstAbility(Role.Healer, Kit.Summoner, 0) == AbilityId.MendingWeb && Upgrades.FirstAbility(Role.Healer, Kit.Summoner, 2) == AbilityId.SoulLantern,
            "the Healer Summoner's 4th key is Octopus Familiar; its first three are unchanged");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.OctopusFamiliar) && Upgrades.All.Any(u => u.Name == "Three Hearts" && u.RequiresAbility == AbilityId.OctopusFamiliar && u.Rarity == Rarity.Rare),
            "Octopus Familiar is a very rare card for Healer Summoners who picked another, and Three Hearts a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Octo", Role.Healer, 0, 3, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        Check(me.Slots[0].Id == AbilityId.OctopusFamiliar && client.Game.Players[1].Slots[0].Id == AbilityId.OctopusFamiliar, "the 4th pick reaches the run, on both screens");

        V2 mePos = new V2(500f, 450f), allyPos = new V2(800f, 450f);
        var mine = new HashSet<int>();
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; mine.Add(e.Id); return e;
        };
        var pinned = new Dictionary<Enemy, V2>();
        Action<float, PlayerInput> stepIn = (secs, input) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                g.Bullets.Clear();
                foreach (var kv in pinned) { kv.Key.Pos = kv.Value; kv.Key.FireT = 99f; kv.Key.KnockLeft = V2.Zero; }
                me.Pos = mePos; ally.Pos = allyPos; me.Hp = me.MaxHp; me.InvulnT = 1f; ally.InvulnT = 1f;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, input);
            }
        };
        Action<float> step = secs => stepIn(secs, new PlayerInput { AimAt = allyPos });
        Func<Minion> octo = () => g.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.Octopus);
        Action reset = () =>
        {
            mine.Clear(); pinned.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Minions.Clear();
            me.Pos = mePos; ally.Pos = allyPos;
            me.MasteryXp.Remove(AbilityId.OctopusFamiliar);
            make(EnemyType.Grunt, new V2(1500f, 120f));  // far off: the wave goes on
        };
        Action<V2> cast = at => { me.Slots[0].Charges = 1; hostAbility.Invoke(g, new object[] { 1, 0, at, false }); };

        // The pick: the player under the cursor (you too), nobody when the cursor is on nobody.
        reset();
        step(0.05f);
        Check(g.OctoPickTarget(me, allyPos + new V2(10f, 5f)) == ally && g.OctoPickTarget(me, mePos) == me && g.OctoPickTarget(me, new V2(650f, 700f)) == null,
            "the pick: the player under the cursor, you included; nobody on empty ground");

        // Press: a pick, nothing spent; the key again cancels; press, click the ally: the octopus goes to them.
        me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
        stepIn(dt, new PlayerInput { AimAt = allyPos, Ability = true });
        Check(g.OctoPickSlot == 0 && me.Slots[0].Charges == 1 && octo() == null, "pressing it starts the pick and spends nothing");
        stepIn(dt, new PlayerInput { AimAt = allyPos });
        stepIn(dt, new PlayerInput { AimAt = allyPos, Ability = true });
        Check(g.OctoPickSlot == -1 && me.Slots[0].Charges == 1, "the key again cancels the pick");
        stepIn(dt, new PlayerInput { AimAt = allyPos });
        stepIn(dt, new PlayerInput { AimAt = allyPos, Ability = true });
        stepIn(dt, new PlayerInput { AimAt = allyPos });
        stepIn(dt, new PlayerInput { AimAt = allyPos, Fire = true });
        stepIn(0.1f, new PlayerInput { AimAt = allyPos });
        var o = octo();
        Check(o != null && o.LinkTo == ally.Id && Math.Abs(o.Hp - Game.OctoHp) < 0.5f && Math.Abs(o.T - Game.OctoLife) < 0.5f && g.OctoPickSlot == -1,
            "a click on the ally: the octopus guards them (260 health, 60 s)");
        Check(client.Game.Minions.Values.Any(m => m.Kind == MinionKind.Octopus && m.LinkTo == ally.Id), "other screens see it, on the same player");
        step(0.5f);
        Check(o.Pos.DistanceTo(allyPos) < Game.OctoOrbit + Game.OctoLift + 10f, "it floats round the player it guards");

        // The slaps: 8 enemies at once in reach, 16 each, the ally healed; one outside is left alone.
        reset();
        cast(allyPos);
        step(0.3f);
        var near = new List<Enemy>();
        for (int i = 0; i < 10; i++)
        {
            var e = make(EnemyType.Grunt, allyPos + V2.FromAngle(i * MathX.Tau / 10f) * 90f);
            pinned[e] = e.Pos; near.Add(e);
        }
        var far = make(EnemyType.Grunt, allyPos + new V2(260f, 0f));
        pinned[far] = far.Pos;
        ally.Hp = ally.MaxHp * 0.5f;
        float allyHp = ally.Hp;
        step(Game.OctoLand + 0.1f);
        int hit = near.Count(e => e.Hp < e.MaxHp);
        Check(hit == Game.OctoArms, "8 arms: 8 of the 10 enemies in reach slapped at once (" + hit + ")");
        Check(near.Where(e => e.Hp < e.MaxHp).All(e => Math.Abs(e.MaxHp - e.Hp - Game.OctoDamage * me.Stats.DmgMult) < 0.6f), "each slap hits for 16");
        Check(far.Hp == far.MaxHp, "an enemy outside its reach is left alone");
        Check(ally.Hp > allyHp + Game.OctoHeal * 7f, "each slap heals the guarded player a little");
        Check(client.Game.Effects.Any(f => f.Kind == FxKind.OctoSlap), "other screens see the arms reach out");
        step(Game.OctoStrike + Game.OctoArmCd + 0.3f);
        Check(near.All(e => e.Hp < e.MaxHp), "the arms come back for the other two");

        // Its health: an enemy shot stops on it; at 0 it bursts into ink, on every screen.
        reset();
        cast(allyPos);
        step(0.6f);
        o = octo();
        float before = o.Hp;
        var stop = typeof(Game).GetMethod("GuardStopsBullet", flags);
        bool stopped = (bool)stop.Invoke(g, new object[] { new Bullet { Kind = 10, Pos = o.Pos + new V2(4f, 0f), Radius = 5f, Dmg = 20f } });
        bool shotHit = stopped && Math.Abs(before - o.Hp - 20f) < 0.01f;
        Check(shotHit, "an enemy shot stops on it and hurts it");
        hurtGuard.Invoke(g, new object[] { o, 1e4f });
        step(0.1f);
        Check(octo() == null && g.Effects.Any(f => f.Kind == FxKind.OctoInk && f.Radius > 0.5f), "at 0 health it bursts into ink");
        Check(client.Game.Minions.Values.All(m => m.Kind != MinionKind.Octopus), "and it's gone on every screen");

        // Its time: 60 s, then it bursts.
        reset();
        cast(allyPos);
        octo().T = 0.05f;
        step(0.1f);
        Check(octo() == null && g.Effects.Any(f => f.Kind == FxKind.OctoInk && f.Radius > 0.5f), "after its 60 s it bursts into ink");

        // One per caster: a new cast moves it.
        reset();
        cast(allyPos);
        step(0.1f);
        cast(mePos);
        step(0.1f);
        Check(g.Minions.Values.Count(m => m.Kind == MinionKind.Octopus) == 1 && octo().LinkTo == me.Id, "one octopus a caster: casting again moves it (here onto you)");

        // Three Hearts: 50% more health and 25% harder slaps.
        reset();
        me.Stats.OctoHpMult = 1.5f; me.Stats.OctoDmgMult = 1.25f;
        cast(allyPos);
        var tough = make(EnemyType.Grunt, allyPos + new V2(60f, 0f));
        pinned[tough] = tough.Pos;
        step(Game.OctoLand + 0.1f);
        Check(Math.Abs(octo().MaxHp - 390f) < 0.5f && Math.Abs(tough.MaxHp - tough.Hp - 20f * me.Stats.DmgMult) < 0.6f, "Three Hearts: 390 health and 20 a slap");
        me.Stats.OctoHpMult = 1f; me.Stats.OctoDmgMult = 1f;

        // Mastery: 380 health and 22 a slap.
        reset();
        me.MasteryXp[AbilityId.OctopusFamiliar] = 1e6f;
        cast(allyPos);
        var m2 = make(EnemyType.Grunt, allyPos + new V2(60f, 0f));
        pinned[m2] = m2.Pos;
        step(Game.OctoLand + 0.1f);
        float want = Game.OctoDamageMastered * me.Stats.DmgMult * octo().Power;
        Check(Math.Abs(octo().MaxHp - 380f) < 0.5f && Math.Abs(m2.MaxHp - m2.Hp - want) < 1f, "mastered: 380 health and 22 a slap");
        me.MasteryXp.Remove(AbilityId.OctopusFamiliar);
    }
}
