using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Telling what an ability is doing, for every class:
    ///   its name pops up over whoever cast it (FxKind.Callout, sent by HostAbility), in their role's colour, gold for an
    ///     ultimate. On by default; Settings > Visual effects > Ability names;
    ///   each timed buff shows a shrinking arc under the player, in the buff's colour (DrawBuffTimers);
    ///   enemies a taunt has turned get an eye over them, watching the taunter (DrawTauntMarks);
    ///   an ability that reaches an ally sends a beam of light to them (FxKind.Link);
    ///   and a few buffs' own looks (Arcane Surge, Blade Tempest, Alpha Call's frenzy, Time Rewind's clock).
    public partial class ArenaApp
    {
        bool fxCallouts = true;

        /// On the HUD (IMGUI), over the arena: each fresh cast's name rising off its caster and fading out. Two casts by
        /// the same player at once stack rather than overlap.
        void DrawAbilityCallouts()
        {
            if (!fxCallouts || game == null)
                return;
            for (int i = 0; i < game.Effects.Count; i++)
            {
                var fx = game.Effects[i];
                if (fx.Kind != FxKind.Callout)
                    continue;
                Player p;
                if (!game.Players.TryGetValue((int)fx.Pos2.X, out p) || LaunchHides(p))
                    continue;
                int id = (int)fx.Pos2.Y;
                if (id <= 0 || id >= Upgrades.Abilities.Length)
                    continue;
                var def = Upgrades.Ability((AbilityId)id);
                bool ult = def.Choice == AbilityDef.Ult;
                float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur));
                float pop = Mathf.Min(1f, fx.T * 10f), fade = f < 0.65f ? 1f : 1f - (f - 0.65f) / 0.35f;
                int stack = 0;  // a newer cast by the same player sits above an older one
                for (int k = i + 1; k < game.Effects.Count; k++)
                {
                    var o = game.Effects[k];
                    if (o.Kind == FxKind.Callout && (int)o.Pos2.X == p.Id)
                        stack++;
                }
                V2 at = ToUi(p.Disp);  // HUD coordinates; it starts just above their name tag (Hud.cs) and rises
                at.Y -= p.Def.BodyR * WorldToUi + 42f + 16f * f + 16f * stack;
                Color c = ult ? new Color(1f, 0.84f, 0.35f) : Color.Lerp(ToColor(p.Def.Color), Color.white, 0.45f);
                c.a = fade;
                int size = Mathf.RoundToInt((ult ? 15f : 12f) * (0.7f + 0.3f * pop));
                Ui.Shadowed(new Rect(at.X - 90f, at.Y - 10f, 180f, 20f), ult ? def.Name.ToUpperInvariant() : def.Name, size, c);
            }
        }

        // --- Buff timers ----------------------------------------------------------------------------------------------

        /// Every timed buff a player can have (all replicated), and its colour. Crimson Pact draws its own countdown.
        static readonly KeyValuePair<Func<Player, float>, Rgba>[] Buffs =
        {
            Buff(p => p.BarrierT, new Rgba(0.5f, 0.8f, 1f)),       // Barrier
            Buff(p => p.TauntT, new Rgba(1f, 0.45f, 0.3f)),        // Roar
            Buff(p => p.OverdriveT, new Rgba(1f, 0.4f, 0.3f)),     // Overdrive
            Buff(p => p.LinkT, new Rgba(0.6f, 1f, 0.9f)),          // Soul Link
            Buff(p => p.AegisT, new Rgba(0.8f, 0.5f, 1f)),         // Arcane Aegis
            Buff(p => p.SurgeT, new Rgba(0.75f, 0.45f, 1f)),       // Arcane Surge
            Buff(p => p.FrenzyT, new Rgba(1f, 0.5f, 0.3f)),        // Alpha Call
            Buff(p => p.WallT, new Rgba(0.55f, 0.9f, 1f)),         // Shield Wall
            Buff(p => p.UnbreakT, new Rgba(1f, 0.85f, 0.5f)),      // Unbreakable
            Buff(p => p.FuryT, new Rgba(1f, 0.3f, 0.2f)),          // Battle Fury
            Buff(p => p.WhirlT, new Rgba(0.8f, 0.85f, 0.9f)),      // Whirlwind
            Buff(p => p.IronT, new Rgba(0.75f, 0.78f, 0.85f)),     // Iron Body
            Buff(p => p.VanishT, new Rgba(0.6f, 0.5f, 0.85f)),     // Vanish
            Buff(p => p.RushT, new Rgba(0.85f, 0.12f, 0.2f)),      // Frenzy Pact
            Buff(p => p.WardT, new Rgba(0.95f, 0.25f, 0.35f)),     // Martyr's ward
            Buff(p => p.UltT, new Rgba(1f, 0.84f, 0.35f)),         // any ultimate
        };

        static KeyValuePair<Func<Player, float>, Rgba> Buff(Func<Player, float> left, Rgba c) => new KeyValuePair<Func<Player, float>, Rgba>(left, c);

        /// The most each buff's timer has read since it went up (player id * 64 + buff), so its arc shows the share left
        /// whatever its length (mastery, upgrades) without the length being sent.
        readonly Dictionary<int, float> buffFull = new Dictionary<int, float>();

        /// Under a player: a short arc for each buff they have up, stacked outward, shrinking toward the middle as it runs out.
        void DrawBuffTimers(Player p, float t)
        {
            int row = 0;
            for (int i = 0; i < Buffs.Length; i++)
            {
                float left = Buffs[i].Key(p);
                int key = p.Id * 64 + i;
                float full;
                if (left <= 0f)
                {
                    buffFull.Remove(key);
                    continue;
                }
                if (!buffFull.TryGetValue(key, out full) || left > full + 0.05f)
                    buffFull[key] = full = left;  // cast (or recast): this is its full length
                float share = Mathf.Clamp01(left / Mathf.Max(0.01f, full)), span = 2.2f;
                float rad = p.Def.BodyR + 17f + 5f * row++;
                var c = Buffs[i].Value;
                bool ending = left < 1f && MathX.Fmod(t, 0.2f) < 0.1f;  // blinks in its last second
                Arc(p.Disp, rad, 3f, new Rgba(0f, 0f, 0f, 0.35f), 16, MathX.Pi / 2f - span / 2f, span);
                Arc(p.Disp, rad, 2.2f, c.WithAlpha(ending ? 0.35f : 0.9f), 16, MathX.Pi / 2f - span * share / 2f, span * share);
            }
        }

        // --- Taunts ---------------------------------------------------------------------------------------------------

        /// The player every enemy is going for right now (Roar, Last Stand, Crimson Pact, Blood Rite), or null.
        Player Taunter()
        {
            Player best = null;
            foreach (var p in game.Players.Values)
            {
                if (p.Downed)
                    continue;
                var ult = p.UltT > 0f ? p.Slots[Upgrades.UltSlot].Id : AbilityId.None;
                if (p.TauntT > 0f || p.PactT > 0f || ult == AbilityId.LastStand || ult == AbilityId.BloodRite)
                    best = p;
            }
            return best;
        }

        /// Over the enemies: while a taunt is up, an eye over each one, its pupil watching the player they're all coming
        /// for (blood-red for a Hemomancer, ember for anyone else). Enemies that turn up later aren't taunted, but are marked.
        void DrawTauntMarks(float t)
        {
            var taunter = Taunter();
            if (taunter == null)
                return;
            var c = taunter.Kit == Kit.Hemomancer ? new Rgba(0.85f, 0.1f, 0.18f) : new Rgba(1f, 0.45f, 0.25f);
            var dark = taunter.Kit == Kit.Hemomancer ? new Rgba(0.35f, 0.03f, 0.08f) : new Rgba(0.3f, 0.1f, 0.05f);
            foreach (var e in game.Enemies.Values)
            {
                if (game.IsSpawning(e) || Game.DevourUnder(e))
                    continue;
                V2 at = e.Disp + new V2(0f, -e.Radius - 12f);
                float blink = 0.75f + 0.25f * Mathf.Sin(t * 8f + e.Id);
                V2 look = (taunter.Disp - at).Normalized();
                Circle(at, 5.5f, dark.WithAlpha(0.9f), 12);
                Arc(at, 5.5f, 1.5f, c.WithAlpha(blink), 12);
                Circle(at + look * 2f, 2.2f, c.Mix(Rgba.White, 0.25f).WithAlpha(blink), 8);
            }
        }

        // --- Buff looks -----------------------------------------------------------------------------------------------

        /// Arcane Surge: a violet glow, a bright ring and sparks crackling round it.
        void DrawArcaneSurge(Player p, float t)
        {
            var c = new Rgba(0.75f, 0.45f, 1f);
            float r = p.Def.BodyR + 10f;
            Circle(p.Disp, r + 3f * Mathf.Sin(t * 12f), c.WithAlpha(0.22f), 32);
            Arc(p.Disp, r, 2f, c.WithAlpha(0.8f), 32);
            for (int k = 0; k < 4; k++)  // little bolts jumping off the ring, a new spot every tenth of a second
            {
                float a = ((float)Math.Floor(t * 10f) * 2.3f + k * 1.7f) % MathX.Tau;
                V2 d = V2.FromAngle(a), side = d.Orthogonal();
                V2 a0 = p.Disp + d * r, a1 = a0 + d * 5f + side * 3f, a2 = a1 + d * 5f - side * 3f;
                Line(a0, a1, 1.5f, new Rgba(0.9f, 0.75f, 1f, 0.9f));
                Line(a1, a2, 1.5f, new Rgba(0.9f, 0.75f, 1f, 0.9f));
            }
        }

        /// Blade Tempest: three spectral swords wheeling round the knight, ready to fly with each swing.
        void DrawBladeTempest(Player p, float r, float t)
        {
            var c = Defs.BulletColor(Defs.KnightWave);
            for (int k = 0; k < 3; k++)
            {
                float a = t * 3f + MathX.Tau * k / 3f;
                V2 d = V2.FromAngle(a), along = d.Orthogonal(), mid = p.Disp + d * (r + 22f);
                Line(mid - along * 9f, mid + along * 7f, 6f, c.WithAlpha(0.25f));
                Line(mid - along * 9f, mid + along * 7f, 2.5f, c.Mix(Rgba.White, 0.4f).WithAlpha(0.9f));
                SetColor(c.Mix(Rgba.White, 0.4f).WithAlpha(0.9f));
                Tri(mid + along * 12f, mid + along * 6f + d * 3f, mid + along * 6f - d * 3f);
                Line(mid - along * 6f + d * 4f, mid - along * 6f - d * 4f, 2f, c.WithAlpha(0.9f));  // the guard
            }
        }

        /// Alpha Call: each of the caster's minions burns with a flickering ember ring and streaks behind it.
        void DrawFrenzyMinions(float t)
        {
            foreach (var p in game.Players.Values)
            {
                if (p.FrenzyT <= 0f || p.Downed)
                    continue;
                foreach (var m in game.Minions.Values)
                {
                    if (m.Owner != p.Id)
                        continue;
                    float rad = m.Def.Radius * m.Scale + 4f + 1.5f * Mathf.Sin(t * 18f + m.Id);
                    Arc(m.Disp, rad, 2f, new Rgba(1f, 0.45f, 0.2f, 0.85f), 16);
                    for (int k = 0; k < 3; k++)
                    {
                        V2 d = V2.FromAngle(m.Aim + MathX.Pi + (k - 1) * 0.5f);
                        float f = MathX.Fmod(t * 5f + k * 0.33f + m.Id * 0.1f, 1f);
                        Line(m.Disp + d * (rad + 6f * f), m.Disp + d * (rad + 4f + 6f * f), 1.5f, new Rgba(1f, 0.55f, 0.25f, 1f - f));
                    }
                }
            }
        }

        // --- Effects --------------------------------------------------------------------------------------------------

        /// A beam of light from a caster to an ally their ability reached (FxKind.Link): it shoots out, the target
        /// flashes when it lands, then it fades.
        void DrawLinkFx(Effect fx, float f)
        {
            V2 a0 = fx.Pos, along = fx.Pos2 - fx.Pos;
            if (along.Length < 1f)
                return;
            float reach = Mathf.Min(1f, f / 0.35f), fade = f < 0.35f ? 1f : 1f - (f - 0.35f) / 0.65f;
            V2 head = a0 + along * reach;
            Line(a0, head, 9f, fx.Color.WithAlpha(0.18f * fade));
            Line(a0, head, 3f, fx.Color.WithAlpha(0.75f * fade));
            Line(a0, head, 1.2f, Rgba.White.WithAlpha(0.8f * fade));
            Circle(head, 5f * fade + 1f, fx.Color.Mix(Rgba.White, 0.5f).WithAlpha(fade), 12);
            if (reach >= 1f)
                Arc(fx.Pos2, 14f + 16f * (f - 0.35f), 2.5f, fx.Color.WithAlpha(fade), 20);
        }

        /// Time Rewind on a player (FxKind.Rewind): a clock face round them, its hand sweeping backwards twice, a trail
        /// behind it.
        void DrawRewindFx(Effect fx, float f)
        {
            Player p;
            V2 at = game.Players.TryGetValue((int)fx.Pos2.X, out p) ? p.Disp : fx.Pos;
            float r = fx.Radius, fade = f < 0.75f ? 1f : (1f - f) / 0.25f;
            var c = fx.Color;
            Arc(at, r, 2f, c.WithAlpha(0.8f * fade), 32);
            for (int k = 0; k < 12; k++)
            {
                V2 d = V2.FromAngle(k * MathX.Tau / 12f);
                Line(at + d * (r - (k % 3 == 0 ? 6f : 3f)), at + d * r, 1.5f, c.WithAlpha(0.8f * fade));
            }
            float hand = -MathX.Pi / 2f - f * MathX.Tau * 2f;  // anticlockwise: back in time
            Arc(at, r - 4f, 4f, c.WithAlpha(0.3f * fade), 16, hand, 1.4f);
            Line(at, at + V2.FromAngle(hand) * (r - 3f), 2.5f, Rgba.White.WithAlpha(0.9f * fade));
            Line(at, at + V2.FromAngle(hand * 0.08f - MathX.Pi / 2f) * (r * 0.55f), 3f, c.Mix(Rgba.White, 0.3f).WithAlpha(0.9f * fade));
        }
    }
}
