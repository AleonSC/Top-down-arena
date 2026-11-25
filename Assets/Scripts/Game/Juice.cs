using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// Visual effects ("juice", Docs/Mockups/visual-flair.html), all on this screen only: nothing here is sent over the
    /// network or touches the simulation. Each frame UpdateJuice compares what's on screen with the frame before (enemies'
    /// health, enemies and bullets that are gone, new effects, your own health and level) and starts effects from that:
    ///   glow        a second, additive pass: halos on bullets, blasts, sparks, bosses (DrawGlowPass)
    ///   shake       a trauma value big events add to; the world is drawn offset by trauma² (Px)
    ///   hit-pause   the picture holds for a few frames on big kills and hits (the last frame's triangles are replayed)
    ///   deaths      enemies break into shards of their own shape, with a flash ring and a scorch mark
    ///   flash       hit enemies flash white a little longer and bulge (DrawEnemy reads JuiceScale / JuiceFlash)
    ///   numbers     damage dealt to each enemy, merged every 0.15 s, rising off it (DrawJuiceHud)
    ///   sparks      on impacts, muzzle flashes; trails behind fast shots
    ///   floor       scorch marks that fade to a stain, and the grid ripples from big blasts
    ///   mood        a tint for the path or boss; a faint red tint in the corners when hurt, beating like a pulse when low
    ///   moments     kill kicks, multikill calls, the wave's last kill, a boss's slow motion, the level shock (JuiceMoments.cs)
    ///   level up    a gold ring and rising sparks (bigger for a legendary); a boss's entrance card
    /// Settings (Menus.cs): Visual effects on/off (off draws exactly as before), Screen shake off/low/full, Damage numbers,
    /// Ability names (AbilityLooks.cs) and Reduced effects (no shake, no hit-pause, no flashes).
    public partial class ArenaApp
    {
        // --- Settings --------------------------------------------------------------------------------------------------
        bool fxOn = true, fxNumbers = true, fxReduced;
        /// Effects held back: Reduced effects on or Visual effects off (the CRT cabinet, holo tabs and widgets draw calmer).
        bool FxCalm => fxReduced || !fxOn;
        float mateShotAlpha = 1f;  // how opaque other players' shots are drawn, 0.25..1 (Settings > Gameplay > Teammate shots)
        int fxShake = 1;  // 0 off, 1 low (the default), 2 full

        void LoadFxSettings()
        {
            fxOn = PlayerPrefs.GetInt("fx_on", 1) == 1;
            fxShake = Mathf.Clamp(PlayerPrefs.GetInt("fx_shake", 1), 0, 2);
            fxNumbers = PlayerPrefs.GetInt("fx_numbers", 1) == 1;
            fxReduced = PlayerPrefs.GetInt("fx_reduced", 0) == 1;
            fxCallouts = PlayerPrefs.GetInt("fx_callouts", 1) == 1;  // AbilityLooks.cs
            lightsOn = PlayerPrefs.GetInt("lights_on", 1) == 1;  // LightingLook.cs
            lightsSoft = PlayerPrefs.GetInt("lights_soft", 1) == 1;
            floorDim = Mathf.Clamp(PlayerPrefs.GetInt("floor_dim", 0), 0, FloorDimNames.Length - 1);
            floorBend = PlayerPrefs.GetInt("floor_bend", 1) == 1;  // FloorGridLook.cs
            floorStyle = Mathf.Clamp(PlayerPrefs.GetInt("floor_style", 0), 0, FloorStyleNames.Length - 1);  // FloorStylesLook.cs
            CrtUi.Theme = PlayerPrefs.GetInt("crt_theme", 0);
            mateShotAlpha = Mathf.Clamp(PlayerPrefs.GetFloat("mate_shot_alpha", 1f), 0.25f, 1f);
        }

        void SetMateShotAlpha(float a)
        {
            mateShotAlpha = Mathf.Clamp(a, 0.25f, 1f);
            PlayerPrefs.SetFloat("mate_shot_alpha", mateShotAlpha);
            PlayerPrefs.Save();
        }

        void SaveFxSettings()
        {
            PlayerPrefs.SetInt("fx_on", fxOn ? 1 : 0);
            PlayerPrefs.SetInt("fx_shake", fxShake);
            PlayerPrefs.SetInt("fx_numbers", fxNumbers ? 1 : 0);
            PlayerPrefs.SetInt("fx_reduced", fxReduced ? 1 : 0);
            PlayerPrefs.SetInt("fx_callouts", fxCallouts ? 1 : 0);
            PlayerPrefs.SetInt("lights_on", lightsOn ? 1 : 0);
            PlayerPrefs.SetInt("lights_soft", lightsSoft ? 1 : 0);
            PlayerPrefs.SetInt("floor_dim", floorDim);
            PlayerPrefs.SetInt("floor_bend", floorBend ? 1 : 0);
            PlayerPrefs.SetInt("floor_style", floorStyle);
            PlayerPrefs.SetInt("crt_theme", CrtUi.Theme);
            PlayerPrefs.Save();
            if (!fxOn)
                ClearJuice();
        }

        /// Effects are drawn for the run on screen only (not the lobby's preview, which borrows DrawArenaContents).
        bool Juicy => fxOn && juiceGame == game;

        // --- What was on screen last frame ---------------------------------------------------------------------------
        class EnemySeen
        {
            public V2 Pos;
            public float Hp, Max, R, Rot, Squash, Flash, Dmg, DmgT, HitFlash;  // HitFlash: the enemy's own Flash last frame (a rise is a real hit)
            public float Owed;  // Impact damage already shown in orange (a Lance shove), left out of its next number
            public int Sides, Frame;
            public Rgba Col;
            public bool Boss, Big, Spawning;
        }

        class ShotSeen
        {
            public V2 Pos, Vel;
            public float Life, Radius;
            public int Owner, Frame, N;
            public byte Kind;
            public readonly V2[] Trail = new V2[6];
        }

        struct Part
        {
            public byte Kind;  // Spark, Shard, Ring, Flash
            public V2 Pos, Vel;
            public float Life, Max, Size, Rot, Spin, A0, A1;
            public Rgba Col;
        }

        // Impact crowding: a coarse grid of how many hits have landed in each cell lately (each hit adds 1, and it
        // drains at ImpactDrain a second), so BulletEnding can thin out the sparks where hits pile up.
        const float ImpactCell = 64f, ImpactDrain = 10f;
        readonly Dictionary<int, float> impactHeat = new Dictionary<int, float>();
        readonly List<int> impactKeys = new List<int>();
        float impactAt;

        /// Adds a hit at this spot and returns how crowded its cell was before it.
        float ImpactCrowd(V2 at)
        {
            float drain = (juiceClock - impactAt) * ImpactDrain;
            impactAt = juiceClock;
            if (drain > 0f && impactHeat.Count > 0)
            {
                impactKeys.Clear();
                impactKeys.AddRange(impactHeat.Keys);
                foreach (int k in impactKeys)
                {
                    float v = impactHeat[k] - drain;
                    if (v <= 0f) impactHeat.Remove(k); else impactHeat[k] = v;
                }
            }
            int key = (int)Mathf.Floor(at.X / ImpactCell) * 4096 + (int)Mathf.Floor(at.Y / ImpactCell);
            float heat;
            impactHeat.TryGetValue(key, out heat);
            impactHeat[key] = heat + 1f;
            return heat;
        }

        const byte PSpark = 0, PShard = 1, PRing = 2, PFlash = 3, PDust = 4, PFade = 5, PSplash = 6, PTumble = 7;  // 8-10: GunBowLooks.cs, 20-25: BowFocusLook.cs

        struct Decal { public V2 Pos; public float R, Age; }
        struct Ripple { public V2 Pos; public float T, Power, Life; public Rgba Col; }  // Life 0: RippleLife; a dash's burst is shorter and lights the grid in Col (FloorGridLook.cs)
        struct Number { public V2 Pos; public float T; public int Value; public bool Big; public string Tag; }  // Tag: an Impact's number, orange with its tag over it (LanceLook.cs)
        struct Burst { public float At; public V2 Pos; public Rgba Col; public float R; }

        Game juiceGame;
        int juiceFrame;
        readonly Dictionary<int, EnemySeen> seenEnemies = new Dictionary<int, EnemySeen>();
        readonly Dictionary<int, ShotSeen> seenShots = new Dictionary<int, ShotSeen>();
        readonly Stack<EnemySeen> enemyPool = new Stack<EnemySeen>();
        readonly Stack<ShotSeen> shotPool = new Stack<ShotSeen>();
        readonly List<int> goneIds = new List<int>();
        readonly List<V2> newBooms = new List<V2>();
        readonly Part[] parts = new Part[1200];  // (each draws up to ~100 vertices with its glow)
        int partN;
        readonly List<Decal> scorches = new List<Decal>();
        readonly List<Ripple> ripples = new List<Ripple>();
        readonly List<Number> numbers = new List<Number>();
        readonly List<Burst> bursts = new List<Burst>();
        readonly Dictionary<int, float> muzzleT = new Dictionary<int, float>();
        float trauma, holdT, holdGap, hurtTint, juiceClock;
        float myHp = -1f;
        int myLevel = -1, myLegendaries = -1, introWave = -1;
        float introT;
        string introName = "";

        /// A boss's entrance card holds the HUD off screen (it slides away as the card comes in, and back as it fades).
        bool BossIntroHoldsHud => introT > 0.35f || WardenIntroHoldsHud;

        /// The card has the HUD off, from when it comes in until the HUD is fully back (the slim boss bars stay away meanwhile).
        bool introHidHud;
        float shakeX, shakeY;  // added to every world vertex (Px) while the world is drawn

        public const float ScorchLife = 45f, RippleLife = 1.2f, NumberLife = 0.75f, IntroTime = 3.2f;

        void ClearJuice()
        {
            foreach (var s in seenEnemies.Values) enemyPool.Push(s);
            foreach (var s in seenShots.Values) shotPool.Push(s);
            seenEnemies.Clear();
            seenShots.Clear();
            partN = 0;
            scorches.Clear(); ripples.Clear(); numbers.Clear(); bursts.Clear(); muzzleT.Clear(); impactHeat.Clear();
            ClearGunBowLooks();  // GunBowLooks.cs
            ClearSoldierLooks();  // SoldierLooks.cs
            ClearCloneWallLook();  // CloneWallLook.cs
            ClearGolemWallLook();  // GolemWallLook.cs
            ClearTankLooks();  // TankLooks.cs
            ClearTombLook();  // GlacialTombLook.cs
            ClearBloodFreezeLook();  // BloodFreezeLook.cs
            ClearShockwaveLook();  // ShockwaveLook.cs
            ClearSentryNovaLook();  // SentryNovaLook.cs
            ClearTankGunLooks();  // TankGunLooks.cs
            ClearGrenadeLauncherLooks();  // GrenadeLauncherLook.cs
            ClearTankAltLooks();  // TankAltFx.cs
            ClearDpsGunLooks();  // DpsGunLooks.cs
            ClearAirTrails();  // AirTrailLooks.cs
            trauma = holdT = holdGap = hurtTint = introT = 0f;
            ClearChargeCam();  // ChargeCameraFx.cs
            wardenIntroT = -1f;
            myHp = -1f; myLevel = -1; myLegendaries = -1;
            heldFrame = false;
            radiantTrails.Clear();
            ClearAmbience();  // Ambience.cs
            ClearJuiceMoments();  // JuiceMoments.cs
        }

        void AddPart(Part p)
        {
            if (partN < parts.Length)
                parts[partN++] = p;
        }

        float Rnd(float a, float b) => a + (float)juiceRng.NextDouble() * (b - a);
        readonly System.Random juiceRng = new System.Random();  // its own rolls: the game's stay untouched

        int lastRapierTempo;

        /// Your own Twin Blades hit: a hit-pause and a kick sized to the move. The quick steps get a short hold with a short gap, so
        /// a whole chain gets one each; the Pirouette only kicks (a hold mid-turn reads as a stutter); the Shear and Twin Fang's
        /// burst hold longest. `hits`: enemies cut, which adds a little.
        void TwinHitFeel(int kind, int hits)
        {
            float more = Mathf.Min(3, Mathf.Max(0, hits - 1));
            switch (kind)
            {
                case 1: case 2: Hold(0.025f, 0.12f); Shake(0.06f + 0.01f * more); break;
                case 3: Hold(0.018f, 0.1f); Shake(0.05f); break;
                case 4: Shake(0.04f + 0.01f * more); break;
                case 5: Hold(0.05f + 0.01f * more, 0.2f); Shake(0.18f + 0.02f * more); break;
                case 6: Shake(0.04f); break;
                case 7: Hold(0.04f, 0.2f); Shake(0.15f); break;
            }
        }

        /// An Arc Knuckles punch connected (Core/ArcKnuckles.cs, KnuckleHitCue): a burst of blue sparks and a ring where it landed on
        /// every screen; on the puncher's own, the picture holds and shakes, sized to the punch (a Flurry jab barely, the hammers,
        /// the dash and a thrown enemy landing the most). More enemies caught shake a little more.
        void KnuckleHitFeel(Effect fx, bool mine)
        {
            int kind = (int)fx.Pos2.Y, hits = Mathf.Max(1, Mathf.RoundToInt(fx.Radius));
            float more = Mathf.Min(3, hits - 1);
            bool heavy = Game.KnuckleHeavyHit(kind), tiny = kind == 8 || kind == 15;
            var col = fx.Color;
            if (!fxReduced)
            {
                Sparks(fx.Pos, col, heavy ? 14 : tiny ? 4 : 8, heavy ? 340f : 240f, heavy ? 0.32f : 0.22f, heavy ? 2.6f : 2f);
                Sparks(fx.Pos, new Rgba(0.93f, 0.98f, 1f), heavy ? 6 : 3, heavy ? 420f : 300f, 0.16f, 1.6f);
                Ring(fx.Pos, heavy ? 34f : tiny ? 16f : 24f, heavy ? 0.26f : 0.18f, col);
            }
            if (!mine)
                return;
            switch (kind)
            {
                case 6: Hold(0.1f + 0.01f * more, 0.2f); Shake(0.34f + 0.02f * more); break;      // Counter Hammer
                case 7: Hold(0.07f, 0.15f); Shake(0.2f); break;                                   // the Rocket Dash's fist
                case 8: Hold(0.012f, 0.05f); Shake(0.025f); break;                                // a Flurry jab
                case 9: Hold(0.06f, 0.15f); Shake(0.18f + 0.02f * more); break;                   // the Rocket Punch
                case 10: Hold(0.06f, 0.12f); Shake(0.15f); break;                                 // the Flurry's straight
                case 11: Hold(0.08f, 0.15f); Shake(0.22f + 0.03f * more); break;                  // a thrown enemy landing
                case 12: Hold(0.04f, 0.1f); Shake(0.08f + 0.01f * more); break;                   // the Pull Punch
                case 13: Hold(0.045f, 0.12f); Shake(0.1f + 0.015f * more); break;                 // a Drive Punch
                case 14: Hold(0.08f + 0.01f * more, 0.18f); Shake(0.22f + 0.02f * more); break;   // the Pin Punch
                case 15: Hold(0.012f, 0.05f); Shake(0.025f); break;                               // a Barrage jab
                case 16: Hold(0.06f, 0.12f); Shake(0.15f); break;                                 // the Barrage's straight
                case 17: Hold(0.09f + 0.01f * more, 0.2f); Shake(0.28f + 0.02f * more); break;    // the Quake Punch
                case 18: Hold(0.06f, 0.14f); Shake(0.18f); break;                                 // a Wall Slam
            }
        }

        const float ShakeRate = 11f;  // the shake's random offsets per second (its fine layer: 2.3x that)

        /// Smooth noise in -1..1 along `x`: a random value at each whole step, eased into the next (smoothstep). `seed`
        /// picks one of several independent tracks (x and y, the slow and fine layers).
        static float ShakeNoise(float x, int seed)
        {
            int i = Mathf.FloorToInt(x);
            float f = x - i, u = f * f * (3f - 2f * f);
            return Mathf.Lerp(ShakeHash(i, seed), ShakeHash(i + 1, seed), u);
        }

        static float ShakeHash(int i, int seed)
        {
            uint h = (uint)i * 374761393u + (uint)seed * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xffff) / 32767.5f - 1f;
        }

        void Shake(float amount)
        {
            if (fxShake == 0 || fxReduced)
                return;
            trauma = Mathf.Min(1f, trauma + amount);
        }

        /// Hold the picture for a moment (at most every 0.4 s, so fast weapons don't stutter).
        void Hold(float seconds, float gap = 0.4f)
        {
            if (fxReduced || holdGap > 0f)
                return;
            holdT = Mathf.Max(holdT, seconds);
            holdGap = gap;
        }

        /// 1 while the particle pool is quiet, down to 0.25 as it fills: in a flood of kills each burst brings fewer
        /// sparks, so the pool (and the vertices it draws) stays in budget instead of cutting the last bursts off.
        float PartCrowd => partN < PartCalm ? 1f : Mathf.Max(0.25f, 1f - (partN - PartCalm) / (float)(parts.Length - PartCalm));
        const int PartCalm = 400;

        void Sparks(V2 at, Rgba col, int n, float speed, float life, float size)
        {
            if (n > 2)
                n = Mathf.Max(2, Mathf.RoundToInt(n * PartCrowd));
            for (int i = 0; i < n; i++)
            {
                float a = Rnd(0f, MathX.Tau), s = speed * Rnd(0.3f, 1f);
                AddPart(new Part { Kind = PSpark, Pos = at, Vel = V2.FromAngle(a) * s, Life = life * Rnd(0.6f, 1f), Max = life, Size = size, Col = col });
            }
        }

        void Ring(V2 at, float r, float dur, Rgba col, byte kind = PRing) =>
            AddPart(new Part { Kind = kind, Pos = at, Life = dur, Max = dur, Size = r, Col = col });

        void Scorch(V2 at, float r)
        {
            if (scorches.Count >= 140)
                scorches.RemoveAt(0);
            scorches.Add(new Decal { Pos = at, R = r });
        }

        // --- Each frame ----------------------------------------------------------------------------------------------

        /// Called at the start of DrawWorld: catches up with what happened since the last frame.
        void UpdateJuice()
        {
            if (!fxOn)
            {
                juiceGame = null;
                return;
            }
            if (juiceGame != game)
            {
                ClearJuice();
                juiceGame = game;
            }
            float realDt = game.Paused ? 0f : Mathf.Min(0.05f, Time.unscaledDeltaTime), dt = realDt * EndSpeed;  // (slow motion as a run ends)
            holdGap = Mathf.Max(0f, holdGap - dt);
            if (holdT > 0f)
            {
                holdT -= dt;
                return;  // the picture holds: effects wait too
            }
            juiceClock += dt;
            juiceFrame++;
            UpdateRadiantStardust(dt);  // the Radiant Orb's trail (RadiantOrbLooks.cs)
            UpdateAirTrails(dt);  // the air trail behind fast movers (AirTrailLooks.cs)
            float t = game.AnimT;
            UpdateTankLooks(t);  // Roar and Bulldoze kick the camera (TankLooks.cs)
            var me = game.Me;

            UpdateTombLook(t);  // GlacialTombLook.cs: remembers the enemy shots a Glacial Tomb is about to erase
            UpdateBloodFreezeLook(t);  // BloodFreezeLook.cs: the wave's screen kick, and forgetting old crystals
            UpdateShockwaveLook(t);  // ShockwaveLook.cs: the wave's screen kick, and sparks for the shots it erased
            UpdateSentryNovaLook(t);  // SentryNovaLook.cs: turret heads and recoil, and the Nova and Turret sounds
            UpdateBulletRainSound(t);  // BulletRainLook.cs: the rain's cast, patter and end sounds
            UpdateFloodSound();  // FloodLook.cs: Flood's stamp, lapping and drain
            UpdateCoverSound();  // DeployableCoverLook.cs: Deployable Cover's build and break
            UpdateDecoySound();  // DecoyDummyLook.cs: Decoy Dummy's throw, squeaks, pop and deflate
            UpdateStaticShockLook(dt);  // StaticShockLook.cs: sparks off wired enemies and the Arc Cannon, Overcharge trails

            JuiceNewEffects(me);
            JuiceEnemies(t, dt);
            JuiceShots(dt);

            KillMoments(dt);  // your kills' kick, multikill calls, the wave's last kill (JuiceMoments.cs)
            JuiceMe(me);

            UpdateBowLooks();    // GunBowLooks.cs: the string snapping and the ring of light on a release
            UpdateBowFocusLooks(dt);  // BowFocusLook.cs: the Focus bar's burst, Focus arrows' wind and hits
            UpdateTankGunLooks();  // TankGunLooks.cs: a pumped Scattergun's shell dropping out
            UpdateGrenadeLauncherLooks(dt);  // GrenadeLauncherLook.cs: shots, the drum swap and reload, trails, the smoke
            UpdateTankAltLooks(dt);  // TankAltFx.cs: the Tank Soldier's Alt fires
            UpdateDpsGunLooks(dt);  // DpsGunLooks.cs: the SMG's barrels turning, a fired Railgun's steam
            UpdateOrbs();        // Ambience.cs: orb trails, pickup pulses
            UpdateDust(dt);      // dust at your feet
            UpdateWeather(dt);   // the path's weather
            JuiceAge(dt, realDt);
        }

        /// New effects this frame: blasts shake, spark, scorch and ripple; your own hits hold the picture.
        void JuiceNewEffects(Player me)
        {
            // New effects: blasts shake, spark, scorch and ripple.
            if (me != null && me.Weapon != null && me.Weapon.Id == WeaponId.Rapier)
            {
                if (me.RapierTempo > lastRapierTempo && me.RapierTempo > 0)
                    Shake(0.02f + 0.18f * me.RapierTempo / Game.RapierTempoMax);  // each Tempo hit: a small kick, harder as it builds
                if (me.RapierTempo >= Game.RapierTempoMax && lastRapierTempo < Game.RapierTempoMax)
                    Hold(0.04f);  // the stack that fills Tempo: a short hit-pause
            }
            lastRapierTempo = me != null ? me.RapierTempo : 0;
            newBooms.Clear();
            float boomShake = 0f;  // explosions this frame shake together, up to a cap, rather than piling up
            foreach (var fx in game.Effects)
            {
                if (fx.Juiced)
                    continue;
                fx.Juiced = true;
                EffectFeel(fx, me);
                if (fx.Kind == FxKind.Boom)
                    BoomFeel(fx, me, ref boomShake);
            }

            if (boomShake > 0f && trauma < 0.35f)
                Shake(Mathf.Min(boomShake, 0.35f - trauma));  // explosions alone never shake past a light rumble
        }

        /// One new effect's feel (hit-stops and shakes) for the weapon or ability that made it.
        void EffectFeel(Effect fx, Player me)
        {
            if (fx.Kind == FxKind.Repulse && trauma < 0.3f && me != null && me.Pos.DistanceTo(fx.Pos) < 40f)
                Shake(0.1f);  // your own cannon's kick: very small, and it never builds up (RepulseLook.cs)
            if (fx.Kind == FxKind.PerfectParry && (int)fx.Pos2.X == game.MyId)
            {
                Hold(Game.PerfectHold, 0.12f);  // your own perfect parry: the picture holds (the world slowing is Core/Parry.cs)
                Shake(0.16f);
            }
            if (fx.Kind == FxKind.KatanaHitCue && fx.Color.A > 0.75f && (int)fx.Pos2.X == game.MyId)
            {
                Hold(0.07f, 0.12f);  // your own Momentum Burst hit something: a heavy hold and a kick (KatanaLook.cs)
                Shake(0.35f);
            }
            else if (fx.Kind == FxKind.KatanaHitCue && (int)fx.Pos2.X == game.MyId)
            {
                Hold(Game.KatanaDashHold + Game.KatanaDashHoldPer * Mathf.Min(3f, fx.Radius - 1f), 0.06f);  // each step that cuts something: a short hit-stop
                Shake(0.07f);
            }
            if (fx.Kind == FxKind.VeinCut && (int)(fx.Radius / 1000f) == game.MyId)
            {
                Hold(0.05f, 0.1f);  // your own Vein Rush cut something: the Needle's hit-stop (VeinRushLook.cs)
                Shake(0.05f);
            }
            if (fx.Kind == FxKind.DoubleBarrel)
                DoubleBarrelFeel(fx);  // TankAltFx.cs
            RailBeamFeel(fx, me);  // a Railgun beam leaving its gun (RailBeamLook.cs)
            if (fx.Kind == FxKind.HeatDump && me != null && fx.Pos.DistanceTo(me.Pos) < 30f)
                Shake(0.08f + 0.25f * fx.Radius / Game.RailHeatMax);  // your own Heat Dump
            if (fx.Kind == FxKind.CoverBreak && me != null && me.Pos.DistanceTo(fx.Pos) < 450f)
                Shake(0.22f);  // a Deployable Cover bursting near you (DeployableCoverLook.cs)
            if (fx.Kind == FxKind.StoneSlam && fx.Pos2.X >= 1f && me != null && fx.Pos.DistanceTo(me.Pos) < 500f)
                Shake(0.3f);  // a Stonespear slam shakes the ground near you
            if (fx.Kind == FxKind.HoneyHarvest && me != null && (int)fx.Pos2.X == me.Id)
                Shake(0.2f);  // your harvest bursting
            if (fx.Kind == FxKind.EmbolusBlast && me != null && (int)fx.Pos2.X == me.Id)
                Shake(0.25f);  // your Embolus Spears bursting (EmbolusLook.cs)
            KnightDpsFeel(fx, me);  // the DPS Knight's cut, impact and executions (KnightDpsLook.cs)
            if (fx.Kind == FxKind.TwinHit && (int)fx.Pos2.X == game.MyId)
                TwinHitFeel((int)fx.Pos2.Y, (int)fx.Radius);  // your own Twin Blades cut something (below)
            if (fx.Kind == FxKind.KnuckleHit)
                KnuckleHitFeel(fx, (int)fx.Pos2.X == game.MyId);  // an Arc Knuckles punch connected: sparks, and for your own a hit-stop and shake (below)
            if (fx.Kind == FxKind.LanceHit && (int)fx.Pos2.X == game.MyId)
                LanceHitFeel((int)fx.Pos2.Y, (int)fx.Radius);  // your own Lance hit something (below)
            if (fx.Kind == FxKind.LanceImpact)
                ImpactNumber(fx);  // a Lance shove's Impact: its own orange number (below)
            if (fx.Kind == FxKind.Jab && me != null && fx.Color.R == Game.HammerBoomColor.R && fx.Color.G == Game.HammerBoomColor.G && fx.Color.B == Game.HammerBoomColor.B && me.Pos.DistanceTo(fx.Pos) < 44f)
            {
                float hp = Mathf.Clamp01((fx.Color.A - 0.2f) / 0.8f);  // your own hammer slam: a hit-stop and a kick, harder the more it was charged
                Hold(0.04f + 0.03f * hp);
                Shake(0.1f + 0.26f * hp);  // Quake: the ground takes it, so it kicks harder
            }
            if (fx.Kind == FxKind.HammerSlam && me != null && me.Pos.DistanceTo(fx.Pos2) < 40f)
            {
                Hold(0.06f);  // your own Storm Hammer slam: the picture holds and the ground kicks
                Shake(0.3f);
            }
            if (fx.Kind == FxKind.Slash && me != null && me.Weapon != null && me.Weapon.Id == WeaponId.Greatsword && fx.Radius > 1f && me.Pos.DistanceTo(fx.Pos) < 44f)
            {
                float gr = fx.Pos2.DistanceTo(fx.Pos), ga = (fx.Pos2 - fx.Pos).Angle;
                foreach (var en in game.Enemies.Values)
                {
                    V2 rel = en.Disp - fx.Pos;
                    float gd = Mathf.Abs(Mathf.Repeat(rel.Angle - ga + MathX.Pi, MathX.Tau) - MathX.Pi);
                    if (rel.Length - en.Radius * 0.5f > gr || gd > fx.Radius + 0.1f)
                        continue;
                    Hold(0.05f);  // your own Greatsword cut caught something: a hit-stop and a kick
                    Shake(0.14f);
                    break;
                }
            }
            if (fx.Kind == FxKind.GreatRend && me != null && me.Weapon != null && me.Weapon.Id == WeaponId.Greatsword && me.Pos.DistanceTo(fx.Pos) < 44f)
            {
                V2 rl = fx.Pos2 - fx.Pos, rd = rl.Length > 1f ? rl / rl.Length : new V2(1f, 0f);
                float rt = Mathf.Clamp01((fx.Color.A - 0.2f) / 0.8f);  // your own Rend: harder the more Temper it carried
                foreach (var en in game.Enemies.Values)
                {
                    V2 rel = en.Disp - fx.Pos;
                    float along = rel.X * rd.X + rel.Y * rd.Y, across = Mathf.Abs(-rel.X * rd.Y + rel.Y * rd.X);
                    if (along < 0f || along > rl.Length + en.Radius || across > fx.Radius + en.Radius)
                        continue;
                    Hold(0.04f + 0.04f * rt);  // it caught something: a hit-stop and a kick
                    Shake(0.12f + 0.2f * rt);
                    break;
                }
            }
            if (fx.Kind == FxKind.KatanaCut && me != null && me.Pos.DistanceTo(fx.Pos) < 900f)
                Shake(fx.Color.A > 0.75f ? 0.08f : 0f);  // a Katana kill nearby (KatanaLook.cs)
            if (fx.Kind == FxKind.Exsanguinate && fx.Pos2.X < 0f && me != null)  // Exsanguinate's Barbed Lash (ExsanguinateLook.cs)
            {
                bool mine = (int)fx.Pos2.Y == game.MyId;
                if (fx.Pos2.X < -1.5f && mine)
                {
                    Hold(0.07f);  // your yank lands: a hit-stop and a hard kick
                    Shake(0.32f);
                }
                else if (fx.Pos2.X < -1.5f && me.Pos.DistanceTo(fx.Pos) < 900f)
                    Shake(0.12f);
                else if (mine)
                    Shake(0.1f);  // the whips crack out
            }
            if (fx.Kind == FxKind.DeathLotus && fx.Pos2.X < 0f && me != null && me.Pos.DistanceTo(fx.Pos) < 900f)
                Shake(0.3f);  // the cast: one kick that dies away over the cuts (DeathLotusLook.cs)
            if (fx.Kind == FxKind.TitanSlam && me != null && fx.Pos2.X < 2f && me.Pos.DistanceTo(fx.Pos) < 1100f)
                Shake(fx.Pos2.X > 0f ? 0.3f : 0.2f);  // a slam or the titan standing up (TitanLook.cs)
            if (fx.Kind == FxKind.TitanSlam && me != null && fx.Pos2.X >= Game.TitanEarth - 0.5f && me.Pos.DistanceTo(fx.Pos) < 1100f)
            {
                int ek = (int)(fx.Pos2.X + 0.5f);  // Stone Colossus (StoneColossusLook.cs): a slam, the rise, a footfall
                if (ek == Game.TitanEarth)
                    Shake(0.24f);
                else if (ek == Game.TitanEarth + 1)
                    Shake(0.3f);
                else if (ek == Game.ColFxStomp)
                    Shake(0.06f);
            }
        }

        /// An explosion: its share of this frame's shake, its sparks or shards, and a scorch and ripple when it's big.
        void BoomFeel(Effect fx, Player me, ref float boomShake)
        {
            newBooms.Add(fx.Pos);  // an enemy's death plays one where it died (DeathFx)
            float near = me != null ? Mathf.Clamp01(1.2f - me.Pos.DistanceTo(fx.Pos) / 900f) : 0.5f;
            boomShake = Mathf.Max(boomShake, Mathf.Min(0.22f, fx.Radius / 450f) * near);
            if (IsGrenadeBoom(fx))
                FragBurst(fx);  // a grenade's shards and smoke (SoldierLooks.cs)
            else if (IsFlakBoom(fx))
                FlakBurstParts(fx);
            else if (IsGlBoom(fx))
                GlBlastParts(fx);  // the Grenade Launcher's fireballs, smoke, debris and scorch (GrenadeLauncherLook.cs)  // a Flak airburst's shrapnel and smoke (TankGunLooks.cs)
            else if (!IsLeapBoom(fx))  // (the Leap Slam's landing draws its own: LeapSlamLook.cs)
                Sparks(fx.Pos, fx.Color, Mathf.Clamp((int)(fx.Radius / 4f), 4, 40), 160f + fx.Radius * 2f, 0.5f, 2.4f);
            if (fx.Radius >= 80f)
            {
                Scorch(fx.Pos, fx.Radius * 1.6f);
                ripples.Add(new Ripple { Pos = fx.Pos, Power = Mathf.Min(2f, fx.Radius / 110f) });
            }
        }

        /// Enemies: hits (flash, bulge, numbers, big-hit pause), and the ones that are gone (shatter).
        void JuiceEnemies(float t, float dt)
        {
            // Enemies: hits (flash, bulge, numbers, big-hit pause), and the ones that are gone (shatter).
            foreach (var e in game.Enemies.Values)
                SeeEnemy(e, t, dt);
            EnemiesGone();
        }

        /// One enemy this frame: a boss's entrance card when it appears, its damage number, flash and bulge.
        void SeeEnemy(Enemy e, float t, float dt)
        {
            EnemySeen s;
            bool fresh = !seenEnemies.TryGetValue(e.Id, out s);
            if (fresh)
            {
                s = enemyPool.Count > 0 ? enemyPool.Pop() : new EnemySeen();
                s.Hp = e.Hp; s.Squash = s.Flash = s.Dmg = s.DmgT = s.Owed = 0f;
                s.HitFlash = e.Flash;
                seenEnemies[e.Id] = s;
                if (e.Type == EnemyType.Warden && game.IsSpawning(e) && game.State == MatchState.Fighting && introWave != game.Wave)
                {
                    introWave = game.Wave;
                    StartWardenIntro(e);  // the Warden's own entrance in place of the card (WardenIntro.cs)
                }
                else if ((e.Def.IsBoss || e.Def.Mini) && e.Type != EnemyType.ReaperOfDawn && e.Type != EnemyType.DualReaper && (introWave != game.Wave || e.Def.Mini) && game.State == MatchState.Fighting
                    && !(e.Def.Mini && game.RoomsOn))  // (the Rooms arena's vault mini-boss is optional: no fanfare)
                {
                    introWave = game.Wave;
                    introT = IntroTime;
                    introName = e.Def.Name.ToUpperInvariant();
                    Shake(0.2f);
                }
            }
            bool spawning = game.IsSpawning(e);
            float lost = s.Hp - e.Hp;
            // A real hit raises the enemy's Flash (Combat.cs HurtEnemy, or the snapshot's hit flag); burning, bleeding and
            // poison only wear its health down, so they add to the damage number without flashing or bulging it.
            bool struck = e.Flash > s.HitFlash + 1e-4f;
            s.HitFlash = e.Flash;
            if (!fresh && lost > 0.01f && !spawning)
            {
                s.Dmg += lost;
                if (s.DmgT <= 0f)
                    s.DmgT = 0.15f;
                if (struck && !e.Def.IsBoss && lost >= Mathf.Max(80f, e.MaxHp * 0.35f))
                {
                    Hold(0.035f);
                    Shake(0.06f);
                }
            }
            if (!fresh && struck && !spawning)
            {
                // Under steady fire the bulge gets smaller and only starts again once the last one has mostly settled,
                // so a boss everyone is shooting pulses gently instead of staying swollen.
                if (s.Squash < 0.35f)
                    s.Squash = 1f - 0.6f * e.FlashHeat;
                s.Flash = 0.09f;
                ChargeHitCheck(e);  // a full Railgun or Warhammer charge landing: hold a moment (ChargeCameraFx.cs)
            }
            s.Pos = e.Disp; s.Hp = e.Hp; s.Max = e.MaxHp; s.Rot = EnemyRotation(e, t);
            s.R = e.Type == EnemyType.Singularity ? e.Def.Radius : e.Radius;
            s.Sides = e.Def.Sides; s.Col = e.Def.Color; s.Boss = e.Def.IsBoss || e.Def.Mini;
            s.Big = e.Elite || e.Def.Heavy || e.MaxHp >= 400f;
            s.Spawning = spawning;
            s.Frame = juiceFrame;
            s.Squash = Mathf.Max(0f, s.Squash - dt * 6f);
            s.Flash = Mathf.Max(0f, s.Flash - dt);
            if (s.DmgT > 0f)
            {
                s.DmgT -= dt;
                if (s.DmgT <= 0f)
                    FlushNumber(s);
            }
        }

        /// Enemies gone since the last frame: their killing blow's number and their shatter.
        void EnemiesGone()
        {
            goneIds.Clear();
            foreach (var kv in seenEnemies)
                if (kv.Value.Frame != juiceFrame)
                    goneIds.Add(kv.Key);
            // An enemy that's gone has died (the host removes one the tick it dies; a client's snapshot just stops listing
            // it, and today's game already plays a death for that). A boss's death plays big only if no boss is left (the
            // Twin Reapers fusing into the Dual Reaper isn't a death).
            bool bossLeft = false;
            foreach (var e in game.Enemies.Values)
                bossLeft |= e.Def.IsBoss || e.Def.Mini;
            foreach (int id in goneIds)
            {
                var s = seenEnemies[id];
                seenEnemies.Remove(id);
                // Its killing blow: whatever health it had left, if the game played a death where it was.
                foreach (var at in newBooms)
                    if (at.DistanceTo(s.Pos) < s.R + 24f)
                    {
                        s.Dmg += Mathf.Max(0f, s.Hp);
                        break;
                    }
                if (s.Dmg > 0f)
                    FlushNumber(s);  // (its Impact damage already shown is left out)
                if (!s.Spawning)
                {
                    Shatter(s, s.Boss && !bossLeft);
                    frameKills.Add(new KillSeen { Pos = s.Pos, R = s.R, Boss = s.Boss });  // whose it was: KillMoments (JuiceMoments.cs)
                }
                enemyPool.Push(s);
            }
        }

        /// Shots: trails, muzzle flashes, sparks where they land.
        void JuiceShots(float dt)
        {
            // Shots: trails, muzzle flashes, sparks where they land.
            foreach (var b in game.Bullets.Values)
                SeeShot(b, dt);
            ShotsGone();
        }

        /// One shot this frame: its muzzle flash when it's new, then its trail and emissions.
        void SeeShot(Bullet b, float dt)
        {
            if (b.Kind == Defs.RodentShot)
                return;  // a Rodent Minigun rodent: no flash, trail or spark; it scampers off on its own (RodentMinigunLook.cs)
            ShotSeen s;
            if (!seenShots.TryGetValue(b.Id, out s))
            {
                s = shotPool.Count > 0 ? shotPool.Pop() : new ShotSeen();
                s.N = 0;
                seenShots[b.Id] = s;
                Player owner = null;
                float cool;
                bool fromGun = b.Owner > 0 && game.Players.TryGetValue(b.Owner, out owner) && b.Pos.DistanceTo(owner.Pos) < owner.Def.BodyR + 40f + (Game.IsTwinRifle(owner.Weapon.Id) ? Game.TwinMuzzleOut + 30f : 0f);  // (the Twin Rifle's rounds start out at its wide-held muzzles, 30 px to each side)
                if (fromGun && owner.Weapon.Id == WeaponId.Revolver && b.Kind == Defs.HealerShot)
                    OnRevolverShot(owner, b);  // the Revolver's flash, smoke and brass (GunBowLooks.cs)
                else if (fromGun && owner.Weapon.Id == WeaponId.TwinPistols && b.Kind == Defs.HealerShot)
                    OnTwinPistolShot(owner, b);  // which gun fired, its flash and casing (TwinPistolsLook.cs)
                else if (fromGun && (owner.Weapon.Id == WeaponId.Scattergun && b.Kind == Defs.TankShot || owner.Weapon.Id == WeaponId.RiotRepeater && b.Kind == Defs.TankShot || owner.Weapon.Id == WeaponId.FlakCannon && b.Kind == Defs.FlakShell))
                    OnTankGunShot(owner, b);  // the Tank guns' flash, smoke and brass (TankGunLooks.cs)
                else if (fromGun && (Game.IsTwinRifle(owner.Weapon.Id) && b.Kind == Defs.DpsShot || owner.Weapon.Id == WeaponId.BurstSmg && b.Kind == Defs.DpsShot || owner.Weapon.Id == WeaponId.Railgun && b.Kind == Defs.RailShot))
                    OnDpsGunShot(owner, b);
                else if (fromGun && owner.Weapon.Id == WeaponId.DoubleBarrel && b.Kind == Defs.DpsShot)
                    OnDoubleBarrelShot(owner, b);  // the Double Barrel's flash and smoke (DoubleBarrelLook.cs)  // the Soldier DPS guns' flash, brass and steam (DpsGunLooks.cs)
                else if (fromGun && b.Kind == Defs.FocusArrow)
                    OnFocusArrow(owner, b);  // the gold ring, wind crescents and FOCUS! (BowFocusLook.cs)
                else if (fromGun && b.Kind == Defs.ScatterArrow)
                    OnScatterArrow(owner, b);  // Scatter's fan of light (BowFocusLook.cs)
                else if (fromGun && owner.Weapon.Id == WeaponId.TwinRifle && b.Kind == Defs.GrenadeShot)
                    OnTwinNadeShot(owner, b);  // the grenade tube's Rocket assist launch (TwinNadeLook.cs)
                if (fromGun && (!muzzleT.TryGetValue(b.Owner, out cool) || cool <= juiceClock))
                {
                    muzzleT[b.Owner] = juiceClock + 0.05f;
                    AddPart(new Part { Kind = PSpark, Pos = b.Pos, Vel = b.Vel.Normalized() * 120f, Life = 0.08f, Max = 0.08f, Size = 4.5f, Col = new Rgba(0.85f, 0.92f, 1f) });
                    NoteMuzzleLight(b.Pos, Defs.BulletColor(b.Kind));  // a flash of light on the floor (LightingLook.cs)
                }
            }
            else if (b.Kind == Defs.Caltrop || b.Kind == Defs.GrenadeShot || b.Vel.Length <= 450f)
                s.N = 0;  // slow (or a caltrop, which draws its own streak, KatanaLook.cs): no trail, and none left behind to stretch back to later
            else
            {
                for (int k = Math.Min(s.N, s.Trail.Length - 1); k > 0; k--)
                    s.Trail[k] = s.Trail[k - 1];
                s.Trail[0] = s.Pos;
                s.N = Math.Min(s.N + 1, s.Trail.Length);
            }
            s.Pos = b.Pos; s.Vel = b.Vel; s.Life = b.Life; s.Radius = b.Radius; s.Owner = b.Owner; s.Kind = b.Kind;
            EmitFromShot(b, dt);  // smoke, embers, dust, drips (ShotLooks.cs)
            s.Frame = juiceFrame;
        }

        /// Shots gone since the last frame: how each one ended.
        void ShotsGone()
        {
            goneIds.Clear();
            foreach (var kv in seenShots)
                if (kv.Value.Frame != juiceFrame)
                    goneIds.Add(kv.Key);
            foreach (int id in goneIds)
            {
                var s = seenShots[id];
                seenShots.Remove(id);
                if (s.Owner == game.MyId)
                    myShotEnds.Add(s.Pos);  // (KillMoments: a kill where your shot ended is yours)
                BulletEnding(s);
                if (s.Kind == Defs.FocusArrow || s.Kind == Defs.ScatterArrow)
                    OnBowArrowGone(s);  // a Focus arrow's last gust, a missed Scatter arrow stuck in the floor (BowFocusLook.cs)
                shotPool.Push(s);
            }
        }

        /// You: hurt (a faint red in the corners, a kick), levels and legendaries.
        void JuiceMe(Player me)
        {
            // You: hurt (a faint red in the corners, a kick), levels and legendaries.
            if (me == null)
                return;
            if (myHp >= 0f && me.Hp < myHp - 0.5f && !me.Downed)
            {
                hurtTint = 1f;
                Shake(0.18f + Mathf.Min(0.3f, (myHp - me.Hp) / Mathf.Max(1f, me.MaxHp)));
            }
            myHp = me.Hp;
            if (myLevel >= 0 && me.Level > myLevel)
            {
                LevelBurst(me, false);
                LevelShock(me, false);  // JuiceMoments.cs
            }
            myLevel = me.Level;
            int legendaries = 0;
            foreach (int u in me.Taken)
                if (u >= 0 && u < Upgrades.All.Length && Upgrades.All[u].Rarity == Rarity.Legendary)
                    legendaries++;
            if (myLegendaries >= 0 && legendaries > myLegendaries)
            {
                LevelBurst(me, true);
                LevelShock(me, true);
            }
            myLegendaries = legendaries;
        }

        /// A boss's death bursts, then everything ages: particles, scorches, ripples, numbers, shake and tints.
        void JuiceAge(float dt, float realDt)
        {
            // A boss's death plays out in waves.
            for (int i = bursts.Count - 1; i >= 0; i--)
                if (bursts[i].At <= juiceClock)
                {
                    var bu = bursts[i];
                    bursts.RemoveAt(i);
                    Ring(bu.Pos, bu.R, 0.5f, bu.Col);
                    Sparks(bu.Pos, new Rgba(1f, 0.85f, 0.6f), 30, 420f, 0.8f, 2.6f);
                    Shake(0.3f);
                }

            // Age everything.
            int n = 0;
            for (int i = 0; i < partN; i++)
            {
                var p = parts[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                    continue;
                if (p.Kind == PBrass)
                    BrassStep(ref p, dt);  // a casing in the air, bouncing, sliding (TwinRifleFx.cs)
                else
                {
                    p.Pos += p.Vel * dt;
                    float drag = (float)Math.Exp(-(p.Kind == PShard ? 2.2f : p.Kind == PDust ? 5f : 3.5f) * dt);
                    p.Vel = p.Vel * drag;
                    p.Rot += p.Spin * dt;
                }
                parts[n++] = p;
            }
            partN = n;
            for (int i = scorches.Count - 1; i >= 0; i--)
            {
                var d = scorches[i];
                d.Age += dt;
                if (d.Age >= ScorchLife) scorches.RemoveAt(i); else scorches[i] = d;
            }
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                var r = ripples[i];
                r.T += dt;
                if (r.T >= (r.Life > 0f ? r.Life : RippleLife)) ripples.RemoveAt(i); else ripples[i] = r;
            }
            for (int i = numbers.Count - 1; i >= 0; i--)
            {
                var nb = numbers[i];
                nb.T += dt;
                nb.Pos += new V2(0f, -38f * dt);
                if (nb.T >= NumberLife) numbers.RemoveAt(i); else numbers[i] = nb;
            }
            trauma = Mathf.Max(0f, trauma - realDt * 1.8f);
            TwinKickStep(realDt);  // the Twin Rifle's screen kick (TwinRifleFx.cs)
            ChargeCamStep(game.Me, realDt);  // a charge weapon's draw pull-back and release kick (ChargeCameraFx.cs)
            hurtTint = Mathf.Max(0f, hurtTint - realDt * 2.2f);
            introT = Mathf.Max(0f, introT - realDt);
            TickWardenIntro(realDt);  // WardenIntro.cs
        }

        // --- Bullet endings: what a shot does as it goes, by why it went ---------------------------------------------------

        public enum Ending : byte { None, RanOut, HitEnemy, Blocked, HitPlayer }

        /// Why a shot that's just gone went, from where it was, how fast, how much life it had left and what was near it.
        public Ending WhyGone(V2 pos, V2 vel, float life, float radius, int owner)
        {
            bool inside = pos.X > 10f && pos.Y > 10f && pos.X < Game.Width - 10f && pos.Y < Game.Height - 10f;
            if (!inside)
                return Ending.None;  // flew off the arena
            if (life <= 0.06f)
                return Ending.RanOut;
            if (owner != 0)
            {
                // A player's shot: stopped by a shield from the front, or hit an enemy.
                foreach (var e in game.Enemies.Values)
                {
                    float reach = e.Radius + radius + 14f;
                    if (e.Disp.DistanceSq(pos) > reach * reach)
                        continue;
                    bool shield = e.Type == EnemyType.Shielder && e.Phase < Game.ShieldBroken
                        || Game.IsWallKin(e.Type) && Game.RiotCrack(e) < Game.RiotCracked && Game.RiotState(e) != Game.RiotLow;
                    if (shield && V2.FromAngle(e.Facing).Dot((pos - e.Disp).Normalized()) > 0.45f)
                        return Ending.Blocked;
                    return Ending.HitEnemy;
                }
                return Ending.None;  // gone for some other reason (a wave cleared, a ward placed...)
            }
            // An enemy's shot: blocked by a parry, Barrier, Shield Wall or a blocking minion, or it hit someone.
            foreach (var p in game.Players.Values)
            {
                if (p.Downed)
                    continue;
                float d = p.Disp.DistanceTo(pos);
                if (p.ParryActive && d < p.Def.BodyR + 46f || p.BarrierT > 0f && d < p.BarrierRadius + radius + 10f || p.WallUp && d < p.Def.BodyR + 60f)
                    return Ending.Blocked;
                if (d < p.Def.HitR + radius + 14f)
                    return Ending.HitPlayer;
            }
            foreach (var m in game.Minions.Values)
                if (m.Def.BlockRadius > 0f && m.Disp.DistanceTo(pos) < m.Def.BlockRadius + radius + 10f)
                    return Ending.Blocked;
            return Ending.None;
        }

        void BulletEnding(ShotSeen s)
        {
            var c = ShotColor(s.Kind, s.Owner);
            float r = Mathf.Max(2.5f, s.Radius);
            V2 dir = s.Vel.IsZero ? V2.FromAngle(0f) : s.Vel.Normalized();
            switch (WhyGone(s.Pos, s.Vel, s.Life, s.Radius, s.Owner))
            {
                case Ending.RanOut:
                    // It shrinks and fades as it drifts to a stop, then leaves a wisp of smoke.
                    AddPart(new Part { Kind = PFade, Pos = s.Pos, Vel = s.Vel * 0.25f, Life = 0.16f, Max = 0.16f, Size = r, Col = c });
                    AddPart(new Part { Kind = PDust, Pos = s.Pos + dir * 3f, Vel = dir * 20f, Life = 0.35f, Max = 0.35f, Size = r * 0.9f, Col = c.Mix(DustColor, 0.5f) });
                    break;
                case Ending.HitEnemy:
                {
                    // A splash flattened against the enemy, and sparks bouncing back the way it came. Where hits are
                    // pouring into one spot (a boss under the whole team's fire) each one throws fewer, so the glow
                    // doesn't add up to a white blob.
                    float crowd = ImpactCrowd(s.Pos);
                    int sparks = crowd < 3f ? 5 : crowd < 6f ? 2 : crowd < 10f ? 1 : 0;
                    if (crowd < 10f || juiceRng.Next(3) == 0)
                        AddPart(new Part { Kind = PSplash, Pos = s.Pos, Rot = dir.Angle, Life = 0.14f, Max = 0.14f, Size = r * 2.6f, Col = c });
                    for (int i = 0; i < sparks; i++)
                    {
                        float a = dir.Angle + MathX.Pi + Rnd(-0.75f, 0.75f);
                        AddPart(new Part { Kind = PSpark, Pos = s.Pos, Vel = V2.FromAngle(a) * Rnd(90f, 260f), Life = Rnd(0.15f, 0.28f), Max = 0.28f, Size = 1.8f, Col = c });
                    }
                    if (s.Kind == Defs.ArcaneShot && ShotWeapon(s.Kind, s.Owner) == WeaponId.IceShards)  // an ice shard shatters
                        for (int i = 0; i < 4; i++)
                        {
                            float a = Rnd(0f, MathX.Tau);
                            AddPart(new Part { Kind = PTumble, Pos = s.Pos, Vel = V2.FromAngle(a) * Rnd(70f, 170f), Rot = a, Spin = Rnd(-14f, 14f), Life = 0.4f, Max = 0.4f, Size = 2.4f, Col = new Rgba(0.9f, 0.98f, 1f) });
                        }
                    break;
                }
                case Ending.Blocked:
                {
                    NoteBarrierBlock(s.Pos);  // a Barrier's plate recoils there (BarrierLook.cs)
                    // A ricochet: the shot glances off, tumbling and fading, with a bright metallic spark.
                    float a = dir.Angle + MathX.Pi + Rnd(-0.9f, 0.9f);
                    AddPart(new Part { Kind = PTumble, Pos = s.Pos, Vel = V2.FromAngle(a) * Mathf.Max(140f, s.Vel.Length * 0.45f), Rot = a, Spin = Rnd(-18f, 18f), Life = 0.42f, Max = 0.42f, Size = r, Col = c });
                    AddPart(new Part { Kind = PSpark, Pos = s.Pos, Vel = V2.Zero, Life = 0.07f, Max = 0.07f, Size = 5f, Col = Rgba.White });
                    for (int i = 0; i < 3; i++)
                        AddPart(new Part { Kind = PSpark, Pos = s.Pos, Vel = V2.FromAngle(a + Rnd(-0.5f, 0.5f)) * Rnd(150f, 320f), Life = 0.2f, Max = 0.2f, Size = 1.6f, Col = new Rgba(0.95f, 0.97f, 1f) });
                    break;
                }
                case Ending.HitPlayer:
                    // It bursts in its own colour with a small ring, so you can see what hit you.
                    Ring(s.Pos, r * 4f + 10f, 0.25f, c);
                    Sparks(s.Pos, c, 8, 220f, 0.3f, 2f);
                    break;
            }
        }

        void FlushNumber(EnemySeen s)
        {
            float owed = Mathf.Min(s.Owed, s.Dmg);
            s.Dmg -= owed;
            s.Owed -= owed;
            if (fxNumbers && s.Dmg >= 0.5f)
            {
                if (numbers.Count >= 60)
                    numbers.RemoveAt(0);
                numbers.Add(new Number { Pos = s.Pos + new V2(Rnd(-8f, 8f), -s.R - 6f), Value = Mathf.RoundToInt(s.Dmg), Big = s.Dmg >= Mathf.Max(60f, s.Max * 0.25f) && !s.Boss || s.Dmg >= 200f });
            }
            s.Dmg = 0f;
            s.DmgT = 0f;
        }

        /// A Lance Impact (Pos2 = the enemy's id and the other's, -1 a wall, -2 a heavy one that took nothing; Radius = the damage
        /// each took): one orange number with its tag where they met, and that damage left out of their own next numbers.
        void ImpactNumber(Effect fx)
        {
            EnemySeen s;
            int a = Mathf.RoundToInt(fx.Pos2.X), b = Mathf.RoundToInt(fx.Pos2.Y);
            if (seenEnemies.TryGetValue(a, out s))
                s.Owed += fx.Radius;
            if (b >= 0 && seenEnemies.TryGetValue(b, out s))
                s.Owed += fx.Radius;
            if (!fxNumbers)
                return;
            if (numbers.Count >= 60)
                numbers.RemoveAt(0);
            numbers.Add(new Number { Pos = fx.Pos + new V2(Rnd(-4f, 4f), -18f), Value = Mathf.RoundToInt(fx.Radius), Big = true, Tag = b == -1 ? "WALL" : "IMPACT" });
        }

        /// Your own Lance hit: a hit-pause and a kick sized to the move. The jabs and the sweep get a light kick, the Breaching
        /// Thrust a hold and the biggest kick, the hook a short tug. `hits`: enemies hit, which adds a little.
        void LanceHitFeel(int kind, int hits)
        {
            float more = Mathf.Min(3, Mathf.Max(0, hits - 1));
            switch (kind)
            {
                case 1: case 2: Hold(0.02f, 0.12f); Shake(0.05f + 0.01f * more); break;
                case 3: Shake(0.05f + 0.01f * more); break;
                case 4: Hold(0.05f + 0.01f * more, 0.2f); Shake(0.2f + 0.02f * more); break;
                case 5: Hold(0.03f, 0.2f); Shake(0.08f); break;
            }
        }

        /// An enemy is gone: it breaks into the triangles of its shape, with a white flash, sparks and a scorch.
        void Shatter(EnemySeen s, bool bossDeath)
        {
            int sides = Mathf.Clamp(s.Sides, 3, 10);
            for (int k = 0; k < sides; k++)
            {
                float a0 = s.Rot + MathX.Tau * k / sides, a1 = s.Rot + MathX.Tau * (k + 1) / sides, mid = (a0 + a1) / 2f;
                float sp = Rnd(140f, 320f) * (bossDeath ? 1.6f : 1f);
                AddPart(new Part { Kind = PShard, Pos = s.Pos, Vel = V2.FromAngle(mid) * sp, A0 = a0 - mid, A1 = a1 - mid, Size = s.R, Rot = mid, Spin = Rnd(-8f, 8f), Life = 0.7f, Max = 0.7f, Col = s.Col });
            }
            if (!fxReduced)
                Ring(s.Pos, s.R * 3.2f, 0.22f, Rgba.White, PFlash);
            Sparks(s.Pos, s.Col, bossDeath ? 90 : 14, bossDeath ? 520f : 320f, bossDeath ? 1.1f : 0.5f, bossDeath ? 3.2f : 2.4f);
            Scorch(s.Pos, s.R * 3.4f);
            if (bossDeath || s.R >= 25f)
                ripples.Add(new Ripple { Pos = s.Pos, Power = bossDeath ? 2.2f : 0.6f });
            if (bossDeath)
            {
                Shake(0.9f);
                Hold(0.15f);
                StartBossSlow();  // time slows for a moment (JuiceMoments.cs)
                for (int k = 1; k <= 4; k++)
                    bursts.Add(new Burst { At = juiceClock + k * 0.16f, Pos = s.Pos + new V2(Rnd(-40f, 40f), Rnd(-40f, 40f)), R = 120f + k * 40f, Col = s.Col });
            }
            else if (s.Big)
            {
                Shake(0.12f);
                Hold(0.05f);
            }
        }

        void LevelBurst(Player me, bool legendary)
        {
            Ring(me.Pos, legendary ? 200f : 140f, legendary ? 0.9f : 0.7f, new Rgba(1f, 0.84f, 0.35f));
            int n = legendary ? 70 : 40;
            for (int i = 0; i < n; i++)
                AddPart(new Part { Kind = PSpark, Pos = me.Pos + new V2(Rnd(-18f, 18f), Rnd(-10f, 10f)), Vel = new V2(Rnd(-40f, 40f), Rnd(-260f, -90f) * (legendary ? 1.4f : 1f)),
                    Life = Rnd(0.6f, 1.1f), Max = 1.1f, Size = 2.6f, Col = new Rgba(1f, 0.85f, 0.4f) });
        }

        /// DrawEnemy: how much bigger to draw an enemy that was just hit, and whether it flashes white.
        float JuiceScale(Enemy e)
        {
            EnemySeen s;
            return Juicy && !fxReduced && seenEnemies.TryGetValue(e.Id, out s) ? 1f + (s.Boss ? 0.05f : 0.15f) * Mathf.Sin(s.Squash * MathX.Pi) : 1f;
        }

        /// How far a hit turns an enemy white: all the way for a fresh hit, down to a light tint once it has been under
        /// steady fire for a moment (Enemy.FlashHeat), and never past 70% on a boss, so it keeps its colour and shape.
        static float HitFlashMix(Enemy e) => Mathf.Min(e.Def.IsBoss ? 0.7f : 1f, 1f - 0.7f * e.FlashHeat);

        bool JuiceFlash(Enemy e)
        {
            EnemySeen s;
            return Juicy && !fxReduced && seenEnemies.TryGetValue(e.Id, out s) && s.Flash > 0f;
        }

        // --- Drawing -------------------------------------------------------------------------------------------------

        /// DrawWorld, before anything else: the screen-shake offset for this frame.
        void BeginJuiceFrame()
        {
            RefreshCull();  // the screen's size for skipping off-screen triangles (WorldRenderer.cs)
            shakeX = shakeY = 0f;
            punchK = 0f;
            if (!Juicy)
                return;  // (frozen with P: the picture holds still)
            if (trauma > 0f)
            {
                // Smooth noise, not a buzz: the picture drifts between random offsets eased into each other (ShakeNoise),
                // a slow sway plus a finer, quicker layer, so a big hit rolls the camera and a small one just nudges it.
                float k = trauma * trauma * (fxShake == 1 ? 0.45f : 1f) * 12f * drawScale, s = Time.unscaledTime;
                shakeX = k * (0.75f * ShakeNoise(s * ShakeRate, 0) + 0.25f * ShakeNoise(s * ShakeRate * 2.3f, 2));
                shakeY = k * (0.75f * ShakeNoise(s * ShakeRate, 1) + 0.25f * ShakeNoise(s * ShakeRate * 2.3f, 3));
            }
            BeginTwinKick(game.Me);  // the Twin Rifle's push and punch (TwinRifleFx.cs)
            BeginChargeCam(game.Me); // a charge weapon's pull-back and kick (ChargeCameraFx.cs)
        }

        void EndJuiceFrame()
        {
            shakeX = shakeY = punchK = 0f;
            EndLeanFrame();  // CameraLean.cs
        }

        /// On the floor, under everything: scorch marks fading, and trails behind fast shots.
        void DrawJuiceFloor()
        {
            if (!Juicy)
                return;
            DrawOrbTrails();  // Ambience.cs
            foreach (var d in scorches)
            {
                if (glClipOn)
                    break;  // (a clipped window, the lobby's preview: gradients don't clip, so no scorch marks)
                float a = ScorchAlpha(d.Age);  // fresh, then a faint stain for a while (JuiceMoments.cs)
                FanGradient(d.Pos, d.R, new Rgba(0.02f, 0.015f, 0.03f, a), new Rgba(0.02f, 0.015f, 0.03f, 0f), 14);
            }
            foreach (var s in seenShots.Values)
            {
                if (s.N < 2)
                    continue;
                var c = Defs.BulletColor(s.Kind);
                V2 from = s.Pos;
                for (int i = 0; i < s.N; i++)
                {
                    float f = 1f - (float)i / s.N;
                    Line(from, s.Trail[i], Mathf.Max(1f, s.Radius * 1.6f * f), c.WithAlpha(0.42f * f));
                    from = s.Trail[i];
                }
            }
        }

        /// Over the enemies: shards, rings and sparks.
        void DrawJuiceParts()
        {
            if (!Juicy)
                return;
            DrawWeather();  // Ambience.cs: the path's weather
            for (int i = 0; i < partN; i++)
            {
                var p = parts[i];
                float f = p.Life / p.Max;
                switch (p.Kind)
                {
                    case PShard:
                    {
                        SetColor(p.Col.WithAlpha(Mathf.Min(1f, f * 1.6f)));
                        float r = p.Size * f;
                        Tri(p.Pos, p.Pos + V2.FromAngle(p.Rot + p.A0) * r, p.Pos + V2.FromAngle(p.Rot + p.A1) * r);
                        break;
                    }
                    case PRing:
                        Arc(p.Pos, p.Size * (1f - f), 8f * f + 1f, p.Col.WithAlpha(f), 32);
                        break;
                    case PFlash:
                        Arc(p.Pos, p.Size * (0.3f + 0.7f * (1f - f)), 6f * f + 1f, Rgba.White.WithAlpha(0.9f * f), 28);
                        break;
                    case PDust:  // a soft puff that swells as it fades
                        Circle(p.Pos, p.Size * (1.8f - 0.8f * f), p.Col.WithAlpha(0.35f * f), 8);
                        break;
                    case PFade:  // a shot at the end of its range, shrinking away
                        Circle(p.Pos, p.Size * f, p.Col.WithAlpha(0.9f * f), 10);
                        break;
                    case PSplash:  // a shot flattened against what it hit: a squashed disc across its path
                    {
                        V2.Axes(p.Rot, out V2 along, out V2 across);
                        float w = p.Size * (1.2f - 0.4f * f), h = p.Size * 0.35f * f;
                        SetColor(p.Col.WithAlpha(f));
                        Quad(p.Pos + across * w, p.Pos + along * h, p.Pos - across * w, p.Pos - along * h);
                        break;
                    }
                    case PBit: case PHexRing: case PPlusUp:  // a picked-up orb's data burst (OrbLook.cs)
                        DrawOrbPart(p, f);
                        break;
                    case PFrag:  // a grenade's shrapnel streak (SoldierLooks.cs)
                        DrawFragPart(p, f);
                        break;
                    case PMuzzle: case PCasing: case PSnap:  // the Revolver's flash and brass, the bow's ring (GunBowLooks.cs)
                        DrawGunPart(p, f);
                        break;
                    case PBowCrescent: case PBowFan: case PBowWind: case PBowGust: case PBowStick: case PBowImpact:  // the Mender's Bow's Focus and Scatter (BowFocusLook.cs)
                        DrawBowFocusPart(p, f);
                        break;
                    case PBrass: case PStreak: case PPool:  // the Twin Rifle's bouncing brass, first-frame streak, light on the floor (TwinRifleFx.cs)
                        DrawTwinPart(p, f);
                        break;
                    case PGlFire: case PGlPuff:  // the Grenade Launcher's fireballs and smoke-shell puffs (GrenadeLauncherLook.cs)
                        DrawGlPart(p, f);
                        break;
                    case PTumble:  // a ricochet spinning off: a short bar that turns as it flies
                    {
                        V2 d = V2.FromAngle(p.Rot) * (p.Size * 1.6f);
                        Line(p.Pos - d, p.Pos + d, p.Size * 1.1f, p.Col.WithAlpha(f));
                        break;
                    }
                    default:
                        Circle(p.Pos, p.Size * (0.5f + 0.5f * f), p.Col.WithAlpha(f), 6);
                        break;
                }
            }
        }

        /// The additive pass: halos that add light. Called between two GL batches with the additive material set.
        void DrawGlowPass()
        {
            DrawLightHaze();  // the dynamic lights' haze over the bodies (LightingLook.cs)
            foreach (var b in game.Bullets.Values)
                ShotGlow(b);  // ShotLooks.cs
            DrawFumaGlow();  // the Fuma Shuriken's trails and halos (FumaLook.cs)
            bool crowded = partN > PartCalm;
            for (int i = 0; i < partN; i++)
            {
                var p = parts[i];
                float f = p.Life / p.Max;
                if (p.Kind == PDust || p.Kind == PBrass || p.Kind == PGlPuff)
                    continue;
                if (p.Kind == PPool || p.Kind == PStreak)
                {
                    GlowDisc(p.Pos, p.Kind == PPool ? p.Size : 10f, p.Col, (p.Kind == PPool ? 0.45f : 0.3f) * f);  // (TwinRifleFx.cs)
                    continue;
                }
                if (p.Kind == PFade || p.Kind == PSplash || p.Kind == PTumble)
                {
                    GlowDisc(p.Pos, p.Size * 4f, p.Col, 0.45f * f);
                    continue;
                }
                if (p.Kind == PSpark)
                    GlowDisc(p.Pos, p.Size * 6f, p.Col, 0.55f * f);
                else if (p.Kind == PShard)
                {
                    if (!crowded)  // (faint under the sparks; left out while the pool is busy)
                        GlowDisc(p.Pos, p.Size * 1.4f, p.Col, 0.3f * f);
                }
                else
                    GlowDisc(p.Pos, p.Size * (1.6f - f), p.Kind == PFlash ? Rgba.White : p.Col, 0.5f * f);
            }
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.Boom && !HammerBoomDrawnByMelee(fx) && !IsLeapBoom(fx))
                {
                    float f = fx.Dur > 0f ? Mathf.Clamp01(fx.T / fx.Dur) : 1f;
                    if (IsGlBoom(fx))
                        GlowDisc(fx.Pos, fx.Radius * 0.8f * (1f + f), FragOrange, 0.28f * (1f - f));  // softer: its fireballs carry the light (GrenadeLauncherLook.cs)
                    else
                        GlowDisc(fx.Pos, fx.Radius * (1f + f), IsGrenadeBoom(fx) || IsFlakBoom(fx) ? FragOrange : fx.Color, 0.55f * (1f - f));
                }
            DrawWeatherGlow();  // Ambience.cs
            DrawHammerGlow();   // the Storm Hammer: slam flash, ring, throw release (StormHammerLook.cs)
            DrawMeleeGlow();    // melee swings' energy (MeleeLooks.cs)
            DrawKatanaGlow();   // the Katana's hops and stance switch (KatanaLook.cs)
            DrawPerfectParryGlow();  // a perfect parry's burst (ParryLook.cs)
            DrawLanceGlow();    // the Lance's hits and Impacts (LanceLook.cs)
            DrawChainLightGlow();  // Chain Light's radiance (ChainLightLooks.cs)
            DrawRadiantStardust();  // the Radiant Orb's trail (RadiantOrbLooks.cs)
            DrawArcLightningGlow();  // Arc Lightning's lock-on chain (ArcLockLook.cs)
            DrawWindClashGlow();  // Wind Clash's waves and Tempest (WindClashLook.cs)
            DrawStaticShockGlow();  // Static Shock's cone, circuit, Overcharge and Arc Cannon (StaticShockLook.cs)
            DrawBloodBlastGlow();  // the Blood Blast's Cleave, Tide and returning blood (BloodBlastLook.cs)
            DrawFrostLanceGlow();  // the Frost Lance's head, slams, shatters and Absolute Zero (FrostLanceLook.cs)
            DrawWarhornGlow();  // Warhorn Volley's horn, burning arrowheads and fires (WarhornVolleyLook.cs)
            DrawOrbitalGlow();  // Orbital Laser's lock-on, beam and crater (OrbitalLaserLook.cs)
            DrawDragonsDogmaGlow();  // Dragon's Dogma's light (DragonsDogmaLook.cs)
            DrawRodGlow();    // Lightning Rod's charge and bolts (LightningRodLook.cs)
            DrawLivingBoltGlow();  // Living Bolt's charge, dashes and residue (LivingBoltLook.cs)
            DrawVgGlow();     // the Vowguard's blade, gem and shield core (VowguardLook.cs)
            DrawLightGlow();  // the Lightbringer's Sundial, Sunrise crescents, Spear, ward plates and marks (LightbringerFxLook.cs)
            foreach (var e in game.Enemies.Values)
                if (e.Def.IsBoss && !game.IsSpawning(e))
                    GlowDisc(e.Disp, e.Radius * 2.6f, e.Def.Color, 0.22f + 0.05f * Mathf.Sin(game.AnimT * 3f));
            DrawWallSpill();  // the walls' and pillars' light on the floor (WallSpillLook.cs)
            DrawOfficerGlow();  // the Warden's officers: halo and lit kit (OfficerGlowLook.cs)
            DrawEnemyPresenceGlow();  // a soft glow round regular enemies (EnemyPresenceLook.cs)
            foreach (var p in game.Players.Values)
                if (!p.Downed)
                    GlowPlayer(p);
        }

        /// A player's glow on the floor around them, in their role's colour: a wide soft halo breathing slowly and a
        /// tighter one hugging the body. Fainter while they're hard to see (Vanish, i-frame flicker).
        void GlowPlayer(Player p)
        {
            float r = p.Def.BodyR, breath = 0.92f + 0.08f * Mathf.Sin(game.AnimT * 2f + p.Id * 1.7f);
            float vis = p.VanishT > 0f ? (p.Id == game.MyId ? 0.5f : 0.2f) : 1f;
            if (Game.FpOn(p) && p.FpAge >= 0.45f)
            {
                GlowDisc(p.Disp, r * 4f, Game.FpRed, 0.1f * breath);  // Forbidden Path's beast: a faint red haze, not the role's glow over its black (ForbiddenPathLook.cs)
                return;
            }
            GlowDisc(p.Disp, r * 5.2f, p.Def.Color, 0.3f * breath * vis);
            GlowDisc(p.Disp, r * 2.4f, p.Def.Color, 0.22f * vis);
        }

        /// A soft spot of light: bright in the middle, gone at the rim. The rim vertex comes first in each triangle,
        /// so the preview tool (which colours a triangle by its first vertex) leaves glow out rather than drawing discs.
        void GlowDisc(V2 c, float r, Rgba col, float a)
        {
            if (a <= 0.01f || r <= 0f)
                return;
            Vector3 s0 = Px(c), s1 = Px(c + new V2(r, 0f));
            int segs = Mathf.Abs(s1.x - s0.x) + Mathf.Abs(s1.y - s0.y) < 28f ? 6 : 8;  // a small glow's rim is clear anyway: 6 sides read the same (a crowd's sparks and shots)
            var mid = col.WithAlpha(a * 0.38f);
            var rim = col.WithAlpha(0f);
            var core = col.WithAlpha(a);
            for (int i = 0; i < segs; i++)
            {
                V2 d0 = V2.FromAngle(MathX.Tau * i / segs), d1 = V2.FromAngle(MathX.Tau * (i + 1) / segs);
                VTri(c + d0 * r, rim, c + d1 * r, rim, c + d1 * (r * 0.3f), mid);
                VTri(c + d0 * r, rim, c + d1 * (r * 0.3f), mid, c + d0 * (r * 0.3f), mid);
                VTri(c + d0 * (r * 0.3f), mid, c + d1 * (r * 0.3f), mid, c, core);
            }
        }

        /// A disc fading from `inner` at its centre to `outer` at its rim (scorch marks, corner tints).
        void FanGradient(V2 c, float r, Rgba inner, Rgba outer, int segs, float start = 0f, float sweep = MathX.Tau)
        {
            for (int i = 0; i < segs; i++)
            {
                V2 d0 = V2.FromAngle(start + sweep * i / segs), d1 = V2.FromAngle(start + sweep * (i + 1) / segs);
                VTri(c + d0 * r, outer, c + d1 * r, outer, c, inner);
            }
        }

        /// A triangle with its own colour at each corner (world coordinates, no clipping).
        void VTri(V2 a, Rgba ca, V2 b, Rgba cb, V2 c, Rgba cc)
        {
            if (glClipOn)
            {
                ClippedVTri(a, ca, b, cb, c, cc);  // (WorldRenderer.cs: inside a clipped preview, like Tri)
                return;
            }
            Vector3 pa = Px(a), pb = Px(b), pc = Px(c);
            if (OffScreen(pa, pb, pc))
                return;
            SetColor(ca); Vtx(pa);
            SetColor(cb); Vtx(pb);
            SetColor(cc); Vtx(pc);
        }

        /// The grid, rippling where a big blast went off (DrawWorld draws it flat when nothing's rippling).
        V2 Warp(float x, float y)
        {
            V2 p = new V2(x, y), off = V2.Zero;
            foreach (var r in ripples)
            {
                V2 d = p - r.Pos;
                float dist = d.Length, front = r.T * 700f, w = 70f;
                if (dist < 1f || Mathf.Abs(dist - front) >= w)
                    continue;
                float k = Mathf.Cos((dist - front) / w * MathX.Pi / 2f) * (1f - r.T / (r.Life > 0f ? r.Life : RippleLife)) * 10f * r.Power;
                off += d / dist * k;
            }
            return p + off;
        }

        /// Over the whole arena, last: a tint for the path or boss, and a faint red in the corners when you're hurt or
        /// low. (There's no edge vignette: the owner had it removed.)
        void DrawMood(float aw, float ah)
        {
            if (!Juicy)
                return;
            // The tint: the path you're on, or the boss you're fighting.
            Rgba tint = new Rgba(0.24f, 0.16f, 0.47f, 0.05f);
            Enemy boss = null;
            foreach (var e in game.Enemies.Values)
                if (e.Def.IsBoss) { boss = e; break; }
            if (boss != null)
            {
                tint = boss.Def.Color.WithAlpha(0.045f);
            }
            else if (game.OnPath && game.CurrentPath != PathId.None && Paths.Get(game.CurrentPath) != null)
                tint = Paths.Get(game.CurrentPath).Accent.WithAlpha(0.05f);
            FillRect(0f, 0f, aw, ah, tint);
            // Hurt, or below 30%: a faint red in the four corners only.
            var me = game.Me;
            float red = Mathf.Max(hurtTint * 0.28f, LowHealthRed(me));  // below 30%: a heartbeat (JuiceMoments.cs)
            if (red > 0.005f)
            {
                float R = ah * 0.32f;
                var hot = new Rgba(0.86f, 0.12f, 0.2f, red);
                var none = hot.WithAlpha(0f);
                FanGradient(new V2(0f, 0f), R, hot, none, 8, 0f, MathX.Pi / 2f);
                FanGradient(new V2(aw, 0f), R, hot, none, 8, MathX.Pi / 2f, MathX.Pi / 2f);
                FanGradient(new V2(aw, ah), R, hot, none, 8, MathX.Pi, MathX.Pi / 2f);
                FanGradient(new V2(0f, ah), R, hot, none, 8, MathX.Pi * 1.5f, MathX.Pi / 2f);
            }
        }

        /// A quad with a colour per corner; c and d (the clear side) lead each triangle, for the preview tool's sake.
        void VQuad(V2 a, Rgba ca, V2 b, Rgba cb, V2 c, Rgba cc, V2 d, Rgba cd)
        {
            VTri(c, cc, a, ca, b, cb);
            VTri(d, cd, a, ca, c, cc);
        }

        // --- Hit-pause: the last frame's triangles, replayed -----------------------------------------------------------

        Material glowMaterial;
        static Color glCur;
        bool recording, heldFrame;
        readonly List<Vector3> recVerts = new List<Vector3>();
        readonly List<Color> recCols = new List<Color>();
        readonly List<int> recGlowFrom = new List<int>(), recGlowTo = new List<int>();

        void Vtx(Vector3 v)
        {
            if (recording)
            {
                recVerts.Add(v);
                recCols.Add(glCur);
            }
            GL.Vertex(v);
        }

        Material GlowMaterial()
        {
            if (glowMaterial == null)
            {
                glowMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                glowMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                glowMaterial.SetInt("_DstBlend", (int)BlendMode.One);  // adds light
                glowMaterial.SetInt("_Cull", (int)CullMode.Off);
                glowMaterial.SetInt("_ZWrite", 0);
                glowMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            }
            return glowMaterial;
        }

        /// DrawWorld, first thing: while the picture holds, draw last frame again and say so (DrawWorld then stops).
        bool ReplayHeldFrame()
        {
            if (!Juicy || holdT <= 0f || !heldFrame || recVerts.Count == 0)
            {
                recording = Juicy && !fxReduced;
                if (recording)
                {
                    recVerts.Clear(); recCols.Clear(); recGlowFrom.Clear(); recGlowTo.Clear();
                }
                return false;
            }
            recording = false;
            int glow = 0;
            for (int i = 0; i < recVerts.Count; i++)
            {
                if (glow < recGlowFrom.Count && i == recGlowFrom[glow])
                    SwitchToGlow(true);
                if (glow < recGlowTo.Count && i == recGlowTo[glow])
                {
                    SwitchToGlow(false);
                    glow++;
                }
                GL.Color(recCols[i]);
                GL.Vertex(recVerts[i]);
            }
            return true;
        }

        /// Ends the current GL batch and starts another with the glow material (or back to the normal one).
        void SwitchToGlow(bool on)
        {
            GL.End();
            (on ? GlowMaterial() : glMaterial)?.SetPass(0);  // (null only in tools that draw without Unity)
            GL.Begin(GL.TRIANGLES);
        }

        /// DrawWorld, after the arena's contents: the glow pass, recorded with the frame so a held frame replays it too.
        void GlowPass()
        {
            if (!Juicy)
                return;
            if (recording) recGlowFrom.Add(recVerts.Count);
            SwitchToGlow(true);
            DrawGlowPass();
            SwitchToGlow(false);
            if (recording) recGlowTo.Add(recVerts.Count);
        }

        void EndRecording()
        {
            if (recording)
                heldFrame = true;
            recording = false;
        }

        // --- On the HUD (IMGUI): damage numbers, the boss's entrance -----------------------------------------------------

        void DrawJuiceHud()
        {
            if (!Juicy)
                return;
            DrawGlHud();  // the Grenade Launcher's drum names, dry clicks and SLOWED tags (GrenadeLauncherLook.cs)
            DrawBowFocusHud();  // FOCUS! over a Focus shot (BowFocusLook.cs)
            foreach (var n in numbers)
            {
                float f = n.T / NumberLife, pop = Mathf.Min(1f, n.T * 12f);
                int size = Mathf.RoundToInt((n.Big ? 17f : 12f) * (0.6f + 0.4f * pop));
                V2 at = ToUi(n.Pos);
                var col = n.Tag != null ? new Color(1f, 0.65f, 0.28f, 1f - f * f) : n.Big ? new Color(1f, 0.84f, 0.35f, 1f - f * f) : new Color(0.92f, 0.94f, 1f, 1f - f * f);
                Ui.Shadowed(new Rect(at.X - 40f, at.Y - 10f, 80f, 20f), n.Value.ToString(), size, col);
                if (n.Tag != null)  // an Impact's tag over its number
                    Ui.Shadowed(new Rect(at.X - 40f, at.Y - 24f, 80f, 14f), n.Tag, Mathf.RoundToInt(10f * (0.6f + 0.4f * pop)), col);
            }
            if (introT > 0f)
            {
                float f = 1f - introT / IntroTime, inn = Mathf.Min(1f, f * 5f), outT = Mathf.Min(1f, introT * 2.5f), k = Mathf.Min(inn, outT);
                if (!fxReduced)
                    Ui.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.4f * k));
                Ui.Fill(new Rect(0f, 0f, W, 64f * k), new Color(0f, 0f, 0f, 1f));
                Ui.Fill(new Rect(0f, H - 64f * k, W, 64f * k), new Color(0f, 0f, 0f, 1f));
                var old = GUI.color;
                GUI.color = old.ScaleAlpha(k);  // fades the title's shadow too
                Ui.Title(new Rect(0f, H / 2f - 64f, W, 72f), introName, 58, new Color(1f, 0.42f, 0.55f, 1f));
                Ui.Label(new Rect(0f, H / 2f + 12f, W, 26f), "WAVE " + game.ShownWave, 17, new Color(0.9f, 0.93f, 0.97f, 0.9f), TextAnchor.MiddleCenter, true);
                GUI.color = old;
            }
            DrawWardenIntroBand();  // WardenIntro.cs
            DrawMultiKill();  // JuiceMoments.cs
        }
    }
}
