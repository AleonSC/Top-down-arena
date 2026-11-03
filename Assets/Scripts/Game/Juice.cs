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
    ///   floor       scorch marks that fade, and the grid ripples from big blasts
    ///   mood        a tint for the path or boss; a faint red tint in the corners when hurt or low
    ///   level up    a gold ring and rising sparks (bigger for a legendary); a boss's entrance card
    /// Settings (Menus.cs): Visual effects on/off (off draws exactly as before), Screen shake off/low/full, Damage numbers,
    /// Ability names (AbilityLooks.cs) and Reduced effects (no shake, no hit-pause, no flashes).
    public partial class ArenaApp
    {
        // --- Settings --------------------------------------------------------------------------------------------------
        bool fxOn = true, fxNumbers = true, fxReduced;
        int fxShake = 1;  // 0 off, 1 low (the default), 2 full

        void LoadFxSettings()
        {
            fxOn = PlayerPrefs.GetInt("fx_on", 1) == 1;
            fxShake = Mathf.Clamp(PlayerPrefs.GetInt("fx_shake", 1), 0, 2);
            fxNumbers = PlayerPrefs.GetInt("fx_numbers", 1) == 1;
            fxReduced = PlayerPrefs.GetInt("fx_reduced", 0) == 1;
            fxCallouts = PlayerPrefs.GetInt("fx_callouts", 1) == 1;  // AbilityLooks.cs
        }

        void SaveFxSettings()
        {
            PlayerPrefs.SetInt("fx_on", fxOn ? 1 : 0);
            PlayerPrefs.SetInt("fx_shake", fxShake);
            PlayerPrefs.SetInt("fx_numbers", fxNumbers ? 1 : 0);
            PlayerPrefs.SetInt("fx_reduced", fxReduced ? 1 : 0);
            PlayerPrefs.SetInt("fx_callouts", fxCallouts ? 1 : 0);
            PlayerPrefs.Save();
            if (!fxOn)
                ClearJuice();
        }

        /// Effects are drawn for the match on screen only (not the lobby's preview, which borrows DrawArenaContents).
        bool Juicy => fxOn && juiceGame == game;

        // --- What was on screen last frame ---------------------------------------------------------------------------
        class EnemySeen
        {
            public V2 Pos;
            public float Hp, Max, R, Rot, Squash, Flash, Dmg, DmgT;
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

        const byte PSpark = 0, PShard = 1, PRing = 2, PFlash = 3, PDust = 4, PFade = 5, PSplash = 6, PTumble = 7;  // 8-10: GunBowLooks.cs

        struct Decal { public V2 Pos; public float R, Age; }
        struct Ripple { public V2 Pos; public float T, Power; }
        struct Number { public V2 Pos; public float T; public int Value; public bool Big; }
        struct Burst { public float At; public V2 Pos; public Rgba Col; public float R; }

        Game juiceGame;
        int juiceFrame;
        readonly Dictionary<int, EnemySeen> seenEnemies = new Dictionary<int, EnemySeen>();
        readonly Dictionary<int, ShotSeen> seenShots = new Dictionary<int, ShotSeen>();
        readonly Stack<EnemySeen> enemyPool = new Stack<EnemySeen>();
        readonly Stack<ShotSeen> shotPool = new Stack<ShotSeen>();
        readonly List<int> goneIds = new List<int>();
        readonly List<V2> newBooms = new List<V2>();
        readonly Part[] parts = new Part[1800];
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
        bool BossIntroHoldsHud => introT > 0.35f;

        /// The card has the HUD off, from when it comes in until the HUD is fully back (the slim boss bars stay away meanwhile).
        bool introHidHud;
        float shakeX, shakeY;  // added to every world vertex (Px) while the world is drawn

        public const float ScorchLife = 8f, RippleLife = 1.2f, NumberLife = 0.75f, IntroTime = 3.2f;

        void ClearJuice()
        {
            foreach (var s in seenEnemies.Values) enemyPool.Push(s);
            foreach (var s in seenShots.Values) shotPool.Push(s);
            seenEnemies.Clear();
            seenShots.Clear();
            partN = 0;
            scorches.Clear(); ripples.Clear(); numbers.Clear(); bursts.Clear(); muzzleT.Clear();
            ClearGunBowLooks();  // GunBowLooks.cs
            trauma = holdT = holdGap = hurtTint = introT = 0f;
            myHp = -1f; myLevel = -1; myLegendaries = -1;
            heldFrame = false;
            radiantTrails.Clear();
            ClearAmbience();  // Ambience.cs
        }

        void AddPart(Part p)
        {
            if (partN < parts.Length)
                parts[partN++] = p;
        }

        float Rnd(float a, float b) => a + (float)juiceRng.NextDouble() * (b - a);
        readonly System.Random juiceRng = new System.Random();  // its own rolls: the game's stay untouched

        void Shake(float amount)
        {
            if (fxShake == 0 || fxReduced)
                return;
            trauma = Mathf.Min(1f, trauma + amount);
        }

        /// Hold the picture for a moment (at most every 0.4 s, so fast weapons don't stutter).
        void Hold(float seconds)
        {
            if (fxReduced || holdGap > 0f)
                return;
            holdT = Mathf.Max(holdT, seconds);
            holdGap = 0.4f;
        }

        void Sparks(V2 at, Rgba col, int n, float speed, float life, float size)
        {
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
            if (scorches.Count >= 80)
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
            float t = game.AnimT;
            var me = game.Me;

            // New effects: blasts shake, spark, scorch and ripple.
            newBooms.Clear();
            float boomShake = 0f;  // explosions this frame shake together, up to a cap, rather than piling up
            foreach (var fx in game.Effects)
            {
                if (fx.Juiced)
                    continue;
                fx.Juiced = true;
                if (fx.Kind == FxKind.Repulse && trauma < 0.3f && me != null && me.Pos.DistanceTo(fx.Pos) < 40f)
                    Shake(0.1f);  // your own cannon's kick: very small, and it never builds up (RepulseLook.cs)
                if (fx.Kind != FxKind.Boom)
                    continue;
                newBooms.Add(fx.Pos);  // an enemy's death plays one where it died (DeathFx)
                float near = me != null ? Mathf.Clamp01(1.2f - me.Pos.DistanceTo(fx.Pos) / 900f) : 0.5f;
                boomShake = Mathf.Max(boomShake, Mathf.Min(0.22f, fx.Radius / 450f) * near);
                Sparks(fx.Pos, fx.Color, Mathf.Clamp((int)(fx.Radius / 4f), 4, 40), 160f + fx.Radius * 2f, 0.5f, 2.4f);
                if (fx.Radius >= 80f)
                {
                    Scorch(fx.Pos, fx.Radius * 1.6f);
                    ripples.Add(new Ripple { Pos = fx.Pos, Power = Mathf.Min(2f, fx.Radius / 110f) });
                }
            }

            if (boomShake > 0f && trauma < 0.35f)
                Shake(Mathf.Min(boomShake, 0.35f - trauma));  // explosions alone never shake past a light rumble

            // Enemies: hits (flash, bulge, numbers, big-hit pause), and the ones that are gone (shatter).
            foreach (var e in game.Enemies.Values)
            {
                EnemySeen s;
                bool fresh = !seenEnemies.TryGetValue(e.Id, out s);
                if (fresh)
                {
                    s = enemyPool.Count > 0 ? enemyPool.Pop() : new EnemySeen();
                    s.Hp = e.Hp; s.Squash = s.Flash = s.Dmg = s.DmgT = 0f;
                    seenEnemies[e.Id] = s;
                    if ((e.Def.IsBoss || e.Def.Mini) && e.Type != EnemyType.TwinB && e.Type != EnemyType.DualReaper && (introWave != game.Wave || e.Def.Mini) && game.State == MatchState.Fighting)
                    {
                        introWave = game.Wave;
                        introT = IntroTime;
                        var entry = BestiaryData.For(e.Type);
                        introName = (entry != null ? entry.Name : e.Def.Name).ToUpperInvariant();
                        Shake(0.2f);
                    }
                }
                bool spawning = game.IsSpawning(e);
                float lost = s.Hp - e.Hp;
                if (!fresh && lost > 0.01f && !spawning)
                {
                    s.Squash = 1f;
                    s.Flash = 0.09f;
                    s.Dmg += lost;
                    if (s.DmgT <= 0f)
                        s.DmgT = 0.15f;
                    if (!e.Def.IsBoss && lost >= Mathf.Max(80f, e.MaxHp * 0.35f))
                    {
                        Hold(0.035f);
                        Shake(0.06f);
                    }
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
                    FlushNumber(s);
                if (!s.Spawning)
                    Shatter(s, s.Boss && !bossLeft);
                enemyPool.Push(s);
            }

            // Shots: trails, muzzle flashes, sparks where they land.
            foreach (var b in game.Bullets.Values)
            {
                ShotSeen s;
                if (!seenShots.TryGetValue(b.Id, out s))
                {
                    s = shotPool.Count > 0 ? shotPool.Pop() : new ShotSeen();
                    s.N = 0;
                    seenShots[b.Id] = s;
                    Player owner = null;
                    float cool;
                    bool fromGun = b.Owner > 0 && game.Players.TryGetValue(b.Owner, out owner) && b.Pos.DistanceTo(owner.Pos) < owner.Def.BodyR + 40f;
                    if (fromGun && owner.Weapon.Id == WeaponId.Pistol && b.Kind == Defs.HealerShot)
                        OnRevolverShot(owner, b);  // the Revolver's flash, smoke and brass (GunBowLooks.cs)
                    if (fromGun && (!muzzleT.TryGetValue(b.Owner, out cool) || cool <= juiceClock))
                    {
                        muzzleT[b.Owner] = juiceClock + 0.05f;
                        AddPart(new Part { Kind = PSpark, Pos = b.Pos, Vel = b.Vel.Normalized() * 120f, Life = 0.08f, Max = 0.08f, Size = 4.5f, Col = new Rgba(0.85f, 0.92f, 1f) });
                    }
                }
                else if (b.Vel.Length > 450f)
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
            goneIds.Clear();
            foreach (var kv in seenShots)
                if (kv.Value.Frame != juiceFrame)
                    goneIds.Add(kv.Key);
            foreach (int id in goneIds)
            {
                var s = seenShots[id];
                seenShots.Remove(id);
                BulletEnding(s);
                shotPool.Push(s);
            }

            // You: hurt (a faint red in the corners, a kick), levels and legendaries.
            if (me != null)
            {
                if (myHp >= 0f && me.Hp < myHp - 0.5f && !me.Downed)
                {
                    hurtTint = 1f;
                    Shake(0.18f + Mathf.Min(0.3f, (myHp - me.Hp) / Mathf.Max(1f, me.MaxHp)));
                }
                myHp = me.Hp;
                if (myLevel >= 0 && me.Level > myLevel)
                    LevelBurst(me, false);
                myLevel = me.Level;
                int legendaries = 0;
                foreach (int u in me.Taken)
                    if (u >= 0 && u < Upgrades.All.Length && Upgrades.All[u].Rarity == Rarity.Legendary)
                        legendaries++;
                if (myLegendaries >= 0 && legendaries > myLegendaries)
                    LevelBurst(me, true);
                myLegendaries = legendaries;
            }

            UpdateBowLooks();    // GunBowLooks.cs: the string snapping and the ring of light on a release
            UpdateOrbs();        // Ambience.cs: orb trails, pickup pulses
            UpdateDust(dt);      // dust at your feet
            UpdateWeather(dt);   // the path's weather

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
                p.Pos += p.Vel * dt;
                float drag = (float)Math.Exp(-(p.Kind == PShard ? 2.2f : p.Kind == PDust ? 5f : 3.5f) * dt);
                p.Vel = p.Vel * drag;
                p.Rot += p.Spin * dt;
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
                if (r.T >= RippleLife) ripples.RemoveAt(i); else ripples[i] = r;
            }
            for (int i = numbers.Count - 1; i >= 0; i--)
            {
                var nb = numbers[i];
                nb.T += dt;
                nb.Pos += new V2(0f, -38f * dt);
                if (nb.T >= NumberLife) numbers.RemoveAt(i); else numbers[i] = nb;
            }
            trauma = Mathf.Max(0f, trauma - realDt * 1.8f);
            hurtTint = Mathf.Max(0f, hurtTint - realDt * 2.2f);
            introT = Mathf.Max(0f, introT - realDt);
        }

        // --- Bullet endings: what a shot does as it goes, by why it went ---------------------------------------------------

        public enum Ending : byte { None, RanOut, HitEnemy, Blocked, HitPlayer }

        static readonly Rgba[] PrismColors = { new Rgba(1f, 0.3f, 0.3f), new Rgba(0.3f, 1f, 0.4f), new Rgba(0.35f, 0.5f, 1f) };

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
                        || e.Type == EnemyType.RiotShield && Game.RiotCrack(e) < Game.RiotCracked && Game.RiotState(e) != Game.RiotLow;
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
                if (p.ParryActive && d < p.Def.BodyR + 46f || p.BarrierT > 0f && d < p.BarrierRadius + radius + 10f || p.WallT > 0f && d < p.Def.BodyR + 60f)
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
            var c = Defs.BulletColor(s.Kind);
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
                    // A splash flattened against the enemy, and sparks bouncing back the way it came.
                    AddPart(new Part { Kind = PSplash, Pos = s.Pos, Rot = dir.Angle, Life = 0.14f, Max = 0.14f, Size = r * 2.6f, Col = c });
                    for (int i = 0; i < 5; i++)
                    {
                        float a = dir.Angle + MathX.Pi + Rnd(-0.75f, 0.75f);
                        AddPart(new Part { Kind = PSpark, Pos = s.Pos, Vel = V2.FromAngle(a) * Rnd(90f, 260f), Life = Rnd(0.15f, 0.28f), Max = 0.28f, Size = 1.8f, Col = c });
                    }
                    break;
                case Ending.Blocked:
                {
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
            if (fxNumbers && s.Dmg >= 0.5f)
            {
                if (numbers.Count >= 60)
                    numbers.RemoveAt(0);
                numbers.Add(new Number { Pos = s.Pos + new V2(Rnd(-8f, 8f), -s.R - 6f), Value = Mathf.RoundToInt(s.Dmg), Big = s.Dmg >= Mathf.Max(60f, s.Max * 0.25f) && !s.Boss || s.Dmg >= 200f });
            }
            s.Dmg = 0f;
            s.DmgT = 0f;
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
            return Juicy && !fxReduced && seenEnemies.TryGetValue(e.Id, out s) ? 1f + 0.15f * Mathf.Sin(s.Squash * MathX.Pi) : 1f;
        }

        bool JuiceFlash(Enemy e)
        {
            EnemySeen s;
            return Juicy && !fxReduced && seenEnemies.TryGetValue(e.Id, out s) && s.Flash > 0f;
        }

        // --- Drawing -------------------------------------------------------------------------------------------------

        /// DrawWorld, before anything else: the screen-shake offset for this frame.
        void BeginJuiceFrame()
        {
            shakeX = shakeY = 0f;
            if (!Juicy || trauma <= 0f || devFrozen)
                return;  // (frozen with P: the picture holds still)
            float k = trauma * trauma * (fxShake == 1 ? 0.45f : 1f) * 12f * drawScale, s = Time.unscaledTime;
            shakeX = k * Mathf.Sin(s * 61.3f) * Mathf.Cos(s * 17.1f);
            shakeY = k * Mathf.Sin(s * 53.7f + 1.3f);
        }

        void EndJuiceFrame() => shakeX = shakeY = 0f;

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
                float a = 0.5f * (1f - d.Age / ScorchLife);
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
                        V2 along = V2.FromAngle(p.Rot), across = along.Orthogonal();
                        float w = p.Size * (1.2f - 0.4f * f), h = p.Size * 0.35f * f;
                        SetColor(p.Col.WithAlpha(f));
                        Quad(p.Pos + across * w, p.Pos + along * h, p.Pos - across * w, p.Pos - along * h);
                        break;
                    }
                    case PMuzzle: case PCasing: case PSnap:  // the Revolver's flash and brass, the bow's ring (GunBowLooks.cs)
                        DrawGunPart(p, f);
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
            foreach (var b in game.Bullets.Values)
                ShotGlow(b);  // ShotLooks.cs
            for (int i = 0; i < partN; i++)
            {
                var p = parts[i];
                float f = p.Life / p.Max;
                if (p.Kind == PDust)
                    continue;
                if (p.Kind == PFade || p.Kind == PSplash || p.Kind == PTumble)
                {
                    GlowDisc(p.Pos, p.Size * 4f, p.Col, 0.45f * f);
                    continue;
                }
                if (p.Kind == PSpark)
                    GlowDisc(p.Pos, p.Size * 6f, p.Col, 0.55f * f);
                else if (p.Kind == PShard)
                    GlowDisc(p.Pos, p.Size * 1.4f, p.Col, 0.3f * f);
                else
                    GlowDisc(p.Pos, p.Size * (1.6f - f), p.Kind == PFlash ? Rgba.White : p.Col, 0.5f * f);
            }
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.Boom && !HammerBoomDrawnByMelee(fx))
                {
                    float f = fx.Dur > 0f ? Mathf.Clamp01(fx.T / fx.Dur) : 1f;
                    GlowDisc(fx.Pos, fx.Radius * (1f + f), fx.Color, 0.55f * (1f - f));
                }
            DrawWeatherGlow();  // Ambience.cs
            DrawMeleeGlow();    // melee swings' energy (MeleeLooks.cs)
            DrawChainLightGlow();  // Chain Light's radiance (ChainLightLooks.cs)
            DrawRadiantStardust();  // the Radiant Orb's trail (RadiantOrbLooks.cs)
            DrawArcLightningGlow();  // Arc Lightning's Thunderhead (ArcLightningLooks.cs)
            foreach (var e in game.Enemies.Values)
                if (e.Def.IsBoss && !game.IsSpawning(e))
                    GlowDisc(e.Disp, e.Radius * 2.6f, e.Def.Color, 0.22f + 0.05f * Mathf.Sin(game.AnimT * 3f));
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
            GlowDisc(p.Disp, r * 5.2f, p.Def.Color, 0.3f * breath * vis);
            GlowDisc(p.Disp, r * 2.4f, p.Def.Color, 0.22f * vis);
        }

        /// A soft spot of light: bright in the middle, gone at the rim. The rim vertex comes first in each triangle,
        /// so the preview tool (which colours a triangle by its first vertex) leaves glow out rather than drawing discs.
        void GlowDisc(V2 c, float r, Rgba col, float a)
        {
            if (a <= 0.01f || r <= 0f)
                return;
            const int segs = 8;
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
            SetColor(ca); Vtx(Px(a));
            SetColor(cb); Vtx(Px(b));
            SetColor(cc); Vtx(Px(c));
        }

        /// The grid, rippling where a big blast went off (DrawWorld draws it flat when nothing's rippling).
        bool DrawRippledGrid(float aw, float ah, Rgba grid)
        {
            if (!Juicy || ripples.Count == 0)
                return false;
            const float step = 16f;
            for (float x = 0; x <= aw; x += 64f)
                for (float y = 0; y < ah; y += step)
                    Line(Warp(x, y), Warp(x, y + step), 1f, grid);
            for (float y = 0; y <= ah; y += 64f)
                for (float x = 0; x < aw; x += step)
                    Line(Warp(x, y), Warp(x + step, y), 1f, grid);
            return true;
        }

        V2 Warp(float x, float y)
        {
            V2 p = new V2(x, y), off = V2.Zero;
            foreach (var r in ripples)
            {
                V2 d = p - r.Pos;
                float dist = d.Length, front = r.T * 700f, w = 70f;
                if (dist < 1f || Mathf.Abs(dist - front) >= w)
                    continue;
                float k = Mathf.Cos((dist - front) / w * MathX.Pi / 2f) * (1f - r.T / RippleLife) * 10f * r.Power;
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
            float red = hurtTint * 0.28f;
            if (me != null && !me.Downed && me.Hp < me.MaxHp * 0.3f)
                red = Mathf.Max(red, 0.12f + 0.08f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 4f)), 4f));
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
            foreach (var n in numbers)
            {
                float f = n.T / NumberLife, pop = Mathf.Min(1f, n.T * 12f);
                int size = Mathf.RoundToInt((n.Big ? 17f : 12f) * (0.6f + 0.4f * pop));
                V2 at = n.Pos * WorldToUi;
                var col = n.Big ? new Color(1f, 0.84f, 0.35f, 1f - f * f) : new Color(0.92f, 0.94f, 1f, 1f - f * f);
                Ui.Shadowed(new Rect(at.X - 40f, at.Y - 10f, 80f, 20f), n.Value.ToString(), size, col);
            }
            if (introT > 0f)
            {
                float f = 1f - introT / IntroTime, inn = Mathf.Min(1f, f * 5f), outT = Mathf.Min(1f, introT * 2.5f), k = Mathf.Min(inn, outT);
                if (!fxReduced)
                    Ui.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.4f * k));
                Ui.Fill(new Rect(0f, 0f, W, 64f * k), new Color(0f, 0f, 0f, 1f));
                Ui.Fill(new Rect(0f, H - 64f * k, W, 64f * k), new Color(0f, 0f, 0f, 1f));
                var old = GUI.color;
                GUI.color = new Color(old.r, old.g, old.b, old.a * k);  // fades the title's shadow too
                Ui.Title(new Rect(0f, H / 2f - 64f, W, 72f), introName, 58, new Color(1f, 0.42f, 0.55f, 1f));
                Ui.Label(new Rect(0f, H / 2f + 12f, W, 26f), "WAVE " + game.Wave, 17, new Color(0.9f, 0.93f, 0.97f, 0.9f), TextAnchor.MiddleCenter, true);
                GUI.color = old;
            }
        }
    }
}
