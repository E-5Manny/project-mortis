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
        Check(Game.TotalFor(Game.Gate) == 10 && Game.TotalFor(4 * Game.Gate) == 20 && Game.TotalFor(10 * Game.Gate) == 31, "Marrow totals");
        Check(Near(Game.LifetimeFor(11), 1.21 * Game.Gate), "next Marrow threshold");

        // a version-1 save keeps its place relative to the next Marrow
        var old = new Game { Version = 1, MarrowEarned = 18 };
        old.Dolor.AllTime = 3.3e8; old.Dolor.Run = 5e7;
        old.Migrate();
        Check(old.Version == 2 && Game.TotalFor(old.Dolor.AllTime) == 18 && Near(old.Dolor.Run / Game.Gate, 0.5), "save migration v1 -> v2");

        // offline: capped, 100% rate, tolls refilled
        var h = new Game();
        h.Owned["kneeler"] = 10;
        h.Tolls = 0;
        double hDps = h.Dps();
        var (away, gained, capped) = h.ApplyAway(100_000);
        Check(capped && away == 8 * 3600, "away cap 8h");
        Check(Near(gained, hDps * 8 * 3600), "away gain = dps × away");
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
        im.Dolor.Run = im.Dolor.AllTime = Game.Gate;
        im.SacBought.Add("S8"); im.SacBought.Add("S1"); im.Owned["choir"] = 9;
        Check(im.CanImmure() && im.Ready(), "immure available at gate");
        Check(im.Immure() == 10 && im.MarrowEarned == 10, "first Immurement grants 10");
        Check(im.Has("S8") && !im.Has("S1") && im.N("choir") == 0 && im.Dolor.Run == 0 && im.Dolor.AllTime == Game.Gate, "reset/persist lists");

        // Mortification: production stops, time passes in full even past the away cap, a wound is given
        var mo = new Game();
        mo.Owned["kneeler"] = 10; mo.Immurements = 1;
        mo.BeginMortify(0);
        mo.Tick(60);
        Check(mo.Dolor.AllTime == 0 && mo.Mortifying(), "no Dolor while mortifying");
        Check(mo.Toll(false) == null, "no tolling while mortifying");
        double moDps = mo.Dps();  // admissions earned in the first tick count from here on
        mo.Tick(900);  // 60s past the end
        Check(!mo.Mortifying() && Near(mo.Dolor.AllTime, moDps * 60), "production resumes after the end");
        Check(mo.NewWound != null && mo.Wounds.Count == 1 && mo.OpenWounds.Count == 1 && mo.Mortifications == 1, "a wound is given and opened");
        var mo2 = new Game { Immurements = 1 };
        mo2.Owned["kneeler"] = 10;
        mo2.BeginMortify(3);  // 8h
        double mo2Dps = mo2.Dps();
        var aw = mo2.ApplyAway(20 * 3600);  // 20h away: 8h mortified + 8h capped production
        Check(!mo2.Mortifying() && Near(aw.gained, mo2Dps * 8 * 3600) && aw.capped, "away: mortify time is uncapped, production capped");
        var wg = new Game();
        wg.Owned["kneeler"] = 1;
        double before = wg.Dps();
        wg.Wounds["salted_knees"] = 2; wg.OpenWounds.Add("salted_knees");
        Check(Near(wg.Dps(), before * 2.5), "open wound multiplies its target");
        wg.Wounds["open_side"] = 1; wg.OpenWounds.Add("open_side");
        Check(Near(wg.Cost(kn, 1), 15 * 1.15 * 1.3), "trade-off raises costs");  // second kneeler, ×1.3
        var wb = SaveFile.Deserialize<Game>(SaveFile.Serialize(wg))!;
        Check(wb.Wounds["salted_knees"] == 2 && wb.OpenWounds.Contains("open_side"), "wounds survive save");

        // the Lattice: bought with free Marrow, in order; spending never weakens the passive bonus
        var lt = new Game { MarrowEarned = 10 };
        Check(!lt.Learn(Data.Node("kneeling_flesh")), "a branch needs the root first");
        Check(lt.Learn(Data.Node("first_stone")) && lt.MarrowFree() == 9 && Near(lt.MultFor("all"), 1.25) && Near(lt.MarrowMult(lt.MarrowEarned), 2),
              "root: All ×1.25, and spent Marrow still counts");
        lt.Learn(Data.Node("thin_mortar"));
        Check(Near(lt.Cost(kn, 1), 15 * 0.9), "Thin Mortar: Rites cost ×0.9");
        lt.Learn(Data.Node("deeper_marrow"));
        lt.Dolor.AllTime = 4 * Game.Gate; lt.Dolor.Run = Game.Gate;
        Check(lt.Pending() == 25 - 10, "Deeper Marrow: ×1.25 Marrow (25 at 4× gate)");
        lt.Lattice.Add("fourth_wound");
        Check(lt.OpenSlots() == 4, "a fourth wound slot");

        // Deacons buy for you, can be switched off, and keep buying while you're away
        var dc = new Game { MarrowEarned = 25 };
        dc.Dolor.Amount = dc.Dolor.Run = 5000;
        dc.Tick(1.01);
        Check(dc.N("kneeler") > 0 && dc.N("choir") > 0, "deacons buy rites");
        var dcOff = new Game { MarrowEarned = 25 };
        dcOff.DeaconsOff.UnionWith(["deacon_low", "deacon_all"]);
        dcOff.Dolor.Amount = dcOff.Dolor.Run = 5000;
        dcOff.Tick(1.01);
        Check(dcOff.TotalOwned() == 0, "switched-off deacons do nothing");
        var dcAway = new Game { MarrowEarned = 25 };
        dcAway.Owned["kneeler"] = 10;
        dcAway.ApplyAway(3600);
        Check(dcAway.N("kneeler") > 10, "deacons buy while you're away");

        // Admissions: each is +1%; Omens: the surge is ×3 for a minute
        var ad = new Game();
        ad.Owned["kneeler"] = 1;
        ad.Tick(1.01);
        Check(ad.Admitted.Contains("kneel1") && Near(ad.AdmissionMult(), 1.01) && ad.NewAdmissions.Count > 0, "an admission is earned");
        ad.ClaimOmen(0);
        double surged = ad.Dps();
        ad.SurgeLeft = 0;
        double plain = ad.Dps();
        ad.SurgeLeft = 60;
        Check(Near(surged, plain * 3) && ad.Stats.Omens == 1, "omen surge ×3");
        ad.Tick(61);
        Check(ad.SurgeLeft == 0, "the surge ends");

        // Biddings: burden while active, boon when kept, curse when failed; abstain breaks on any purchase
        var realBids = Data.Biddings;
        Bidding Make(string id, string type, string? target, double amount, double seconds) =>
            new(id, id, ["..."], "...", new Objective(type, target, amount, seconds), new Mod("kneeler", 0.5),
                new Mod("kneeler", 4, 600), new Mod("all", 0.75, 300), "", "", "", "");
        Data.Biddings = [Make("t_toll", "toll", null, 3, 300), Make("t_abstain", "abstain", null, 0, 120),
                         Make("t_silence", "silence", null, 0, 120), Make("t_mortify", "mortify", null, 0, 3600)];
        var bg = new Game { Immurements = 1 };
        bg.Owned["kneeler"] = 10;
        double free = bg.MultFor("kneeler");
        bg.Accept(Data.Bid("t_toll")!);
        Check(Near(bg.MultFor("kneeler"), free * 0.5), "burden applies while a bidding is active");
        for (int i = 0; i < 3; i++) bg.Toll(false);
        bg.TickBidding(0.1);
        Check(bg.Bidding == null && bg.BiddingOutcome == ("t_toll", true) && Near(bg.MultFor("kneeler"), free * 4), "kept: burden lifts, boon applies");
        bg.Tick(601);
        Check(bg.Effects.Count == 0 && Near(bg.MultFor("kneeler"), free), "the boon runs out");
        bg.Accept(Data.Bid("t_abstain")!);
        bg.Dolor.Amount = 1e6;
        bg.Buy(kn, 1);
        bg.TickBidding(0.1);
        Check(bg.BiddingOutcome == ("t_abstain", false) && Near(bg.MultFor("all"), 0.75), "abstain broken by a purchase: cursed");
        bg.Effects.Clear();
        bg.Accept(Data.Bid("t_silence")!);
        bg.TickBidding(121);
        Check(bg.BiddingOutcome == ("t_silence", true), "silence endured");
        bg.Accept(Data.Bid("t_mortify")!);
        bg.BeginMortify(0);
        bg.Tick(901);
        bg.TickBidding(0.1);
        Check(bg.BiddingOutcome == ("t_mortify", true), "a finished Mortification keeps a mortify bidding");
        bg.Accept(Data.Bid("t_toll")!);
        var bgSaved = SaveFile.Deserialize<Game>(SaveFile.Serialize(bg))!;
        Check(bgSaved.Bidding?.Id == "t_toll" && bgSaved.Effects.Count == bg.Effects.Count, "an active bidding survives save");
        bg.Dolor.Run = bg.Dolor.AllTime = Game.Gate;
        bg.Immure();
        Check(bg.Bidding == null, "Immurement ends a bidding");
        Data.Biddings = realBids;
        foreach (var b in Data.Biddings)  // the shipped content only uses what the engine understands
            Check(new[] { "toll", "beat", "buy", "gather", "abstain", "silence", "mortify" }.Contains(b.Objective.Type)
                  && (b.Objective.Type != "buy" || Data.Rites.Any(r => r.Id == b.Objective.Target))
                  && new[] { b.Burden, b.Boon, b.Curse }.All(m => m == null || m.Target is "all" or "toll" or "costs" or "tollRegen" || Data.Rites.Any(r => r.Id == m.Target)),
                  $"bidding {b.Id} uses known objectives and targets");

        // every generated sound is finite, audible, unclipped; loops join without a click
        foreach (var (name, smp, loop) in Audio.All())
        {
            double peak = 0, sum = 0;
            foreach (var v in smp) { peak = Math.Max(peak, Math.Abs(v)); sum += v * v; }
            double rms = Math.Sqrt(sum / smp.Length);
            Check(!double.IsNaN(peak) && peak <= 1.0 && rms > 0.005, $"sound {name}: peak {peak:0.000} rms {rms:0.0000}");
            if (loop) Check(Math.Abs(smp[0] - smp[^1]) < 0.05, $"loop {name} seam jump {Math.Abs(smp[0] - smp[^1]):0.000}");
        }

        // greedy player: first Immurement should land at 1.5-2h for a person, so ~70-110 min for this tireless greedy one
        double run1 = Greedy(new Game(), out var after);
        Console.WriteLine($"greedy run 1: {Ui.Duration(run1)} to first Immurement ({after.MarrowEarned} Marrow)");
        Check(run1 is > 70 * 60 and < 110 * 60, "run 1 pacing " + Ui.Duration(run1));
        double run2 = Greedy(after, out _, stopAtRun: Game.Gate);
        Console.WriteLine($"greedy run 2: {Ui.Duration(run2)} back to the gate ({(1 - run2 / run1) * 100:0}% faster)");
        Check(run2 < run1 * 0.7, "run 2 at least 30% faster");

        // MORTIS_SIM_SAVE=<save.json>: greedy runs from that save's persistent state, each to the Ready point (pending ≥ earned).
        if (Environment.GetEnvironmentVariable("MORTIS_SIM_SAVE") is { } simSave)
        {
            var v = SaveFile.Deserialize<Game>(File.ReadAllText(simSave))!;
            v.Migrate();
            for (int i = 0; i < 4; i++)
            {
                v.ResetRun();
                v.Stats.RunTimeSec = 0;
                double t = Greedy(v, out v, ready: true);
                Console.WriteLine($"veteran run: {Ui.Duration(t)} to Ready, {v.MarrowEarned} Marrow, dps {Ui.Num(v.Stats.BestDps)}");
            }
        }

        Console.WriteLine(_fails == 0 ? "selftest OK" : $"{_fails} failure(s)");
        return _fails;
    }

    // Buys whatever pays back fastest (waiting included), tolls every 5s. Returns seconds to the first Immurement.
    // MORTIS_SIM_TIMELINE=1 prints when each Rite and Sacrament first arrives (for balancing).
    static readonly bool Timeline = Environment.GetEnvironmentVariable("MORTIS_SIM_TIMELINE") == "1";
    static double Greedy(Game g, out Game result, double stopAtRun = 0, bool ready = false)
    {
        const double dt = 0.1;
        double t = 0, sinceToll = 0;
        var seen = new HashSet<string>();
        void Note(string what) { if (Timeline && seen.Add(what)) Console.WriteLine($"  {Ui.Duration(t),9}  {what}  (dps {Ui.Num(g.Dps())})"); }
        while (t < 20 * 3600)
        {
            if (stopAtRun > 0 ? g.Dolor.Run >= stopAtRun : ready ? g.Ready() : g.CanImmure()) break;
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
            if (Timeline)
            {
                foreach (var r in Data.Rites) if (g.N(r.Id) > 0) Note("rite " + r.Name);
                foreach (var s in g.SacBought) Note("sac " + Data.Sac(s).Name);
            }
        }
        if (stopAtRun == 0 && g.CanImmure()) g.Immure();
        result = g;
        return t;
    }
}
