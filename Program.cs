using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;
using static Ui;

static class App
{
    static Game G = new();

    // screen state
    static int Tab, LedgerTab;
    static bool SextonView;
    static int MortifyChoice = 1;
    static float HoldMortify, HoldAbandon;
    static string? RevealWound;
    static float ScreamIn = 45;
    static int SoundTest, BricksHeard;
    static float T;  // animation clock
    static float AccountScroll;
    static float HoldImmure, HoldReset;
    static bool Hidden;

    // heart
    static readonly Vector2 HeartC = new(115, 178);
    static readonly float[] Irregular = [0.55f, 0.45f, 1.6f];
    static double SinceBeat = 99, NextInterval = 1;
    static int BeatIdx;
    static float BeatAnim = 9, ReadyGlow;
    static float GlimpseIn = 150, GlimpseT = 9;  // the face shows itself now and then, not only when ready
    static readonly List<float> Rings = new();

    // toll feedback
    static float FlashT = 9, ShakeT = 9, EmptyT = 9;
    static bool TollPulse;

    // text lines
    static readonly List<string> Log = new();
    static int WhisperIdx;
    static float WhisperT, GlitchIn = 30, GlitchT = 9;
    static int GlitchPos;
    static char GlitchChar;
    static string? Override;
    static float OverrideT;

    // saves / idle
    static double SinceSave;
    static double IdleT;
    static bool AnyKey;  // GetKeyPressed drains a queue: read once per frame

    // overlays
    static (double away, double gained, bool capped)? Away;
    static string AwayReach = "";
    static float SeqT = -1, FadeT = -1;
    static int SeqStanza;

    static readonly Rectangle CandleBox = new(12, 336, 206, 44), PoolBox = new(12, 388, 206, 44);
    static readonly string[] Tabs = ["Rites", "Sacraments", "Immure", "Wounds", "Ledger"];
    static readonly int[] TabW = [70, 96, 76, 76, 82];

    static readonly string[] Whispers =
    [
        "The hymn is late again. No one is allowed to notice.",
        "A woman on Tallow Street has been burning for sixty years.",
        "His heart missed once in the year of the flood. We do not speak of that year.",
        "The masons found a name on a femur. It was yours.",
        "Kneel longer. He is listening, or He is dying. It is the same sound.",
        "Somewhere a man falls from the tower. He gets up. He always gets up.",
        "The salt remembers every knee.",
        "Do not pray for rest. Rest is what He is holding back.",
        "The rope is warm tonight.",
        "No one has been buried in Ashkirk. The graveyard is full anyway.",
        "The wheel creaks in the key of the old hymn.",
        "You have been forgiven so many times it no longer means anything.",
        "The Engine breathes out. Four hundred people cough at once.",
        "Every candle here was once a promise.",
        "He beats {bpm} times a minute. Keep Him beating.",
        "Souls held from death: {souls}. Each one is still screaming.",
    ];

    static void Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if (args.Contains("--selftest")) { Environment.Exit(SelfTest.Run()); return; }
        if (args.Contains("--dump-sounds"))  // Mortis.exe --dump-sounds [dir]: every generated sound as WAV
        {
            Audio.Dump(args.SkipWhile(a => a != "--dump-sounds").Skip(1).FirstOrDefault() ?? "sound-dump");
            return;
        }
        if (args.Contains("--dump-art"))  // Mortis.exe --dump-art [dir]: export generated textures as PNG templates
        {
            SetTraceLogLevel(TraceLogLevel.Warning);
            SetConfigFlags(ConfigFlags.HiddenWindow);
            InitWindow(630, 520, "Mortis");
            Art.Init();
            Art.Dump(args.SkipWhile(a => a != "--dump-art").Skip(1).FirstOrDefault() ?? "art-dump");
            CloseWindow();
            return;
        }

        // One copy only: a second launch (e.g. forgot it was hidden) reveals the running one instead of fighting over the save.
        // keyed by save folder: one copy per save (test runs with MORTIS_SAVE_DIR don't collide with the real game)
        string key = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(SaveFile.Dir.ToLowerInvariant())))[..12];
        using var single = new Mutex(true, "Mortis.SingleInstance." + key, out bool first);
        using var reveal = new EventWaitHandle(false, EventResetMode.AutoReset, "Mortis.Reveal." + key);
        if (!first) { reveal.Set(); return; }

        Load();
        SetTraceLogLevel(TraceLogLevel.Warning);
        InitWindow(630, 520, "Mortis");
        SetExitKey(KeyboardKey.Null);  // Esc hides, it doesn't quit
        Ui.Init();
        Fx.Init();
        Art.Init();
        Post.Init();
        Audio.Init();
        Fx.OnSplash = () => Audio.Play("drip", 0.25f + 0.2f * Random.Shared.NextSingle(), 0.8f + 0.45f * Random.Shared.NextSingle(), 0.35f + 0.3f * Random.Shared.NextSingle());
        Panic.Start();
        WhisperIdx = Random.Shared.Next(Whispers.Length);

        var clock = Stopwatch.StartNew();
        double last = 0;
        while (!WindowShouldClose())
        {
            double now = clock.Elapsed.TotalSeconds, dt = now - last;
            last = now;

            // PC slept (or the loop stalled) for over a minute: treat it as time away.
            if (dt > 60) ShowAway(G.ApplyAway(dt)); else G.Tick(dt);

            if (Panic.Toggled()) SetHidden(!Hidden);
            if (reveal.WaitOne(0)) SetHidden(false);
            SinceSave += dt;
            if (SinceSave >= 30) Save();

            if (Hidden)
            {
                Thread.Sleep(50);
                PollInputEvents();
                continue;
            }

            SetTargetFPS(IsWindowFocused() ? G.Settings.FpsCap : 10);
            RunFrame((float)Math.Min(dt, 0.1));
        }
        Save();
        CloseWindow();
    }

    // ---------------------------------------------------------------- persistence

    static void Load()
    {
        if (SaveFile.Load<Game>() is not { } g) return;
        G = g;
        double away = (DateTime.UtcNow - G.LastSeenUtc.ToUniversalTime()).TotalSeconds;
        var result = G.ApplyAway(away);
        if (away >= 60) ShowAway(result);
    }

    static void Save()
    {
        G.LastSeenUtc = DateTime.UtcNow;
        try { SaveFile.Save(G); SinceSave = 0; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { SinceSave = 25; }  // locked / read-only / AV: retry in 5s
    }

    static void SetHidden(bool hide)
    {
        if (hide && !Panic.Registered) { MinimizeWindow(); return; }  // no global key: minimize so the taskbar can restore
        Hidden = hide;
        Audio.Hide(hide);
        if (hide) { SetWindowState(ConfigFlags.HiddenWindow); Save(); return; }
        ClearWindowState(ConfigFlags.HiddenWindow);
        if (IsWindowMinimized()) RestoreWindow();
        SetWindowFocused();
    }

    static void ShowAway((double away, double gained, bool capped) r)
    {
        Away = r;
        int rites = Data.Rites.Count(x => G.Revealed.Contains(x.Id) && G.Cost(x, 1) <= G.Dolor.Amount);
        int sacs = Data.Sacraments.Count(G.CanBuySac);
        AwayReach = rites + sacs == 0 ? "Nothing new within reach."
            : $"{rites} rite{(rites == 1 ? "" : "s")} and {sacs} sacrament{(sacs == 1 ? "" : "s")} within reach";
        AddLog($"Knelt away {Duration(r.away)}: +{Num(r.gained)}");
    }

    static void AddLog(string s) { Log.Add(s); if (Log.Count > 3) Log.RemoveAt(0); }

    // ---------------------------------------------------------------- frame

    static Color Accent => Lerp(AccentAsh, Crimson, (float)Math.Clamp(Math.Log10(G.Dolor.Run + 1) / 8, 0, 1));

    static void RunFrame(float dt)
    {
        T = (T + dt) % 3600;  // keep float precision for sine animations over long sessions
        // Modifiers don't count: Alt-Tabbing back in must not dismiss the away screen.
        var key = (KeyboardKey)GetKeyPressed();
        AnyKey = key != KeyboardKey.Null && !(key is >= KeyboardKey.LeftShift and <= KeyboardKey.RightSuper);
        bool anyInput = AnyKey || GetMouseDelta() != Vector2.Zero || IsMouseButtonPressed(MouseButton.Left) || GetMouseWheelMove() != 0;
        IdleT = anyInput ? 0 : IdleT + dt;
        bool dim = G.Settings.IdleDim && IdleT > 60;
        bool quiet = G.Settings.Quiet;

        UpdateBeat(dt);
        UpdateWhisper(dt);
        FlashT += dt; ShakeT += dt; EmptyT += dt;

        if (G.NewWound is { } nw) { RevealWound = nw; G.NewWound = null; AddLog("A wound opens: " + Data.Wound(nw).Name); Audio.Play("wound", 0.9f); Save(); }
        bool overlay = Away != null || RevealWound != null || SeqT >= 0;
        if (!overlay) Keys();

        if (SextonView) Sexton.Render(dt, G.Mortifying(), (float)G.MortifyProgress(), G.Mortifications);
        Sounds(dt);
        BeginTextureMode(Post.Target);
        ClearBackground(Bg0);
        Ui.BeginFrame();
        Ui.Blocked = overlay && FadeT < 0;

        var shake = !quiet && ShakeT < 0.12f ? new Vector2(Random.Shared.Next(-2, 3), Random.Shared.Next(-2, 3)) : Vector2.Zero;
        BeginMode2D(new Camera2D { Offset = shake, Zoom = 1 });
        Art.DrawWall();
        float glow = 0.35f + 0.08f * MathF.Sin(7 * T) * MathF.Sin(3.1f * T);
        DrawCircleGradient(new Vector2(115, 360), 150, ColorAlpha(Candle, glow * 0.55f), ColorAlpha(Candle, 0));

        LeftPanel(dt);
        switch (Tab)
        {
            case 0: RitesTab(); break;
            case 1: SacramentsTab(); break;
            case 2: ImmureTab(); break;
            case 3: WoundsTab(); break;
            case 4: LedgerTabView(); break;
        }
        TabBar();
        Header();
        Fx.Bleed(dt, 48, 16);
        Fx.Ash(dt, T, G.Dps(), quiet, dim);
        WhisperBar();
        EndMode2D();

        if (!quiet && FlashT < 0.08f) DrawRectangle(0, 0, 630, 520, ColorAlpha(Ichor, 0.12f));
        if (dim) DrawRectangle(0, 0, 630, 520, ColorAlpha(Color.Black, 0.30f));
        if (Away != null) AwayOverlay();
        else if (RevealWound != null) WoundOverlay();
        if (SeqT >= 0) ImmureSequence(dt);
        EndTextureMode();

        BeginDrawing();
        float beatFx = BeatAnim < 0.3f ? 1 - BeatAnim / 0.3f : 0;
        Post.Present(T, beatFx, FlashT, ReadyGlow, quiet, HeartC);
        EndDrawing();
    }

    static void Sounds(float dt)
    {
        Audio.Update(G.Settings, IsWindowFocused(), Math.Min(12, G.N("tallow")) / 12f);
        if (Sexton.Struck)
        {
            Audio.Play("lash", 0.75f, 0.9f + 0.2f * Random.Shared.NextSingle(), 0.45f);
            int voice = Random.Shared.Next(4);  // mostly he takes it in silence
            if (voice == 0) Audio.PlayAny("grunt_", 0.45f);
            else if (voice == 1) Audio.Play("hiss", 0.4f, 0.9f + 0.2f * Random.Shared.NextSingle());
        }
        ScreamIn -= dt;
        if (ScreamIn > 0) return;
        ScreamIn = (G.Ready() ? 30 : 60) + Random.Shared.Next(G.Ready() ? 30 : 90);
        if (!G.Settings.Screams) return;
        if (Random.Shared.Next(5) == 0) Audio.Play("scream_crowd", 0.5f);
        else Audio.PlayAny("scream_far_", 0.45f);
    }

    static void Keys()
    {
        if (IsKeyPressed(KeyboardKey.Escape)) SetHidden(true);
        if (IsKeyPressed(KeyboardKey.Space)) DoToll();
        if (IsKeyPressed(KeyboardKey.B)) G.Settings.BuyMode = G.Settings.BuyMode switch { "x1" => "x10", "x10" => "max", _ => "x1" };
        if (IsKeyPressed(KeyboardKey.S)) SextonView = !SextonView;
        if (IsKeyPressed(KeyboardKey.M)) G.Settings.SoundOn = !G.Settings.SoundOn;
        KeyboardKey[] nums = [KeyboardKey.One, KeyboardKey.Two, KeyboardKey.Three, KeyboardKey.Four, KeyboardKey.Five];
        for (int i = 0; i < 5; i++)
            if (IsKeyPressed(nums[i]) && TabOpen(i)) Tab = i;
    }

    static bool ImmureRevealed => G.Immurements > 0 || G.Dolor.Run >= 1e7;
    static bool TabOpen(int i) => i switch { 2 => ImmureRevealed, 3 => G.Wounds.Count > 0, _ => true };

    // ---------------------------------------------------------------- heart + toll

    static void UpdateBeat(float dt)
    {
        SinceBeat += dt;
        BeatAnim += dt;
        for (int i = Rings.Count - 1; i >= 0; i--) { Rings[i] += dt; if (Rings[i] > 0.8f) Rings.RemoveAt(i); }
        ReadyGlow = Math.Clamp(ReadyGlow + (G.Ready() ? dt : -dt), 0, 1);
        if (SinceBeat < NextInterval) return;
        SinceBeat = Math.Min(SinceBeat - NextInterval, 0.05);
        BeatAnim = 0;
        Rings.Add(0);
        Audio.Play("heart", (G.Mortifying() ? 0.4f : 0.8f) * (SextonView ? 0.55f : 1), G.Mortifying() ? 0.85f : 0.98f + 0.04f * Random.Shared.NextSingle());
        double period = 60 / G.Bpm();
        NextInterval = G.Ready() ? period * Irregular[BeatIdx++ % Irregular.Length] : period;
    }

    static bool OnBeat() => SinceBeat <= 0.10 || NextInterval - SinceBeat <= 0.10;

    static void DoToll()
    {
        bool onBeat = OnBeat();
        if (G.Toll(onBeat) is not { } v)
        {
            EmptyT = 0;
            Override = G.Mortifying() ? "No one is at the rope. The Sexton is at the scourge." : "The rope is still swinging.";
            Audio.Play("thunk", 0.7f);
            OverrideT = 2;
            return;
        }
        Fx.Float("+" + Num(v), HeartC.X, HeartC.Y - 60, onBeat ? GoldBright : Bone);
        FlashT = 0; ShakeT = 0; TollPulse = true;
        Audio.Play("bell", onBeat ? 1f : 0.8f);
        if (onBeat) Audio.Play("chime", 0.45f, 0.5f);
        if (onBeat) AddLog($"Tolled in time: +{Num(v)}");
    }

    static void HeartPanel(float dt)
    {
        var sigil = Lerp(Accent, BoneWhite, ReadyGlow);
        float rot = G.Settings.Quiet ? 0 : T * 0.02f * 57.3f;

        // expanding beat rings
        foreach (var r in Rings)
            DrawCircleLinesV(HeartC, 40 + 50 * (r / 0.8f), ColorAlpha(IchorBright, 0.5f * (1 - r / 0.8f)));

        // a crown of thorns in rusted iron
        DrawCircleGradient(HeartC, 78, ColorAlpha(Ichor, 0.35f), ColorAlpha(Ichor, 0));
        DrawPolyLinesEx(HeartC, 9, 72, rot, 3, ColorAlpha(Bg0, 0.8f));
        DrawPolyLinesEx(HeartC, 9, 72, rot, 2, sigil);
        DrawPolyLinesEx(HeartC, 9, 62, -rot + 20, 1, ColorAlpha(sigil, 0.35f));
        for (int i = 0; i < 9; i++)
        {
            float a = (rot + i * 40) * MathF.PI / 180, w = 0.09f;
            Vector2 Dir(float ang) => new(MathF.Cos(ang), MathF.Sin(ang));
            Art.Tri(HeartC + Dir(a) * 88, HeartC + Dir(a - w) * 70, HeartC + Dir(a + w) * 70, sigil);              // outward thorn
            Art.Tri(HeartC + Dir(a + 0.35f) * 60, HeartC + Dir(a + 0.2f - w) * 72, HeartC + Dir(a + 0.2f + w) * 72, ColorAlpha(sigil, 0.7f));  // inward barb
        }

        // the organ: throbs on the beat, breathes between, a face pushes through when He is ready (or when it wants to)
        float beat = BeatAnim < 0.18f ? MathF.Sin(MathF.PI * BeatAnim / 0.18f) : 0;
        float breathe = 0.012f * MathF.Sin(T * 1.3f), pulse = TollPulse ? 1.1f : 1;
        float sx = (1 + 0.07f * beat + breathe) * pulse, sy = (1 + 0.035f * beat - breathe) * pulse;
        TollPulse = false;
        var flesh = G.Mortifying() ? new Color(150, 128, 124, 255) : Color.White;  // starving: the colour leaves it
        Art.DrawCentered(Art.Heart, HeartC, sx, sy, flesh);
        GlimpseIn -= dt; GlimpseT += dt;
        if (GlimpseIn <= 0)
        {
            GlimpseIn = 150 + Random.Shared.Next(240);
            GlimpseT = 0;
            if (G.Settings.Screams) Audio.Play("scream_near", 0.55f, 0.95f + 0.1f * Random.Shared.NextSingle());
        }
        float glimpse = GlimpseT < 2.4f ? 0.55f * MathF.Sin(MathF.PI * GlimpseT / 2.4f) : 0;
        float face = Math.Max(ReadyGlow, glimpse);
        if (face > 0) Art.DrawCentered(Art.HeartFace, HeartC, sx, sy, ColorAlpha(Color.White, face));
        int maggots = (int)Math.Clamp((Math.Log10(G.Dolor.Run + 1) - 3) * 1.4, 0, 8);  // the rot sets in as the run ages
        Fx.Writhe(dt, T, HeartC, maggots, 30 * sx);
        Fx.Flies(T, HeartC, 3 + (G.Ready() ? 4 : 0) + Math.Min(3, G.Immurements));

        // drip and pool sit behind the heart-panel text
        float level = (float)G.PoolLevel();
        Fx.Drip(dt, HeartC + new Vector2(0, 40), Fx.PoolSurface(PoolBox, level), G.Dps());
        Fx.Pool(PoolBox, level, T);
        Fx.Candles(CandleBox, G.N("tallow"), T);

        bool overHeart = !Ui.Blocked && Vector2.Distance(GetMousePosition(), HeartC) <= 80;
        if (overHeart)
        {
            HoverText = $"Toll the Last Bell for +{Num(G.TollValue(false))}. Space also tolls. On the beat: ×1.5.";
            if (!Ui.Clicked && IsMouseButtonPressed(MouseButton.Left)) DoToll();
        }

        // toll charges
        int max = G.MaxTolls();
        float x0 = 115 - (max * 20 - 6) / 2f;
        for (int i = 0; i < max; i++)
        {
            var r = new Rectangle(x0 + i * 20, 270, 14, 14);
            if (i < G.Tolls) DrawRectangleRec(r, Gold);
            else
            {
                if (i == G.Tolls)
                {
                    float f = (float)(G.TollRegen / G.TollRegenSec());
                    DrawRectangleRec(new Rectangle(r.X, r.Y + 14 * (1 - f), 14, 14 * f), ColorAlpha(Gold, 0.45f));
                }
                DrawRectangleLinesEx(r, 1, EmptyT < 0.3f && EmptyT % 0.1f < 0.05f ? IchorBright : Line);
            }
        }
        if (G.Mortifying())
        {
            TextCentered("The rope hangs untended", 115, 292, 16, BoneDim);
            TextCentered($"the Sexton returns in {Duration(G.MortifyLeft)}", 115, 312, 13, BoneDim);
        }
        else
        {
            TextCentered("Toll: +" + Num(G.TollValue(false)), 115, 292, 16, Bone);
            TextCentered(G.Tolls < max ? $"next in {Math.Ceiling(G.TollRegenSec() - G.TollRegen)}s" : "rope ready", 115, 312, 13, BoneDim);
        }
    }

    // ---------------------------------------------------------------- left panel: the Heart or the Sexton

    static void LeftPanel(float dt)
    {
        if (SextonView) SextonPanel(dt); else HeartPanel(dt);
        Fx.Floats(dt);
        if (TextButton(new Rectangle(8, 54, 105, 18), "The Heart", 13, true, !SextonView)) SextonView = false;
        if (TextButton(new Rectangle(117, 54, 105, 18), "The Sexton", 13, true, SextonView)) SextonView = true;
        for (int i = 0; i < Log.Count; i++)
            Text(Fit(Log[i], 13, 206), 12, 438 + i * 14, 13, ColorAlpha(BoneDim, 0.6f + 0.4f * i / Math.Max(1, Log.Count - 1)));
    }

    static void SextonPanel(float dt)
    {
        bool whipping = G.Mortifying();
        DrawCircleGradient(new Vector2(115, 190), 130, ColorAlpha(Candle, 0.14f), ColorAlpha(Candle, 0));
        // the bell rope he left, still swaying
        float sway = MathF.Sin(T * 0.7f) * (whipping ? 5 : 2);
        DrawLineEx(new Vector2(30, 76), new Vector2(30 + sway, 236), 3, new Color(30, 22, 15, 255));
        DrawLineEx(new Vector2(30, 76), new Vector2(30 + sway, 236), 2, new Color(98, 78, 50, 255));
        DrawCircleV(new Vector2(30 + sway, 238), 4, new Color(98, 78, 50, 255));
        Sexton.Draw(new Vector2(43, 78), 3);
        if (Hover(new Rectangle(43, 78, 144, 192)))
            HoverText = whipping ? "He does not stop. He does not look up." : $"The Sexton of the Last Bell. {G.Mortifications} mortification{(G.Mortifications == 1 ? "" : "s")} endured.";

        TitleCentered("Mortification", 115, 274, 22, G.CanMortify() || whipping ? Bone : BoneDim);
        if (whipping)
        {
            var bar = new Rectangle(14, 304, 202, 10);
            Frame(bar, Bg0);
            DrawRectangleGradientH((int)bar.X + 1, (int)bar.Y + 1, (int)((bar.Width - 2) * G.MortifyProgress()), (int)bar.Height - 2, Blood, IchorBright);
            TextCentered($"{Duration(G.MortifyLeft)} left of {Duration(G.MortifyTotal)}", 115, 322, 13, Bone);
            TextCentered("The heart starves. No Dolor until it ends.", 115, 340, 13, BoneDim);
            var r = new Rectangle(12, 372, 206, 34);
            if (HoldButton(r, "Hold to break the vow", 1.5f, ref HoldAbandon, true, Crimson))
            {
                G.AbandonMortify();
                AddLog("The vow broken. Nothing was given.");
            }
            if (Hover(r)) HoverText = "Breaking it gives nothing, and the time is not returned.";
            return;
        }
        if (!G.CanMortify())
        {
            TextCentered("Wall yourself in once, and He will", 115, 310, 13, BoneDim);
            TextCentered("let you take up the scourge.", 115, 326, 13, BoneDim);
            return;
        }
        for (int i = 0; i < 4; i++)
        {
            var r = new Rectangle(12 + i * 52, 302, 50, 20);
            if (TextButton(r, Data.MortifyLabels[i], 13, true, MortifyChoice == i)) MortifyChoice = i;
            if (Hover(r)) HoverText = $"{Data.MortifyLabels[i]} at the scourge. Longer vows cut deeper wounds.";
        }
        double secs = Data.MortifySeconds[MortifyChoice];
        TextCentered($"Costs {Num(G.Dps() * secs)} Dolor never made", 115, 330, 13, Bone);
        var odds = Data.DepthOdds[MortifyChoice];
        string oddsText = string.Join(" · ", Enumerable.Range(0, 3).Where(d => odds[d] > 0).Select(d => $"{Data.Depths[d]} {odds[d] * 100:0}%"));
        TextCentered(Fit(oddsText, 13, 206), 115, 348, 13, BoneDim);
        var hold = new Rectangle(12, 372, 206, 34);
        if (HoldButton(hold, "Take up the scourge", 1.5f, ref HoldMortify, true, Crimson))
        {
            G.BeginMortify(MortifyChoice);
            AddLog($"The scourge, for {Data.MortifyLabels[MortifyChoice]}.");
            Save();
        }
        if (Hover(hold)) HoverText = "Production stops until it ends, even with the game closed. A wound is given at the end.";
    }

    // ---------------------------------------------------------------- header, tabs, whisper bar

    static void Header()
    {
        DrawRectangle(0, 0, 630, 48, ColorAlpha(Bg1, 0.88f));
        DrawRectangle(0, 46, 630, 2, Blood);
        Title("Mortis", 13, 3, 24, Blood);
        Title("Mortis", 12, 2, 24, Lerp(Scale(Bone, 0.8f), IchorBright, (float)Math.Clamp(Math.Log10(G.Dolor.Run + 1) / 8, 0, 1)));
        Text($"He beats {G.Bpm():0} times a minute", 12, 30, 13, BoneDim);
        TitleCentered(Num(G.Dolor.Amount), 315, 0, 30, Bone);
        TextCentered(G.Mortifying() ? "the heart starves" : Rate(G.Dps()), 315, 32, 13, G.Mortifying() ? IchorBright : BoneDim);
        if (G.MarrowEarned > 0)
        {
            string m = $"{G.MarrowEarned} Marrow {Mult(G.MarrowMult(G.MarrowEarned))}";
            TextRight(m, 618, 6, 16, Gold, Face.Title);
            Cross(618 - Width(m, 16, Face.Title) - 9, 15, 11, Gold);
        }
        TextRight($"Souls held from death: {Num(G.Souls())}", 618, 28, 13, BoneDim);
    }

    static void TabBar()
    {
        for (int i = 0, tx = 230; i < Tabs.Length; tx += TabW[i], i++)
        {
            var r = new Rectangle(tx, 48, TabW[i], 28);
            bool open = TabOpen(i), active = Tab == i;
            Frame(r, active ? Bg2 : Bg1);
            if (active) DrawRectangle((int)r.X, 74, (int)r.Width, 2, Accent);
            if (i == 2 && G.Ready()) DrawRectangleRec(r, ColorAlpha(Crimson, 0.15f + 0.12f * MathF.Sin(T * 4)));
            string label = open ? Tabs[i] : "?";
            bool hover = Hover(r) && open;
            TitleCentered(label, r.X + r.Width / 2, 52, 18, active || hover ? Bone : BoneDim);
            if (i == 1 && G.AnySacAffordable()) DrawCircle((int)(r.X + r.Width - 7), 54, 3, IchorBright);
            if (open && Pressed(r)) Tab = i;
        }

        DrawRectangle(230, 76, 400, 22, ColorAlpha(Bg1, 0.85f));
        DrawLine(230, 97, 630, 97, Line);
        switch (Tab)
        {
            case 0:
                string[] modes = ["x1", "x10", "max"], labels = ["×1", "×10", "Max"];
                for (int i = 0; i < 3; i++)
                    if (TextButton(new Rectangle(238 + i * 52, 78, 48, 18), labels[i], 13, true, G.Settings.BuyMode == modes[i]))
                        G.Settings.BuyMode = modes[i];
                TextRight("Run " + Duration(G.Stats.RunTimeSec), 618, 80, 13, BoneDim);
                break;
            case 1: Text("Harsher sacraments, approved in His name.", 238, 80, 13, BoneDim); break;
            case 2: Text("The only death He allows.", 238, 80, 13, BoneDim); break;
            case 3: Text("Kept open with salt, they bleed into Him.", 238, 80, 13, BoneDim); break;
            case 4: Text("What was given, and by whom.", 238, 80, 13, BoneDim); break;
        }
    }

    static void UpdateWhisper(float dt)
    {
        WhisperT += dt;
        if (WhisperT >= 12) { WhisperT = 0; WhisperIdx = (WhisperIdx + 1 + Random.Shared.Next(Whispers.Length - 1)) % Whispers.Length; }
        OverrideT -= dt;
        if (OverrideT <= 0) Override = null;
        GlitchT += dt;
        GlitchIn -= dt;
        if (GlitchIn <= 0)
        {
            GlitchIn = 20 + Random.Shared.Next(21);
            GlitchT = 0;
            GlitchPos = Random.Shared.Next(1000);
            GlitchChar = "#%&?!*"[Random.Shared.Next(6)];
        }
    }

    static void WhisperBar()
    {
        DrawRectangle(0, 484, 630, 36, ColorAlpha(Bg1, 0.88f));
        DrawLine(0, 484, 630, 484, Line);
        string s = Override ?? HoverText ?? Whispers[WhisperIdx]
            .Replace("{bpm}", G.Bpm().ToString("0"))
            .Replace("{souls}", Num(G.Souls()));
        if (HoverText == null && Override == null && !G.Settings.Quiet && GlitchT < 0.08f && s[GlitchPos % s.Length] != ' ')
            s = s[..(GlitchPos % s.Length)] + GlitchChar + s[(GlitchPos % s.Length + 1)..];
        Text(Fit(s, 13, 490), 12, 495, 13, Override == null && HoverText != null ? Bone : BoneDim);

        string saved = $"saved {Duration(SinceSave)} ago";
        TextRight(saved, 618, 495, 13, ColorAlpha(BoneDim, 0.7f));
        DrawCircle((int)(618 - Width(saved, 13) - 8), 502, 3, SinceSave < 1.5 ? GoldBright : Line);
    }

    // ---------------------------------------------------------------- Rites

    static double Payback(Rite r) => G.UnitRate(r) > 0 ? G.Cost(r, 1) / G.UnitRate(r) : double.PositiveInfinity;

    static void RitesTab()
    {
        var visible = Data.Rites.Where(r => G.Revealed.Contains(r.Id)).ToList();
        var best = visible.MinBy(Payback);
        double dps = G.Dps();
        float shimmer = 0.85f + 0.15f * MathF.Sin(2 * T);

        for (int i = 0; i < visible.Count; i++)
        {
            var r = visible[i];
            var rect = new Rectangle(234, 100 + 64 * i, 392, 62);
            int k = G.BuyCount(r);
            double cost = G.Cost(r, k);
            bool can = cost <= G.Dolor.Amount;
            bool firstOne = G.N(r.Id) == 0;
            if (Button(rect) && G.Buy(r))
            {
                Fx.ForceDrop();
                Audio.Play("clack", 0.55f, 0.9f + 0.2f * Random.Shared.NextSingle());
                if (firstOne) AddLog("First " + r.Name);
            }
            float y = rect.Y;
            DrawRectangle(234, (int)y, 3, 62, Accent);
            Art.DrawIcon(r.Id, 240, y + 7, firstOne ? new Color(110, 100, 95, 255) : Color.White);
            Title(r.Name, 294, y + 2, 20, Bone);
            TextRight(G.N(r.Id).ToString("#,0"), 614, y + 2, 22, Bone, Face.Title);
            Text(Fit(r.Flavor, 13, 318), 294, y + 26, 13, BoneDim);

            string costText = (G.Settings.BuyMode == "x1" ? "" : $"×{k}  ") + Num(cost, ceil: true) + " Dolor";
            Text(costText, 294, y + 44, 13, can ? Scale(Bone, shimmer) : CantAfford);
            string stats = $"{Rate(G.RiteRate(r))} · {Pct(dps > 0 ? G.RiteRate(r) / dps : 0)} · {Ui.Payback(Payback(r))}";
            TextRight(stats, 614, y + 44, 13, BoneDim);
            if (r == best) Cross(614 - Width(stats, 13) - 10, y + 51, 9, Gold);

            if (Hover(rect))
                HoverText = $"{r.Name}: each gives {Rate(G.UnitRate(r))}. ×{k} costs {Num(cost, ceil: true)}. B cycles ×1/×10/Max.";
        }

        if (Data.Rites.FirstOrDefault(r => !G.Revealed.Contains(r.Id)) is { } next)
        {
            var rect = new Rectangle(234, 100 + 64 * visible.Count, 392, 62);
            Frame(rect, Bg1);
            Art.DrawIcon(next.Id, 240, rect.Y + 7, new Color(0, 0, 0, 230));  // a shape under the shroud
            Title("???", 294, rect.Y + 2, 20, BoneDim);
            Text(Fit($"An unseen rite. Revealed at {Num(0.5 * next.BaseCost, ceil: true)} Dolor this run.", 13, 318), 294, rect.Y + 26, 13, BoneDim);
        }
    }

    // ---------------------------------------------------------------- Sacraments

    static void SacramentsTab()
    {
        var tiles = Data.Sacraments.Where(G.SacVisible).ToList();
        var locked = Data.Sacraments.FirstOrDefault(s => !G.SacVisible(s));
        float shimmer = 0.85f + 0.15f * MathF.Sin(2 * T);

        for (int i = 0; i <= tiles.Count; i++)
        {
            var rect = new Rectangle(i % 2 == 0 ? 234 : 432, 100 + 63 * (i / 2), 196, 60);
            if (i == tiles.Count)
            {
                if (locked == null) break;
                Frame(rect, Bg1);
                Title("???", rect.X + 8, rect.Y + 2, 18, BoneDim);
                Text(Fit(locked.Hint, 13, 180), rect.X + 8, rect.Y + 26, 13, BoneDim);
                break;
            }
            var s = tiles[i];
            if (G.Has(s.Id))
            {
                Frame(rect, Bg0);
                Title(s.Name, rect.X + 8, rect.Y + 2, 18, BoneDim);
                Text(Fit(s.Effect, 13, 170), rect.X + 8, rect.Y + 26, 13, ColorAlpha(BoneDim, 0.7f));
                Text("Taken", rect.X + 8, rect.Y + 42, 13, ColorAlpha(BoneDim, 0.6f));
                Cross(rect.X + 182, rect.Y + 32, 12, ColorAlpha(Gold, 0.6f));
                if (Hover(rect)) HoverText = $"{s.Name}: {s.Effect} Taken.";
                continue;
            }
            bool can = G.CanBuySac(s);
            if (Button(rect) && G.BuySac(s)) { AddLog("Sacrament taken: " + s.Name); Audio.Play("chime", 0.7f); }
            Title(s.Name, rect.X + 8, rect.Y + 2, 18, Bone);
            Text(Fit(s.Effect, 13, 180), rect.X + 8, rect.Y + 26, 13, BoneDim);
            Text(Num(s.Cost, ceil: true) + " Dolor", rect.X + 8, rect.Y + 42, 13, can ? Scale(Bone, shimmer) : CantAfford);
            if (Hover(rect)) HoverText = $"{s.Name}: {s.Effect}";
        }
    }

    // ---------------------------------------------------------------- Immure

    static void ImmureTab()
    {
        int m = G.MarrowEarned, p = G.Pending();
        Title("Immurement", 250, 102, 30, Bone);
        string[] lines =
        [
            $"Marrow walled so far: {m} ({Mult(G.MarrowMult(m))})",
            $"Walled in now: +{p} Marrow (then {Mult(G.MarrowMult(m + p))})",
            $"Next Marrow at {Num(G.NextMarrowAt(), ceil: true)} lifetime Dolor",
            $"This run: {Num(G.Dolor.Run)} / {Num(Game.Gate)} Dolor",
        ];
        for (int i = 0; i < lines.Length; i++) Text(lines[i], 250, 142 + 18 * i, 13, i == 1 && G.Ready() ? BoneWhite : Bone);

        bool vigil = G.Has("S8");
        Text("Lost to the rot:", 250, 236, 13, Crimson);
        string[] lost = ["Dolor", "Every Rite", vigil ? "Every Sacrament but the Vigil" : "Every Sacrament", "Toll charges"];
        for (int i = 0; i < lost.Length; i++) Text(lost[i], 258, 256 + 16 * i, 13, BoneDim);
        Text("Carried in the bone:", 440, 236, 13, Gold);
        var kept = new List<string> { "Marrow", "Lifetime Dolor", "The Sexton's Account" };
        if (vigil) kept.Add("Vigil Unbroken");
        for (int i = 0; i < kept.Count; i++) Text(kept[i], 448, 256 + 16 * i, 13, BoneDim);

        bool can = G.CanImmure();
        if (HoldButton(new Rectangle(310, 392, 240, 48), can ? "Hold to be walled in" : G.Mortifying() ? "Not while he bleeds" : "The chamber will not take you yet", 1.5f, ref HoldImmure, can, Crimson))
            StartImmure();
        if (can && !G.Ready() && m > 0) TextCentered("You could go now. The bone would be thin.", 430, 452, 13, BoneDim);
        else TextCentered("The mortar is already wet. It always is.", 430, 452, 13, BoneDim);
    }

    static void StartImmure()
    {
        int gained = G.Immure();
        SeqStanza = G.Immurements;
        SeqT = 0;
        BricksHeard = 0;
        FadeT = -1;
        Tab = 0;
        AccountScroll = 0;
        Save();
        AddLog($"Immured. {gained} Marrow in the bone.");
    }

    static string StanzaText(int n) => n <= Data.Stanzas.Length ? Data.Stanzas[n - 1] : Data.AccountComplete;
    static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", 10 => "X", _ => "" };

    static void ImmureSequence(float dt)
    {
        const float bricks = 3, black = 3.5f, cps = 30;
        string stanza = StanzaText(SeqStanza);
        float typedEnd = black + stanza.Length / cps;
        bool input = AnyKey || IsMouseButtonPressed(MouseButton.Left);

        if (FadeT >= 0)
        {
            FadeT += dt;
            DrawRectangle(0, 0, 630, 520, ColorAlpha(Color.Black, Math.Max(0, 1 - FadeT)));
            if (FadeT >= 1) { SeqT = -1; FadeT = -1; }
            return;
        }

        SeqT += dt;
        if (input && SeqT > 1)
        {
            if (SeqT < typedEnd) SeqT = typedEnd;
            else FadeT = 0;
        }

        if (SeqT < bricks)
        {
            const int cols = 30, rows = 12;
            float bw = 630f / cols, bh = 520f / rows, filled = SeqT / bricks * rows;
            for (; BricksHeard < (int)filled; BricksHeard++) Audio.Play("brick", 0.7f, 0.85f + 0.3f * Random.Shared.NextSingle(), 0.3f + 0.4f * Random.Shared.NextSingle());
            for (int row = 0; row < rows && row < filled; row++)
            {
                int n = row + 1 <= filled ? cols + 1 : (int)((filled - row) * (cols + 1));
                float y = 520 - (row + 1) * bh, off = row % 2 == 0 ? 0 : -bw / 2;
                DrawRectangleRec(new Rectangle(0, y, Math.Min(630, off + n * bw), bh), Bg0);  // mortar hides the scene
                for (int c = 0; c < n; c++)
                {
                    byte shade = (byte)(34 + (c * 7 + row * 13) % 12);
                    DrawRectangleRec(new Rectangle(off + c * bw + 1, y + 1, bw - 2, bh - 2), new Color(shade, (byte)(shade * 0.75f), (byte)(shade * 0.7f), (byte)255));
                }
            }
            return;
        }

        DrawRectangle(0, 0, 630, 520, Color.Black);
        if (SeqT < black) return;
        int chars = (int)Math.Min(stanza.Length, (SeqT - black) * cps);
        string title = SeqStanza <= Data.Stanzas.Length ? $"The Sexton's Account, {Roman(SeqStanza)}" : "The Sexton's Account";
        TitleCentered(title, 315, 140, 34, ColorAlpha(Gold, 0.9f));
        var wrapped = Wrap(stanza, 20, 480);
        int left = chars;
        for (int i = 0; i < wrapped.Count && left > 0; i++)
        {
            string part = wrapped[i][..Math.Min(wrapped[i].Length, left)];
            Text(part, 315 - Width(wrapped[i], 20) / 2, 220 + i * 28, 20, Bone);
            left -= wrapped[i].Length + 1;
        }
        if (SeqT >= typedEnd) TextCentered("press any key", 315, 430, 13, ColorAlpha(BoneDim, 0.6f + 0.4f * MathF.Sin(T * 3)));
    }

    // ---------------------------------------------------------------- Ledger

    static void LedgerTabView()
    {
        string[] subs = ["Account", "Tally", "Settings"];
        for (int i = 0; i < 3; i++)
            if (TextButton(new Rectangle(234 + i * 132, 100, 130, 20), subs[i], 13, true, LedgerTab == i)) LedgerTab = i;

        switch (LedgerTab)
        {
            case 0: AccountView(); break;
            case 1: TallyView(); break;
            case 2: SettingsView(); break;
        }
    }

    static void AccountView()
    {
        int shown = Math.Min(G.Immurements, Data.Stanzas.Length);
        if (shown == 0) { Text("Nothing is written yet. The first stone has not been laid.", 242, 132, 13, BoneDim); return; }

        var lines = new List<(string text, bool head)>();
        for (int i = 1; i <= shown; i++)
        {
            lines.Add((Roman(i) + ".", true));
            lines.AddRange(Wrap(Data.Stanzas[i - 1], 13, 366).Select(l => (l, false)));
        }
        if (G.Immurements > Data.Stanzas.Length) lines.Add((Data.AccountComplete, true));

        float contentH = lines.Count * 15 + 8, viewH = 352;
        AccountScroll = Math.Clamp(AccountScroll - GetMouseWheelMove() * 30, 0, Math.Max(0, contentH - viewH));
        BeginScissorMode(234, 126, 392, (int)viewH);
        for (int i = 0; i < lines.Count; i++)
            Text(lines[i].text, lines[i].head ? 242 : 256, 128 + i * 15 - AccountScroll, 13, lines[i].head ? Gold : Bone, lines[i].head ? Face.Title : Face.Body);
        EndScissorMode();
    }

    static void TallyView()
    {
        float y = 128;
        foreach (var r in Data.Rites.Where(r => G.Revealed.Contains(r.Id)))
        {
            double mult = G.MultFor(r.Id) * G.AllMult();
            string line = $"{r.Name}  {Mult(mult)}  ·  {Rate(G.RiteRate(r))}";
            Text(line, 242, y, 13, Bone);
            if (Hover(new Rectangle(234, y - 1, 392, 16)))
            {
                var parts = new List<string> { $"base {Num(r.BaseProd)}/s" };
                parts.AddRange(Data.Sacraments.Where(s => G.Has(s.Id) && (s.Target == r.Id || s.Target == "all")).Select(s => $"{s.Name} {Mult(s.Mult(G))}"));
                if (G.MarrowEarned > 0) parts.Add($"Marrow {Mult(G.MarrowMult(G.MarrowEarned))}");
                HoverText = string.Join(" × ", parts);
            }
            y += 18;
        }
        y += 8;
        var st = G.Stats;
        string[] stats =
        [
            $"All production {Mult(G.AllMult())}   ·   Tolls {Mult(G.MultFor("toll"))}",
            $"Lifetime Dolor: {Num(G.Dolor.AllTime)}",
            $"Best rate: {Rate(st.BestDps)}",
            $"Tolls rung: {st.TollsTotal:#,0}  ({st.TollsOnBeat:#,0} in time)",
            $"Immurements: {G.Immurements}   ·   Fastest run: {(st.FastestRunSec is { } f ? Duration(f) : "none")}",
            $"This run: {Duration(st.RunTimeSec)}   ·   All time: {Duration(st.PlayTimeSec)}",
        ];
        foreach (var s in stats) { Text(s, 242, y, 13, BoneDim); y += 18; }
    }

    static void SettingsView()
    {
        var set = G.Settings;
        float y = 128;
        bool Row(string label, string value, bool active)
        {
            Text(label, 242, y + 2, 13, Bone);
            bool hit = TextButton(new Rectangle(546, y, 70, 18), value, 13, true, active);
            y += 23;
            return hit;
        }
        bool Toggle(string label, bool on) => Row(label, on ? "On" : "Off", on) ? !on : on;
        set.Quiet = Toggle("Quiet mode (no shake, flash or glitches)", set.Quiet);
        set.IdleDim = Toggle("Dim after a minute without input", set.IdleDim);
        if (Row("Frame cap while focused", set.FpsCap + " fps", false)) set.FpsCap = set.FpsCap == 30 ? 20 : 30;
        set.SoundOn = Toggle("Sound (M toggles it)", set.SoundOn);
        if (Row("Volume", $"{set.Volume * 100:0}%", false)) set.Volume = set.Volume >= 1 ? 0.25f : set.Volume + 0.25f;
        set.Screams = Toggle("Screams", set.Screams);
        set.SilentUnfocused = Toggle("Silent when the window isn't focused", set.SilentUnfocused);
        var names = Audio.Names.ToArray();
        if (Row($"Test sounds: {names[SoundTest % names.Length]}", "Play", false)) { Audio.Audition(names[SoundTest % names.Length]); SoundTest++; }
        y += 6;

        Text($"Panic key: {Panic.KeyName} hides and silences, from anywhere", 242, y, 13, BoneDim);
        if (!Panic.Registered) Text("Unavailable: another app owns it. Esc minimizes instead.", 242, y + 16, 13, CantAfford);
        y += Panic.Registered ? 18 : 34;
        Text("Esc hides · Space tolls · S Sexton · M mute · B buy mode · 1-5 tabs", 242, y, 13, BoneDim);
        y += 18;
        Text(Fit("Save: " + SaveFile.Dir, 13, 380), 242, y, 13, BoneDim);

        if (HoldButton(new Rectangle(310, 420, 240, 40), "Hold 3s to erase everything", 3f, ref HoldReset, true, Crimson))
        {
            G = new Game();
            Log.Clear();
            Tab = 0;
            Save();
        }
    }

    // ---------------------------------------------------------------- Wounds

    static Color DepthColor(int d) => d switch { 0 => BoneDim, 1 => Gold, _ => IchorBright };

    static void WoundsTab()
    {
        var owned = Data.Wounds.Where(w => G.Wounds.ContainsKey(w.Id)).ToList();
        Text($"Open: {G.OpenWounds.Count} of {Game.OpenSlots}. Click a wound to open or close it.", 242, 103, 13, BoneDim);
        for (int i = 0; i < owned.Count; i++)
        {
            var w = owned[i];
            int rank = G.Wounds[w.Id];
            bool open = G.OpenWounds.Contains(w.Id), canOpen = open || G.OpenWounds.Count < Game.OpenSlots;
            var r = new Rectangle(234, 120 + 33 * i, 392, 32);
            if (Button(r, canOpen, open ? BgHover : null)) G.ToggleWound(w.Id);
            if (open) { DrawRectangleLinesEx(r, 1, ColorAlpha(Crimson, 0.8f)); Cross(r.X + r.Width - 12, r.Y + 16, 10, Gold); }
            Title(w.Name, 242, r.Y, 16, open ? Bone : BoneDim);
            TextRight($"{Data.Depths[w.Depth]} · {Roman(rank)}", 598, r.Y + 2, 13, DepthColor(w.Depth));
            string good = w.Good(rank);
            Text(good, 242, r.Y + 17, 13, open ? Bone : BoneDim);
            if (w.Bad != null) Text("but " + w.Bad, 242 + Width(good + "   ", 13), r.Y + 17, 13, CantAfford);
            if (Hover(r)) HoverText = open ? $"{w.Name} is open. It bleeds into Him." : canOpen ? $"{w.Name} has healed over. Click to open it again." : "Three are already open. Close one first.";
        }
        if (owned.Count == 0) TextCentered("No wounds yet. Take up the scourge at the Sexton's side.", 430, 200, 13, BoneDim);
    }

    static void WoundOverlay()
    {
        var w = Data.Wound(RevealWound!);
        int rank = G.Wounds.GetValueOrDefault(w.Id, 1);
        DrawRectangle(0, 0, 630, 520, ColorAlpha(Bg0, 190 / 255f));
        var panel = new Rectangle(105, 150, 420, 220);
        Frame(panel, Bg1);
        DrawRectangleLinesEx(panel, 1, ColorAlpha(DepthColor(w.Depth), 0.6f));
        TitleCentered(rank == 1 ? "A wound is given" : "The wound deepens", 315, 164, 22, BoneDim);
        TitleCentered(w.Name, 315, 196, 32, DepthColor(w.Depth) == BoneDim ? Bone : DepthColor(w.Depth));
        TextCentered($"{Data.Depths[w.Depth]} · rank {Roman(rank)}", 315, 238, 13, DepthColor(w.Depth));
        TextCentered(w.Good(rank), 315, 262, 16, Bone);
        if (w.Bad != null) TextCentered("but " + w.Bad, 315, 284, 16, CantAfford);
        TextCentered(G.OpenWounds.Contains(w.Id) ? "It is open. It bleeds into Him." : "Three are open already. Choose in the Wounds tab.", 315, 314, 13, BoneDim);
        TextCentered("Click or press any key", 315, 344, 13, ColorAlpha(BoneDim, 0.7f));
        if (AnyKey || IsMouseButtonPressed(MouseButton.Left)) RevealWound = null;
    }

    // ---------------------------------------------------------------- offline overlay

    static void AwayOverlay()
    {
        var (away, gained, capped) = Away!.Value;
        DrawRectangle(0, 0, 630, 520, ColorAlpha(Bg0, 170 / 255f));
        var panel = new Rectangle(115, 160, 400, 200);
        Frame(panel, Bg1);
        TitleCentered("While you knelt away", 315, 170, 26, Bone);
        string[] lines =
        [
            $"Away {Duration(away)}" + (capped ? $" (capped at {G.OfflineCap() / 3600:0}h)" : ""),
            $"+{Num(gained)} Dolor",
            $"Tolls refilled: {G.Tolls}/{G.MaxTolls()}",
            AwayReach,
        ];
        for (int i = 0; i < lines.Length; i++) TextCentered(lines[i], 315, 212 + i * 22, 16, i == 1 ? Gold : BoneDim);
        TextCentered("Click or press any key", 315, 334, 13, ColorAlpha(BoneDim, 0.7f));
        if (AnyKey || IsMouseButtonPressed(MouseButton.Left)) Away = null;
    }
}
