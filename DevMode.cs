using Raylib_cs;
using static Raylib_cs.Raylib;
using static Ui;

// Dev Mode: cheats for testing faster, from the menu's Dev Mode or with F1 over the game. Debug builds, or MORTIS_DEV=1.
static partial class App
{
    static bool DevOpen;        // the panel over the game
    static string DevSaid = "";  // what the last cheat did

    // A cheat that shows something in the game (a dialog, an eye, an overlay) leaves the menu and closes the panel first.
    record Cheat(string Label, Func<string> Act, bool ToGame = false);

    static (string Group, Cheat[] Cheats)[] Cheats() =>
    [
        ("Dolor", [
            new("+1M", () => Grant(1e6)), new("+1B", () => Grant(1e9)), new("+1T", () => Grant(1e12)),
            new("+1h of rate", () => Grant(G.Dps() * 3600))]),
        ("Rites", [
            new("Reveal all", () => { G.DevRites(0); return "Every Rite revealed."; }),
            new("+10 of each", () => { G.DevRites(10); return "Ten more of every Rite."; })]),
        ("Sacraments", [
            new("Unlock all", () => { G.DevSacraments(false); return "Every Sacrament offered."; }),
            new("Buy all", () => { G.DevSacraments(true); return "Every Sacrament approved."; })]),
        ("Marrow", [
            new("+100 Marrow", () => { G.DevMarrow(100); return "+100 Marrow."; }),
            new("Whole Lattice", () => { G.DevLattice(); return "The whole Lattice, for nothing."; }),
            new("Ready to Immure", () => { G.DevReady(); return "Ready. The Immure tab is open."; })]),
        ("Rope, time", [
            new("Refill rope", () => { G.Tolls = G.MaxTolls(); G.TollRegen = 0; return "The rope is whole."; }),
            new("+1h away", () => DevAway(3600), true),
            new("+8h away", () => DevAway(8 * 3600), true)]),
        ("Wounds", [
            new("Give a Wound", () => { G.DevWound(); return G.NewWound != null ? "A wound is given." : "Every wound is at its deepest."; }, true)]),
        ("Visitors", [
            new("Prophet now", () => DevVisit(false), true),
            new("Ysmay now", () => DevVisit(true), true),
            new("Knock", DevKnock, true)]),
        ("Biddings", [
            new("Keep it", () => G.DevEndBidding(true) ? "Bidding kept." : "No Bidding is active.", true),
            new("Fail it", () => G.DevEndBidding(false) ? "Bidding failed." : "No Bidding is active.", true)]),
        ("Omens", [
            new("Open an eye", () => { SpawnOmen(); return "An eye opens in the wall."; }, true)]),
    ];

    static string Grant(double v) { G.DevGain(v); return $"+{Num(v)} Dolor."; }

    static string DevAway(double seconds)
    {
        ShowAway(G.ApplyAway(seconds));
        return $"{Duration(seconds)} passed.";
    }

    // Straight to the dialog, skipping the knock: the Prophet with any of his Biddings, Ysmay with her next chapter.
    static string DevVisit(bool wife)
    {
        var pool = wife ? Data.Chapters() : Data.Biddings.Where(b => b.By == null).ToArray();
        if (pool.Length == 0) return "No such visitor in biddings.json.";
        AtDoor = wife ? pool[Math.Min(G.WifeKept.Count, pool.Length - 1)] : pool[Random.Shared.Next(pool.Length)];
        VisitWait = -1;
        OpenVisit();
        return $"{Data.Who(AtDoor)?.Name} is at the door.";
    }

    // Whoever would come next knocks now, so the card at the door can be tested.
    static string DevKnock()
    {
        if (Data.Biddings.Length == 0) return "No Biddings loaded.";
        AtDoor = G.NextBidding();
        VisitWait = 0;
        KnockIn = 0;
        return "Someone knocks.";
    }

    static void RunCheat(Cheat c)
    {
        if (c.ToGame) { InMenu = false; MenuPanel = Panel.None; DevOpen = false; HasSave = true; }
        DevSaid = c.Act();
        AddLog("Dev: " + DevSaid);
        Audio.Play("chime", 0.3f, 0.8f);
    }

    static void DevPanel()
    {
        DrawRectangle(0, 0, 630, 520, ColorAlpha(Bg0, 0.8f));
        var p = new Rectangle(30, 30, 570, 460);
        Frame(p, Bg1);
        DrawRectangleLinesEx(p, 1, ColorAlpha(Crimson, 0.5f));
        TitleCentered("Dev Mode", 315, 36, 28, Bone);
        TextCentered(InMenu ? "Cheats for testing. F1 opens them over the game too." : "Cheats for testing. F1 or Esc closes this.", 315, 68, 13, BoneDim);
        float y = 92;
        foreach (var (group, cheats) in Cheats())
        {
            Title(group, 48, y + 2, 18, Gold);
            for (int i = 0; i < cheats.Length; i++)
                if (TextButton(new Rectangle(168 + i * 106, y, 100, 24), cheats[i].Label, 13)) RunCheat(cheats[i]);
            y += 31;
        }
        TextCentered(DevSaid, 315, y + 6, 13, GoldBright);
        TextCentered($"{Num(G.Dolor.Amount)} Dolor · {G.MarrowEarned} Marrow, {G.MarrowFree()} free · {G.Immurements} Immurements", 315, y + 26, 13, BoneDim);
        if (TextButton(new Rectangle(255, 454, 120, 26), InMenu ? "Back" : "Close", 16))
        {
            if (InMenu) MenuPanel = Panel.None; else DevOpen = false;
        }
    }
}
