// `Mortis.exe --selftest`: asserts on the rules, then a greedy-player sim that should land near the balance targets.
// Exit code = number of failures. Output: `Mortis.exe --selftest | more` (WinExe has no console of its own).
static class SelfTest
{
    static int _fails;
    static void Check(bool ok, string what) { if (!ok) { _fails++; Console.WriteLine("FAIL " + what); } }
    static bool Near(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Abs(b));

    public static int Run()
    {
        // number formatting
        Check(Ui.Num(7.56) == "7.5", "Num 7.56 -> " + Ui.Num(7.56));
        Check(Ui.Num(123456.7) == "123,456", "Num 123456.7 -> " + Ui.Num(123456.7));
        Check(Ui.Num(1234567) == "1.23M", "Num 1234567 -> " + Ui.Num(1234567));
        Check(Ui.Num(999999.7, ceil: true) == "1.00M", "Num ceil 999999.7 -> " + Ui.Num(999999.7, true));
        Check(Ui.Num(1.2345e10, ceil: true) == "12.4B", "Num ceil 1.2345e10 -> " + Ui.Num(1.2345e10, true));
        Check(Ui.Num(1e36) == "1.00e36", "Num 1e36 -> " + Ui.Num(1e36));
        Check(Ui.Duration(7500) == "2h 05m", "Duration 7500 -> " + Ui.Duration(7500));

        // rite costs
        var g = new Game();
        var kn = Data.Rite("kneeler");
        Check(Near(g.Cost(kn, 1), 15), "cost of first kneeler");
        g.Owned["kneeler"] = 3;
        Check(Near(g.Cost(kn, 2), 15 * Math.Pow(1.15, 3) + 15 * Math.Pow(1.15, 4)), "bulk cost");
        g.Dolor.Amount = g.Cost(kn, 7);
        Check(g.MaxAffordable(kn) == 7, "max affordable at exact boundary: " + g.MaxAffordable(kn));
        g.Dolor.Amount = g.Cost(kn, 7) * 0.999999;
        Check(g.MaxAffordable(kn) == 6, "max affordable just under boundary");

        // Marrow
        Check(Game.TotalFor(1e8) == 10 && Game.TotalFor(4e8) == 20 && Game.TotalFor(1e9) == 31, "Marrow totals");
        Check(Near(Game.LifetimeFor(11), 1.21e8), "next Marrow threshold");

        // offline: capped, 100% rate, tolls refilled
        var h = new Game();
        h.Owned["kneeler"] = 10;
        h.Tolls = 0;
        var (away, gained, capped) = h.ApplyAway(100_000);
        Check(capped && away == 8 * 3600, "away cap 8h");
        Check(Near(gained, 10 * 0.3 * 8 * 3600), "away gain = dps × away");
        Check(h.Tolls == 5, "tolls refilled");
        Check(h.ApplyAway(-500).away == 0, "clock going backwards grants nothing");
        h.Tolls = 0;
        h.ApplyAway(3);
        Check(h.Tolls == 0, "a quick relaunch does not refill tolls");

        // save round trip keeps everything that matters
        h.SacBought.Add("S8"); h.MarrowEarned = 7; h.Settings.BuyMode = "max";
        var back = SaveFile.Deserialize<Game>(SaveFile.Serialize(h))!;
        Check(back.N("kneeler") == 10 && back.Has("S8") && back.MarrowEarned == 7 && back.Settings.BuyMode == "max"
              && Near(back.Dolor.AllTime, h.Dolor.AllTime), "save round trip");

        // Immurement keeps S8 and lifetime, resets the run
        var im = new Game();
        im.Dolor.Run = im.Dolor.AllTime = 1e8;
        im.SacBought.Add("S8"); im.SacBought.Add("S1"); im.Owned["choir"] = 9;
        Check(im.CanImmure() && im.Ready(), "immure available at gate");
        Check(im.Immure() == 10 && im.MarrowEarned == 10, "first Immurement grants 10");
        Check(im.Has("S8") && !im.Has("S1") && im.N("choir") == 0 && im.Dolor.Run == 0 && im.Dolor.AllTime == 1e8, "reset/persist lists");

        // greedy player: first Immurement should land in the 45-90 min band (balance sim said ~49 min)
        double run1 = Greedy(new Game(), out var after);
        Console.WriteLine($"greedy run 1: {Ui.Duration(run1)} to first Immurement ({after.MarrowEarned} Marrow)");
        Check(run1 is > 40 * 60 and < 90 * 60, "run 1 pacing " + Ui.Duration(run1));
        double run2 = Greedy(after, out _, stopAtRun: Game.Gate);
        Console.WriteLine($"greedy run 2: {Ui.Duration(run2)} back to the gate ({(1 - run2 / run1) * 100:0}% faster)");
        Check(run2 < run1 * 0.7, "run 2 at least 30% faster");

        Console.WriteLine(_fails == 0 ? "selftest OK" : $"{_fails} failure(s)");
        return _fails;
    }

    // Buys whatever pays back fastest (waiting included), tolls every 5s. Returns seconds to the first Immurement.
    static double Greedy(Game g, out Game result, double stopAtRun = 0)
    {
        const double dt = 0.1;
        double t = 0, sinceToll = 0;
        while (t < 6 * 3600)
        {
            if (stopAtRun > 0 ? g.Dolor.Run >= stopAtRun : g.CanImmure()) break;
            g.Tick(dt); t += dt; sinceToll += dt;
            if (sinceToll >= 5 && g.Tolls > 0) { g.Toll(false); sinceToll = 0; }

            double dps = Math.Max(g.Dps(), 1e-9);
            double bestScore = double.MaxValue;
            Action? buy = null; double buyCost = 0;
            foreach (var r in Data.Rites.Where(r => g.Revealed.Contains(r.Id)))
            {
                double c = g.Cost(r, 1), gain = g.UnitRate(r);
                double score = Math.Max(0, c - g.Dolor.Amount) / dps + c / gain;
                if (score < bestScore) { bestScore = score; buyCost = c; buy = () => { g.Settings.BuyMode = "x1"; g.Buy(r); }; }
            }
            foreach (var s in Data.Sacraments.Where(s => g.SacVisible(s) && !g.Has(s.Id)))
            {
                double gain;
                if (s.Target is null || s.Target == "toll") gain = s.Cost <= 120 * dps ? double.MaxValue : 0;  // utility: buy when cheap
                else { g.SacBought.Add(s.Id); gain = g.Dps() - dps; g.SacBought.Remove(s.Id); }
                if (gain <= 0) continue;
                double score = Math.Max(0, s.Cost - g.Dolor.Amount) / dps + s.Cost / gain;
                if (score < bestScore) { bestScore = score; buyCost = s.Cost; buy = () => g.BuySac(s); }
            }
            if (buy != null && buyCost <= g.Dolor.Amount) buy();
        }
        if (stopAtRun == 0 && g.CanImmure()) g.Immure();
        result = g;
        return t;
    }
}
