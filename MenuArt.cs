using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;
using static Frame;

// The main menu's backdrop: the nave of His ribs, looked up at from the floor of Ashkirk. The ribs rise as pointed arches
// and recede toward the dark where the heart hangs (drawn live over this, at Heart); each one is snapped short of the
// crown, where the ribcage was split. The bell rope runs up from the heart, two chains take its weight, and the town's
// roofs and spires crowd the floor. Its lit windows flicker live.
// The same grain, ramps and dither as the frame. assets/sprites/title.png replaces it (315×260, drawn at 2×).
static class MenuArt
{
    public const int W = 315, H = 260;
    public static readonly Vector2 Heart = new(157.5f, 100);  // art pixels
    public static readonly List<Vector2> Windows = new();    // art pixels; empty when overridden
    public static Texture2D Tex;

    public static void Init() => Tex = Art.Get("title", () => { var buf = Paint(); return Art.Make(W, H, (x, y) => buf[y * W + x]); });

    public static void Draw() => DrawTexturePro(Tex, new Rectangle(0, 0, Tex.Width, Tex.Height), new Rectangle(0, 0, 630, 520), Vector2.Zero, 0, Color.White);

    // The windows of Ashkirk: most burn steadily, some gutter, and now and then one goes out for a while.
    public static void DrawWindows(float t)
    {
        for (int i = 0; i < Windows.Count; i++)
        {
            float h = Art.Hash(i, 0, 410);
            if (MathF.Sin(t * 0.11f + h * 40) > 0.94f) continue;
            float f = 0.7f + 0.3f * MathF.Sin(t * (1.5f + 6 * h) + i) * MathF.Sin(t * 3.7f + 2 * i);
            var p = Windows[i] * Art.Px;
            DrawCircleGradient(new Vector2(p.X + 1, p.Y + 2), 6, ColorAlpha(Ui.Candle, 0.16f * f), ColorAlpha(Ui.Candle, 0));
            DrawRectangle((int)p.X, (int)p.Y, Art.Px, 2 * Art.Px, ColorAlpha(h < 0.25f ? Ui.FlameCore : Ui.Candle, f));
        }
    }

    static Color C(int r, int g, int b) => new(r, g, b, 255);
    static readonly Color[] DarkFleshR = [Ink, FleshR[0], FleshR[1], FleshR[2]];
    static readonly Color[] SootR = [Ink, StoneR[0], StoneR[1], StoneR[2]];
    static readonly Color[] RopeR = [C(40, 30, 20), C(70, 54, 34), C(98, 78, 50)];
    static readonly Vector2 Vanish = new(157.5f, 96);
    const int RibCount = 6;

    static Color[] Paint()
    {
        var buf = new Color[W * H];
        void Set(Vector2 p, Color c) { int x = (int)MathF.Round(p.X), y = (int)MathF.Round(p.Y); if (x >= 0 && y >= 0 && x < W && y < H) buf[y * W + x] = c; }
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                buf[y * W + x] = Pixel(x, y);

        // sinew and clots hang from the near ribs' undersides, in front of everything
        for (int k = 0; k < 2; k++)
        {
            var (baseY, gap, half, c, r) = RibShape(k);
            for (int ax = (int)gap + 4; ax < 170; ax++)
                foreach (int side in new[] { -1, 1 })
                {
                    if (Art.Hash(ax, k * 2 + side, 470) > 0.07f) continue;
                    float under = (r - half) * (r - half) - (ax + c) * (ax + c);
                    if (under < 0) continue;
                    var top = new Vector2(Heart.X + side * ax, baseY - MathF.Sqrt(under));
                    int len = 3 + (int)(Art.Hash(ax, side, 471) * 12);
                    for (int i = 0; i < len; i++)
                        Set(top + new Vector2(0, i), i == len - 1 ? BloodR[2] : Ramp(FleshR, 0.45f - 0.4f * i / len + 0.6f * Light(top.X, top.Y), (int)top.X, (int)top.Y + i, true));
                }
        }

        // two chains take His weight; the heart's own crown hides where they bite
        foreach (int side in new[] { -1, 1 })
        {
            var a = new Vector2(Heart.X + side * 66, -2);
            var b = new Vector2(Heart.X + side * 22, Heart.Y - 36);
            var dir = Vector2.Normalize(b - a);
            var across = new Vector2(-dir.Y, dir.X);
            for (int i = 0; i < (int)(b - a).Length(); i++)
            {
                var p = a + dir * i;
                if (i / 3 % 2 == 1) { Set(p, IronR[3]); continue; }  // a link seen edge on
                Set(p + across, IronR[2]); Set(p - across, IronR[1]);  // one seen face on, open in the middle
                if (i % 3 == 0) Set(p, IronR[2]);
            }
        }
        // the bell rope, tied to the heart, runs up to a bell out of sight
        for (int y = 0; y < Heart.Y - 30; y++)
            for (int dx = 0; dx < 2; dx++)
                Set(new Vector2(Heart.X - 0.5f + dx, y), RopeR[(y + 2 * dx) % 3]);
        return buf;
    }

    static float Light(float x, float y)
    {
        float d2 = Vector2.DistanceSquared(new Vector2(x, y), Heart);
        return 1 / (1 + d2 / (50 * 50));
    }

    // Nearest first: the two near ribs stand in front of the town, the rest behind it.
    static Color Pixel(int x, int y)
    {
        for (int k = 0; k < 2; k++) if (Rib(k, x, y) is { } c) return c;
        if (Town(x, y) is { } t) return t;
        for (int k = 2; k < RibCount; k++) if (Rib(k, x, y) is { } c) return c;
        return Insides(x, y);
    }

    // His insides, far back in the dark: muscle and vein, warmed only near the heart.
    static Color Insides(int x, int y)
    {
        float a = Art.Fbm(x * 0.015f, y * 0.015f, 430) * 6.3f;  // the grain of the muscle wanders
        float fibre = MathF.Sin((x * MathF.Cos(a) + y * MathF.Sin(a)) * 0.7f + 4 * Art.Fbm(x * 0.05f, y * 0.05f, 431));
        float v = 0.05f + 0.6f * Light(x, y) + 0.07f * fibre + 0.15f * (Art.Fbm(x * 0.06f, y * 0.06f, 432) - 0.5f);
        if (Art.Ridge(x * 0.03f, y * 0.03f, 433) > 0.97f) return Ramp(VeinR, v * 0.8f, x, y);
        return Ramp(DarkFleshR, v, x, y);
    }

    // Rib k (0 nearest) is a pointed arch scaled about the vanishing point: each side an arc centred on the far side's
    // base line. Both sides break off short of the crown, raggedly, and taper to the break.
    static Color? Rib(int k, int x, int y)
    {
        var (baseY, gap0, half0, c, r) = RibShape(k);
        if (y > baseY) return null;
        float s = MathF.Pow(0.7f, k);
        int side = x < Vanish.X ? 0 : 1;
        float ax = MathF.Abs(x + 0.5f - Vanish.X);
        float gap = gap0 + 6 * Art.Hash(k, side, 440) + 2 * Art.Hash(y, k, 441);  // a ragged break
        if (ax < gap) return null;
        float half = half0 * (0.75f + 0.5f * Art.Fbm(x * 0.08f, y * 0.08f, 444 + k)) * Math.Clamp((ax - gap) / (3 + 10 * s), 0.35f, 1);  // knotted, tapering to the break
        float e = MathF.Sqrt((ax + c) * (ax + c) + (y - baseY) * (y - baseY)) - r;
        if (MathF.Abs(e) > half + 1) return null;
        if (MathF.Abs(e) > half) return Ink;
        float lit = 0.5f - 0.5f * e / half;  // the inner face, toward the nave and the heart, catches the light
        float v = (0.04f + 0.05f * s + 0.75f * Light(x, y)) * (0.25f + 0.9f * lit * lit) + 0.16f * (Art.Fbm(x * 0.2f, y * 0.2f, 442 + k) - 0.5f);
        if (Art.Ridge(x * 0.09f, y * 0.09f, 450 + k) > 0.968f) return Ink;  // cracks
        if (Art.Fbm(x * 0.07f, y * 0.07f, 460 + k) > 0.62f) return Ramp(FleshR, v + 0.08f, x, y, true);  // meat still on it
        if (baseY - y < 40 * s + 10 && Art.Fbm(x * 0.08f, y * 0.08f, 452) > 0.52f) return Ramp(BloodR, v + 0.1f, x, y);  // where the town leans on it
        if (Art.Fbm(x * 0.06f, y * 0.06f, 454 + k) > 0.64f) return Ramp(RotR, v, x, y);
        return Ramp(BoneR, v, x, y);
    }

    // A rib's base line, the half-gap at its crown, its half-thickness, and the centre offset and radius of its arcs.
    static (float baseY, float gap, float half, float c, float r) RibShape(int k)
    {
        float s = MathF.Pow(0.7f, k);
        float baseY = Vanish.Y + (300 - Vanish.Y) * s, topY = Vanish.Y - (Vanish.Y - 4) * s;
        float w = 165 * s, h = baseY - topY, c = (h * h - w * w) / (2 * w);
        return (baseY, 4 + 14 * s, 1.5f + 5.5f * s, c, w + c);
    }

    // A house spans [X0, X1); its walls rise to Eave and its roof, a gable or a spire, Peak above that.
    record struct House(int X0, int X1, int Eave, int Peak, bool Spire, int Row);
    static readonly House[] Houses = BuildTown();

    // Checked last to first: the front row, then the cathedral, then the back row.
    static House[] BuildTown()
    {
        var list = new List<House>();
        (int lo, int hi)[] rows = [(152, 176), (186, 208)];
        void Row(int row)
        {
            for (int x = -4, i = 0; x < W; i++)
            {
                int w = 7 + (int)(Art.Hash(i, row, 420) * 10);
                int eave = rows[row].lo + (int)(Art.Hash(i, row, 421) * (rows[row].hi - rows[row].lo));
                bool spire = row == 0 && Art.Hash(i, row, 422) < 0.16f;
                int peak = spire ? 14 + (int)(Art.Hash(i, row, 423) * 20) : w / 2 + (int)(Art.Hash(i, row, 424) * 3);
                list.Add(new House(x, x + w, eave, peak, spire, row));
                x += w + (Art.Hash(i, row, 425) < 0.2f ? 2 : 0);  // an alley now and then
            }
        }
        Row(0);
        // the cathedral under the heart: a long roof between two towers, both pointing up at Him
        list.Add(new House(140, 176, 156, 14, false, 0));
        list.Add(new House(133, 143, 146, 22, true, 0));
        list.Add(new House(173, 183, 146, 22, true, 0));
        Row(1);
        return [.. list];
    }

    static Color? Town(int x, int y)
    {
        for (int i = Houses.Length - 1; i >= 0; i--)
        {
            var b = Houses[i];
            if (x < b.X0 || x >= b.X1) continue;
            float half = (b.X1 - b.X0) / 2f, off = MathF.Abs(x + 0.5f - (b.X0 + half));
            float reach = b.Spire ? half * 0.7f : half;
            float top = off < reach ? b.Eave - b.Peak * (1 - off / reach) : b.Eave;
            if (y < MathF.Floor(top)) continue;

            bool front = b.Row == 1;
            float L = Light(x, y), v = front ? 0.04f + 0.3f * L : 0.14f + 0.5f * L;
            if (y < top + 1.5f) v += front ? 0.15f : 0.25f;  // the roofline catches the heart's light
            // windows: one pixel wide, two tall, on a loose grid; some still lit
            int col = x - b.X0 - 2, row = y - b.Eave - 3;
            if (col >= 0 && col % 4 == 0 && x < b.X1 - 2 && row >= 0 && row % 6 < 2 && y < H - 4)
            {
                float hw = Art.Hash(b.X0 * 7 + col / 4, row / 6 + b.Row * 50, 426);
                if (hw < 0.24f)
                {
                    if (hw < 0.13f && row % 6 == 0) Windows.Add(new Vector2(x, y));
                    return GlowR[1];
                }
            }
            return Ramp(SootR, v, x, y);
        }
        return null;
    }
}
