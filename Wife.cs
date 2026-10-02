using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Ysmay's portrait for the dialog: a 64x80 bust baked per pixel like the Prophet (4-tone ramps, ordered
// dither, ink outline, lit from the left). Two frames: mouth closed / open.
// Override with assets/sprites/wife_0.png and wife_1.png (64x80).
static class Wife
{
    public const int W = 64, H = 80;
    static Texture2D[] _frames = [];

    public static void Init() =>
        _frames = [.. Enumerable.Range(0, 2).Select(i => Art.Get($"wife_{i}", () => Art.Make(W, H, (x, y) => Pixel(x, y, i == 1))))];

    public static void Dump(Action<string, Texture2D> save) { for (int i = 0; i < _frames.Length; i++) save($"wife_{i}", _frames[i]); }

    // The face in the headcloth, kerchief and all, for the visitor card.
    public static void DrawFace(Rectangle dest) =>
        DrawTexturePro(_frames[0], new Rectangle(13, 16, 36, 42), dest, Vector2.Zero, 0, Color.White);

    public static void Draw(Vector2 pos, int scale, bool mouthOpen, Color tint) =>
        DrawTexturePro(_frames[mouthOpen ? 1 : 0], new Rectangle(0, 0, W, H), new Rectangle(pos.X, pos.Y, W * scale, H * scale), Vector2.Zero, 0, tint);

    static readonly Color[] Linen = [new(46, 50, 56, 255), new(74, 80, 86, 255), new(106, 110, 112, 255), new(140, 142, 138, 255)];
    static readonly Color[] Patch = [new(54, 50, 44, 255), new(82, 76, 66, 255), new(110, 102, 88, 255), new(136, 128, 110, 255)];
    static readonly Color[] Dress = [new(16, 15, 18, 255), new(27, 25, 29, 255), new(40, 36, 40, 255), new(56, 50, 54, 255)];
    static readonly Color[] Skin = [new(64, 52, 54, 255), new(110, 94, 92, 255), new(156, 140, 132, 255), new(196, 182, 170, 255)];
    static readonly Color[] Flush = [new(88, 48, 50, 255), new(130, 74, 72, 255), new(168, 104, 96, 255), new(194, 132, 120, 255)];
    static readonly Color[] Rag = [new(96, 88, 76, 255), new(130, 122, 106, 255), new(162, 154, 136, 255), new(186, 178, 158, 255)];
    static readonly Color Hair = new(52, 50, 50, 255), HairLit = new(84, 80, 78, 255), Ring = new(70, 50, 60, 255);
    static readonly Color Blood = new(104, 14, 16, 255), Old = new(74, 22, 20, 255), Dark = new(8, 5, 5, 255), Eye = new(192, 182, 158, 255);

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

    // Half-width of the face at row y: narrow, the jaw drawn in by the years of coughing.
    static float FaceHalf(float fy)
    {
        float t = (fy - 37) / 13f;
        if (t * t >= 1) return -1;
        return 8.5f * MathF.Sqrt(1 - t * t) * (fy > 40 ? 1 - (fy - 40) * 0.035f : 1);
    }

    static Color Body(int x, int y, bool open)
    {
        float dx = x - 32, fy = y;
        var p = new Vector2(x, y);

        // the hand raised to her chin, a bony fist round a stained kerchief; the sleeve falls away to the right
        if (Sexton.SegDist(p, new(46, 60), new(53, 79)) < 3.8f && fy > 58)
        {
            if (fy < 61) return Sexton.Tone(Linen, 0.55f - (x - 44) / 12f, x, y);                                       // the frayed cuff of her shift
            return Sexton.Tone(Dress, 0.62f - (x - 46 - (fy - 60) * 0.37f) / 5f - (fy - 60) / 60, x, y);
        }
        if (Sexton.SegDist(p, new(43, 54), new(46, 60)) < 1.8f)                                                          // the wrist, all tendon
            return Sexton.Tone(Skin, 0.5f - (x - 43) / 10f, x, y);
        if (Sexton.SegDist(p, new(44, 48), new(39.5f, 46.5f)) < 1.1f)                                                    // the thumb, over the top
            return Sexton.Tone(Skin, 0.7f - (x - 39) / 14f, x, y);
        if (x >= 37 && x <= 44 && fy >= 47 && fy <= 54 && !(x == 37 && (y & 1) == 0))                                   // four fingers curled, stacked
        {
            float l = 0.66f - (x - 37) / 14f - (fy - 47) / 30;
            if ((y & 1) == 0 && x > 37) l -= 0.28f;                                                                       // the creases between them
            if (x == 37) l -= 0.2f;                                                                                          // fingertips, curled in
            return Sexton.Tone(Skin, l, x, y);
        }
        float kx = (x - 39.5f) / 3.6f, ky = (fy - 45.5f) / 2.2f;
        if (kx * kx + ky * ky < 1 || (fy >= 55 && fy <= 59 && x >= 36 && x <= 39 - (fy - 55) * 0.5f))
        {
            if ((x, y) is (38, 45) or (40, 46) or (37, 57)) return Blood;                                                // spots, coughed into it
            if ((x, y) is (41, 44) or (38, 56)) return Old;
            float l = 0.66f - (x - 36) / 14f - (fy - 44) / 16 + MathF.Sin(x * 1.3f + fy * 0.9f) * 0.12f;
            return Sexton.Tone(Rag, l, x, y);
        }

        // a few dark-grey strands escaping the headcloth at the temples
        if ((x, y) is (25, 27) or (24, 28) or (24, 29) or (23, 30) or (23, 31) or (24, 32) or (24, 33) or (40, 29) or (41, 30) or (41, 31) or (40, 32))
            return y < 30 ? HairLit : Hair;

        // the face: gaunt, pale, the fever high on the cheeks
        float half = FaceHalf(fy);
        bool face = half > 0 && MathF.Abs(dx + 0.5f) < half;
        if (face)
        {
            // eyes open, sunken, ringed, turned to the viewer's left where he kneels
            foreach (float ex in new[] { -4.5f, 4f })
            {
                float ax = dx - ex, ay = fy - 34;
                bool pupil = ax < -0.9f && ax > -1.6f;
                if (ay == 0 && MathF.Abs(ax) < 2) return pupil ? Dark : (ax > 0.5f ? Sexton.Tone(Skin, 0.25f, x, y) : Eye);
                if (ay == 1 && MathF.Abs(ax) < 1.6f) return pupil ? Dark : Sexton.Tone(Skin, 0.3f, x, y);
                if (ay == -1 && MathF.Abs(ax) < 2.2f) return Dark;                                                           // heavy lid
                if (ay == 2 && MathF.Abs(ax) < 2) return Ring;                                                               // the ring under it
                if (ay == 3 && MathF.Abs(ax) < 1.5f && ex > 0) return Ring;
                if (ay == -3 && ax > -2.5f && ax < 1.5f) return Sexton.Tone(Skin, 0.12f, x, y);  // thin brow
            }

            // the mouth: thin lips, a fleck of blood at the corner
            float m = MathF.Abs(dx + 0.5f);
            if ((fy == 44 && m < 3) || (open && fy == 45 && m < 2)) return Dark;
            if ((x, y) is (28, 45)) return Blood;
            if ((x, y) is (28, 46)) return Old;
            if (fy == (open ? 46 : 45) && m < 2) return Sexton.Tone(Flush, 0.45f - dx / 12, x, y);                         // a pale, thin lower lip

            float lightF = 0.66f - dx / 20 - (fy - 36) / 55;
            if (fy > 38 && fy < 45 && MathF.Abs(MathF.Abs(dx + 0.5f) - 6) < 1.4f) lightF -= 0.28f;                             // hollow cheeks
            if (MathF.Abs(dx - 0.5f) < 1 && fy > 34 && fy < 41) lightF += dx < 0.5f ? 0.25f : -0.2f;                          // the nose
            if (fy == 41 && MathF.Abs(dx) < 2) lightF -= 0.3f;
            if (fy == 38 && MathF.Abs(MathF.Abs(dx + 0.5f) - 5) < 1.6f)
                return Sexton.Tone(Flush, lightF - 0.2f, x, y);                                                              // the fever
            return Sexton.Tone(Skin, lightF, x, y);
        }

        // headcloth: undyed linen, faded, patched, over the head and down onto the shoulders
        float hx = dx / 17f, hy = (fy - 30) / 21f;
        bool cloth = (fy < 30 && hx * hx + hy * hy < 1) || (fy >= 30 && fy < 58 + MathF.Abs(dx) * 0.3f + MathF.Sin(x * 0.9f) && MathF.Abs(dx) < 17 + (fy - 30) * 0.3f);
        if (cloth)
        {
            float light = 0.6f - dx / 40 - (fy - 26) / 90;
            float edge = MathF.Abs(dx + 0.5f) - half;
            if (half > 0 && edge < 1) light -= 0.35f;                                                                          // shadow round the face
            else if (half > 0 && edge < 2.5f && dx < 0) light += 0.15f;                                                        // the turned-back hem
            if (fy > 48 && MathF.Abs(dx + 1) < 7) light -= 0.15f;                                                               // under the chin
            if (MathF.Abs(MathF.Abs(dx) - 12 - (fy - 40) * 0.25f) < 0.6f && fy > 40) light -= 0.25f;                       // folds
            if (x >= 15 && x <= 20 && fy >= 41 && fy <= 46)                                                                    // a patch, stitched on
                return (x == 15 || fy == 46) && (x + y) % 2 == 0 ? Linen[0] : Sexton.Tone(Patch, light + 0.05f, x, y);
            return Sexton.Tone(Linen, light + (H01(x, y) - 0.5f) * 0.12f, x, y);
        }

        // a plain dark dress and shawl at the shoulders
        if (fy >= 54 && MathF.Abs(dx) < Math.Min(31, 20 + (fy - 54) * 0.8f))
        {
            float light = 0.45f - dx / 50 - (fy - 54) / 80;
            if (MathF.Abs(dx + 2) < 0.6f && fy > 60) light -= 0.25f;                                                            // where the shawl crosses
            return Sexton.Tone(Dress, light, x, y);
        }
        return Color.Blank;
    }
}
