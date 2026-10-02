using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// The frame around the game in full screen is the inside of Him, where Ashkirk was built: masonry rotting into raw muscle,
// the game held open in a wound by iron hooks, ribs and two spines pushing through the wall, and skulls with tallow
// melted over them in torn hollows. (Teeth along the wound's lips were tried: at this grain they read as a barcode.)
// It is generated for the screen it fills, at the art's own grain (one art pixel is 2 layout pixels), with 4-tone ramps
// and ordered dither (noise dither only on the raw lips of the wound; everywhere else it reads as static). Live: the candle flames, drops running down the old blood,
// and a throb on the heartbeat.
// assets/sprites/frame.png replaces it: scaled to cover the screen, with the game drawn over its middle.
// `--dump-art` writes the 1920×1080 version as a template.
static class Frame
{
    static Texture2D _tex;
    static (int w, int h, Vector2 at, float s) _builtFor;  // rebuilt when any of these change (switching modes changes them a frame apart)
    static int _px;
    static bool _override;
    static readonly List<Vector2> Wicks = new();               // art pixels; a live flame stands on each
    static readonly List<(Vector2 top, int len)> Runs = new();  // blood runs that a fresh drop slides down now and then

    static Color C(int r, int g, int b) => new(r, g, b, 255);
    static readonly Color Ink = C(8, 5, 5);
    static readonly Color[] StoneR = [C(15, 13, 12), C(27, 24, 21), C(41, 37, 32), C(57, 51, 44)];
    static readonly Color[] MoldR = [C(20, 21, 15), C(32, 34, 22), C(46, 48, 30), C(62, 64, 40)];
    static readonly Color[] BloodR = [C(24, 4, 5), C(48, 7, 8), C(78, 12, 12), C(108, 22, 20)];
    static readonly Color[] FleshR = [C(36, 9, 12), C(68, 18, 22), C(104, 34, 36), C(138, 62, 58)];
    static readonly Color[] RawR = [C(54, 5, 9), C(100, 14, 18), C(146, 34, 34), C(186, 82, 74)];
    static readonly Color[] BruiseR = [C(38, 22, 32), C(66, 40, 54), C(98, 74, 64), C(132, 116, 78)];
    static readonly Color[] VeinR = [C(22, 8, 20), C(40, 14, 34), C(56, 22, 46), C(74, 34, 58)];
    static readonly Color[] RotR = [C(34, 30, 16), C(62, 56, 26), C(94, 84, 40), C(128, 116, 62)];
    static readonly Color[] BoneR = [C(40, 33, 24), C(94, 82, 58), C(146, 130, 96), C(184, 168, 128)];
    static readonly Color[] IronR = [C(14, 12, 11), C(34, 28, 25), C(58, 46, 38), C(92, 62, 44)];
    static readonly Color[] GlowR = [C(9, 6, 5), C(26, 15, 10), C(56, 32, 17), C(98, 60, 28)];
    static readonly Color[] WaxR = [C(92, 80, 56), C(140, 124, 88), C(180, 164, 120), C(208, 194, 150)];
    static readonly Color DropDark = C(96, 10, 12), DropWet = C(170, 28, 26);

    public static void Draw(Vector2 at, float scale, float t, float beat)
    {
        int sw = GetScreenWidth(), sh = GetScreenHeight();
        if (_builtFor != (sw, sh, at, scale)) Build(sw, sh, at, scale);
        var src = new Rectangle(0, 0, _tex.Width, _tex.Height);
        float k = _override ? Math.Max(sw / (float)_tex.Width, sh / (float)_tex.Height) : _px;
        var tint = _override ? Color.White : Ui.Lerp(C(204, 192, 192), Color.White, beat);  // the flesh darkens between beats
        DrawTexturePro(_tex, src, new Rectangle((sw - _tex.Width * k) / 2, (sh - _tex.Height * k) / 2, _tex.Width * k, _tex.Height * k), Vector2.Zero, 0, tint);
        if (_override) return;
        for (int i = 0; i < Runs.Count; i++)
        {
            // a fresh drop down an old run, then a long wait
            var (top, len) = Runs[i];
            float cycle = len + 60, pos = (t * 4 + Art.Hash(i, 0, 201) * cycle) % cycle;
            if (pos >= len) continue;
            int x = (int)top.X * _px, y = (int)(top.Y + pos) * _px;
            DrawRectangle(x, y - _px, _px, _px, DropDark);
            DrawRectangle(x, y, _px, _px, DropWet);
        }
        for (int i = 0; i < Wicks.Count; i++)
        {
            // a flame in whole art pixels: a bright core, a taller tongue that gutters and leans
            var w = Wicks[i] * _px;
            float f = 0.5f + 0.5f * MathF.Sin(t * 9.1f + i * 1.7f) * MathF.Sin(t * 5.3f + i * 2.9f);
            DrawCircleGradient(new Vector2(w.X + _px / 2f, w.Y), 7 * _px * (0.85f + 0.15f * f), ColorAlpha(Ui.Candle, 0.14f), ColorAlpha(Ui.Candle, 0));
            int lean = f > 0.8f ? _px : 0, tall = f > 0.35f ? 3 : 2;
            DrawRectangle((int)w.X, (int)w.Y - _px, _px, _px, Ui.FlameCore);
            for (int k2 = 2; k2 <= tall; k2++) DrawRectangle((int)w.X + (k2 == tall ? lean : 0), (int)w.Y - k2 * _px, _px, _px, Ui.Candle);
        }
    }

    static void Build(int sw, int sh, Vector2 at, float scale)
    {
        if (_tex.Id != 0) UnloadTexture(_tex);
        _builtFor = (sw, sh, at, scale);
        _px = (int)MathF.Round(Art.Px * scale);
        Wicks.Clear();
        Runs.Clear();
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "sprites", "frame.png");
        _override = File.Exists(path);
        if (_override) { _tex = LoadTexture(path); SetTextureFilter(_tex, TextureFilter.Point); return; }
        _tex = Generate(sw, sh, at, scale, Wicks, Runs);
    }

    // The frame for a 1920×1080 screen at the full-screen scale there (1.5), for --dump-art.
    public static Texture2D Template() => Generate(1920, 1080, new Vector2(487, 150), 1.5f, new(), new());

    static Texture2D Generate(int sw, int sh, Vector2 at, float scale, List<Vector2> wicks, List<(Vector2, int)> runs)
    {
        int px = (int)MathF.Round(Art.Px * scale);
        int w = (sw + px - 1) / px, h = (sh + px - 1) / px;
        int x0 = (int)(at.X / px), y0 = (int)(at.Y / px);
        int x1 = (int)MathF.Ceiling((at.X + 630 * scale) / px), y1 = (int)MathF.Ceiling((at.Y + 520 * scale) / px);
        var buf = new Painter(w, h, x0, y0, x1, y1, wicks, runs).Paint();
        var tex = Art.Make(w, h, (x, y) => buf[y * w + x]);
        SetTextureFilter(tex, TextureFilter.Point);
        return tex;
    }

    static readonly int[] Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    // Picks a tone from a ramp, dithering between neighbours: ordered for worked surfaces, noise for living ones.
    static Color Ramp(Color[] r, float v, int x, int y, bool organic = false)
    {
        float f = Math.Clamp(v, 0, 0.999f) * (r.Length - 1);
        int i = (int)f;
        float threshold = organic ? Art.Hash(x, y, 7) : (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16;
        return f - i > threshold ? r[i + 1] : r[i];
    }

    static readonly string[] Skull =
    [
        "..kkkkk..",
        ".kbbbbbk.",
        "kbbbbbbdk",
        "kbkkbkkdk",
        "kbkkbkkdk",
        "kbbbkbbdk",
        ".kbdbdbk.",
        ".kbkbkbk.",
        "..kkkkk..",
    ];

    // The game sits in [x0, x1) × [y0, y1), in art pixels.
    sealed class Painter(int w, int h, int x0, int y0, int x1, int y1, List<Vector2> wicks, List<(Vector2, int)> runs)
    {
        readonly Color[] _buf = new Color[w * h];
        readonly float _far = Math.Max(1, Math.Max(Math.Max(x0, w - x1), Math.Max(y0, h - y1)));

        void Set(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < w && y < h) _buf[y * w + x] = c; }
        bool InGame(int x, int y) => x >= x0 && x < x1 && y >= y0 && y < y1;
        float Dist(float x, float y) { float dx = Math.Max(0, Math.Max(x0 - x, x - x1)), dy = Math.Max(0, Math.Max(y0 - y, y - y1)); return MathF.Sqrt(dx * dx + dy * dy); }
        float Fade(float x, float y) => 0.9f - 0.68f * Math.Min(1, Dist(x, y) / _far);  // darker away from the game

        public Color[] Paint()
        {
            int sideL = x0 - 16, sideR = w - x1 - 16;
            Wall();
            if (Math.Min(sideL, sideR) >= 110) { Hollows(0, sideL); Hollows(w - sideR, sideR); }
            if (Math.Min(sideL, sideR) >= 40) Ribs(Math.Min(sideL, sideR));
            if (y0 >= 36) Spine(y0 - 24);
            if (h - y1 >= 36) Spine(y1 + 23);
            Wound();
            Hooks();
            Bleed();
            return _buf;
        }

        // Rotten masonry, with muscle pushing through it: more of it the nearer the wound.
        void Wall()
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fade = Fade(x, y), meat = Art.Fbm(x * 0.022f, y * 0.022f, 101) + 0.2f * Math.Max(0, 1 - Dist(x, y) / 40);
                    _buf[y * w + x] = meat > 0.6f ? Flesh(x, y, meat, fade)
                                    : meat > 0.575f ? (Art.Hash(x, y, 103) > 0.45f ? Ink : FleshR[0])  // the torn edge where it broke through
                                    : StoneAt(x, y, fade);
                }
        }

        static Color Flesh(int x, int y, float meat, float fade)
        {
            float a = Art.Fbm(x * 0.012f, y * 0.012f, 105) * 6.3f;  // the grain of the muscle wanders
            float fibre = MathF.Sin((x * MathF.Cos(a) + y * MathF.Sin(a)) * 0.9f + 5 * Art.Fbm(x * 0.05f, y * 0.05f, 107));
            float v = (0.24f + 0.14f * fibre + 0.3f * (Art.Fbm(x * 0.07f, y * 0.07f, 109) - 0.5f) + Math.Min(0.3f, (meat - 0.6f) * 2.5f)) * fade;
            if (Art.Ridge(x * 0.035f, y * 0.035f, 111) > 0.955f) return Ramp(VeinR, v + 0.1f, x, y);
            if (Art.Fbm(x * 0.09f, y * 0.09f, 115) > 0.75f) return Ramp(RotR, v * 0.9f, x, y);                  // gone necrotic
            if (fibre > 0.85f && Art.Fbm(x * 0.3f, y * 0.3f, 113) > 0.74f && fade > 0.7f) return FleshR[3];    // wet
            return Ramp(FleshR, v, x, y);
        }

        static Color StoneAt(int x, int y, float fade)
        {
            int sy = y + (int)(Art.Fbm(x * 0.02f, 0.5f, 121) * 8);  // the courses sag
            int course = sy / 10, ly = sy % 10, blockW = 14 + (int)(Art.Hash(course, 1, 5) * 14);
            int bx = x + (int)(Art.Hash(course, 2, 5) * blockW), lx = bx % blockW, block = bx / blockW;
            if (ly == 0 || lx == 0 || (ly == 9 || lx == blockW - 1) && Art.Hash(block, course, 127) > 0.6f) return Ink;  // mortar long gone
            if (Art.Ridge(x * 0.05f, y * 0.05f, 129) > 0.955f) return Ink;                                                // cracks
            float v = 0.42f + 0.4f * (Art.Fbm(x * 0.09f, y * 0.09f, 123) - 0.5f) + (Art.Hash(block, course, 125) - 0.5f) * 0.25f;
            if (ly == 1) v += 0.12f;
            if (Art.Fbm(x * 0.15f, y * 0.15f, 131) < 0.3f) v *= 0.5f;                                           // eaten away
            v *= (0.72f + 0.28f * Art.Fbm(x * 0.4f, y * 0.03f, 133)) * fade;                                       // streaked
            if (Art.Fbm(x * 0.03f, y * 0.03f, 135) > 0.68f) return Ramp(BloodR, v * 1.2f, x, y);               // soaked
            float mold = Art.Fbm(x * 0.045f, y * 0.045f, 137);
            return mold > 0.68f ? Ramp(MoldR, v + (mold - 0.68f), x, y) : Ramp(StoneR, v, x, y);
        }

        // A shaded tube along a quadratic curve, inked first so its outline sits under the fill. Lit from the top left.
        void Tube(Vector2 p0, Vector2 p1, Vector2 p2, float tEnd, float r0, float r1, Color[] ramp)
        {
            int n = (int)(((p1 - p0).Length() + (p2 - p1).Length()) * 2) + 2;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i <= n; i++)
                {
                    float t = tEnd * i / n, r = r0 + (r1 - r0) * i / n;
                    var c = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
                    Disc(c, pass == 0 ? r + 1 : r, pass == 0 ? null : ramp, Fade(c.X, c.Y));
                }
        }

        void Disc(Vector2 c, float r, Color[]? ramp, float fade, bool organic = false)
        {
            for (int dy = -(int)MathF.Ceiling(r); dy <= r; dy++)
                for (int dx = -(int)MathF.Ceiling(r); dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r * r) continue;
                    int x = (int)MathF.Round(c.X) + dx, y = (int)MathF.Round(c.Y) + dy;
                    if (ramp == null) { Set(x, y, Ink); continue; }
                    float lit = (-dx * 0.6f - dy * 0.8f) / Math.Max(r, 1);
                    float v = (0.55f + 0.35f * lit + 0.2f * (Art.Fbm(x * 0.3f, y * 0.3f, 141) - 0.5f)) * fade;
                    if (Art.Hash(x, y, 143) > 0.96f) v *= 0.5f;  // pitted
                    Set(x, y, Ramp(ramp, v, x, y, organic));
                }
        }

        // Ribs pushing out of the wall on both sides of the wound, some of them snapped.
        void Ribs(int side)
        {
            float most = Math.Min(side * 0.55f, 80);
            for (int i = 0, yb = y0 + 16; yb < y1 - 10; i++, yb += 30)
                foreach (int dir in new[] { -1, 1 })
                {
                    float root = dir < 0 ? x0 - 12 : x1 + 11, len = most * (0.7f + 0.3f * Art.Hash(i, dir, 150)), bow = 8 + 14 * Art.Hash(i, dir, 151);
                    var p0 = new Vector2(root, yb + (int)(Art.Hash(i, dir, 152) * 8) - 4);
                    var p1 = new Vector2(root + dir * len * 0.5f, p0.Y - bow);
                    var p2 = new Vector2(root + dir * len, p0.Y + 12 + Art.Hash(i, dir, 153) * 14);
                    bool snapped = Art.Hash(i, dir, 155) < 0.4f;
                    float end = snapped ? 0.55f + 0.25f * Art.Hash(i, dir, 157) : 1;
                    Disc(p0, 7, FleshR, Fade(root, yb), true);  // it comes out of meat
                    Tube(p0, p1, p2, end, 4.5f, snapped ? 3.5f : 2.2f, Art.Hash(i, dir, 154) < 0.25f ? RotR : BoneR);
                    if (!snapped) continue;
                    var tip = (1 - end) * (1 - end) * p0 + 2 * (1 - end) * end * p1 + end * end * p2;
                    Disc(tip, 2, BloodR, 1);  // the marrow shows
                    Set((int)tip.X + dir * 2, (int)tip.Y - 1, Ink);
                    runs.Add((tip + new Vector2(0, 3), 8 + (int)(Art.Hash(i, dir, 159) * 20)));
                }
        }

        // A spine laid along the top or the bottom, half sunk in a ridge of meat: worn vertebrae pinched at the waist,
        // gristle between, the cord's hole in each.
        void Spine(int sy)
        {
            for (int i = 0, x = x0 + 6; x < x1 - 8; i++)
            {
                int vw = 7 + (int)(Art.Hash(i, sy, 223) * 6), lift = (int)(Art.Fbm(i * 0.4f, sy, 225) * 10) - 5, tilt = (int)(Art.Hash(i, sy, 222) * 5) - 2;
                if (Art.Hash(i, sy, 224) < 0.12f) { x += vw + 4; continue; }  // one is missing; the gristle closed over the gap
                var ramp = Art.Hash(i, sy, 226) < 0.25f ? RotR : BoneR;
                Tube(new(x, sy + lift - tilt), new(x + vw / 2f, sy + lift - 1), new(x + vw, sy + lift + tilt), 1, 3 + Art.Hash(i, sy, 221) * 2, 3.5f, ramp);  // slumped, uneven
                Set(x + vw / 2, sy + lift, Ink); Set(x + vw / 2 + 1, sy + lift, Ink);  // the cord ran here
                for (int dx = vw / 2 - 1; dx <= vw / 2 + 2; dx++) { Set(x + dx, sy + lift - 4, Ink); Set(x + dx, sy + lift + 4, Ink); }  // the waist
                if (Art.Hash(i, sy, 227) < 0.3f) { Set(x + 2, sy + lift - 2, Ink); Set(x + 3, sy + lift - 1, Ink); }  // a crack
                Disc(new Vector2(x + vw + 2, sy + lift + 1), 2.2f, VeinR, Fade(x, sy));  // gristle
                x += vw + 4;
            }
            // the meat has grown over its far side, and over whole stretches of it
            int away = sy < y0 ? -1 : 1;
            for (int x = x0 - 10; x < x1 + 10; x++)
            {
                float edge = Art.Fbm(x * 0.03f, sy * 0.1f, 229) > 0.66f ? -6 : 1 + 4 * Art.Fbm(x * 0.12f, sy * 0.1f, 228);
                for (int d = (int)MathF.Ceiling(edge); d < 10; d++)
                {
                    int y = sy + away * d;
                    Set(x, y, d < edge + 1 ? Ink : Flesh(x, y, 0.7f, Fade(x, y)));
                }
            }
        }

        void DrawSkull(int x, int y, int sc, float fade, bool rotten)
        {
            var ramp = rotten ? RotR : BoneR;
            for (int yy = 0; yy < Skull.Length * sc; yy++)
                for (int xx = 0; xx < Skull[0].Length * sc; xx++)
                {
                    char ch = Skull[yy / sc][xx / sc];
                    if (ch == '.') continue;
                    float v = ch == 'k' ? -1 : (ch == 'b' ? 0.75f : 0.4f) - 0.25f * yy / (Skull.Length * sc);
                    Set(x + xx, y + yy, v < 0 ? Ink : Ramp(ramp, v * fade + 0.1f, x + xx, y + yy));
                }
        }

        // The game is a wound held open: a black cut, raw wet tissue, then swollen bruised skin torn ragged at its edge.
        void Wound()
        {
            const int reach = 20;
            for (int y = y0 - reach; y < y1 + reach; y++)
                for (int x = x0 - reach; x < x1 + reach; x++)
                {
                    int l = x0 - x, r = x - (x1 - 1), t = y0 - y, b = y - (y1 - 1);
                    int k = Math.Max(Math.Max(l, r), Math.Max(t, b));
                    if (k < 1) continue;
                    int side = k == t ? 0 : k == b ? 1 : k == l ? 2 : 3, along = side < 2 ? x : y;
                    float band = 11 + 8 * Art.Fbm(along * 0.07f, side * 13.1f, 161);  // swollen in places
                    if (k > band || k > band - 1.5f && Art.Hash(x, y, 163) > 0.5f) continue;
                    float f = k / band;
                    Color c;
                    if (k == 1) c = Ink;
                    else if (f < 0.55f)
                    {
                        float v = 0.15f + 1.1f * (f - 0.1f) + 0.35f * (Art.Fbm(x * 0.2f, y * 0.2f, 165) - 0.5f);
                        c = Art.Fbm(x * 0.3f, y * 0.3f, 167) > 0.7f ? RawR[3] : Ramp(RawR, v, x, y, true);
                    }
                    else c = f > 0.85f && Art.Hash(x, y, 169) > 0.6f ? Ink : Ramp(BruiseR, 0.25f + 0.6f * Art.Fbm(x * 0.08f, y * 0.08f, 171), x, y, true);
                    Set(x, y, c);
                }
        }

        // Iron hooks hold it open on every side; a spike is driven through each corner. They all weep.
        void Hooks()
        {
            for (int i = 0, x = x0 + 30; x < x1 - 20; i++, x += 48)
                foreach (int dir in new[] { -1, 1 })
                {
                    int y = dir < 0 ? y0 - 4 : y1 + 3;
                    Tube(new(x, y), new(x - 1, y + dir * 4), new(x - 3, y + dir * 9), 1, 1.4f, 1, IronR);
                    Disc(new Vector2(x, y), 1.6f, IronR, 1);
                    if (dir > 0) runs.Add((new Vector2(x, y + 3), 6 + (int)(Art.Hash(i, 5, 193) * 14)));
                }
            for (int i = 0, y = y0 + 22; y < y1 - 16; i++, y += 34)
                foreach (int dir in new[] { -1, 1 })
                {
                    int x = dir < 0 ? x0 - 4 : x1 + 3;
                    Tube(new(x, y), new(x + dir * 4, y - 1), new(x + dir * 9, y - 3), 1, 1.4f, 1, IronR);  // the hook, pulled taut outward
                    Disc(new Vector2(x, y), 1.6f, IronR, 1);
                    runs.Add((new Vector2(x, y + 3), 10 + (int)(Art.Hash(i, dir, 193) * 26)));
                }
            foreach (var (cx, cy, dx, dy) in new[] { (x0 - 1, y0 - 1, -1, -1), (x1, y0 - 1, 1, -1), (x0 - 1, y1, -1, 1), (x1, y1, 1, 1) })
            {
                var c = new Vector2(cx, cy);
                Disc(c, 6, BloodR, 1, true);
                Tube(c + new Vector2(dx, dy) * 24, c + new Vector2(dx, dy) * 13, c + new Vector2(dx, dy) * 2, 1, 2.6f, 1, IronR);
                Disc(c + new Vector2(dx, dy) * 25, 4.5f, null, 1);                // the nail's head, hammered flat
                Disc(c + new Vector2(dx, dy) * 25, 3.5f, IronR, 1);
                runs.Add((c + new Vector2(dx * 3, 4), 14 + (int)(Art.Hash(cx, cy, 195) * 24)));
            }
        }

        // Torn hollows down the outer walls, each with a skull and a tallow candle melted down onto its crown.
        void Hollows(int left, int width)
        {
            int nw = Math.Clamp(width / 4, 16, 34) & ~1, nh = (int)(nw * 1.7f), cx = left + width / 3 + (left == 0 ? 0 : width / 3);
            int sc = nw >= 28 ? 2 : 1, count = Math.Clamp((h - 20) / (nh + 18), 1, 5), gap = (h - count * nh) / (count + 1);
            for (int i = 0; i < count; i++)
            {
                int top = gap + i * (nh + gap), cy = top + nh / 2, floor = top + nh - 4;
                int candleH = 3 * sc + (int)(Art.Hash(i, left, 201) * 4 * sc);
                var flame = new Vector2(cx, floor - Skull.Length * sc - candleH - 2);
                for (int y = top - 4; y < top + nh + 4; y++)
                    for (int x = cx - nw / 2 - 4; x < cx + nw / 2 + 4; x++)
                    {
                        float ex = (x - cx) / (nw / 2f), ey = (y - cy) / (nh / 2f), rag = 0.3f * (Art.Fbm(x * 0.25f, y * 0.25f, 203) - 0.5f);
                        float e = ex * ex + ey * ey * (ey < 0 ? 1.15f : 0.85f) + rag;  // ragged, and narrower toward the top
                        if (e < 1)
                        {
                            float glow = 1 - Vector2.Distance(new Vector2(x, y), flame) / (nh * 0.6f);
                            Set(x, y, Ramp(GlowR, 0.05f + 0.75f * Math.Max(0, glow), x, y));
                        }
                        else if (e < 1.12f) Set(x, y, Ink);
                        else if (e < 1.4f && Art.Fbm(x * 0.2f, y * 0.2f, 205) > 0.42f)  // it was torn, not cut: flaps of it hang on
                            Set(x, y, Ramp(FleshR, 0.2f + 0.4f * Art.Fbm(x * 0.3f, y * 0.3f, 206), x, y));
                    }
                int sx = cx - 9 * sc / 2, sy = floor - Skull.Length * sc;
                DrawSkull(sx, sy, sc, 0.85f, Art.Hash(i, left, 208) < 0.4f);
                // the candle stands on its crown; the tallow has run down over the brow and into one eye
                int ww = 2 + 2 * sc, wx = cx - ww / 2;
                for (int y = sy - candleH; y < sy + 1; y++)
                    for (int dx = 0; dx < ww; dx++) Set(wx + dx, y, WaxR[dx == 0 ? 3 : dx == ww - 1 ? 0 : 2 - (y + dx) % 2]);
                for (int dx = -2 * sc; dx < ww + 2 * sc; dx++)
                {
                    int run = 1 + (int)(Art.Hash(dx, i, 207) * 3 * sc);
                    for (int d = 0; d < run; d++) Set(wx + dx, sy + 1 + d, WaxR[d == 0 ? 2 : 1]);
                }
                int wick = wx + ww / 2 - 1;
                Set(wick, sy - candleH - 1, Ink);
                wicks.Add(new Vector2(wick, sy - candleH - 1));
                runs.Add((new Vector2(cx + nw / 3, top + nh + 2), 10 + (int)(Art.Hash(i, left, 209) * 30)));
            }
        }

        // Old blood run down from every wound in the wall, each ending in a bead. Fresh drops follow them (see Draw).
        void Bleed()
        {
            for (int i = 0; i < 26; i++)  // and some from nowhere in particular
            {
                var at = new Vector2((int)(Art.Hash(i, 0, 211) * w), (int)(Art.Hash(i, 1, 211) * h * 0.6f));
                if (!InGame((int)at.X, (int)at.Y)) runs.Add((at, 12 + (int)(Art.Hash(i, 2, 211) * 50)));
            }
            for (int i = 0; i < runs.Count; i++)
            {
                var (top, len) = runs[i];
                int x = (int)top.X, y = (int)top.Y;
                for (int d = 0; d < len; d++) Set(x, y + d, BloodR[d == 0 ? 3 : 2 - (int)(Art.Hash(x, y + d, 213) * 1.4f)]);
                Set(x, y + len, BloodR[3]); Set(x + 1, y + len, BloodR[2]); Set(x, y + len + 1, BloodR[1]);  // the bead
                runs[i] = (top, len - 1);  // live drops stop short of it
            }
        }
    }
}
