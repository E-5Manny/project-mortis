using System.Globalization;
using System.Numerics;
using System.Text;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Immediate-mode UI helpers: palette, fonts, text, buttons, number formatting.
static class Ui
{
    static Color Hex(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, (byte)255);
    // Dark crimson and tarnished gold over soot; bone is yellowed, never white.
    public static readonly Color Bg0 = Hex(0x070504), Bg1 = Hex(0x0E0908), Bg2 = Hex(0x170F0D), BgHover = Hex(0x24130F);
    public static readonly Color BgLow = Hex(0x140A08), Line = Hex(0x33211B);
    public static readonly Color Bone = Hex(0xCDBE9E), BoneDim = Hex(0x857661), BoneWhite = Hex(0xE6DAC0);
    public static readonly Color Ash = Hex(0x5E544C), AccentAsh = Hex(0x6E5E54), Crimson = Hex(0x8E1722);
    public static readonly Color Ichor = Hex(0x6A0A0D), IchorBright = Hex(0xA51E1E), CantAfford = Hex(0x6E2C25);
    public static readonly Color Candle = Hex(0xD0903A), FlameCore = Hex(0xF0CF80), Gold = Hex(0xA27A30), GoldBright = Hex(0xCDA24E);
    public static readonly Color Blood = Hex(0x4A0507);

    public static Color Lerp(Color a, Color b, float t) => new(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t), (byte)(a.A + (b.A - a.A) * t));
    public static Color Scale(Color c, float k) => new((byte)Math.Min(255, c.R * k), (byte)Math.Min(255, c.G * k), (byte)Math.Min(255, c.B * k), c.A);

    // Title = blackletter (names, headings). Body = weathered letterpress serif, drawn 2px larger
    // because its x-height is small; layout sizes stay in "nominal" pixels.
    public enum Face { Body, Title }
    const int BodyBump = 2;
    static readonly Dictionary<(Face, int), Font> Fonts = new();
    static string? _titleFont, _bodyFont;
    static readonly int[] Codepoints = [.. Enumerable.Range(32, 95), '—', '×', '·', '†', '…'];

    public static bool Clicked { get; private set; }  // a widget consumed this frame's click
    public static bool Blocked;                        // an overlay owns input; widgets are inert
    public static string? HoverText;                   // detail line for the whisper bar

    public static void Init()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");
        var sys = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string? Find(params string[] paths) => paths.FirstOrDefault(File.Exists);
        _bodyFont = Find(Path.Combine(dir, "imfelldwpica-IMFePIrm28P.ttf"), Path.Combine(sys, "georgia.ttf"));
        _titleFont = Find(Path.Combine(dir, "unifrakturmaguntia-UnifrakturMaguntia-Book.ttf"), Path.Combine(sys, "GARA.TTF")) ?? _bodyFont;
    }

    public static void BeginFrame() { Clicked = false; HoverText = null; }

    static int Real(int size, Face face) => face == Face.Body ? size + BodyBump : size;

    static Font F(int size, Face face)
    {
        if (Fonts.TryGetValue((face, size), out var f)) return f;
        var path = face == Face.Title ? _titleFont : _bodyFont;
        f = path != null ? LoadFontEx(path, Real(size, face), Codepoints, Codepoints.Length) : GetFontDefault();
        SetTextureFilter(f.Texture, TextureFilter.Bilinear);
        return Fonts[(face, size)] = f;
    }

    // --- text --- (y is the top of a `size`-pixel line; bumped body text is nudged up to sit in the same box)
    public static float Width(string s, int size, Face face = Face.Body) => MeasureTextEx(F(size, face), s, Real(size, face), 0).X;
    public static void Text(string s, float x, float y, int size, Color c, Face face = Face.Body) =>
        DrawTextEx(F(size, face), s, new Vector2(MathF.Round(x), MathF.Round(y - (Real(size, face) - size) / 2f)), Real(size, face), 0, c);
    public static void TextCentered(string s, float cx, float y, int size, Color c, Face face = Face.Body) => Text(s, cx - Width(s, size, face) / 2, y, size, c, face);
    public static void TextRight(string s, float right, float y, int size, Color c, Face face = Face.Body) => Text(s, right - Width(s, size, face), y, size, c, face);
    public static void Title(string s, float x, float y, int size, Color c) => Text(s, x, y, size, c, Face.Title);
    public static void TitleCentered(string s, float cx, float y, int size, Color c) => TextCentered(s, cx, y, size, c, Face.Title);

    // Truncate with an ellipsis to fit maxW.
    public static string Fit(string s, int size, float maxW)
    {
        if (Width(s, size) <= maxW) return s;
        while (s.Length > 1 && Width(s + "…", size) > maxW) s = s[..^1];
        return s.TrimEnd() + "…";
    }

    public static List<string> Wrap(string s, int size, float maxW)
    {
        var lines = new List<string>();
        var cur = new StringBuilder();
        foreach (var word in s.Split(' '))
        {
            var next = cur.Length == 0 ? word : cur + " " + word;
            if (cur.Length > 0 && Width(next, size) > maxW) { lines.Add(cur.ToString()); cur.Clear().Append(word); }
            else cur.Clear().Append(next);
        }
        if (cur.Length > 0) lines.Add(cur.ToString());
        return lines;
    }

    // --- shapes ---
    public static void Cross(float cx, float cy, float size, Color c)
    {
        float t = MathF.Max(1.5f, size / 4);
        DrawRectangleRec(new Rectangle(cx - t / 2, cy - size / 2, t, size), c);
        DrawRectangleRec(new Rectangle(cx - size / 2, cy - size * 0.2f - t / 2, size, t), c);
    }

    // A recess cut into the wall: the stone shows through, darkened, with a shadowed top-left and a worn lip bottom-right.
    public static void Frame(Rectangle r, Color fill)
    {
        DrawRectangleRec(r, ColorAlpha(fill, 0.78f));
        int x = (int)r.X, y = (int)r.Y, w = (int)r.Width, h = (int)r.Height;
        DrawRectangle(x, y, w, 2, ColorAlpha(Color.Black, 0.55f));
        DrawRectangle(x, y, 2, h, ColorAlpha(Color.Black, 0.45f));
        DrawRectangle(x, y + h - 1, w, 1, ColorAlpha(Bone, 0.07f));
        DrawRectangle(x + w - 1, y, 1, h, ColorAlpha(Bone, 0.05f));
    }

    // --- widgets ---
    public static bool Hover(Rectangle r) => !Blocked && CheckCollisionPointRec(GetMousePosition(), r);

    public static bool Pressed(Rectangle r)
    {
        if (!Hover(r) || Clicked || !IsMouseButtonPressed(MouseButton.Left)) return false;
        Clicked = true;
        return true;
    }

    // Framed clickable rect; caller draws contents on top.
    public static bool Button(Rectangle r, bool enabled = true, Color? fill = null)
    {
        bool hover = Hover(r);
        Frame(r, hover && enabled ? BgHover : fill ?? Bg2);
        if (hover && enabled) DrawRectangleLinesEx(r, 1, ColorAlpha(Crimson, 0.55f));
        return enabled && Pressed(r);
    }

    public static bool TextButton(Rectangle r, string label, int size = 13, bool enabled = true, bool active = false)
    {
        bool hit = Button(r, enabled, active ? BgHover : null);
        if (active) DrawRectangleLinesEx(r, 1, Gold);
        TextCentered(label, r.X + r.Width / 2, r.Y + (r.Height - size) / 2 - 1, size, enabled ? Bone : BoneDim);
        return hit;
    }

    // Hold-to-confirm: fills over `seconds` while held; returns true on completion. `held` is the caller's progress field.
    public static bool HoldButton(Rectangle r, string label, float seconds, ref float held, bool enabled, Color fillColor)
    {
        bool down = enabled && Hover(r) && IsMouseButtonDown(MouseButton.Left);
        held = down ? held + GetFrameTime() : 0;
        Frame(r, enabled && Hover(r) ? BgHover : Bg2);
        if (held > 0) DrawRectangleRec(new Rectangle(r.X + 1, r.Y + 1, (r.Width - 2) * Math.Min(1, held / seconds), r.Height - 2), ColorAlpha(fillColor, 0.55f));
        TitleCentered(label, r.X + r.Width / 2, r.Y + (r.Height - 20) / 2, 20, enabled ? Bone : BoneDim);
        if (held < seconds) return false;
        held = 0;
        return true;
    }

    // --- numbers (all invariant culture) ---
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly string[] Suffix = ["", "", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc"];

    // <10: one decimal · <1e6: 123,456 · <1e36: 1.23M … · then 1.23e36.
    // Amounts floor; costs (ceil: true) round up, so "shown cost <= shown amount" always means affordable.
    public static string Num(double v, bool ceil = false)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "—";
        if (v < 0) return "-" + Num(-v, !ceil);
        Func<double, double> round = ceil ? Math.Ceiling : Math.Floor;
        if (v < 10) { double d = round(v * 10) / 10; if (d < 10) return d.ToString("0.0", Inv); v = d; }
        if (v < 1e6) { double d = round(v); if (d < 1e6) return d.ToString("#,0", Inv); }
        int e3 = (int)Math.Floor(Math.Log10(v) / 3);
        double m = v / Math.Pow(1000, e3);
        int dec = m < 10 ? 2 : m < 100 ? 1 : 0;
        m = round(m * Math.Pow(10, dec)) / Math.Pow(10, dec);
        if (m >= 1000) { e3++; m /= 1000; dec = 2; }  // 999.99K -> 1.00M, never 1000K
        if (e3 >= Suffix.Length) return v.ToString("0.00e0", Inv);
        return m.ToString("F" + dec, Inv) + Suffix[e3];
    }

    public static string Rate(double v) => "+" + Num(v) + "/s";
    public static string Mult(double v) => "×" + (v < 100 ? v.ToString("0.00", Inv) : Num(v));
    public static string Pct(double f) => f <= 0 ? "0%" : f < 0.01 ? "<1%" : $"{(int)(f * 100)}%";

    public static string Duration(double s)
    {
        s = Math.Max(0, s);
        long t = (long)s;
        if (t < 60) return $"{t}s";
        if (t < 3600) return $"{t / 60}m {t % 60}s";
        if (t < 86400) return $"{t / 3600}h {t % 3600 / 60:00}m";
        return $"{t / 86400}d {t % 86400 / 3600:00}h";
    }

    // Largest unit only: ~45s, ~3m, ~2h, ~4d.
    public static string Payback(double s) =>
        double.IsInfinity(s) || double.IsNaN(s) ? "never" : s < 60 ? $"~{(int)s}s" : s < 3600 ? $"~{(int)(s / 60)}m" : s < 86400 ? $"~{(int)(s / 3600)}h" : $"~{(int)(s / 86400)}d";
}
