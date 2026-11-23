using System;
using Arena;

// The Tank role passive Ironhide (a 75 shield before health, refilling at 25/s after 3 s without damage; empty when
// downed; Tanks only; in the snapshot) and the Healer aura's falloff (17 HP/s next to the Healer down to 5 at the rim).
static partial class Program
{
    static void IronhideTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var damage = typeof(Game).GetMethod("Damage", flags);
        var tick = typeof(Game).GetMethod("TickIronhide", flags);
        Game.IronhideOff = false;
        try { IronhideChecks(dt, flags, damage, tick); } finally { Game.IronhideOff = true; }
    }

    static void IronhideChecks(float dt, System.Reflection.BindingFlags flags, System.Reflection.MethodInfo damage, System.Reflection.MethodInfo tick)
    {

        Func<Role, Session> Make = role =>
        {
            var host = new Session { AutoStart = 1 };
            host.Host(nextPort++, "Iron", role, 0, 0, Kit.Soldier);
            host.Game.Bot = false;
            host.StartMatch();
            for (int f = 0; f < 360 && host.Game.State != MatchState.Fighting; f++) Step(host, dt);
            return host;
        };

        var tank = Make(Role.Tank); var g = tank.Game; var p = g.Players[1];
        Check(p.MaxHp <= 150f + 0.01f && Defs.Get(Role.Tank).Hp == 150f, "a Tank has 150 health");
        Check(Math.Abs(Game.Ironhide(p) - Game.IronhideMax) < 0.01f, "Ironhide starts full (" + Game.IronhideMax + ")");

        float hp0 = p.Hp;
        damage.Invoke(g, new object[] { p, 10f, "test" });
        float took = Game.IronhideMax - Game.Ironhide(p);
        Check(took > 0f && Math.Abs(p.Hp - hp0) < 0.01f, "a small hit comes off the shield, not health");
        Check(Math.Abs(p.IronWait - Game.IronhideDelay) < 0.01f, "a hit starts the " + Game.IronhideDelay + " s wait");

        for (int f = 0; f < (int)((Game.IronhideDelay - 0.3f) / dt); f++) tick.Invoke(g, new object[] { p, dt });
        Check(Math.Abs(Game.IronhideMax - Game.Ironhide(p) - took) < 0.01f, "no refill during the wait");
        for (int f = 0; f < (int)(1.3f / dt); f++) tick.Invoke(g, new object[] { p, dt });
        Check(Math.Abs(Game.Ironhide(p) - Game.IronhideMax) < 0.01f, "then it refills");

        p.IronLost = Game.IronhideMax - 20f;  // 20 left
        damage.Invoke(g, new object[] { p, 1000f, "test" });
        Check(Game.Ironhide(p) <= 0.01f, "a big hit breaks it");

        p.Downed = false; p.Hp = p.MaxHp;  // (the big hit put them down)
        p.IronLost = 30f; p.IronWait = 0f;
        for (int f = 0; f < (int)(0.5f / dt); f++) tick.Invoke(g, new object[] { p, dt });
        Check(Math.Abs(p.IronLost - (30f - Game.IronhideRegen * 0.5f)) < 0.6f, "it refills at " + Game.IronhideRegen + " a second");

        p.IronLost = 0f; p.Downed = true; tick.Invoke(g, new object[] { p, dt }); p.Downed = false;
        Check(Game.Ironhide(p) <= 0.01f && p.IronWait > 0f, "going down empties it");

        // Round-trip over the snapshot (a client draws the plates and the HUD).
        p.IronLost = 42f; p.IronWait = 1.5f;
        var sync = typeof(Game).GetMethod("SyncSnapshot", flags);
        var ms = new System.IO.MemoryStream();
        sync.Invoke(g, new object[] { new NetSync(new System.IO.BinaryWriter(ms)) });
        p.IronLost = 0f; p.IronWait = 0f;
        ms.Position = 0;
        sync.Invoke(g, new object[] { new NetSync(new System.IO.BinaryReader(ms)) });
        Check(Math.Abs(p.IronLost - 42f) < 0.001f && Math.Abs(p.IronWait - 1.5f) < 0.001f, "the shield travels in the snapshot");

        var dps = Make(Role.Dps); var dg = dps.Game; var dp = dg.Players[1];
        float dhp = dp.Hp;
        damage.Invoke(dg, new object[] { dp, 10f, "test" });
        Check(Game.Ironhide(dp) == 0f && dp.Hp < dhp, "a DPS has no Ironhide");

        // The Healer aura's falloff.
        var heal = Make(Role.Healer); var hp = heal.Game.Players[1];
        float mult = hp.Stats.AuraHealMult;
        Check(Math.Abs(Game.AuraHealAt(hp, hp.Pos) - Game.AuraHealNear * mult) < 0.01f, "the aura heals " + Game.AuraHealNear + " HP/s next to the Healer");
        Check(Math.Abs(Game.AuraHealAt(hp, hp.Pos + new V2(hp.AuraRange * 0.5f, 0f)) - (Game.AuraHealNear + Game.AuraHealRim) / 2f * mult) < 0.01f, "half way out, half way down");
        Check(Math.Abs(Game.AuraHealAt(hp, hp.Pos + new V2(hp.AuraRange - 0.01f, 0f)) - Game.AuraHealRim * mult) < 0.01f, Game.AuraHealRim + " HP/s at the rim");
        Check(Game.AuraHealAt(hp, hp.Pos + new V2(hp.AuraRange + 1f, 0f)) == 0f, "nothing outside");
    }
}
