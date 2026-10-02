using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// The Lampless Prophet's portrait for the dialog: a 64x80 bust baked per pixel like the Sexton
// (4-tone ramps, ordered dither, ink outline). Two frames: mouth closed / open.
// Override with assets/sprites/prophet_0.png and prophet_1.png (64x80).
static class Prophet
{
    public const int W = 64, H = 80;
    static Texture2D[] _frames = [];

    public static void Init() =>
        _frames = [.. Enumerable.Range(0, 2).Select(i => Art.Get($"prophet_{i}", () => Art.Make(W, H, (x, y) => Pixel(x, y, i == 1))))];

    public static void Dump(Action<string, Texture2D> save) { for (int i = 0; i < _frames.Length; i++) save($"prophet_{i}", _frames[i]); }

    // The face inside the hood, for the visitor card.
    public static void DrawFace(Rectangle dest) =>
        DrawTexturePro(_frames[0], new Rectangle(14, 12, 36, 42), dest, Vector2.Zero, 0, Color.White);

    public static void Draw(Vector2 pos, int scale, bool mouthOpen, Color tint) =>
        DrawTexturePro(_frames[mouthOpen ? 1 : 0], new Rectangle(0, 0, W, H), new Rectangle(pos.X, pos.Y, W * scale, H * scale), Vector2.Zero, 0, tint);

    static readonly Color[] Robe = [new(18, 14, 13, 255), new(30, 24, 21, 255), new(46, 37, 32, 255), new(64, 52, 44, 255)];
    static readonly Color[] Skin = [new(56, 46, 42, 255), new(92, 79, 70, 255), new(132, 116, 102, 255), new(170, 152, 134, 255)];
    static readonly Color[] Cloth = [new(58, 50, 40, 255), new(88, 78, 62, 255), new(118, 106, 86, 255), new(146, 134, 110, 255)];
    static readonly Color[] Beard = [new(70, 66, 60, 255), new(108, 102, 94, 255), new(146, 140, 130, 255), new(182, 176, 164, 255)];
    static readonly Color[] Iron = [new(30, 28, 28, 255), new(54, 50, 48, 255), new(84, 78, 74, 255), new(120, 112, 104, 255)];
    static readonly Color Blood = new(110, 12, 14, 255), BloodLit = new(156, 22, 24, 255), Dark = new(8, 5, 5, 255);

    static Color Pixel(int x, int y, bool open)
    {
        var c = Body(x, y, open);
        if (c.A > 0) return c;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
                if ((ox == 0) != (oy == 0) && Body(x + ox, y + oy, open).A > 0) return Sexton.Outline;
        return Color.Blank;
    }

    static float H01(int x, int y) => (uint)(x * 73856093 ^ y * 19349663) % 1000 / 1000f;  // stable per-pixel noise

    static Color Body(int x, int y, bool open)
    {
        float dx = x - 32, fy = y;
        var p = new Vector2(x, y);

        // the lantern he carries, never lit: iron cage, dark glass, hung from a bony hand
        if (x >= 6 && x <= 20 && y >= 58 && y <= 79)
        {
            if (y == 58 || y == 59) return x >= 11 && x <= 15 ? Sexton.Tone(Iron, 0.7f, x, y) : Color.Blank;                     // top ring
            if (y >= 60 && y <= 62 && x >= 9 && x <= 17) return Sexton.Tone(Iron, 0.55f, x, y);                                  // cap
            if (y >= 63 && y <= 76 && x >= 8 && x <= 18)
            {
                if (x == 8 || x == 18 || x == 13 || y == 63 || y == 76) return Sexton.Tone(Iron, 0.45f + (x < 13 ? 0.2f : 0), x, y);
                return y < 66 && x < 12 ? new Color(52, 50, 54, 255) : new Color(16, 15, 18, 255);                                // cold glass
            }
            if (y >= 77 && x >= 10 && x <= 16) return Sexton.Tone(Iron, 0.35f, x, y);
        }
        if (Sexton.SegDist(p, new(14, 57), new(19, 50)) < 1.6f || Vector2.Distance(p, new(14, 56)) < 2.4f)                       // knuckles on the ring
            return Sexton.Tone(Skin, 0.55f, x, y);

        // hood studded with iron nails
        float hx = dx / 22f, hy = (fy - 30) / 22f;
        bool hood = (fy < 30 && hx * hx + hy * hy < 1) || (fy >= 30 && MathF.Abs(dx) < 22 + (fy - 30) * 0.45f);
        float ox = dx / 12.5f, oy = (fy - 37) / 17f;
        bool opening = ox * ox + oy * oy < 1;
        if (hood && !opening)
        {
            for (int k = 0; k < 9; k++)
            {
                float a = MathF.PI * (1.08f + k * 0.105f);
                var nail = new Vector2(32 + MathF.Cos(a) * 18.5f, 30 + MathF.Sin(a) * 18.5f);
                if (Vector2.Distance(p, nail) < 1.3f) return Sexton.Tone(Iron, Vector2.Distance(p, nail - new Vector2(0.5f, 0.5f)) < 0.8f ? 0.95f : 0.5f, x, y);
            }
            float light = 0.5f - dx / 50 - (fy - 30) / 120 + (fy > 58 ? -0.12f : 0);
            if (MathF.Abs(MathF.Abs(dx) - 14) < 0.7f && fy > 54) light -= 0.25f;  // folds at the shoulders
            return Sexton.Tone(Robe, light, x, y);
        }
        if (!opening) return Color.Blank;

        // inside the hood: the dark first, then the face floating in it
        float fx = dx / 9.5f, ffy = (fy - 38) / 14f;
        if (fx * fx + ffy * ffy > 1) return Sexton.Tone(Robe, 0.08f, x, y);

        // the blindfold, soaked through beneath each eye
        if (fy >= 29 && fy <= 33)
        {
            bool stain = (MathF.Abs(dx + 5) < 1.6f || MathF.Abs(dx - 5) < 1.6f) && fy >= 32;
            if (stain) return fy >= 32 ? Blood : BloodLit;
            return Sexton.Tone(Cloth, 0.6f - dx / 30 - (fy - 29) / 12 + (H01(x, y) - 0.5f) * 0.3f, x, y);
        }
        if (fy > 33 && fy < 41 && (MathF.Abs(dx + 5) < 0.9f || MathF.Abs(dx - 5.5f) < 0.9f) && H01(x, 0) > 0.2f)          // tracks of it down the cheeks
            return fy < 38 ? Blood : Sexton.Tone(Skin, 0.3f, x, y);

        // beard: matted, grey, falling past the frame
        bool beard = fy >= 44 && MathF.Abs(dx) < 9.5f - (fy - 44) * 0.05f + MathF.Sin(fy * 0.9f + dx) * 0.8f;
        float mx = dx / 4f, my = (fy - 46.5f) / (open ? 2.4f : 0.9f);
        if (mx * mx + my * my < 1) return open && fy < 45.5f && x % 2 == 0 ? Sexton.Tone(Beard, 0.7f, x, y) : Dark;            // the mouth
        if (beard || (fy >= 43 && fy < 46 && MathF.Abs(dx) > 2.5f && MathF.Abs(dx) < 8))                                        // and moustache
        {
            float strand = MathF.Sin(x * 1.7f + MathF.Floor(fy / 3) * 0.6f) * 0.2f;
            return Sexton.Tone(Beard, 0.55f - dx / 28 + strand - (fy - 44) / 90, x, y);
        }

        // the face: gaunt, lit from the left, cheeks sunk under the bones
        float lightF = 0.62f - dx / 22 - (fy - 36) / 60;
        if (fy > 34 && fy < 43 && MathF.Abs(MathF.Abs(dx) - 6.5f) < 1.6f) lightF -= 0.25f;   // hollow cheeks
        if (fy > 33 && fy < 35 && MathF.Abs(dx) > 3 && MathF.Abs(dx) < 8) lightF += 0.2f;   // cheekbones
        if (MathF.Abs(dx - 0.5f) < 1 && fy > 33 && fy < 41) lightF += dx < 0.5f ? 0.25f : -0.15f;   // the nose
        if (fy > 40 && fy < 42 && MathF.Abs(dx) < 2) lightF -= 0.3f;
        return Sexton.Tone(Skin, lightF, x, y);
    }
}
