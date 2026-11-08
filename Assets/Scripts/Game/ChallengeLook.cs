using UnityEngine;

namespace Arena
{
    /// Challenge rooms on screen (Core/Challenges.cs): the portal swirling open in a break, the room's own floor and
    /// rune border, the crystal, the zones, the altar, the Gatekeeper and the shadows; on the HUD, the portal's call,
    /// the room's title card, its objective plate and how it ended, and a flash as you warp in and out.
    public partial class ArenaApp
    {
        RoomPhase lookPhase;
        float lookPhaseAt = -10f, warpAt = -10f, crystalFlash;
        float lastCrystalHp;
        bool wasInRoom;
        static bool glShadow;
        ChallengeKind lastRoomKind;  // the last room this screen saw end, how, and on which wave (the pause menu's run card)
        bool lastRoomWon;
        int lastRoomWave;  // SetColor turns everything into a shadow's dark violet (a Mirror Match's shadows)

        /// Each frame (from the HUD): notice the room's phase changing, for the animations.
        void TickChallengeLook()
        {
            float now = Time.unscaledTime;
            if (game.Room != lookPhase)
            {
                lookPhase = game.Room;
                lookPhaseAt = now;
                if (game.Room == RoomPhase.Won || game.Room == RoomPhase.Lost)
                {
                    lastRoomKind = game.RoomKind;
                    lastRoomWon = game.Room == RoomPhase.Won;
                    lastRoomWave = game.Wave;
                }
            }
            if (game.InRoom != wasInRoom)
            {
                wasInRoom = game.InRoom;
                warpAt = now;
                ripples.Add(new Ripple { Pos = new V2(AW / 2f, AH / 2f), Power = 2f });  // Juice.cs
                Shake(0.25f);
            }
            if (game.RoomKind == ChallengeKind.Defend && game.CrystalHp < lastCrystalHp - 0.01f)
                crystalFlash = 1f;
            lastCrystalHp = game.CrystalHp;
            crystalFlash = Mathf.Max(0f, crystalFlash - Time.unscaledDeltaTime * 5f);
        }

        float PhaseAge => Time.unscaledTime - lookPhaseAt;

        // --- The world ----------------------------------------------------------------------------------------------

        /// Under everything: the room's own ground (a tint in its colour, a great rune circle, glyphs round the edge).
        void DrawRoomFloor(float t)
        {
            if (!game.InRoom)
                return;
            var acc = Challenges.Accent(game.RoomKind);
            float k = Mathf.Clamp01((Time.unscaledTime - warpAt) / 0.6f);
            FillRect(0, 0, AW, AH, new Rgba(acc.R * 0.12f, acc.G * 0.12f, acc.B * 0.16f, 0.75f * k));
            V2 mid = new V2(AW / 2f, AH / 2f);
            // The rune circle: two rings and slow-turning ticks between them.
            Arc(mid, 400f, 2f, acc.WithAlpha(0.16f * k), 96);
            Arc(mid, 430f, 1.5f, acc.WithAlpha(0.1f * k), 96);
            for (int i = 0; i < 36; i++)
            {
                float a = MathX.Tau * i / 36f + t * 0.05f;
                float len = i % 3 == 0 ? 22f : 10f;
                Line(mid + V2.FromAngle(a) * 404f, mid + V2.FromAngle(a) * (404f + len), 2f, acc.WithAlpha(0.18f * k));
            }
            for (int i = 0; i < 6; i++)  // six glyphs round the circle
            {
                V2 at = mid + V2.FromAngle(MathX.Tau * i / 6f - t * 0.03f) * 415f;
                Polygon(at, 9f, 4, t * 0.3f + i, acc.WithAlpha(0.25f * k));
            }
            // Glyphs along the walls, a pulse running round them.
            float run = t * 0.4f;
            for (int i = 0; i < 40; i++)
            {
                float f = i / 40f;
                V2 at = EdgePoint(f, 18f);
                float pulse = Mathf.Max(0f, 1f - Mathf.Abs(MathX.Fmod(f - run, 1f) - 0.5f) * 8f);
                Polygon(at, 4f + 3f * pulse, 4, 0.785f, acc.WithAlpha((0.22f + 0.6f * pulse) * k));
            }
            Line(new V2(10f, 10f), new V2(AW - 10f, 10f), 2f, acc.WithAlpha(0.3f * k));
            Line(new V2(10f, AH - 10f), new V2(AW - 10f, AH - 10f), 2f, acc.WithAlpha(0.3f * k));
            Line(new V2(10f, 10f), new V2(10f, AH - 10f), 2f, acc.WithAlpha(0.3f * k));
            Line(new V2(AW - 10f, 10f), new V2(AW - 10f, AH - 10f), 2f, acc.WithAlpha(0.3f * k));
        }

        /// A point along the arena's edge (f from 0 to 1 goes once round), inset by `inset`.
        V2 EdgePoint(float f, float inset)
        {
            float w = AW - 2f * inset, h = AH - 2f * inset, d = MathX.Fmod(f, 1f) * 2f * (w + h);
            if (d < w) return new V2(inset + d, inset);
            d -= w;
            if (d < h) return new V2(AW - inset, inset + d);
            d -= h;
            if (d < w) return new V2(AW - inset - d, AH - inset);
            d -= w;
            return new V2(inset, AH - inset - d);
        }

        /// On the floor, with the arena's contents: the portal, the crystal, the zones and the altar.
        void DrawChallengeWorld(float t)
        {
            if (game.Room == RoomPhase.None)
                return;
            if (game.Room == RoomPhase.Portal)
            {
                DrawPortal(game.PortalPos, t);
                return;
            }
            var acc = Challenges.Accent(game.RoomKind);
            if (game.RoomKind == ChallengeKind.HoldZones && (game.Room == RoomPhase.Active || game.Room == RoomPhase.Intro))
                for (int i = 0; i < Challenges.ZoneCount; i++)
                    DrawZone(i, game.RoomZones[i], acc, t);
            if (game.RoomKind == ChallengeKind.Defend && (game.Room == RoomPhase.Active || game.Room == RoomPhase.Intro || game.Room == RoomPhase.Lost && PhaseAge < 0.3f))
                DrawCrystal(game.CrystalPos, acc, t);
            if (game.Room == RoomPhase.Won || game.Room == RoomPhase.Claimed)
                DrawAltar(game.AltarPos, t);
        }

        /// A Data Gate (Docs/Mockups/challenge-portals-digital.html, "Data Gate"): rows of data scrolling round a dark core
        /// in the challenge's colour, dashed rings turning, bytes streaming in, and a ring of pips that lights as the party
        /// stands in it. It flickers as its time runs out. Reduced effects stops the scrolling, the streaming and the flicker.
        void DrawPortal(V2 pos, float t)
        {
            var acc = Challenges.Accent(game.RoomKind);
            float open = Mathf.Clamp01(PhaseAge / 0.7f), e = 1f - (1f - open) * (1f - open) * (1f - open);
            float fade = game.RoomT < 3f && !fxReduced ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 9f)) : 1f;
            float R = Challenges.PortalR * e, tt = fxReduced ? 0f : t;
            GlowDisc(pos, R * 2.4f, acc, 0.3f * fade);
            Circle(pos, R * 0.95f, new Rgba(0.012f, 0.02f, 0.06f, 0.93f), 40);
            // The data: rows of bars sliding the opposite way to their neighbours, brighter toward the rim.
            const int rows = 15;
            float rh = R * 1.9f / rows, cell = 6.4f;
            for (int i = 0; i < rows; i++)
            {
                float y = -R * 0.95f + rh * (i + 0.5f), dir = i % 2 == 0 ? -1f : 1f, off = tt * (7f + (i * 7 % 11) * 3f) * dir;
                int baseCell = Mathf.FloorToInt(off / cell);
                float fx = off - baseCell * cell;
                for (int k = -16; k <= 16; k++)
                {
                    float x = k * cell - fx, d = Mathf.Sqrt(x * x + y * y) / Mathf.Max(1f, R);
                    if (d > 0.97f || d < 0.3f)
                        continue;
                    int idx = baseCell + k;
                    float hv = GateHash(i, idx + (GateHash(i, idx, 0) > 0.82f ? Mathf.FloorToInt(tt * 3f) : 0), 1);
                    bool hot = GateHash(i, idx, 9) > 0.93f;
                    float h = 2f + hv * (rh * 0.7f);
                    var c = hot ? Rgba.White.WithAlpha(0.85f * fade) : acc.WithAlpha((0.12f + 0.6f * d) * fade);
                    FillRect(pos.X + x - 1.2f, pos.Y + y - h / 2f, 2.4f, h, c);
                }
            }
            Circle(pos, R * 0.34f, new Rgba(0.012f, 0.02f, 0.06f, 0.95f), 24);  // the dark core
            float sq = R * 0.2f, sp = tt * 0.6f;
            Polygon(pos, sq * 1.4142f, 4, sp + 0.785f, acc.WithAlpha(0.2f * fade));
            Arc(pos, sq * 1.4142f, 1.6f, acc.WithAlpha(0.9f * fade), 4, sp + 0.785f, MathX.Tau);
            Arc(pos, sq * 1.4142f * 1.4142f, 1.6f, acc.WithAlpha(0.5f * fade), 4, sp, MathX.Tau);
            FillRect(pos.X - 3f, pos.Y - 3f, 6f, 6f, Rgba.White.WithAlpha(0.85f * fade));
            for (int k = 0; k < 24; k++)  // the dashed rim, turning
            {
                float a0 = k * MathX.Tau / 24f - tt * 0.6f;
                Arc(pos, R * 0.98f, 2.6f, acc.WithAlpha(0.9f * fade), 4, a0, MathX.Tau / 24f * 0.58f);
            }
            for (int k = 0; k < 16; k++)  // a fainter outer ring, turning the other way
                Arc(pos, R * 1.1f, 1.2f, acc.WithAlpha(0.5f * fade), 3, k * MathX.Tau / 16f + tt * 0.5f, MathX.Tau / 16f * 0.22f);
            if (!fxReduced)
                for (int i = 0; i < 14; i++)  // bytes streaming in along eight lanes
                {
                    float f = MathX.Fmod(t * 0.55f + i / 14f, 1f);
                    float a = Mathf.Round(i * 2.4f / (MathX.Pi / 4f)) * (MathX.Pi / 4f);
                    V2 at = pos + V2.FromAngle(a) * (R * 2.2f * (1f - f) + R * 0.2f);
                    float sz = 3.2f * (1f - f) + 1.4f;
                    FillRect(at.X - sz / 2f, at.Y - sz / 2f, sz, sz, acc.WithAlpha(fade * Mathf.Min(1f, f * 3f)));
                }
            // The fill: 48 pips, lit up to the party's share. Everyone in, and it's full.
            const int pips = 48;
            float ring = Challenges.PortalR + 12f;
            for (int k = 0; k < pips; k++)
            {
                float a = -MathX.Pi / 2f + k * MathX.Tau / pips;
                bool lit = (k + 1f) / pips <= game.PortalFill + 1e-4f;
                V2 dir = V2.FromAngle(a);
                Line(pos + dir * (ring - 3.5f), pos + dir * (ring + 3.5f), 2.6f, lit ? acc.WithAlpha(0.95f) : new Rgba(1f, 1f, 1f, 0.12f * e));
            }
            // A thread from each player standing in it.
            foreach (var p in game.Players.Values)
                if (!p.Downed && p.Disp.DistanceTo(pos) < Challenges.PortalR)
                    Line(pos, p.Disp, 2f, acc.WithAlpha(0.5f));
        }

        static float GateHash(int a, int b, int c)
        {
            float x = Mathf.Sin(a * 127.1f + b * 311.7f + c * 74.7f) * 43758.5453f;
            return x - Mathf.FloorToInt(x);
        }

        /// A zone: the one being held glows and fills; the ones to come are dashed outlines; the ones done are faint.
        void DrawZone(int i, V2 at, Rgba acc, float t)
        {
            float R = Challenges.ZoneR;
            if (i < game.ZoneIndex)
            {
                Arc(at, R, 2f, acc.WithAlpha(0.2f), 48);
                Polygon(at, 10f, 4, 0.785f, acc.WithAlpha(0.35f));
                return;
            }
            if (i > game.ZoneIndex)
            {
                for (int k = 0; k < 16; k++)
                    Arc(at, R, 2f, acc.WithAlpha(0.22f), 4, MathX.Tau * k / 16f + t * 0.1f, MathX.Tau / 32f);
                return;
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
            Circle(at, R, acc.WithAlpha(0.08f + 0.05f * pulse), 48);
            Circle(at, R * Mathf.Clamp01(game.ZoneFill), acc.WithAlpha(0.18f), 48);
            Arc(at, R, 3f, acc.WithAlpha(0.8f), 48);
            Arc(at, R + 9f, 6f, acc.WithAlpha(0.95f), 48, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(game.ZoneFill));
            for (int k = 0; k < 4; k++)  // corner marks turning
            {
                float a = t * 0.8f + k * MathX.Pi / 2f;
                Arc(at, R + 22f, 3f, acc.WithAlpha(0.6f), 6, a, 0.35f);
            }
        }

        /// The crystal: a tall faceted gem with a glow, flashing when hit and cracking as it wears down.
        void DrawCrystal(V2 at, Rgba acc, float t)
        {
            float frac = game.CrystalMax > 0f ? Mathf.Clamp01(game.CrystalHp / game.CrystalMax) : 1f;
            float R = Challenges.CrystalR, bob = 3f * Mathf.Sin(t * 2f);
            GlowDisc(at, R * 3.2f, acc, 0.35f + 0.25f * crystalFlash);
            Circle(at + new V2(0f, R * 0.9f), R * 0.8f, new Rgba(0f, 0f, 0f, 0.3f), 20);  // its shadow
            V2 c = at + new V2(0f, -6f + bob);
            var body = crystalFlash > 0.5f ? Rgba.White : acc;
            SetColor(body.Darkened(0.35f));
            Tri(c + new V2(0f, -R * 1.4f), c + new V2(-R * 0.75f, 0f), c + new V2(0f, R * 1.1f));
            SetColor(body);
            Tri(c + new V2(0f, -R * 1.4f), c + new V2(R * 0.75f, 0f), c + new V2(0f, R * 1.1f));
            SetColor(Rgba.White.WithAlpha(0.55f));
            Tri(c + new V2(0f, -R * 1.4f), c + new V2(R * 0.22f, -R * 0.3f), c + new V2(-R * 0.1f, -R * 0.2f));  // a glint
            Line(c + new V2(0f, -R * 1.4f), c + new V2(0f, R * 1.1f), 1.5f, Rgba.White.WithAlpha(0.4f));
            if (frac < 0.66f)  // cracks
                Line(c + new V2(-R * 0.3f, -R * 0.5f), c + new V2(R * 0.15f, R * 0.1f), 1.5f, new Rgba(0.1f, 0.05f, 0.15f, 0.9f));
            if (frac < 0.33f)
                Line(c + new V2(R * 0.35f, -R * 0.1f), c + new V2(-R * 0.1f, R * 0.6f), 1.5f, new Rgba(0.1f, 0.05f, 0.15f, 0.9f));
            // Its health, as a ring round it.
            var hpCol = frac > 0.5f ? new Rgba(0.4f, 1f, 0.6f) : frac > 0.25f ? new Rgba(1f, 0.82f, 0.3f) : new Rgba(1f, 0.35f, 0.4f);
            Arc(at, R + 26f, 4f, new Rgba(1f, 1f, 1f, 0.12f), 48);
            Arc(at, R + 26f, 5f, hpCol, 48, -MathX.Pi / 2f, MathX.Tau * frac);
        }

        /// The altar: a stone plinth under a column of gold light, a prize card turning above it. Touch it.
        void DrawAltar(V2 at, float t)
        {
            float k = Mathf.Clamp01(PhaseAge / 0.5f);
            var gold = new Rgba(1f, 0.84f, 0.42f);
            bool claimed = game.Room == RoomPhase.Claimed;
            float a = claimed ? Mathf.Max(0f, 1f - PhaseAge / 0.8f) : k;
            GlowDisc(at, 150f, gold, 0.35f * a);
            VQuad(at + new V2(-26f, 0f), gold.WithAlpha(0f), at + new V2(26f, 0f), gold.WithAlpha(0f), at + new V2(14f, -300f), gold.WithAlpha(0.35f * a), at + new V2(-14f, -300f), gold.WithAlpha(0.35f * a));
            Polygon(at + new V2(0f, 10f), 30f, 4, 0.785f, new Rgba(0.28f, 0.24f, 0.32f, a));
            Polygon(at + new V2(0f, 4f), 22f, 4, 0.785f, new Rgba(0.42f, 0.36f, 0.46f, a));
            Arc(at, 40f + 6f * Mathf.Sin(t * 3f), 2.5f, gold.WithAlpha(0.7f * a), 40);
            if (!claimed)
            {
                float lift = 8f * Mathf.Sin(t * 2.2f), turn = Mathf.Abs(Mathf.Cos(t * 1.6f));
                V2 card = at + new V2(0f, -46f + lift);
                float hw = 13f * Mathf.Max(0.15f, turn), hh = 18f;
                FillRect(card.X - hw, card.Y - hh, hw * 2f, hh * 2f, new Rgba(0.65f, 0.4f, 1f, a));
                FillRect(card.X - hw + 2f, card.Y - hh + 2f, hw * 2f - 4f, hh * 2f - 4f, new Rgba(0.2f, 0.12f, 0.32f, a));
                Polygon(card, 6f * Mathf.Max(0.15f, turn), 4, 0f, gold.WithAlpha(a));
            }
        }

        /// The Gatekeeper: a bronze octagon in turning gate bars, a keyhole eye; its charge line while winding up.
        void DrawGatekeeper(Enemy e, V2 pos, float r, bool flash, bool spawning, float t)
        {
            var bronze = new Rgba(0.55f, 0.38f, 0.22f, spawning ? 0.4f : 1f);
            var glow = e.Def.Color;
            bool enraged = e.MaxHp > 0f && e.Hp < e.MaxHp * 0.5f;
            if (e.Phase == 1)  // the charge's line
            {
                float w = 0.5f + 0.5f * Mathf.Sin(t * 30f);
                V2 dir = V2.FromAngle(e.Facing);
                Line(pos, pos + dir * (Game.GateDash * Game.GateDashTime + r), 10f, new Rgba(1f, 0.3f, 0.2f, 0.12f + 0.12f * w));
                Line(pos, pos + dir * (Game.GateDash * Game.GateDashTime + r), 2f, new Rgba(1f, 0.4f, 0.3f, 0.8f));
            }
            if (e.Phase == 2)
                for (int k = 1; k <= 3; k++)
                    Circle(pos - V2.FromAngle(e.Facing) * (k * 22f), r * (1f - k * 0.15f), glow.WithAlpha(0.25f / k), 20);
            GlowDisc(pos, r * 2.2f, enraged ? new Rgba(1f, 0.35f, 0.2f) : glow, enraged ? 0.35f : 0.18f);
            // The gate bars, turning.
            for (int i = 0; i < 8; i++)
            {
                float a = MathX.Tau * i / 8f + t * (enraged ? 1.2f : 0.5f);
                Line(pos + V2.FromAngle(a) * (r + 4f), pos + V2.FromAngle(a) * (r + 14f), 4f, (flash ? Rgba.White : glow).WithAlpha(0.85f));
            }
            Arc(pos, r + 9f, 2f, glow.WithAlpha(0.5f), 40);
            Polygon(pos, r, 8, e.Facing + MathX.Pi / 8f, flash ? Rgba.White : bronze);
            Polygon(pos, r * 0.78f, 8, e.Facing + MathX.Pi / 8f, (flash ? Rgba.White : bronze).Darkened(0.35f));
            for (int i = 0; i < 4; i++)  // rivets
                Circle(pos + V2.FromAngle(e.Facing + MathX.Pi / 4f + i * MathX.Pi / 2f) * (r * 0.62f), 3f, new Rgba(0.85f, 0.7f, 0.45f), 8);
            // The keyhole eye, looking where it faces.
            V2 eye = pos + V2.FromAngle(e.Facing) * (r * 0.25f);
            Circle(eye, r * 0.26f, new Rgba(0.08f, 0.04f, 0.02f), 16);
            Circle(eye, r * 0.17f, enraged ? new Rgba(1f, 0.3f, 0.2f) : glow, 16);
            SetColor(new Rgba(0.08f, 0.04f, 0.02f));
            Tri(eye + new V2(0f, r * 0.05f), eye + new V2(-r * 0.09f, r * 0.34f), eye + new V2(r * 0.09f, r * 0.34f));
        }

        /// A shadow: its player's own look (class, weapon) drawn in dark violet, with red eyes and smoke round it.
        readonly Player shadowPuppet = new Player();

        void DrawShadow(Enemy e, V2 pos, float r, bool flash, bool spawning, float t)
        {
            int id;
            Player of;
            if (!game.ShadowOf.TryGetValue(e.Id, out id) || !game.Players.TryGetValue(id, out of))
                of = null;
            var q = shadowPuppet;
            q.Id = -1;
            q.Role = of != null ? of.Role : Role.Dps;
            q.Def = of != null ? of.Def : Defs.Get(Role.Dps);
            q.Kit = of != null ? of.Kit : Kit.Soldier;
            q.Weapon = of != null ? of.Weapon : Defs.Weapons[0];
            q.Stats = of != null ? of.Stats : q.Stats;
            q.Disp = pos;
            q.Pos = pos;
            q.Aim = e.Facing;
            q.Hp = q.MaxHp = 1f;
            q.Flash = flash ? 1f : 0f;
            for (int k = 0; k < 5; k++)  // smoke curling off it
            {
                float f = MathX.Fmod(t * 0.8f + k * 0.2f + e.Id * 0.13f, 1f);
                V2 at = pos + new V2(Mathf.Sin(k * 2.1f + t) * r * 0.6f, r * 0.4f - f * r * 2.2f);
                Circle(at, (r * 0.45f) * (1f - f) + 2f, new Rgba(0.25f, 0.1f, 0.4f, 0.35f * (1f - f) * (spawning ? 0.4f : 1f)), 10);
            }
            GlowDisc(pos, r * 2.4f, new Rgba(0.5f, 0.2f, 0.9f), 0.25f);
            glShadow = !flash;
            DrawPlayer(q, t, true);
            glShadow = false;
            V2 dir = V2.FromAngle(e.Facing), side = dir.Orthogonal();
            Circle(pos + dir * (r * 0.35f) + side * (r * 0.25f), 2.5f, new Rgba(1f, 0.2f, 0.25f), 6);
            Circle(pos + dir * (r * 0.35f) - side * (r * 0.25f), 2.5f, new Rgba(1f, 0.2f, 0.25f), 6);
            if (e.Hp < e.MaxHp)
                GhostBar(pos + new V2(-20f, -r - 12f), 40f, 4f, e.Hp / Mathf.Max(1f, e.MaxHp), new Rgba(0.75f, 0.45f, 1f), EnemyKey(e.Id));  // HealthBars.cs
        }

        /// SetColor, for a shadow: the colour turned to dark violet, as bright as it was.
        static Color ShadowTint(Color c)
        {
            float l = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
            return new Color(0.12f + 0.42f * l, 0.04f + 0.18f * l, 0.22f + 0.62f * l, c.a);
        }

        // --- The HUD ------------------------------------------------------------------------------------------------

        /// The room's HUD: the portal's call, the title card, the objective plate, the result, the warp's flash.
        void DrawChallengeHud(Player me)
        {
            TickChallengeLook();
            float now = Time.unscaledTime;
            var acc = ToColor(Challenges.Accent(game.RoomKind));
            // The warp: a flash of the room's colour (violet on the way out).
            float wk = (now - warpAt) / 0.6f;
            if (wk >= 0f && wk < 1f)
            {
                var fc = game.InRoom ? acc : new Color(0.7f, 0.6f, 1f);
                Ui.Fill(new Rect(0, 0, W, H), new Color(Mathf.Lerp(fc.r, 1f, 0.5f), Mathf.Lerp(fc.g, 1f, 0.5f), Mathf.Lerp(fc.b, 1f, 0.5f), 0.7f * (1f - wk) * (1f - wk)));
            }
            switch (game.Room)
            {
                case RoomPhase.Portal:
                    DrawPortalLabel(acc, now);
                    break;
                case RoomPhase.Intro:
                    DrawRoomTitle(acc, now);
                    break;
                case RoomPhase.Active:
                    DrawRoomObjective(acc, now);
                    break;
                case RoomPhase.Won:
                case RoomPhase.Claimed:
                    DrawRoomResult(game.Room == RoomPhase.Won ? "CHALLENGE COMPLETE!" : "PRIZE CLAIMED",
                        game.Room == RoomPhase.Won ? "Touch the altar for your prize" : "Your prize is waiting back in the arena", Ui.Gold, now);
                    break;
                case RoomPhase.Lost:
                    DrawRoomResult("CHALLENGE FAILED", "Back to the arena: no harm done", new Color(1f, 0.4f, 0.45f), now);
                    break;
            }
        }

        /// Over the portal: what's on the other side, who's in, and how long it stays open.
        void DrawPortalLabel(Color acc, float now)
        {
            V2 at = ToUi(game.PortalPos);
            int alive = 0, inside = 0;
            foreach (var p in game.Players.Values)
                if (!p.Downed)
                {
                    alive++;
                    if (p.Disp.DistanceTo(game.PortalPos) < Challenges.PortalR)
                        inside++;
                }
            float y = at.Y - Challenges.PortalR * UiWorldScale - 78f;
            var card = new Rect(at.X - 130f, y, 260f, 58f);
            Ui.HudPanel(card, acc);
            Ui.Label(new Rect(card.x, card.y + 5f, card.width, 14f), "DATA GATE", 10, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter, true);
            Ui.Fancy(new Rect(card.x, card.y + 18f, card.width, 24f), Challenges.Name(game.RoomKind).ToUpperInvariant(), 18, Color.white, acc, TextAnchor.MiddleCenter);
            string sub = alive > 1 ? inside + " of " + alive + " in  ·  closes in " + Mathf.CeilToInt(Mathf.Max(0f, game.RoomT)) + " s"
                : "Step in  ·  closes in " + Mathf.CeilToInt(Mathf.Max(0f, game.RoomT)) + " s";
            Ui.Label(new Rect(card.x, card.y + 40f, card.width, 14f), sub, 11, Ui.Muted, TextAnchor.MiddleCenter, true);
        }

        /// Entering: the challenge's name, big, and what it takes, with a countdown.
        void DrawRoomTitle(Color acc, float now)
        {
            float age = PhaseAge, k = Mathf.Clamp01(age / 0.35f), out1 = Mathf.Clamp01((age - (Challenges.IntroTime - 0.4f)) / 0.4f);
            float q = k - 1f, back = 1f + 2.70158f * q * q * q + 1.70158f * q * q, s = Mathf.Lerp(1.4f, 1f, back), a = k * (1f - out1);
            if (a <= 0f)
                return;
            Ui.Fill(new Rect(0f, H / 2f - 120f, W, 170f), new Color(0f, 0f, 0f, 0.45f * a));
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            Ui.Alpha = a;
            Ui.Label(new Rect(0f, H / 2f - 108f, W, 20f), "DATA GATE", 14, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter, true);
            Ui.Fancy(new Rect(W / 2f - 400f * s, H / 2f - 90f - 6f * s, 800f * s, 70f * s), Challenges.Name(game.RoomKind).ToUpperInvariant(), Mathf.RoundToInt(54f * s), Color.white, acc,
                TextAnchor.MiddleCenter, new Color(acc.r, acc.g, acc.b, 0.6f));
            Ui.Label(new Rect(0f, H / 2f - 14f, W, 22f), Challenges.Goal(game.RoomKind, game.Players.Count), 17, new Color(0.9f, 0.92f, 1f), TextAnchor.MiddleCenter, true);
            int count = Mathf.CeilToInt(Mathf.Max(0f, game.RoomT));
            if (count > 0)
                Ui.Label(new Rect(0f, H / 2f + 14f, W, 22f), "Starts in " + count, 13, Ui.Muted, TextAnchor.MiddleCenter, true);
            Ui.Alpha = 1f;
            GUI.color = oldColor;
        }

        /// The objective plate under the top of the screen: the goal's progress and the clock.
        void DrawRoomObjective(Color acc, float now)
        {
            bool bar = game.RoomKind == ChallengeKind.MiniBoss;  // its boss bar sits at the top
            var plate = new Rect(W / 2f - 210f, bar ? 84f : 14f, 420f, 62f);
            Ui.HudPanel(plate, acc);
            Ui.Label(new Rect(plate.x + 14f, plate.y + 6f, 260f, 16f), Challenges.Name(game.RoomKind).ToUpperInvariant(), 12, acc, TextAnchor.MiddleLeft, true);
            float clock = Challenges.Clock(game.RoomKind);
            if (clock > 0f)
            {
                float left = Mathf.Max(0f, game.RoomClock);
                bool hurry = left < 10f;
                Ui.Label(new Rect(plate.xMax - 120f, plate.y + 4f, 106f, 20f), Mathf.CeilToInt(left) + " s", hurry ? 17 : 15,
                    hurry && MathX.Fmod(now, 0.5f) < 0.25f ? new Color(1f, 0.45f, 0.45f) : Color.white, TextAnchor.MiddleRight, true);
            }
            string line;
            float frac;
            Color fill = acc;
            switch (game.RoomKind)
            {
                case ChallengeKind.Defend:
                    frac = game.CrystalMax > 0f ? game.CrystalHp / game.CrystalMax : 0f;
                    line = "Crystal  " + Mathf.CeilToInt(game.CrystalHp) + " / " + Mathf.CeilToInt(game.CrystalMax);
                    fill = frac > 0.5f ? Ui.Good : frac > 0.25f ? Ui.Warn : new Color(1f, 0.35f, 0.4f);
                    break;
                case ChallengeKind.KillRush:
                    frac = game.RoomGoal > 0 ? (float)game.RoomKills / game.RoomGoal : 0f;
                    line = game.RoomKills + " / " + game.RoomGoal + " kills";
                    break;
                case ChallengeKind.HoldZones:
                    frac = game.ZoneFill;
                    line = "Zone " + Mathf.Min(Challenges.ZoneCount, game.ZoneIndex + 1) + " of " + Challenges.ZoneCount + "  ·  stand in it";
                    break;
                case ChallengeKind.Mirror:
                {
                    int left = 0;
                    foreach (var e in game.Enemies.Values)
                        if (e.Type == EnemyType.Shadow)
                            left++;
                    frac = game.Players.Count > 0 ? 1f - (float)left / game.Players.Count : 0f;
                    line = left == 1 ? "1 shadow left" : left + " shadows left";
                    break;
                }
                default:
                    frac = -1f;
                    line = "Defeat the Gatekeeper";
                    break;
            }
            Ui.Label(new Rect(plate.x + 14f, plate.y + 24f, plate.width - 28f, 18f), line, 14, Color.white, TextAnchor.MiddleLeft, true);
            if (frac >= 0f)
                Ui.Bar(new Rect(plate.x + 14f, plate.y + 46f, plate.width - 28f, 7f), Mathf.Clamp01(frac), fill, new Color(0f, 0f, 0f, 0.5f));
            if (game.RoomKind == ChallengeKind.HoldZones)  // the zone's number over it
            {
                for (int i = game.ZoneIndex; i < Challenges.ZoneCount; i++)
                {
                    V2 at = ToUi(game.RoomZones[i]);
                    Ui.Shadowed(new Rect(at.X - 40f, at.Y - 12f, 80f, 24f), (i + 1).ToString(), i == game.ZoneIndex ? 22 : 15,
                        i == game.ZoneIndex ? Color.white : new Color(1f, 1f, 1f, 0.4f));
                }
            }
        }

        /// How it went: a banner across the middle.
        void DrawRoomResult(string title, string sub, Color col, float now)
        {
            float age = PhaseAge, k = Mathf.Clamp01(age / 0.3f);
            float q = k - 1f, back = 1f + 2.70158f * q * q * q + 1.70158f * q * q, s = Mathf.Lerp(1.5f, 1f, back);
            Ui.Fill(new Rect(0f, 60f, W, 96f), new Color(0f, 0f, 0f, 0.4f * k));
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, k);
            Ui.Alpha = k;
            Ui.Fancy(new Rect(W / 2f - 400f * s, 72f - 10f * (s - 1f), 800f * s, 52f * s), title, Mathf.RoundToInt(42f * s), Color.white, col, TextAnchor.MiddleCenter,
                new Color(col.r, col.g, col.b, 0.55f));
            Ui.Label(new Rect(0f, 126f, W, 20f), sub, 14, new Color(0.9f, 0.92f, 1f), TextAnchor.MiddleCenter, true);
            Ui.Alpha = 1f;
            GUI.color = oldColor;
            if (game.Room == RoomPhase.Won)  // a pointer over the altar
            {
                V2 at = ToUi(game.AltarPos);
                float bob = 6f * Mathf.Sin(now * 4f);
                Ui.Shadowed(new Rect(at.X - 60f, at.Y - 118f + bob, 120f, 20f), "PRIZE", 14, Ui.Gold);
            }
        }

        /// The Gatekeeper's boss bar note.
        string GatekeeperNote(Enemy e, ref Color col)
        {
            if (e.Phase == 1)
            {
                col = new Color(1f, 0.45f, 0.35f);
                return "CHARGING — get off its line!";
            }
            if (e.Hp < e.MaxHp * 0.5f)
            {
                col = new Color(1f, 0.45f, 0.35f);
                return "ENRAGED — faster, and it rings twice";
            }
            return "DATA GATE MINI-BOSS";
        }
    }
}
