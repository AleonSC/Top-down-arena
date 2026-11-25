using System;
using System.Collections.Generic;

namespace Arena
{
    /// How the lobby's loadout preview (Showcase.cs) presses a weapon's Alt (PlayerInput.WeaponAction): one entry per weapon with a
    /// secondary, so the preview shows it too. An entry gets the preview's clock and says whether Alt is held this tick; it may also
    /// stage what the Alt needs (loaded grenades, a mark...). To preview a new weapon's Alt, add a line to Table and nothing else.
    public static class ShowcaseAlt
    {
        public delegate bool Demo(Game game, Player me, float t, float dt);

        /// True on the single tick when the clock passes `at` seconds into each `period`: a tap of the key.
        public static bool Tap(float t, float dt, float period, float at)
        {
            float p = MathX.Fmod(t, period);
            return p >= at && p - dt < at;
        }

        /// True while the clock is within `len` seconds after `at` into each `period`: the key held down.
        public static bool Hold(float t, float period, float at, float len)
        {
            float p = MathX.Fmod(t, period);
            return p >= at && p < at + len;
        }

        public const float BoStaffEvery = 5.2f, BoStaffShove = 3.3f;  // two full chains and a third cut short by the shove (BoStaff.cs)
        public const float TwinNadeEvery = 1.6f, TwinNadeAt = 0.8f;   // a grenade every 1.6 s, the tubes taking turns (TwinGrenades.cs)

        // The Double Barrel's Alt needs both shells loaded, so the trigger is let go for a moment before it (HoldsFire): the reload finishes.
        public const float DoubleEvery = 4f, DoubleAt = 2.2f, DoubleQuietFrom = 1.0f;

        /// Does the preview let go of Fire right now, so the Alt has what it needs (the Double Barrel's reload)?
        public static bool HoldsFire(WeaponId id, float t)
        {
            if (Off || id != WeaponId.DoubleBarrel)
                return false;
            float p = MathX.Fmod(t, DoubleEvery);
            return p >= DoubleQuietFrom && p < DoubleAt + 0.15f;
        }

        static readonly Dictionary<WeaponId, Demo> Table = new Dictionary<WeaponId, Demo>
        {
            // The Bo Staff: the shove, partway into a chain so the reset shows.
            { WeaponId.BoStaff, (g, me, t, dt) => Tap(t, dt, BoStaffEvery, BoStaffShove) },
            // The Twin Rifle: hits would load the tubes (60 of them each), too slow for a preview, so an empty pair is refilled just before the press.
            { WeaponId.TwinRifle, (g, me, t, dt) =>
                {
                    bool tap = Tap(t, dt, TwinNadeEvery, TwinNadeAt);
                    if (tap && me.TwinNades == 0)
                        me.TwinNades = (byte)Game.TwinNadeMax;
                    return tap;
                } },
            // The Railgun: Heat Dump every 6 s, once a few charged shots have heated it.
            { WeaponId.Railgun, (g, me, t, dt) => Primed(t, dt, 3f, 1.5f, () => { me.RailHeatF = Game.RailHeatMax; me.RailHeat = (byte)Game.RailHeatMax; }) },
            // The Twin Rifle Auto: lock on, and let go again a few seconds later (a dummy under the cursor is locked if one is near; TwinAuto.cs).
            { WeaponId.TwinRifleAuto, (g, me, t, dt) => Tap(t, dt, 4f, 1f) || Tap(t, dt, 4f, 3f) },
            // Out of Ammo: the spin, every 5 s.
            { WeaponId.OutOfAmmo, (g, me, t, dt) => Tap(t, dt, 5f, 2.5f) },
            // The Nimbus: the Eye of the Storm, every 14 s (its cooldown is 12).
            { WeaponId.Nimbus, (g, me, t, dt) => Tap(t, dt, 14f, 1f) },
            // Arc Lightning: Forked Volley between draws (the preview holds fire 1.05 s of every 1.3 s).
            { WeaponId.ArcLightning, (g, me, t, dt) => Tap(t, dt, 2.6f, 1.15f) },
            // Spirit Chalk: Ignite, just after the loop round the middle dummy seals (ChalkAim).
            { WeaponId.SpiritChalk, (g, me, t, dt) => Tap(t, dt, ChalkEvery, ChalkIgniteAt) },

            // --- Soldier ---
            // The Scattergun: Double Barrel, shells topped up first (they refill slowly); the kick sends you back, and you walk home.
            { WeaponId.Scattergun, (g, me, t, dt) => Fill(Tap(t, dt, 3f, 1.5f), () => { if (me.ScatShells < 2) me.ScatShells = Game.ScatShellMax; }) },
            { WeaponId.FlakCannon, (g, me, t, dt) => Tap(t, dt, 5.5f, 1.5f) },      // a proximity mine (5 s cooldown)
            { WeaponId.RiotRepeater, (g, me, t, dt) => Tap(t, dt, 9.5f, 1.5f) },    // tear gas (9 s cooldown)
            { WeaponId.TwinPistols, (g, me, t, dt) => Tap(t, dt, 12.5f, 1.5f) },    // Laser Sights (12 s cooldown)
            { WeaponId.Revolver, (g, me, t, dt) => Tap(t, dt, 4.5f, 1.5f) },        // the Fan stance and back
            { WeaponId.MendersBow, (g, me, t, dt) => Tap(t, dt, 3.5f, 1.5f) },      // Scatter (3 s cooldown)
            // The Repulsion Cannon: Undertow pulls them in while held, with the meter full.
            { WeaponId.RepulsionCannon, (g, me, t, dt) => Fill(Hold(t, 6f, 1.5f, 1.8f), () => me.RepelMeter = 100) },
            // The Double Barrel: both barrels, reloaded in between.
            { WeaponId.DoubleBarrel, (g, me, t, dt) => Tap(t, dt, DoubleEvery, DoubleAt) },
            // --- Elementalist ---
            { WeaponId.FrostLance, (g, me, t, dt) => Tap(t, dt, 14.5f, 1.5f) },     // Absolute Zero (14 s cooldown)
            { WeaponId.StormHammer, (g, me, t, dt) => Tap(t, dt, 6.5f, 1.5f) },     // the slam (6 s cooldown)
            // --- Summoner ---
            { WeaponId.BoneGuard, (g, me, t, dt) => Tap(t, dt, 8.5f, 1.5f) },       // an Ossuary Wall (8 s cooldown)
            // The Wolf Whistle: Alt merges the pack into the alpha; kills would grow the pack (10 each), too slow for a preview, so it is topped up first.
            { WeaponId.WolfWhistle, (g, me, t, dt) => Fill(Tap(t, dt, 6f, 2f), () => g.ShowcaseWolves(me, 4)) },
            { WeaponId.WyvernStaff, (g, me, t, dt) => Tap(t, dt, 7.5f, 1.5f) },     // the Wyrm Slam (7 s cooldown)
            // The Honeybee Staff: the honey spray, held while the meter lasts.
            { WeaponId.HoneybeeStaff, (g, me, t, dt) => Hold(t, 6f, 1.5f, 2f) },
            // --- Knight ---
            { WeaponId.Lance, (g, me, t, dt) => Tap(t, dt, 4.5f, 1.5f) },           // Hook & Haul (4 s cooldown)
            // The Lightbringer: the Sunlance through a teammate (stand-ins are there for it), with the light gauge filled.
            { WeaponId.Lightbringer, (g, me, t, dt) => Fill(Tap(t, dt, 3f, 1.5f), () => { if (me.Light < Game.LanceNeed) me.Light = Game.LanceNeed; }) },
            { WeaponId.Censer, (g, me, t, dt) => Fill(Tap(t, dt, 6.5f, 1.5f), () => { if (me.Embers < 3) me.Embers = 3; }) },  // Censer Cast with Embers lit
            { WeaponId.TwinBlades, (g, me, t, dt) => Tap(t, dt, 4f, 1.5f) },        // Twin Fang (3.5 s cooldown)
            { WeaponId.Greatsword, (g, me, t, dt) => Tap(t, dt, 4.5f, 1.5f) },      // the Moonwheel (4 s cooldown)
            // The Rapier: Riposte up, then dropped again.
            { WeaponId.Rapier, (g, me, t, dt) => Tap(t, dt, 6f, 1f) || Tap(t, dt, 6f, 4f) },
            { WeaponId.BulwarkBarrier, (g, me, t, dt) => Hold(t, 7f, 1f, 3f) },      // Brace, held
            // The Quarterstaff: Jab spends its stacks while the key is down.
            { WeaponId.Quarterstaff, (g, me, t, dt) => { if (Hold(t, 5f, 1.8f, 0.7f)) me.StaffStacks = (byte)Game.StaffBaseStacks; return Hold(t, 5f, 2.5f, 1f); } },
            // --- Ninja ---
            { WeaponId.Kusarigama, (g, me, t, dt) => Hold(t, 6f, 1f, 2.5f) },        // the whirl, held; let go for the snap
            { WeaponId.FumaShuriken, (g, me, t, dt) => Tap(t, dt, 6f, 1.5f) || Tap(t, dt, 6f, 4.5f) },  // Orbit, then Throw again
            { WeaponId.Katana, (g, me, t, dt) => Tap(t, dt, 4f, 1.5f) },            // the stance switch
            { WeaponId.Nodachi, (g, me, t, dt) => Tap(t, dt, 4f, 1.5f) },
            { WeaponId.Vowguard, (g, me, t, dt) => Tap(t, dt, 4f, 1.5f) },        // the stance switch           // the stance switch
            { WeaponId.Kunai, (g, me, t, dt) => Primed(t, dt, 3f, 1.5f, () => { var e = Dummy(g, me); if (e != null && me.KunaiMarkT <= 0f) { me.KunaiMark = e.Pos + new V2(45f, 35f); me.KunaiMarkT = 1f; } }) },             // blink to the mark
            { WeaponId.Shuriken, (g, me, t, dt) => Tap(t, dt, 8f, 1.5f) || Tap(t, dt, 8f, 5.5f) },  // the fan to the Great Star and back
            { WeaponId.Nunchaku, (g, me, t, dt) => Tap(t, dt, 8f, 1.5f) || Tap(t, dt, 8f, 5.5f) },  // the guard up, then down
            // --- Hemomancer ---
            { WeaponId.BloodLash, (g, me, t, dt) => Hold(t, 8f, 2f, 2.5f) },        // Martyr's Leash, held
            { WeaponId.BloodBrawler, (g, me, t, dt) => Primed(t, dt, 3f, 1.5f, () => { var e = Dummy(g, me); if (e != null) { me.BsTarget = e.Id; me.BsT = 1f; } }) },      // Sympathy on the marked enemy
            { WeaponId.NeedleSwarm, (g, me, t, dt) => Tap(t, dt, 5f, 3f) },     // a fan of needles out of the swarm every 5 s
            { WeaponId.EmbolusSpears, (g, me, t, dt) => Primed(t, dt, 3f, 1.5f, () => StickSpears(g, me)) },     // burst the spears
            { WeaponId.BloodBlast, (g, me, t, dt) => Tap(t, dt, 8.5f, 1.5f) },      // the Crimson Tide (8 s cooldown)
            // --- Fourth weapons ---
            { WeaponId.ArcKnuckles, (g, me, t, dt) => Tap(t, dt, 3f, 1.5f) },       // Rocket Dash
            { WeaponId.Stonespear, (g, me, t, dt) => Primed(t, dt, 4f, 2f, () => { if (me.SsMask == 0) { int n = g.SsSideCount(me); me.SsSide = n; me.SsMask = (1 << (2 * n)) - 1; me.SsAge = 0f; } }) },          // the spear volley
            { WeaponId.GrenadeLauncher, (g, me, t, dt) => Tap(t, dt, 8f, 1.5f) || Tap(t, dt, 8f, 5.5f) },  // the drum: smoke, then back to HE
            { WeaponId.StaticShock, (g, me, t, dt) => Tap(t, dt, 5.5f, 1.5f) },     // the Arc Cannon (5 s cooldown)
            { WeaponId.WindClash, (g, me, t, dt) => Tap(t, dt, 9.5f, 1.5f) },       // the Tempest (9 s cooldown)
        };

        // Spirit Chalk draws where the cursor goes, so its preview moves the cursor (Chalk.cs): a loop round the middle dummy (it's penned in),
        // Ignite, then once that ring has gone a line swept across all three. Each fits the 600 px of ink (ChalkLoopR: about 470 px of ring).
        public const float ChalkEvery = 6.5f, ChalkLoopEnd = 1.1f, ChalkIgniteAt = 1.4f, ChalkLineAt = 4.7f, ChalkLineEnd = 5.5f;
        public const float ChalkLoopR = 75f, ChalkLoopTurns = 1.15f, ChalkLineHalf = 150f;

        /// Where the Spirit Chalk's preview puts the cursor at `t`, and whether the button is held (drawing). `target`: the middle dummy's spot.
        public static V2 ChalkAim(float t, V2 target, V2 home, out bool draw)
        {
            float p = MathX.Fmod(t, ChalkEvery);
            if (p < ChalkLoopEnd)
            {
                draw = true;
                float a = MathX.Pi / 2f + MathX.Tau * ChalkLoopTurns * p / ChalkLoopEnd;  // from the side nearest you, round
                return target + V2.FromAngle(a) * ChalkLoopR;
            }
            if (p >= ChalkLineAt && p < ChalkLineEnd)
            {
                draw = true;
                float u = (p - ChalkLineAt) / (ChalkLineEnd - ChalkLineAt) * 2f - 1f;  // -1..1, left to right
                float dist = home.DistanceTo(target);
                // Along the dummies' arc (they stand on a circle round you), so the line runs through each.
                return home + V2.FromAngle(-MathX.Pi / 2f + u * ChalkLineHalf / dist) * dist;
            }
            draw = false;
            return target;
        }

        // ALT FIRE shows the Alt without the trigger, so an Alt that spends what the main fire builds (heat, stacks, a mark, spears out)
        // gets it set up just before the press (PrimeLead seconds, so it shows first).
        const float PrimeLead = 0.7f;

        /// A tap at `at` into each `period`, with `stage` run every tick of the PrimeLead before it.
        static bool Primed(float t, float dt, float period, float at, Action stage)
        {
            if (Hold(t, period, at - PrimeLead, PrimeLead + dt))
                stage();
            return Tap(t, dt, period, at);
        }

        /// The dummy the preview aims at (the one nearest the cursor's spot ahead of you).
        static Enemy Dummy(Game g, Player me)
        {
            V2 at = me.Pos + V2.FromAngle(me.Aim) * 250f;
            Enemy best = null;
            foreach (var e in g.Enemies.Values)
                if (e.Hp > 0f && (best == null || e.Pos.DistanceTo(at) < best.Pos.DistanceTo(at)))
                    best = e;
            return best;
        }

        /// The Embolus Spears: both spears pinned into the aimed dummy, ready to burst.
        static void StickSpears(Game g, Player me)
        {
            var e = Dummy(g, me);
            if (e == null)
                return;
            for (int i = 0; i < 2; i++)
                if (me.EsSt[i] == Game.EsInHand || me.EsSt[i] == Game.EsStuck)
                {
                    V2 dir = (e.Pos - me.Pos).Normalized();
                    me.EsSt[i] = Game.EsStuck;
                    me.EsDir[i] = dir;
                    me.EsPos[i] = e.Pos + new V2(-dir.Y, dir.X) * (i == 0 ? -14f : 14f);
                }
        }

        /// Runs `stage` (it sets up what the Alt needs) when the key is about to go down; passes `down` through.
        static bool Fill(bool down, Action stage)
        {
            if (down)
                stage();
            return down;
        }

        /// Alts that go through a teammate (the Lightbringer's Sunlance): the preview brings stand-in teammates and aims the press at one.
        public static bool NeedsAlly(WeaponId id) => id == WeaponId.Lightbringer;

        public static bool Off;  // tests: the preview never presses Alt (to compare a run with and without it)

        public static bool Has(WeaponId id) => Table.ContainsKey(id);

        /// Is Alt held this tick for this weapon?
        public static bool Held(Game game, Player me, float t, float dt) =>
            !Off && Table.TryGetValue(me.Weapon.Id, out var demo) && demo(game, me, t, dt);
    }
}
