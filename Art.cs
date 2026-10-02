using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Generated textures, drawn at 2x with point filtering so they read as pixel art.
// A PNG with the same name in assets/sprites/ replaces the generated one (wall, heart, heart_face, rite_<id>;
// the Sexton, Prophet and Wife list their own frame names).
static class Art
{
    public const int Px = 2;  // screen pixels per art pixel
    public static Texture2D Wall, Heart, HeartFace;
    public static readonly Dictionary<string, Texture2D> Icons = new();

    public static void Init()
    {
        Wall = Get("wall", () => Make(315, 260, WallPixel));
        Heart = Get("heart", () => Make(64, 64, (x, y) => HeartPixel(x, y, false)));
        HeartFace = Get("heart_face", () => Make(64, 64, (x, y) => HeartPixel(x, y, true)));
        foreach (var r in Data.Rites) Icons[r.Id] = Get("rite_" + r.Id, () => Icon(r.Id));
        Sexton.Init();
        Prophet.Init();
        Wife.Init();
    }

    public static Texture2D Get(string name, Func<Texture2D> generate)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "sprites", name + ".png");
        var tex = File.Exists(path) ? LoadTexture(path) : generate();
        SetTextureFilter(tex, TextureFilter.Point);
        return tex;
    }

    public static Texture2D Make(int w, int h, Func<int, int, Color> px)
    {
        var data = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                data[y * w + x] = px(x, y);
        var img = GenImageColor(w, h, Color.Blank);
        var tex = LoadTextureFromImage(img);
        UnloadImage(img);
        UpdateTexture(tex, data);
        return tex;
    }

    // Writes every texture as a PNG at art resolution: a template to paint over.
    public static void Dump(string dir)
    {
        Directory.CreateDirectory(dir);
        void Save(string name, Texture2D t) { var img = LoadImageFromTexture(t); ExportImage(img, Path.Combine(dir, name + ".png")); UnloadImage(img); }
        Save("wall", Wall); Save("heart", Heart); Save("heart_face", HeartFace);
        foreach (var (id, t) in Icons) Save("rite_" + id, t);
        Sexton.Dump(Save);
        Prophet.Dump(Save);
        Wife.Dump(Save);
        Save("frame", Frame.Template());
        var icon = AppIcon(16); ExportImage(icon, Path.Combine(dir, "icon.png")); UnloadImage(icon);
        WriteIco(Path.Combine(dir, "icon.ico"));
    }

    // --- the app icon: a 16x16 Orthodox cross in tarnished gold. The ink outline is added in code. ---
    // The footrest rises on the viewer's left, as in the ☦ glyph. Override: assets/sprites/icon.png.
    // Copy the dumped icon.ico over assets/icon.ico to change the exe's icon too (the csproj embeds it).
    static readonly string[] CrossRows =
    [
        "................",
        ".......hl.......",
        ".......lm.......",
        "....hllllmmd....",
        ".......lm.......",
        ".......lm.......",
        ".hllllllllllmmm.",
        ".mmmmmmmmdddddd.",
        ".......lm.......",
        ".......lm.......",
        "....hlllm.......",
        "......llmmm.....",
        ".......lm.mmd...",
        ".......lm...r...",
        ".......md.......",
        "............r...",
    ];

    public static Image AppIcon(int size)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "sprites", "icon.png");
        Image img;
        if (File.Exists(path)) { img = LoadImage(path); ImageFormat(ref img, PixelFormat.UncompressedR8G8B8A8); }
        else
        {
            img = GenImageColor(16, 16, Color.Blank);
            bool Metal(int x, int y) => x >= 0 && y >= 0 && x < 16 && y < 16 && CrossRows[y][x] is not ('.' or 'r');
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    Color? c = CrossRows[y][x] switch
                    {
                        'h' => new Color(246, 220, 150, 255), 'l' => new Color(206, 160, 74, 255),
                        'm' => Brass, 'd' => new Color(78, 48, 22, 255), 'r' => BloodC,
                        _ => null,
                    };
                    if (c == null && (Metal(x - 1, y) || Metal(x + 1, y) || Metal(x, y - 1) || Metal(x, y + 1))) c = Ink;  // 4-way: open corners keep the bars apart at 16px
                    if (c is Color col) ImageDrawPixel(ref img, x, y, col);
                }
        }
        ImageResizeNN(ref img, size, size);  // ponytail: nearest-neighbour; an override painted larger than 16px will lose detail at 16
        return img;
    }

    public static void SetWindowIcon()
    {
        Image[] imgs = [AppIcon(16), AppIcon(32), AppIcon(48)];  // GLFW picks the closest size for title bar and taskbar
        SetWindowIcons(imgs);
        foreach (var i in imgs) UnloadImage(i);
    }

    // An .ico holding PNG entries (fine on Vista and later): header, one directory entry per size, then the PNGs.
    static void WriteIco(string path)
    {
        var pngs = new List<(int size, byte[] data)>();
        foreach (int size in new[] { 16, 32, 48, 256 })
        {
            var img = AppIcon(size);
            var tmp = Path.Combine(Path.GetTempPath(), $"mortis_icon_{size}.png");
            ExportImage(img, tmp);
            UnloadImage(img);
            pngs.Add((size, File.ReadAllBytes(tmp)));
            File.Delete(tmp);
        }
        using var w = new BinaryWriter(File.Create(path));
        w.Write((short)0); w.Write((short)1); w.Write((short)pngs.Count);
        int offset = 6 + 16 * pngs.Count;
        foreach (var (size, data) in pngs)
        {
            w.Write((byte)(size % 256)); w.Write((byte)(size % 256));  // 0 means 256
            w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
            w.Write(data.Length); w.Write(offset);
            offset += data.Length;
        }
        foreach (var (_, data) in pngs) w.Write(data);
    }

    // --- drawing ---
    public static void DrawWall() =>
        DrawTexturePro(Wall, new Rectangle(0, 0, Wall.Width, Wall.Height), new Rectangle(0, 0, 630, 520), Vector2.Zero, 0, Color.White);

    public static void DrawCentered(Texture2D t, Vector2 c, float sx, float sy, Color tint)
    {
        float w = 64 * Px * sx, h = 64 * Px * sy;  // sprites are laid out on a 64x64 art grid
        DrawTexturePro(t, new Rectangle(0, 0, t.Width, t.Height), new Rectangle(c.X - w / 2, c.Y - h / 2, w, h), Vector2.Zero, 0, tint);
    }

    public static void DrawIcon(string riteId, float x, float y, Color tint) =>
        DrawTexturePro(Icons[riteId], new Rectangle(0, 0, Icons[riteId].Width, Icons[riteId].Height), new Rectangle(x, y, 48, 48), Vector2.Zero, 0, tint);

    // --- noise ---
    internal static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return h / (float)uint.MaxValue;
        }
    }

    static float Value(float x, float y, int seed)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    internal static float Fbm(float x, float y, int seed, int octaves = 4)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int i = 0; i < octaves; i++) { sum += amp * Value(x, y, seed + i * 17); norm += amp; x *= 2; y *= 2; amp *= 0.5f; }
        return sum / norm;
    }

    internal static float Ridge(float x, float y, int seed) => 1 - MathF.Abs(2 * Fbm(x, y, seed, 3) - 1);  // 1 on the ridge lines

    static Color Rgb(float r, float g, float b, float a = 1) =>
        new((byte)Math.Clamp(r * 255, 0, 255), (byte)Math.Clamp(g * 255, 0, 255), (byte)Math.Clamp(b * 255, 0, 255), (byte)Math.Clamp(a * 255, 0, 255));

    static Vector3 Mix(Vector3 a, Vector3 b, float t) => a + (b - a) * Math.Clamp(t, 0, 1);

    // --- the wall: ashlar courses, cracks, grime streaks, old blood ---
    static readonly int[] CourseTop = BuildCourses();
    static int[] BuildCourses()
    {
        var tops = new List<int>();
        for (int y = 0, i = 0; y < 260; i++) { tops.Add(y); y += 12 + (int)(Hash(i, 0, 3) * 8); }
        return [.. tops];
    }

    static Color WallPixel(int x, int y)
    {
        int course = Array.FindLastIndex(CourseTop, t => t <= y);
        int top = CourseTop[course], height = (course + 1 < CourseTop.Length ? CourseTop[course + 1] : 260) - top;
        int blockW = 20 + (int)(Hash(course, 1, 5) * 16);
        int bx = x + (int)(Hash(course, 2, 5) * blockW);
        int block = bx / blockW, lx = bx % blockW, ly = y - top;

        float n = Fbm(x * 0.09f, y * 0.09f, 1);
        float v = 0.10f + 0.09f * n + Hash(block, course, 9) * 0.05f;
        if (ly == 1 || lx == 1) v += 0.03f;               // worn top/left lip of each block
        if (ly == height - 1 || lx == blockW - 1) v -= 0.03f;
        v *= 0.80f + 0.20f * Fbm(x * 0.35f, y * 0.025f, 7);  // vertical grime streaks
        v *= 0.82f + 0.18f * (1 - y / 260f);                  // darker toward the floor
        var col = new Vector3(v, v * 0.87f, v * 0.80f);

        float stain = Fbm(x * 0.035f, y * 0.035f, 11);
        float runs = Fbm(x * 0.6f, y * 0.04f, 13);
        float blood = Math.Max(stain - 0.62f, 0) * 3.2f + (stain > 0.55f && runs > 0.62f ? 0.35f : 0);
        col = Mix(col, new Vector3(0.20f, 0.025f, 0.03f), blood);

        if (ly == 0 || lx == 0) col *= 0.35f;                                   // mortar
        if (Ridge(x * 0.045f, y * 0.045f, 3) > 0.965f) col *= 0.45f;            // cracks
        if (Hash(x, y, 77) > 0.985f) col *= 0.6f;                                // pits
        return Rgb(col.X, col.Y, col.Z);
    }

    // --- the heart: a veined, torn organ cradled by broken ribs; optionally a face pressing out from inside ---
    static Color HeartPixel(int x, int y, bool face)
    {
        const float cx = 32, cy = 35;
        float dx = x - cx, dy = y - cy, d = MathF.Sqrt(dx * dx + dy * dy), th = MathF.Atan2(dy, dx);
        float R = 24 * (1 + 0.07f * MathF.Sin(2 * th + 0.5f) + 0.20f * (Fbm(MathF.Cos(th) * 1.6f + 5, MathF.Sin(th) * 1.6f + 5, 21) - 0.5f));
        R *= 1 - 0.22f * MathF.Pow(Math.Max(0, MathF.Sin(th)), 2);  // taper toward the apex

        // ribs: short broken arcs caging the sides, never the face
        {
            float rx = MathF.Abs(x - 32f), ry = y - 31f, rd = MathF.Sqrt(rx * rx + ry * ry), ang = MathF.Atan2(ry, rx) * 180 / MathF.PI;
            for (int k = 0; k < 3; k++)
            {
                float rr = 26.5f + k * 0.8f, a0 = -42 + k * 31, a1 = a0 + 19 + Hash(k, x < 32 ? 0 : 1, 41) * 9;
                if (ang < a0 || ang > a1 || MathF.Abs(rd - rr) > 1.9f) continue;
                if (MathF.Abs(rd - rr) > 1.15f) return Rgb(0.07f, 0.05f, 0.04f);
                float grime = 0.72f + 0.28f * Fbm(x * 0.4f, y * 0.4f, 43), lit = rd < rr ? 1.0f : 0.78f;
                return Rgb(0.64f * grime * lit, 0.58f * grime * lit, 0.45f * grime * lit);
            }
        }

        // severed vessels at the crown
        foreach (var (vx, vy, vr) in new[] { (26f, 10f, 3.6f), (37f, 9f, 3.0f) })
        {
            float ex = x - vx, ey = y - vy;
            if (ey > -3 && ey < 10 && MathF.Abs(ex - ey * 0.15f) < vr)
            {
                if (ey < -1.6f) return Rgb(0.05f, 0.0f, 0.015f);  // the open end
                float sh = 0.55f + 0.45f * (1 - MathF.Abs(ex) / vr);
                return Rgb(0.36f * sh, 0.08f * sh, 0.16f * sh);
            }
        }

        if (d > R + 1.2f) return Color.Blank;
        if (d > R) return Rgb(0.06f, 0.0f, 0.015f);  // inked outline

        float t = d / R, nz = MathF.Sqrt(Math.Max(0, 1 - t * t));
        var n = Vector3.Normalize(new Vector3(dx / R, dy / R, nz + 0.001f));
        float diff = Math.Clamp(Vector3.Dot(n, Vector3.Normalize(new Vector3(-0.5f, -0.6f, 0.65f))), 0, 1);
        float mottle = Fbm(x * 0.15f, y * 0.15f, 25);
        var col = Mix(new Vector3(0.24f, 0.02f, 0.04f), new Vector3(0.70f, 0.12f, 0.12f), diff * 0.85f + mottle * 0.3f);

        // veins: dark purple ridges, thin bright capillaries
        float vein = Ridge(x * 0.11f, y * 0.11f, 33);
        if (vein > 0.92f) col = Mix(col, new Vector3(0.22f, 0.04f, 0.15f), (vein - 0.92f) * 14);
        else if (Ridge(x * 0.25f, y * 0.25f, 35) > 0.955f) col = Mix(col, new Vector3(0.80f, 0.22f, 0.22f), 0.45f);

        if (face)
        {
            float fx = dx / 11.5f, fy = (dy + 1) / 14.5f, inFace = 1 - (fx * fx + fy * fy);
            if (inFace > 0)
            {
                col = Mix(col, new Vector3(0.58f, 0.36f, 0.32f) * (0.55f + 0.65f * diff), MathF.Min(1, inFace * 1.6f) * 0.8f);  // skin stretched thin
                foreach (var ex in new[] { -5.5f, 5.5f })
                {
                    float sx = (dx - ex) / 3.0f, sy = (dy + 3) / 2.5f, socket = sx * sx + sy * sy;
                    if (socket < 1) return Rgb(0.03f, 0.0f, 0.01f);
                    if (socket < 1.8f) col *= 0.5f;  // sunken rim
                    if (MathF.Abs(dy + 6.3f + 0.07f * (dx - ex) * (dx - ex)) < 0.55f && MathF.Abs(dx - ex) < 3.2f) col *= 0.35f;  // brow
                }
                if (MathF.Abs(MathF.Abs(dx) - 1) < 0.55f && MathF.Abs(dy - 2.5f) < 0.6f) col *= 0.3f;  // nostrils
                float mx = dx / 3.8f, my = (dy - 8) / 3.6f, mouth = mx * mx + my * my;
                if (mouth < 1) return my < -0.5f && x % 2 == 0 ? Rgb(0.70f, 0.64f, 0.50f) : Rgb(0.04f, 0.0f, 0.01f);  // teeth on the top lip
                if (mouth < 1.7f) col *= 0.55f;
            }
        }

        // a ragged tear low on the left
        var a = new Vector2(14, 41); var b = new Vector2(26, 52);
        var p = new Vector2(x, y);
        float h = Math.Clamp(Vector2.Dot(p - a, b - a) / (b - a).LengthSquared(), 0, 1);
        float gash = Vector2.Distance(p, a + (b - a) * h) - (0.8f + 1.4f * MathF.Sin(h * MathF.PI) + Fbm(x * 0.5f, y * 0.5f, 51) * 0.6f);
        if (gash < 0) return Rgb(0.07f, 0.0f, 0.015f);
        if (gash < 0.9f) col = new Vector3(0.85f, 0.25f, 0.22f);

        if (diff > 0.80f && Fbm(x * 0.3f, y * 0.3f, 61) > 0.58f) col = Mix(col, new Vector3(0.96f, 0.74f, 0.70f), 0.75f);  // wet sheen
        return Rgb(col.X, col.Y, col.Z);
    }

    // --- rite icons: 24x24 silhouettes drawn with primitives, flipped into a normal texture ---
    static readonly Color Ink = new(16, 10, 9, 255), BoneC = new(190, 174, 138, 255), BloodC = new(128, 16, 18, 255),
        Brass = new(150, 108, 44, 255), SaltC = new(205, 203, 192, 255), Flame = new(228, 168, 64, 255), Wood = new(86, 58, 38, 255);

    // raylib culls triangles by winding; drawing both orders makes any vertex order safe.
    public static void Tri(Vector2 a, Vector2 b, Vector2 c, Color col) { DrawTriangle(a, b, c, col); DrawTriangle(a, c, b, col); }

    static Texture2D Icon(string id)
    {
        var rt = LoadRenderTexture(24, 24);
        BeginTextureMode(rt);
        ClearBackground(new Color(8, 5, 4, 255));
        DrawRectangleGradientV(1, 1, 22, 22, new Color(78, 58, 44, 255), new Color(30, 21, 17, 255));  // a candle-lit niche
        switch (id)
        {
            case "kneeler":
                for (int x = 1; x < 23; x++) if (Hash(x, 0, 90) > 0.35f) DrawPixel(x, 21 + (int)(Hash(x, 1, 90) * 2), SaltC);
                DrawRectangle(5, 19, 9, 2, BoneC);                    // shin flat on the salt
                DrawLineEx(new(13, 19.5f), new(11, 13), 3.2f, BoneC);  // thigh
                DrawLineEx(new(11, 13.5f), new(13, 6), 4f, BoneC);     // torso, upright
                DrawCircle(15, 5, 2.4f, BoneC);                        // head bowed forward
                DrawLineEx(new(13, 7.5f), new(17, 10), 1.8f, BoneC);   // arms reaching to the hands
                DrawRectangle(16, 9, 2, 2, BoneC);                     // clasped hands
                DrawPixel(10, 9, Ink); DrawPixel(10, 11, Ink);         // spine knuckles
                DrawPixel(13, 21, BloodC); DrawPixel(12, 21, BloodC); DrawPixel(14, 22, BloodC);
                break;
            case "choir":
                for (int i = 0; i < 3; i++)
                {
                    int x = 2 + i * 7, top = 4 + (i == 1 ? 0 : 2);
                    Tri(new(x + 3, top), new(x, top + 6), new(x + 6, top + 6), Ink);
                    DrawRectangle(x, top + 5, 7, 22 - top - 5, Ink);
                    DrawPixel(x + 3, top + 4, BoneC);
                }
                DrawLine(3, 9, 9, 17, BloodC); DrawLine(12, 7, 17, 15, BloodC); DrawLine(19, 10, 22, 16, BloodC);
                break;
            case "tallow":
                DrawRectangle(8, 9, 8, 13, BoneC);
                DrawCircle(12, 8, 3.4f, BoneC);
                DrawPixel(11, 8, Ink); DrawPixel(13, 8, Ink); DrawPixel(12, 10, Ink);
                DrawLine(9, 12, 9, 19, Ink); DrawLine(14, 13, 14, 21, new Color(150, 136, 104, 255));
                DrawRectangle(6, 21, 12, 2, new Color(150, 136, 104, 255));         // wax pooling
                Tri(new(12, 1), new(10, 5), new(14, 5), Flame);
                DrawPixel(12, 4, new Color(250, 225, 150, 255));
                break;
            case "mason":
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        int x = 1 + c * 8 - (r % 2) * 3, y = 2 + r * 7;
                        DrawRectangle(x, y, 7, 6, BoneC);
                        DrawPixel(x + 2, y + 2, Ink); DrawPixel(x + 4, y + 2, Ink);
                        DrawLine(x + 1, y + 5, x + 6, y + 5, Ink);
                    }
                DrawRectangle(0, 22, 24, 2, Ink);
                break;
            case "wheel":
                DrawRing(new Vector2(12, 13), 7, 9, 0, 360, 24, Wood);
                for (int s = 0; s < 8; s++)
                {
                    float an = s * MathF.PI / 4;
                    DrawLineV(new(12, 13), new Vector2(12 + MathF.Cos(an) * 8, 13 + MathF.Sin(an) * 8), Wood);
                }
                DrawCircle(12, 13, 1.5f, Ink);
                Vector2[] body = [new(2, 9), new(7, 5), new(12, 3.5f), new(17, 5), new(21, 9)];
                for (int i = 0; i < body.Length - 1; i++) DrawLineEx(body[i], body[i + 1], 2.4f, BoneC);
                DrawCircle(2, 10, 2, BoneC);
                DrawPixel(13, 6, BloodC); DrawPixel(13, 7, BloodC); DrawPixel(13, 9, BloodC);
                break;
            case "engine":
                DrawRectangle(11, 1, 2, 7, Ink);
                DrawEllipse(7, 14, 4.5f, 8, Brass);
                DrawEllipse(17, 14, 4.5f, 8, Brass);
                for (int y = 9; y < 21; y += 3) { DrawLine(4, y, 10, y, Wood); DrawLine(14, y, 20, y, Wood); }
                DrawPixel(7, 7, SaltC); DrawPixel(17, 7, SaltC);
                DrawLine(10, 8, 14, 8, Ink);
                DrawPixel(7, 22, BloodC); DrawPixel(17, 22, BloodC);
                break;
            case "gibbet":
                var iron = new Color(74, 64, 58, 255);
                DrawLine(12, 0, 12, 4, iron);                                      // the chain
                DrawCircle(13, 9, 2.4f, BoneC);                                    // the head, lolling
                DrawRectangle(10, 11, 5, 7, BoneC);
                DrawLine(10, 18, 9, 20, BoneC); DrawLine(14, 18, 15, 20, BoneC);  // legs dangling
                DrawRectangleLines(6, 4, 12, 17, iron);
                for (int x = 9; x < 18; x += 3) DrawLine(x, 4, x, 20, iron);       // bars, in front of him
                DrawPixel(12, 21, BloodC); DrawPixel(12, 23, BloodC);
                break;
            case "bishop":
                DrawLine(19, 3, 19, 23, Brass);                                    // crozier
                DrawLine(19, 3, 21, 3, Brass); DrawPixel(21, 4, Brass); DrawPixel(20, 5, Brass);
                Tri(new(11, 0), new(6, 8), new(16, 8), BoneC);                     // mitre
                DrawLine(7, 7, 15, 7, Brass);
                DrawRectangle(8, 8, 6, 4, Ink);                                    // no face under it
                Tri(new(11, 11), new(4, 23), new(18, 23), new Color(92, 20, 24, 255));  // robe
                DrawEllipse(11, 17, 2, 3, Ink);                                    // the emptied chest
                DrawPixel(11, 20, BloodC); DrawPixel(10, 21, BloodC); DrawPixel(12, 22, BloodC);
                break;
        }
        EndTextureMode();
        var img = LoadImageFromTexture(rt.Texture);
        ImageFlipVertical(ref img);
        var tex = LoadTextureFromImage(img);
        UnloadImage(img);
        UnloadRenderTexture(rt);
        return tex;
    }
}
