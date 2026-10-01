using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// The Sexton: a 48x64 16-bit-style sprite, seen from behind, kneeling. Frames are baked pixel by pixel
// from poses (4-tone ramps, ordered dither, ink outline); welts, scars, the blood pool and drops are
// drawn over them at runtime in art pixels, so everything stays on the same pixel grid.
// Override any frame with assets/sprites/sexton_whip_<n>.png or sexton_idle_<n>.png (48x64).
static class Sexton
{
    public const int W = 48, H = 64;

    // Hand/elbow/tail ends are in body space; Dx/Dy shift the whole body (flinch).
    record Pose(float Dx, float Dy, Vector2 Elbow, Vector2 Hand, Vector2[] Tails, float Seconds, bool Spray);

    static readonly Pose[] WhipPoses =
    [
        new(0, 0, new(41, 18), new(42, 10), [new(45, 24), new(47, 26), new(43, 27)], 0.40f, false),   // raise
        new(1, 0, new(42, 13), new(40, 4), [new(46, 14), new(47, 18), new(44, 19)], 0.25f, false),    // wind up
        new(0, 0, new(40, 14), new(34, 7), [new(44, 1), new(47, 5), new(41, 0)], 0.07f, false),       // swing
        new(-1, 1, new(38, 16), new(31, 12), [new(14, 33), new(17, 38), new(21, 40)], 0.12f, true),   // strike
        new(-1, 1, new(39, 17), new(33, 13), [new(21, 40), new(26, 42), new(18, 42)], 0.30f, true),   // recoil
        new(0, 0, new(40, 18), new(38, 12), [new(30, 36), new(33, 38), new(28, 38)], 0.30f, false),   // lift
    ];
    static readonly Pose[] IdlePoses =
    [
        new(0, 0, new(38, 32), new(38, 40), [new(39, 50), new(41, 52), new(37, 51)], 1.3f, false),
        new(0, 1, new(38, 33), new(38, 41), [new(39, 51), new(41, 53), new(37, 52)], 1.3f, false),
    ];
    const int StrikeFrame = 3;

    static Texture2D[] _whip = [], _idle = [];
    static RenderTexture2D _rt;
    static float _t;
    static int _lastFrame = -1;
    static readonly List<(Vector2 p, Vector2 v)> Drops = new();
    public static bool Struck { get; private set; }  // the lash landed during the last Render

    public static void Init()
    {
        _whip = WhipPoses.Select((p, i) => Art.Get($"sexton_whip_{i}", () => Art.Make(W, H, (x, y) => Pixel(p, x, y)))).ToArray();
        _idle = IdlePoses.Select((p, i) => Art.Get($"sexton_idle_{i}", () => Art.Make(W, H, (x, y) => Pixel(p, x, y)))).ToArray();
        _rt = LoadRenderTexture(W, H);
    }

    public static void Dump(Action<string, Texture2D> save)
    {
        for (int i = 0; i < _whip.Length; i++) save($"sexton_whip_{i}", _whip[i]);
        for (int i = 0; i < _idle.Length; i++) save($"sexton_idle_{i}", _idle[i]);
    }

    // Advance the animation and paint this frame into the sprite's own texture.
    // Must run outside any other BeginTextureMode (raylib can't nest them), i.e. before the main frame starts.
    // progress: 0..1 of the current session; scars: past sessions.
    public static void Render(float dt, bool whipping, float progress, int scars)
    {
        var poses = whipping ? WhipPoses : IdlePoses;
        var frames = whipping ? _whip : _idle;
        float cycle = poses.Sum(p => p.Seconds);
        _t = (_t + dt) % cycle;
        int f = 0;
        for (float acc = poses[0].Seconds; acc < _t && f < poses.Length - 1; acc += poses[++f].Seconds) { }
        var pose = poses[f];
        if (whipping && f == StrikeFrame && _lastFrame != StrikeFrame)
            for (int i = 0; i < 5; i++)
                Drops.Add((new Vector2(22 + i, 30 + i % 3) + new Vector2(pose.Dx, pose.Dy), new Vector2(-14 - i * 6, -18 - i * 4)));
        Struck = whipping && f == StrikeFrame && _lastFrame != StrikeFrame;
        _lastFrame = f;

        BeginTextureMode(_rt);
        ClearBackground(Color.Blank);
        // ground: shadow, then the pool he kneels in
        DrawEllipse(24, 61, 20, 3, new Color(0, 0, 0, 150));
        float pool = Math.Clamp((whipping ? progress : 0) * 16 + Math.Min(scars, 8) * 0.6f, 0, 18);
        if (pool > 1) DrawEllipse(24, 61, pool, Math.Max(1, pool / 6), new Color(70, 4, 6, 255));
        if (pool > 4) DrawEllipse(21, 61, pool * 0.5f, 1, new Color(120, 14, 16, 255));
        DrawTexture(frames[f], 0, 0, Color.White);
        Welts(pose, whipping ? 1 + (int)(progress * 6.99f) : 0, scars);
        if (pose.Spray)
            foreach (var (x, y) in new[] { (14, 22), (12, 19), (16, 17), (10, 24), (18, 15), (13, 26) })
                DrawPixel(x + (int)pose.Dx, y + (int)pose.Dy, new Color(170, 20, 22, 255));
        for (int i = Drops.Count - 1; i >= 0; i--)
        {
            var (p, v) = Drops[i];
            v.Y += 90 * dt;
            p += v * dt;
            if (p.Y >= 61) { Drops.RemoveAt(i); continue; }
            Drops[i] = (p, v);
            DrawPixel((int)p.X, (int)p.Y, new Color(150, 16, 18, 255));
        }
        EndTextureMode();
    }

    // Blit the last rendered frame at `scale`, top-left at pos.
    public static void Draw(Vector2 pos, int scale) =>
        DrawTexturePro(_rt.Texture, new Rectangle(0, 0, W, -H), new Rectangle(pos.X, pos.Y, W * scale, H * scale), Vector2.Zero, 0, Color.White);

    // Diagonal stripes across the back: pale healed scars from past sessions, fresh weeping welts from this one.
    static void Welts(Pose pose, int fresh, int scars)
    {
        int dx = (int)pose.Dx, dy = (int)pose.Dy;
        for (int k = 0; k < Math.Max(fresh, Math.Min(scars, 7)); k++)
        {
            float y1 = 23 + k * 2.6f, y2 = y1 + 5 + k % 2;  // spaced so skin shows between the stripes
            int x1 = (int)(24 + HalfWidth(y1) - 2), x2 = (int)(24 - HalfWidth(y2) + 2);
            bool open = k < fresh;
            var c = open ? new Color(176, 22, 24, 255) : new Color(150, 98, 82, 255);
            DrawLine(x1 + dx, (int)y1 + dy, x2 + dx, (int)y2 + dy, c);
            if (!open) continue;
            DrawLine(x1 + dx, (int)y1 + dy + 1, x2 + dx, (int)y2 + dy + 1, new Color(92, 8, 10, 255));
            int drip = x2 + (x1 - x2) * ((k * 7) % 5) / 5;
            float yd = y2 + (y1 - y2) * ((k * 7) % 5) / 5f;
            DrawLine(drip + dx, (int)yd + dy + 1, drip + dx, (int)yd + dy + 2 + k % 3, new Color(120, 12, 14, 255));
        }
    }

    // ---------------------------------------------------------------- baking

    static float HalfWidth(float y) => 12.5f - 4f * (y - 19) / 24;  // torso tapers from shoulders (y 19) to waist (y 43)

    internal static readonly Color Outline = new(10, 6, 6, 255);
    static readonly Color[] HoodRamp = [new(22, 16, 15, 255), new(36, 27, 24, 255), new(54, 41, 35, 255), new(76, 59, 49, 255)];
    static readonly Color[] SkinRamp = [new(74, 44, 36, 255), new(114, 73, 58, 255), new(156, 109, 86, 255), new(192, 146, 116, 255)];
    static readonly Color[] RobeRamp = [new(28, 22, 19, 255), new(44, 35, 29, 255), new(62, 50, 41, 255), new(82, 68, 55, 255)];
    static readonly Color[] RopeRamp = [new(66, 51, 32, 255), new(98, 79, 50, 255), new(128, 104, 68, 255), new(148, 122, 80, 255)];
    static readonly Color Wood = new(58, 37, 24, 255), Leather = new(46, 29, 21, 255), Knot = new(28, 17, 13, 255);

    static readonly float[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
    internal static Color Tone(Color[] ramp, float light, int x, int y)
    {
        float d = (Bayer[y & 3, x & 3] / 16f - 0.5f) * 0.5f;
        return ramp[Math.Clamp((int)MathF.Floor(light * 3.99f + d), 0, 3)];
    }

    internal static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / Math.Max(ab.LengthSquared(), 1e-4f), 0, 1);
        return Vector2.Distance(p, a + ab * t);
    }

    // A pixel with the outline pass: transparent pixels touching the body become ink.
    static Color Pixel(Pose pose, int x, int y)
    {
        var c = Body(pose, x, y);
        if (c.A > 0) return c;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
                if ((ox != 0 || oy != 0) && (ox == 0 || oy == 0) && Body(pose, x + ox, y + oy).A > 0) return Outline;
        return Color.Blank;
    }

    static Color Body(Pose pose, int x, int y)
    {
        var p = new Vector2(x, y);
        var b = p - new Vector2(pose.Dx, pose.Dy);  // body space
        float bx = b.X, by = b.Y;

        // the scourge and the raised arm sit in front of everything
        var shoulder = new Vector2(35, 22);
        var grip = pose.Hand + Vector2.Normalize(pose.Hand - pose.Elbow) * 3;
        foreach (var end in pose.Tails)
        {
            var mid = (grip + end) / 2 + new Vector2(end.Y - grip.Y, grip.X - end.X) * 0.12f;
            for (float t = 0; t <= 1; t += 0.04f)
            {
                var q = (1 - t) * (1 - t) * grip + 2 * (1 - t) * t * mid + t * t * end;
                if (Vector2.Distance(b, q) < 0.75f) return t > 0.88f ? (pose.Spray ? new Color(120, 14, 16, 255) : Knot) : Leather;
            }
        }
        if (SegDist(b, pose.Hand, grip) < 0.9f) return Wood;
        if (Vector2.Distance(b, pose.Hand) < 2.1f) return Tone(SkinRamp, 0.55f, x, y);
        if (SegDist(b, pose.Elbow, pose.Hand) < 1.9f) return Tone(SkinRamp, 0.5f, x, y);
        if (SegDist(b, shoulder, pose.Elbow) < 2.3f) return Tone(SkinRamp, 0.45f, x, y);

        // hood: a pointed cowl over a bowed head, draped onto the shoulders
        float hx = bx - 24, hy = by - 12;
        bool cowl = hx * hx + hy * hy < 49 || (by > 2.5f && by < 9 && MathF.Abs(hx) < (by - 2.5f) * 0.8f);
        bool drape = MathF.Pow((bx - 24) / 10, 2) + MathF.Pow((by - 20) / 3.6f, 2) < 1;
        if (cowl || drape)
        {
            float light = 0.55f - hx / 16 - hy / 30;
            if (MathF.Abs(hx) < 0.6f && by < 18) light -= 0.3f;        // the seam down the back of the hood
            if (drape && !cowl) light -= 0.15f;
            return Tone(HoodRamp, light, x, y);
        }

        // left arm hanging, hand braced on the ground
        if (SegDist(b, new(12, 22), new(10, 36)) < 2.2f || SegDist(b, new(10, 36), new(8, 47)) < 1.9f || Vector2.Distance(b, new(8, 48)) < 2.1f)
            return Tone(SkinRamp, 0.62f - (by - 22) / 80, x, y);

        // bare back: wasted, spine and ribs showing
        if (by >= 19 && by <= 43)
        {
            float hw = HalfWidth(by);
            if (by < 22) hw *= MathF.Sqrt(Math.Max(0, 1 - MathF.Pow((22 - by) / 3.2f, 2)));
            float nx = (bx - 24) / hw;
            if (MathF.Abs(nx) < 1)
            {
                float light = 0.62f - nx * 0.38f - (by - 19) / 90;
                if (MathF.Abs(bx - 24) < 0.6f && by > 23) light -= 0.28f;   // spine groove
                foreach (var sx in new[] { 19f, 29f })                       // shoulder blades
                {
                    float e = MathF.Pow((bx - sx) / 4, 2) + MathF.Pow((by - 26) / 3, 2);
                    if (e > 0.75f && e < 1.05f) light -= 0.18f;
                }
                if (MathF.Abs(nx) > 0.6f && (int)by % 3 == 0 && by > 28) light -= 0.2f;  // ribs at the flanks
                return Tone(SkinRamp, light, x, y);
            }
        }

        // rope belt, then the robe bunched over folded legs
        if (by >= 42 && by < 44.5f && MathF.Abs(bx - 24) < 10.5f) return Tone(RopeRamp, 0.55f + ((int)bx % 3 == 0 ? 0.25f : 0), x, y);
        bool robe = (by >= 43 && by < 52 && MathF.Abs(bx - 24) < 10.5f + (by - 43) * 0.55f) || MathF.Pow((bx - 24) / 15, 2) + MathF.Pow((by - 52) / 7.5f, 2) < 1;
        if (robe)
        {
            float light = 0.5f - (bx - 24) / 36 - (by - 44) / 40 + 0.18f * MathF.Sin(bx * 0.9f + by * 0.25f);  // folds
            return Tone(RobeRamp, light, x, y);
        }

        // soles of the feet, dirty
        foreach (var fx in new[] { 17f, 31f })
            if (MathF.Pow((bx - fx) / 3.2f, 2) + MathF.Pow((by - 59) / 1.8f, 2) < 1)
                return Tone(SkinRamp, 0.25f + (by < 58.5f ? 0.15f : 0), x, y);

        return Color.Blank;
    }
}
