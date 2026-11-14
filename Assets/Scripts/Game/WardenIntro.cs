using UnityEngine;

namespace Arena
{
    /// The Warden's entrance, Lockdown (option B of the Warden intro mockup): push-bullet walls close in from all four
    /// sides into a cell round it, a red padlock snaps shut and the silhouette inside lights up pink, then it blows the
    /// cell open and a CRT scan band types THE WARDEN. It plays in place of the boss card (Juice.cs) on everyone's
    /// screen from when they first see it; the host holds the Warden and its escort for Game.WardenIntroTime
    /// (EnemyAI.SpawnEnemy), which is when the cell breaks. Preview: WARDENINTRO=<s> Tools/Preview/shot.sh x hud.
    public partial class ArenaApp
    {
        // The timeline, in seconds since the Warden appeared.
        const float WiClose0 = 0.2f, WiClose1 = 1.3f, WiLock = 1.3f, WiShut = 1.65f, WiSeen = 1.35f, WiLit = 1.7f;
        const float WiBreak = Game.WardenIntroTime - 0.3f, WiBlow = 0.7f, WiBand0 = 3f, WiType0 = 3.25f, WiTypeLen = 0.6f, WiSub = 3.9f, WiBand1 = 4.8f, WiEnd = 5f;
        const float WiHalfW = 170f, WiHalfH = 140f, WiFar = 440f, WiStep = 34f;
        const string WiTitle = "THE WARDEN";
        static readonly Rgba WiInk = new Rgba(1f, 0.42f, 0.55f);

        float wardenIntroT = -1f, wardenIntroFrom;  // (From: where it starts, 0 but in the preview tool)
        int wardenIntroId;
        V2 wardenIntroAt;

        bool WardenIntroOn => wardenIntroT >= 0f && wardenIntroT < WiEnd;
        bool WardenIntroHoldsHud => wardenIntroT >= 0.3f && wardenIntroT < WiBand1;

        void StartWardenIntro(Enemy e)
        {
            wardenIntroT = wardenIntroFrom;
            wardenIntroId = e.Id;
            wardenIntroAt = e.Disp;
            sounds?.Play(Sfx.BossSiren, 0.55f, Time.unscaledTime);
        }

        static bool Crossed(float a, float b, float at) => a < at && b >= at;
        static int WiTyped(float s) => Mathf.FloorToInt(Mathf.Clamp01((s - WiType0) / WiTypeLen) * WiTitle.Length);

        /// Its cues: the cell clanging shut, the lock, the break, the band's sweep and typing.
        void TickWardenIntro(float dt)
        {
            if (wardenIntroT < 0f)
                return;
            float a = wardenIntroT, b = a + dt, now = Time.unscaledTime;
            wardenIntroT = b;
            if (Crossed(a, b, WiClose1)) { sounds?.Play(Sfx.BossCage, 0.7f, now); Shake(0.25f); }
            if (Crossed(a, b, WiShut)) sounds?.Play(Sfx.BossChime, 0.45f, now);
            if (Crossed(a, b, WiBreak)) { sounds?.Play(Sfx.BossCrack, 0.8f, now); Shake(0.45f); }
            if (Crossed(a, b, WiBand0)) sounds?.Play(Sfx.CrtSweep, 0.5f, now);
            for (int i = 0, n = Mathf.Min(2, WiTyped(b) - WiTyped(a)); i < n; i++)
                sounds?.Play(Sfx.CrtType, 0.5f, now);
            if (b >= WiEnd)
                wardenIntroT = -1f;
        }

        /// The Warden's body while it waits in the cell: unseen, then a dark silhouette, then lit as the lock shuts.
        Rgba WardenIntroBody(Rgba col)
        {
            float s = wardenIntroT, seen = Mathf.Clamp01((s - WiSeen) / 0.65f), lit = Mathf.Clamp01((s - WiLit) / 0.4f);
            return new Rgba(0.16f, 0.08f, 0.13f).Mix(col, lit).WithAlpha(seen);
        }

        /// The cell, its lock and the blast (over the enemies).
        void DrawWardenIntro(float t)
        {
            if (!WardenIntroOn || wardenIntroT >= WiBreak + WiBlow)
                return;
            float s = wardenIntroT;
            V2 c = wardenIntroAt;
            float close = Mathf.Clamp01((s - WiClose0) / (WiClose1 - WiClose0)), blow = Mathf.Clamp01((s - WiBreak) / WiBlow);
            float inset = WiFar * (1f - close * close * close), fade = 1f - blow;
            float l = -WiHalfW - inset, r = WiHalfW + inset, top = -WiHalfH - inset, bot = WiHalfH + inset;
            float gap = Game.GateBulletR * 2f + 8f;  // the outer row
            for (float x = l; x <= r + 1f; x += WiStep)
            {
                WiPush(c, new V2(x, top), new V2(0f, 1f), true, blow, fade);
                WiPush(c, new V2(x, top - gap), new V2(0f, 1f), false, blow, fade);
                WiPush(c, new V2(x, bot), new V2(0f, -1f), true, blow, fade);
                WiPush(c, new V2(x, bot + gap), new V2(0f, -1f), false, blow, fade);
            }
            for (float y = top + WiStep; y < bot - 1f; y += WiStep)
            {
                WiPush(c, new V2(l, y), new V2(1f, 0f), true, blow, fade);
                WiPush(c, new V2(l - gap, y), new V2(1f, 0f), false, blow, fade);
                WiPush(c, new V2(r, y), new V2(-1f, 0f), true, blow, fade);
                WiPush(c, new V2(r + gap, y), new V2(-1f, 0f), false, blow, fade);
            }
            // The walls meet: a white ring off the lock.
            V2 lockAt = c + new V2(0f, WiHalfH);
            if (s >= WiClose1 && s < WiClose1 + 0.35f)
            {
                float k = (s - WiClose1) / 0.35f;
                Arc(lockAt, 20f + 80f * k, 4f * (1f - k) + 1f, new Rgba(1f, 1f, 1f, 0.7f * (1f - k)), 32);
            }
            // The padlock: drops in as the walls meet, its shackle snaps shut, cracks just before the break.
            if (s >= WiLock && s < WiBreak + 0.15f)
            {
                float a = Mathf.Clamp01((s - WiLock) / 0.15f) * (1f - Mathf.Clamp01((s - WiBreak) / 0.15f));
                float shut = Mathf.Clamp01((s - WiShut + 0.15f) / 0.15f), lift = (1f - shut * shut * (3f - 2f * shut)) * 12f;
                var red = Warden_LockCol.WithAlpha(a);
                Arc(lockAt + new V2(0f, -14f - lift), 11f, 5f, red, 12, MathX.Pi, MathX.Pi);  // the shackle
                FillRect(lockAt.X - 13.5f, lockAt.Y - 14f - lift, 5f, 6f + lift, red);
                FillRect(lockAt.X + 8.5f, lockAt.Y - 14f, 5f, 6f, red);
                FillRect(lockAt.X - 17f, lockAt.Y - 9f, 34f, 27f, red);
                FillRect(lockAt.X - 17f, lockAt.Y - 9f, 34f, 4f, new Rgba(1f, 0.65f, 0.55f, 0.6f * a));  // a sheen
                var hole = new Rgba(0.16f, 0.03f, 0.02f, a);
                Circle(lockAt + new V2(0f, 2f), 4f, hole, 10);
                FillRect(lockAt.X - 1.5f, lockAt.Y + 2f, 3f, 9f, hole);
                if (s > WiBreak - 0.25f)  // cracking
                {
                    var crack = new Rgba(1f, 1f, 1f, 0.9f * a);
                    Line(lockAt + new V2(-15f, -7f), lockAt + new V2(-3f, 3f), 1.6f, crack);
                    Line(lockAt + new V2(-3f, 3f), lockAt + new V2(-8f, 16f), 1.6f, crack);
                    Line(lockAt + new V2(-3f, 3f), lockAt + new V2(14f, 0f), 1.6f, crack);
                }
            }
            // The break: a pink shockwave.
            if (blow > 0f && blow < 1f)
            {
                float k = 1f - (1f - blow) * (1f - blow) * (1f - blow);
                Arc(c, 46f + 380f * k, 10f * (1f - blow) + 2f, WiInk.WithAlpha(0.8f * (1f - blow)), 48);
            }
        }

        static readonly Rgba Warden_LockCol = new Rgba(1f, 0.3f, 0.25f);

        /// One of the cell's push bullets (DrawPushBullet's look, fading), its chevron pointing in; blown outward as it breaks.
        void WiPush(V2 c, V2 o, V2 dir, bool lead, float blow, float a)
        {
            if (a <= 0f)
                return;
            V2 p = c + o;
            if (blow > 0f)
                p += o.Normalized() * (260f * (1f - (1f - blow) * (1f - blow) * (1f - blow)));
            float rr = Game.GateBulletR;
            Circle(p, rr * 1.6f, new Rgba(0.65f, 0.78f, 1f, 0.16f * a), 10);
            SetColor(PushEdge.WithAlpha(a));
            Quad(p + new V2(-rr - 1f, -rr - 1f), p + new V2(rr + 1f, -rr - 1f), p + new V2(rr + 1f, rr + 1f), p + new V2(-rr - 1f, rr + 1f));
            SetColor((lead ? PushLead : PushCol).WithAlpha(a));
            Quad(p + new V2(-rr, -rr), p + new V2(rr, -rr), p + new V2(rr, rr), p + new V2(-rr, rr));
            var dark = new Rgba(0.1f, 0.14f, 0.27f, 0.95f * a);
            V2 side = new V2(-dir.Y, dir.X);
            V2 tip = p + dir * 2.5f;
            Line(p - dir * 2f - side * 4f, tip, 2f, dark);
            Line(tip, p - dir * 2f + side * 4f, 2f, dark);
        }

        /// The CRT scan band, low on the screen, typing its name (HUD layer, over the world).
        void DrawWardenIntroBand()
        {
            if (!WardenIntroOn || wardenIntroT < WiBand0)
                return;
            float s = wardenIntroT;
            float open = Mathf.Clamp01((s - WiBand0) / 0.3f) * (1f - Mathf.Clamp01((s - (WiBand1 - 0.3f)) / 0.3f));
            int n = WiTyped(s);
            string title = WiTitle.Substring(0, n) + (n < WiTitle.Length && Mathf.FloorToInt(s * 8f) % 2 == 0 ? "_" : "");
            string sub = s >= WiSub ? "LOCKDOWN IN EFFECT  ·  ALL INMATES TO CELLS" : "";
            string wave = "WAVE " + game.ShownWave;
            DrawScanBand(title, sub, "SYS> CELL BLOCK " + game.ShownWave + "\nSYS> DOORS SEALED", wave, -1f,
                new Color(WiInk.R, WiInk.G, WiInk.B), open, false, H / 2f + 90f);
        }
    }
}
