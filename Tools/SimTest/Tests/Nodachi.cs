using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Tank Ninja's fourth weapon, the Nodachi (Nodachi.cs, NodachiMotion.cs).
static partial class Program
{
    /// Arashi's five steps land their damage and go round; there's no timer (a wait keeps the place); Alt switches to Mamori and both
    /// stances keep their place; Mamori's jabs, its slower walk and its cone blocking shots; the Storm's armoured wind-ups; Tenchi
    /// stuns; the parry sends both combos back to the start; the other screen sees the stance and the places; Tamahagane.
    static void NodachiTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Blade", Role.Tank, 3, 0, Kit.Ninja);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.Nodachi && me.Weapon.Melee && Defs.WeaponCount(Role.Tank, Kit.Ninja) == 4, "the Tank Ninja's fourth key is the Nodachi (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f), far = from + new V2(400f, 0f);
        Func<V2, EnemyType, Enemy> Dummy = (at, type) =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action<float, bool, bool> Step = (secs, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = far, Fire = fire && f < 2, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        float mult = me.Stats.DmgMult;
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();

        // Arashi: five steps, each landing as its edge comes round, then round again
        var d1 = Dummy(from + new V2(100f, 0f), EnemyType.Brute);
        float[] want = { 20f, 25f, 30f, 22f + 58f, 18f + 18f };
        for (int k = 0; k < 5; k++)
        {
            float before = d1.Hp;
            d1.Pos = from + new V2(100f, 0f);
            me.FireCd = 0f;
            Step(dt * 2f, true, false);
            Check(me.NodCode == k && me.NodStorm == (k + 1) % 5, "press " + (k + 1) + " is " + NodachiMotion.Storm[k].Name + " (code " + me.NodCode + ", next " + me.NodStorm + ")");
            if (k == 1)
            {
                var armour = (float)typeof(Game).GetMethod("NodArmor", flags).Invoke(g, new object[] { me });
                Check(Math.Abs(armour - Game.NodArmorCut) < 0.001f, "a Storm step's wind-up is armoured: " + (Game.NodArmorCut * 100f) + "% less damage taken (" + armour + ")");
            }
            if (k == 3)
            {
                Step(NodachiMotion.Storm[3].Segs[1].T0 + 0.03f, false, false);
                Check(d1.StunT > 0f || d1.Def.IsBoss, "Tenchi's slam stuns what's in its ring (" + d1.StunT.ToString("0.00") + ")");
                Step(NodachiMotion.Storm[k].Dur - NodachiMotion.Storm[3].Segs[1].T0 + 0.1f, false, false);
            }
            else
                Step(NodachiMotion.Storm[k].Dur + 0.15f, false, false);
            float took = before - d1.Hp;
            Check(Math.Abs(took - want[k] * mult) < 1f, NodachiMotion.Storm[k].Name + " deals " + want[k] + " to an enemy 100 px ahead (" + took.ToString("0.#") + ")");
        }
        var armourAfter = (float)typeof(Game).GetMethod("NodArmor", flags).Invoke(g, new object[] { me });
        Check(armourAfter == 0f, "and the armour is down once the step is over");
        Check(g.Effects.Any(e => e.Kind == FxKind.NodStep && Math.Round(e.Radius) == 103), "each step is sent as FxKind.NodStep (Tenchi: id 1, code 3)");
        Check(me.NodStorm == 0, "after Nagi the Storm combo is back at Nukitsuke");

        // no timer: a long wait keeps the place
        me.FireCd = 0f; Step(dt * 2f, true, false); Step(0.8f, false, false);
        me.FireCd = 0f; Step(dt * 2f, true, false);
        Step(4f, false, false);
        Check(me.NodStorm == 2, "four seconds idle and the Storm combo still waits at Engetsu (" + me.NodStorm + ")");

        // Alt: Mamori; both stances keep their place
        float speed = me.Speed;
        Step(dt * 4f, false, true);
        Check(me.NodStance == 1, "Alt switches to Mamori, the Guard stance");
        Check(Math.Abs(me.Speed - speed * Game.NodGuardSlow) < 0.5f, "you walk at 85% in it (" + me.Speed.ToString("0") + " of " + speed.ToString("0") + ")");
        Step(0.4f, false, false);
        me.FireCd = 0f; Step(dt * 2f, true, false);
        Check(me.NodCode == 8 && me.NodGuard == 1 && me.NodStorm == 2, "Fire plays Mamori's first step, Kasumi-zuki, and the Storm keeps its place (" + me.NodCode + ", " + me.NodGuard + ", " + me.NodStorm + ")");
        Step(0.6f, false, false);
        Check(client.Game.Players[1].NodStance == 1 && client.Game.Players[1].NodStorm == 2 && client.Game.Players[1].NodGuard == 1, "the other screen sees the stance and both places");
        Step(dt * 4f, false, true);
        Step(0.4f, false, false);
        Check(me.NodStance == 0 && me.NodStorm == 2 && me.NodGuard == 1, "Alt back: Arashi, still at Engetsu, and Mamori keeps its place too");
        me.FireCd = 0f; Step(dt * 2f, true, false);
        Check(me.NodCode == 2, "so the next press is Engetsu (" + me.NodCode + ")");
        Step(1.2f, false, false);

        // Mamori's jab, and its cone blocking a shot while it plays
        Step(dt * 4f, false, true);
        Step(0.4f, false, false);
        me.NodGuard = 0;
        g.Enemies.Clear(); g.Bullets.Clear();
        var d2 = Dummy(from + new V2(120f, 0f), EnemyType.Brute);
        me.FireCd = 0f; Step(dt * 2f, true, false);
        var b = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(100f, 10f), new V2(-300f, 0f), 0f, 2f, 5f, 0, false, 0f });
        Step(0.1f, false, false);
        Check(!g.Bullets.ContainsKey(b.Id), "a small shot coming in front is blocked while a jab is out");
        Step(0.4f, false, false);
        Check(Math.Abs(d2.MaxHp - d2.Hp - 12f * mult) < 1f, "Kasumi-zuki deals 12 to an enemy 120 px ahead (" + (d2.MaxHp - d2.Hp).ToString("0.#") + ")");
        var b2 = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(100f, 10f), new V2(-300f, 0f), 0f, 2f, 5f, 0, false, 0f });
        Step(0.1f, false, false);
        Check(g.Bullets.ContainsKey(b2.Id) || b2.Pos.DistanceTo(from) < 40f, "with no jab out the cone is down");
        g.Bullets.Clear();

        // the parry starts both combos over
        me.NodStorm = 3; me.NodGuard = 2;
        typeof(Game).GetMethod("ResetAttackChains", flags).Invoke(g, new object[] { me });
        Check(me.NodStorm == 0 && me.NodGuard == 0, "raising the parry sends both combos back to their first step");

        // Tamahagane
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Tamahagane");
        Check(card != null && card.Rarity == Rarity.Rare && card.MaxStacks == 2 && Array.IndexOf(card.Weapons, WeaponId.Nodachi) >= 0, "Tamahagane is the Nodachi's Rare card");
        if (card != null)
        {
            card.Apply(me.Stats);
            Check(Math.Abs(me.Stats.NodReachMult - 1.15f) < 0.001f && Math.Abs(me.Stats.NodConeBonus - 30f) < 0.001f, "it reaches 15% farther and widens the cone by 30 degrees");
        }
    }
}
