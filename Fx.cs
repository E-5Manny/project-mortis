using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Cheap procedural atmosphere: ash, candles, ichor drip + pool, floaters, vignette.
static class Fx
{
    static readonly Random Rng = new();
    static float R => (float)Rng.NextDouble();

    // --- falling ash ---
    struct Mote { public float X, Y, Speed, Phase; public int Size; }
    static readonly Mote[] Motes = new Mote[80];

    public static void Init()
    {
        for (int i = 0; i < Motes.Length; i++)
            Motes[i] = new Mote { X = R * 630, Y = R * 520, Speed = 8 + 12 * R, Phase = R * 6.28f, Size = R < 0.7f ? 1 : 2 };
    }

    public static void Ash(float dt, float t, double dps, bool quiet, bool dim)
    {
        int n = quiet ? 20 : (int)Math.Clamp(20 + 8 * Math.Log10(dps + 1), 20, 80);
        float k = dim ? 0.5f : 1;
        for (int i = 0; i < n; i++)
        {
            ref var m = ref Motes[i];
            m.Y += m.Speed * dt * k;
            if (m.Y > 520) { m.Y = -2; m.X = R * 630; }
            float x = m.X + (quiet ? 0 : 3 * MathF.Sin(t * 0.8f + m.Phase));
            DrawRectangle((int)x, (int)m.Y, m.Size, m.Size, ColorAlpha(Ui.Ash, 0.55f));
        }
    }

    // --- candles: one per Tallow Saint, max 12 ---
    public static void Candles(Rectangle box, int count, float t)
    {
        count = Math.Min(12, count);
        float baseY = box.Y + box.Height - 2;
        for (int i = 0; i < count; i++)
        {
            float phase = i * 1.7f;
            float h = 14 + (i * 37 % 7);  // 14..20, stable per candle
            float x = box.X + 8 + i * 17;
            var wax = new Color(150, 128, 96, 255);  // tallow, not church wax
            DrawRectangleRec(new Rectangle(x, baseY - h, 5, h), wax);
            DrawRectangleRec(new Rectangle(x, baseY - h, 5, 2), Ui.Scale(wax, 1.15f));
            DrawRectangleRec(new Rectangle(x + 3, baseY - h + 2, 1, h * 0.6f), Ui.Scale(wax, 0.75f));  // a run of melted fat
            float fr = 3 + 0.6f * MathF.Sin(7 * t + phase) + 0.3f * MathF.Sin(13 * t + phase) + (R - 0.5f) * 0.4f;
            var c = new Vector2(x + 2.5f, baseY - h - fr - 1);
            DrawCircleGradient(c, fr * 4, ColorAlpha(Ui.Candle, 0.18f), ColorAlpha(Ui.Candle, 0));
            DrawCircleV(c, fr, Ui.Candle);
            DrawCircleV(c + new Vector2(0, fr * 0.3f), fr * 0.5f, Ui.FlameCore);
        }
    }

    // --- ichor drip into the pool ---
    class Drop { public float Y, Size, Vel, Splash = -1; }
    static readonly List<Drop> Drops = new();
    static float _dripTimer;

    public static void ForceDrop() => Drops.Add(new Drop());
    public static Action? OnSplash;  // a drop reached the pool

    public static void Drip(float dt, Vector2 from, float surfaceY, double dps)
    {
        float perMin = (float)Math.Min(60, 2 + 6 * Math.Log10(dps + 1));
        _dripTimer += dt;
        if (_dripTimer >= 60 / perMin) { _dripTimer = 0; Drops.Add(new Drop()); }

        foreach (var d in Drops)
        {
            if (d.Splash >= 0)
            {
                d.Splash += dt;
                float a = 1 - d.Splash / 0.4f;
                float w = 4 + d.Splash * 30;
                DrawEllipseLines((int)from.X, (int)surfaceY, w, w * 0.25f, ColorAlpha(Ui.IchorBright, a * 0.7f));
                continue;
            }
            if (d.Size < 3) { d.Size += dt * 5; DrawCircleV(new Vector2(from.X, from.Y + d.Size), d.Size, Ui.Ichor); continue; }
            d.Vel += 400 * dt;
            d.Y += d.Vel * dt;
            float y = from.Y + 3 + d.Y;
            if (y >= surfaceY) { d.Splash = 0; OnSplash?.Invoke(); continue; }
            DrawCircleV(new Vector2(from.X, y), 2.5f, Ui.Ichor);
            DrawRectangleRec(new Rectangle(from.X - 1, y - 6, 2, 5), ColorAlpha(Ui.Ichor, 0.5f));
        }
        Drops.RemoveAll(d => d.Splash >= 0.4f);
    }

    public static void Pool(Rectangle box, float level, float t)
    {
        DrawRectangleLinesEx(box, 1, Ui.Line);
        float h = (box.Height - 2) * level;
        if (h < 1) return;
        float top = box.Y + box.Height - 1 - h;
        DrawRectangleGradientV((int)box.X + 1, (int)top, (int)box.Width - 2, (int)h, Ui.Ichor, Ui.Blood);
        // clots drifting in it
        for (int i = 0; i < 9; i++)
        {
            float cx = box.X + 6 + (MathF.Sin(t * 0.05f + i * 2.3f) * 0.5f + 0.5f) * (box.Width - 12);
            float cy = top + 3 + (i * 37 % 11) / 11f * Math.Max(0, h - 6);
            DrawCircleV(new Vector2(cx, cy), 1.5f + i % 3, ColorAlpha(new Color(26, 2, 4, 255), 0.85f));
        }
        // slow, thick surface
        for (int x = 1; x < box.Width - 1; x += 2)
        {
            float wy = MathF.Sin(t * 0.9f + x * 0.07f) * 1.0f;
            DrawRectangle((int)(box.X + x), (int)(top + wy - 1), 2, 2, ColorAlpha(Ui.IchorBright, 0.55f));
        }
        // a bubble rises, swells, pops
        float bp = t * 0.7f % 1, bx = box.X + 10 + (int)(t * 0.7f) * 53 % (int)(box.Width - 20);
        if (bp < 0.8f) DrawCircleLinesV(new Vector2(bx, top - 1), 1 + bp * 3, ColorAlpha(Ui.IchorBright, 0.6f));
    }

    public static float PoolSurface(Rectangle box, float level) => box.Y + box.Height - 1 - (box.Height - 2) * level;

    // --- floating "+X" numbers ---
    record struct Floater(string Text, float X, float Y, float Age, Color Color);
    static readonly List<Floater> Floaters = new();

    public static void Float(string text, float x, float y, Color c) => Floaters.Add(new Floater(text, x, y, 0, c));

    public static void Floats(float dt)
    {
        for (int i = Floaters.Count - 1; i >= 0; i--)
        {
            var f = Floaters[i] with { Age = Floaters[i].Age + dt };
            if (f.Age >= 1.2f) { Floaters.RemoveAt(i); continue; }
            Floaters[i] = f;
            float k = f.Age / 1.2f;
            Ui.TextCentered(f.Text, f.X, f.Y - 30 * k, 16, ColorAlpha(f.Color, 1 - k));
        }
    }

    // --- blood seeping from an edge: stalks swell, bead, and let go ---
    class Seep { public float X, Len, Max, Speed, DropY = -1, DropV; }
    static readonly List<Seep> Seeps = new();

    public static void Bleed(float dt, float edgeY, int count)
    {
        while (Seeps.Count < count) Seeps.Add(new Seep { X = 6 + R * 618, Max = 10 + R * 18, Speed = 0.5f + R * 1.8f, Len = R * 16 });
        foreach (var s in Seeps)
        {
            s.Len += s.Speed * dt;
            if (s.Len >= s.Max) { s.DropY = edgeY + s.Len; s.DropV = 0; s.Len = 2; s.Max = 10 + R * 18; }
            int x = (int)s.X;
            DrawRectangle(x - 2, (int)edgeY, 7, 2, Ui.Blood);                     // the stain it seeps from
            DrawRectangle(x, (int)edgeY, 3, (int)s.Len, Ui.Blood);
            DrawCircleV(new Vector2(x + 1.5f, edgeY + s.Len), 1.6f + 1.4f * s.Len / s.Max, Ui.Blood);
            DrawRectangle(x, (int)edgeY + 1, 1, (int)s.Len - 1, ColorAlpha(Ui.IchorBright, 0.35f));  // wet edge
            if (s.DropY < 0) continue;
            s.DropV += 320 * dt;
            s.DropY += s.DropV * dt;
            DrawRectangle(x, (int)s.DropY, 3, 4, ColorAlpha(Ui.Blood, 0.9f));
            if (s.DropY > edgeY + 140) s.DropY = -1;
        }
    }

    // --- flies circling the heart ---
    public static void Flies(float t, Vector2 c, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float ph = i * 2.399f;
            var p = c + new Vector2(MathF.Sin(t * 1.3f + ph) * 72 + MathF.Sin(t * 5.1f + ph * 3) * 14,
                                    MathF.Cos(t * 1.7f + ph * 1.3f) * 58 + MathF.Sin(t * 6.3f + ph) * 12);
            DrawRectangle((int)p.X, (int)p.Y, 2, 2, new Color(10, 7, 6, 255));
            if ((int)(t * 30 + i) % 2 == 0) DrawRectangle((int)p.X - 1, (int)p.Y - 2, 4, 1, ColorAlpha(Ui.BoneDim, 0.5f));
        }
    }

    // --- maggots writhing over the heart; more of them as the run rots on ---
    struct Maggot { public Vector2 Pos; public float Heading, Phase; }
    static readonly Maggot[] Maggots = Enumerable.Range(0, 12).Select(i => new Maggot
    {
        Pos = new Vector2(MathF.Cos(i * 2.1f), MathF.Sin(i * 2.1f)) * (8 + i * 2),
        Heading = i * 1.3f, Phase = i * 0.7f,
    }).ToArray();

    public static void Writhe(float dt, float t, Vector2 c, int n, float radius)
    {
        var body = new Color(214, 199, 172, 255);
        var rim = new Color(92, 70, 52, 255);
        for (int i = 0; i < Math.Min(n, Maggots.Length); i++)
        {
            ref var m = ref Maggots[i];
            m.Heading += MathF.Sin(t * 1.7f + m.Phase) * 1.8f * dt;
            if (m.Pos.Length() > radius) m.Heading = MathF.Atan2(-m.Pos.Y, -m.Pos.X) + MathF.Sin(m.Phase) * 0.6f;  // turn back onto the flesh
            var dir = new Vector2(MathF.Cos(m.Heading), MathF.Sin(m.Heading));
            m.Pos += dir * 4.5f * dt;
            var side = new Vector2(-dir.Y, dir.X);
            for (int k = 3; k >= 0; k--)
            {
                var p = c + m.Pos - dir * (k * 2.2f) + side * MathF.Sin(t * 9 + m.Phase + k * 1.4f) * 0.9f;
                float r = k == 0 ? 1.7f : 1.9f - k * 0.25f;
                DrawCircleV(p, r + 0.8f, rim);
                DrawCircleV(p, r, k == 3 ? Ui.Scale(body, 0.8f) : body);
            }
        }
    }
}
