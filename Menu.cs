using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;
using static Ui;

// The main menu: the first screen on launch, and where Settings → Main menu goes back to. The game keeps ticking behind
// it, as it does while hidden; Biddings, visitors and Omens wait, because they only move in RunFrame.
static partial class App
{
    static bool InMenu = true, HasSave, Quit;
    static int MenuSel = -1;
    static Panel MenuPanel;
    static float HoldNew, MenuFace;
    static readonly Vector2 MenuHeart = new(315, 200);  // MenuArt.Heart, in layout pixels
    enum Panel { None, Settings, Dev, ConfirmNew }

    // Dev Mode is for testing, so players' builds hide it unless asked for.
#if DEBUG
    static readonly bool DevMode = true;
#else
    static readonly bool DevMode = Environment.GetEnvironmentVariable("MORTIS_DEV") == "1";
#endif
    static readonly string Version = "v" + typeof(App).Assembly.GetName().Version?.ToString(3);

    record MenuItem(string Label, bool Enabled, string Hint, Action Act);

    static List<MenuItem> MenuItems()
    {
        var items = new List<MenuItem>
        {
            new("Continue", HasSave, "Back to the rope. He kept beating while you were gone.", Continue),
            new("New Game", true, "Begin again from the first stone.", () => { if (HasSave) { MenuPanel = Panel.ConfirmNew; HoldNew = 0; } else NewGame(); }),
            new("Settings", true, "Sound, the window, and what may interrupt you.", () => MenuPanel = Panel.Settings),
        };
        if (DevMode) items.Add(new("Dev Mode", true, "Cheats for testing. F1 opens them over the game too.", () => MenuPanel = Panel.Dev));
        items.Add(new("Exit", true, "Close the game. The heart beats on without you.", () => Quit = true));
        return items;
    }

    static void MenuFrame(float dt)
    {
        T = (T + dt) % 3600;
        bool quiet = G.Settings.Quiet;
        UpdateBeat(dt);
        Audio.Update(G.Settings, IsWindowFocused(), 0.25f);
        if (IsKeyPressed(KeyboardKey.Escape)) { if (MenuPanel != Panel.None) MenuPanel = Panel.None; else if (HasSave) Continue(); }  // Esc backs out, then resumes
        if (IsKeyPressed(KeyboardKey.M)) G.Settings.SoundOn = !G.Settings.SoundOn;
        if (IsKeyPressed(KeyboardKey.F11)) { G.Settings.Fullscreen = !G.Settings.Fullscreen; ApplyWindow(); }
        if (MenuPanel != Panel.None && (IsKeyPressed(KeyboardKey.Backspace) || IsMouseButtonPressed(MouseButton.Right))) MenuPanel = Panel.None;

        var items = MenuItems();
        if (MenuSel < 0 || MenuSel >= items.Count || !items[MenuSel].Enabled) MenuSel = items.FindIndex(i => i.Enabled);
        if (MenuPanel == Panel.None)
        {
            int step = IsKeyPressed(KeyboardKey.Down) || IsKeyPressed(KeyboardKey.S) ? 1 : IsKeyPressed(KeyboardKey.Up) || IsKeyPressed(KeyboardKey.W) ? -1 : 0;
            if (step != 0)
                do MenuSel = (MenuSel + step + items.Count) % items.Count; while (!items[MenuSel].Enabled);
        }
        // He shows His face to whoever thinks of starting over
        bool forgetting = MenuPanel == Panel.ConfirmNew || HasSave && MenuPanel == Panel.None && items[MenuSel].Label == "New Game";
        MenuFace = Math.Clamp(MenuFace + (forgetting ? dt : -dt) * 0.8f, 0, 0.6f);

        BeginTextureMode(Post.Target);
        ClearBackground(Bg0);
        Ui.BeginFrame();
        Ui.Blocked = MenuPanel != Panel.None;
        BeginMode2D(new Camera2D { Zoom = Ui.Zoom });

        MenuArt.Draw();
        float beat = BeatAnim < 0.18f ? MathF.Sin(MathF.PI * BeatAnim / 0.18f) : 0;
        DrawCircleGradient(MenuHeart, 190, ColorAlpha(Candle, 0.08f + 0.05f * beat), ColorAlpha(Candle, 0));
        Crown(MenuHeart, Lerp(AccentAsh, Bone, 0.25f), quiet ? 0 : T * 0.02f * 57.3f);
        float breathe = 0.012f * MathF.Sin(T * 1.3f), sx = 1 + 0.07f * beat + breathe, sy = 1 + 0.035f * beat - breathe;
        Art.DrawCentered(Art.Heart, MenuHeart, sx, sy, Color.White);
        if (MenuFace > 0) Art.DrawCentered(Art.HeartFace, MenuHeart, sx, sy, ColorAlpha(Color.White, MenuFace));
        Fx.Flies(T, MenuHeart, 4);
        Fx.Ash(dt, T, G.Dps(), quiet, false);

        // the town darkens toward the floor, so the words stand out from it; its windows still show
        DrawRectangleGradientV(0, 280, 630, 80, ColorAlpha(Bg0, 0), ColorAlpha(Bg0, 0.4f));
        DrawRectangle(0, 360, 630, 160, ColorAlpha(Bg0, 0.4f));
        MenuArt.DrawWindows(T);

        TitleCentered("Mortis", 318, 19, 64, ColorAlpha(Color.Black, 0.8f));
        TitleCentered("Mortis", 317, 18, 64, Blood);
        TitleCentered("Mortis", 315, 16, 64, Scale(Bone, 0.85f));
        TextCentered("The Long Dying", 316, 85, 16, ColorAlpha(Color.Black, 0.8f));
        TextCentered("The Long Dying", 315, 84, 16, BoneDim);

        float y = 292;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var hit = new Rectangle(195, y - 2, 240, 32);
            if (it.Enabled && Hover(hit) && GetMouseDelta() != Vector2.Zero) MenuSel = i;
            bool sel = i == MenuSel && MenuPanel == Panel.None;
            if (sel)
            {
                DrawRectangleGradientH(165, (int)y + 2, 150, 26, ColorAlpha(Crimson, 0), ColorAlpha(Crimson, 0.35f));
                DrawRectangleGradientH(315, (int)y + 2, 150, 26, ColorAlpha(Crimson, 0.35f), ColorAlpha(Crimson, 0));
                float off = Width(it.Label, 26, Face.Title) / 2 + 20;
                Cross(315 - off, y + 15, 11, Gold);
                Cross(315 + off, y + 15, 11, Gold);
            }
            var col = !it.Enabled ? ColorAlpha(Ash, 0.8f) : sel ? GoldBright : Bone;
            TitleCentered(it.Label, 316, y + 1, 26, ColorAlpha(Color.Black, 0.8f));
            TitleCentered(it.Label, 315, y, 26, col);
            if (it.Enabled && Pressed(hit)) { MenuSel = i; Activate(it); }
            y += 34;
            if (it.Label == "Continue" && HasSave) { TextCentered(SaveLine(), 315, y - 6, 13, BoneDim); y += 12; }
        }
        if (MenuPanel == Panel.None && (IsKeyPressed(KeyboardKey.Enter) || IsKeyPressed(KeyboardKey.KpEnter) || IsKeyPressed(KeyboardKey.Space)))
            Activate(items[MenuSel]);

        if (MenuPanel == Panel.None && MenuSel >= 0) TextCentered(items[MenuSel].Hint, 315, 476, 13, ColorAlpha(BoneDim, 0.85f));
        Text((HasSave ? "Esc resumes · " : "") + "M mutes · F11 full screen", 12, 500, 13, ColorAlpha(BoneDim, 0.75f));
        TextRight(Version, 618, 500, 13, ColorAlpha(BoneDim, 0.75f));

        Ui.Blocked = false;
        switch (MenuPanel)
        {
            case Panel.Settings: MenuSettings(); break;
            case Panel.Dev: DevPanel(); break;
            case Panel.ConfirmNew: ConfirmNewGame(); break;
        }
        EndMode2D();
        EndTextureMode();

        BeginDrawing();
        ClearBackground(Color.Black);
        float beatFx = BeatAnim < 0.3f ? 1 - BeatAnim / 0.3f : 0;
        if (FullView) Frame.Draw(ViewAt, Ui.Zoom, T, beatFx);
        Post.Present(ViewAt, T, beatFx, 99, 0, quiet, MenuHeart);
        EndDrawing();
    }

    static void Activate(MenuItem it)
    {
        if (!it.Enabled) return;
        if (it.Label is "Settings" or "Dev Mode") Audio.Play("chime", 0.4f, 0.6f);
        it.Act();
    }

    // Under Continue: where the save stands.
    static string SaveLine() => G.Immurements > 0
        ? $"Immured {G.Immurements} time{(G.Immurements == 1 ? "" : "s")} · {G.MarrowEarned} Marrow · {Num(G.Dolor.Amount)} Dolor"
        : $"The first run · {Num(G.Dolor.Amount)} Dolor · {Duration(G.Stats.RunTimeSec)} in";

    static void Continue()
    {
        InMenu = false;
        Audio.Play("bell", 0.7f);
    }

    // A fresh Game that keeps the settings. The old save is copied aside first, never deleted.
    static void NewGame()
    {
        if (HasSave)
        {
            Save();
            try { SaveFile.KeepCopy("before-new-game"); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }  // ponytail: the .bak still holds the save until the next autosave
        }
        G = new Game { Settings = G.Settings };
        ResetScreens();
        HasSave = true;
        InMenu = false;
        MenuPanel = Panel.None;
        Save();
        Audio.Play("bell", 0.8f, 0.85f);
    }

    static void ToMenu()
    {
        Save();
        InMenu = true;
        MenuSel = 0;
        MenuPanel = Panel.None;
    }

    // Every screen and overlay back to how a new game finds it.
    static void ResetScreens()
    {
        Log.Clear();
        Tab = LedgerTab = ImmureSub = 0;
        RitesScroll = SacsScroll = AccountScroll = 0;
        SextonView = false;
        Away = null;
        RevealWound = null;
        Talk = null;
        AtDoor = null;
        VisitWait = -1;
        OmenAge = -1;
        Override = null;
        SeqT = FadeT = -1;
    }

    static Rectangle MenuPanelBox(float x, float y, float w, float h)
    {
        DrawRectangle(0, 0, 630, 520, ColorAlpha(Bg0, 0.78f));
        var p = new Rectangle(x, y, w, h);
        Frame(p, Bg1);
        DrawRectangleLinesEx(p, 1, ColorAlpha(Crimson, 0.5f));
        return p;
    }

    static bool BackButton(Rectangle r) => TextButton(r, "Back", 16);

    static void MenuSettings()
    {
        MenuPanelBox(112, 66, 406, 426);
        TitleCentered("Settings", 315, 72, 28, Bone);
        SettingsList(124, 112);
        if (BackButton(new Rectangle(255, 454, 120, 26))) MenuPanel = Panel.None;
    }

    static void ConfirmNewGame()
    {
        MenuPanelBox(115, 160, 400, 200);
        TitleCentered("Begin again?", 315, 168, 28, Bone);
        TextCentered("Dolor, Marrow, the Lattice, Wounds, Admissions", 315, 210, 13, Bone);
        TextCentered("and the Account: all of it is forgotten. He is not.", 315, 226, 13, Bone);
        TextCentered("Settings stay. A copy of the old save is kept in the save folder.", 315, 252, 13, BoneDim);
        if (HoldButton(new Rectangle(135, 300, 220, 34), "Hold to forget", 3f, ref HoldNew, true, Crimson)) NewGame();
        if (BackButton(new Rectangle(370, 300, 125, 34))) MenuPanel = Panel.None;
    }
}
