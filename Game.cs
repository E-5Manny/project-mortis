// Game data and rules. No raylib in here: the self-test and the window both drive this.

record Rite(string Id, string Name, string Flavor, double BaseCost, double Growth, double BaseProd);

// Target: a rite id, "all", "toll", or null (effect checked by id elsewhere, e.g. S5/S8).
record Sacrament(string Id, string Name, string Effect, double Cost, string? Target,
                 Func<Game, double> Mult, Func<Game, bool> Unlock, string Hint);

static class Data
{
    public static readonly Rite[] Rites =
    [
        new("kneeler", "Salt-Kneeler", "They kneel on rock salt until their knees stop being theirs.", 15, 1.15, 0.3),
        new("choir", "Flagellant Choir", "The hymn keeps time. The cords keep count.", 120, 1.15, 1.8),
        new("tallow", "Tallow Saint", "Candles rendered from the canonized. Every wick was once a vow.", 1300, 1.14, 9),
        new("mason", "Charnel Mason", "Builds the new nave from the old congregation.", 14000, 1.14, 51),
        new("wheel", "Dawn Wheel", "Broken on it at dawn, winched back up by dusk. Every day.", 160000, 1.13, 288),
        new("engine", "Sepulchral Engine", "Brass lungs in the crypt, breathing for Him when He forgets.", 1500000, 1.13, 1800),
    ];

    static Func<Game, bool> Own(string id, int n) => g => g.N(id) >= n;
    static Func<Game, bool> Run(double v) => g => g.Dolor.Run >= v;
    static Func<Game, double> X(double m) => _ => m;

    public static readonly Sacrament[] Sacraments =
    [
        new("S1", "Rock Salt", "Salt-Kneelers ×2", 100, "kneeler", X(2), Own("kneeler", 5), "Own 5 Salt-Kneelers."),
        new("S2", "Barbed Hymnals", "Flagellant Choirs ×2", 1000, "choir", X(2), Own("choir", 5), "Own 5 Flagellant Choirs."),
        new("S3", "Heavier Clapper", "Tolls ×2", 300, "toll", X(2), Run(150), "Gather 150 Dolor this run."),
        new("S4", "Double Wicks", "Tallow Saints ×2", 11000, "tallow", X(2), Own("tallow", 5), "Own 5 Tallow Saints."),
        new("S5", "The Second Rope", "Toll charges 5 to 8", 6000, null, X(1), Run(3000), "Gather 3,000 Dolor this run."),
        new("S6", "Shared Scourge", "Kneelers +10% per Choir", 40000, "kneeler", g => 1 + 0.10 * g.N("choir"), Own("choir", 25), "Own 25 Flagellant Choirs."),
        new("S7", "Knuckle Mortar", "Charnel Masons ×2", 120000, "mason", X(2), Own("mason", 5), "Own 5 Charnel Masons."),
        new("S8", "Vigil Unbroken", "Away cap 8h to 24h. Kept through Immurement.", 50000, null, X(1), Run(25000), "Gather 25,000 Dolor this run."),
        new("S9", "Candlelit Vespers", "All +1% per Tallow Saint", 400000, "all", g => 1 + 0.01 * g.N("tallow"), Own("tallow", 15), "Own 15 Tallow Saints."),
        new("S10", "Greased Axle", "Dawn Wheels ×2", 1.5e6, "wheel", X(2), Own("wheel", 5), "Own 5 Dawn Wheels."),
        new("S11", "Sackcloth Edict", "All ×1.5", 4e6, "all", X(1.5), Run(2e6), "Gather 2M Dolor this run."),
        new("S12", "Brass Bellows", "Sepulchral Engines ×2", 1e7, "engine", X(2), Own("engine", 5), "Own 5 Sepulchral Engines."),
    ];

    public static Rite Rite(string id) => Rites.First(r => r.Id == id);
    public static Sacrament Sac(string id) => Sacraments.First(s => s.Id == id);

    public static readonly string[] Stanzas =
    [
        "I was the bell-ringer of Ashkirk. Nothing more. I want that written first.",
        "When He took the dying into Him, the bishops said a bell must call the knife. Mine hung nearest.",
        "I did not ask what the knife was for. I pulled the rope. That is what a rope is for.",
        "He opened like a door. Nothing came out. Every death that was owed went in.",
        "My wife was already coughing then. She coughs still. It has been nine hundred years.",
        "They thanked me. They built the town in the hollow of Him and hung my bell at its heart.",
        "I learned that He lives on what we give up. Then I learned I could give up other people's.",
        "Each time I wall myself in, I pray the mortar holds. He does not let it.",
        "Past the ribs a child has been drowning for three centuries. I have counted every breath.",
        "Tonight the rope was in my hand. He was quiet. I could have let Him rest… and I rang it again.",
    ];
    public const string AccountComplete = "The Account is complete. The bell does not care.";
}

class Currency { public double Amount, Run, AllTime; }

class Stats
{
    public double PlayTimeSec, RunTimeSec, BestDps;
    public double? FastestRunSec;
    public long TollsTotal, TollsOnBeat;
}

class Settings
{
    public string BuyMode = "x1";  // x1 | x10 | max
    public bool Quiet, IdleDim = true;
    public int FpsCap = 30;
}

// The whole save. Public fields are the JSON; methods are the rules.
class Game
{
    public const double Gate = 1e8, K = 10, TollRegenSec = 30;

    public int Version = 1;  // ponytail: no migration list until a v2 save format exists
    public DateTime LastSeenUtc = DateTime.UtcNow;
    public Currency Dolor = new();
    public Dictionary<string, int> Owned = new();
    public HashSet<string> Revealed = ["kneeler"], SacUnlocked = [], SacBought = [];
    public int Tolls = 5;
    public double TollRegen;
    public int MarrowEarned, Immurements;
    public Stats Stats = new();
    public Settings Settings = new();

    public int N(string id) => Owned.GetValueOrDefault(id);
    public bool Has(string sac) => SacBought.Contains(sac);

    // --- production ---
    public double MultFor(string target)
    {
        double m = 1;
        foreach (var s in Data.Sacraments)
            if (s.Target == target && Has(s.Id)) m *= s.Mult(this);
        return m;
    }
    public double MarrowMult(int marrow) => 1 + 0.10 * marrow;
    public double AllMult() => MultFor("all") * MarrowMult(MarrowEarned);
    public double UnitRate(Rite r) => r.BaseProd * MultFor(r.Id) * AllMult();
    public double RiteRate(Rite r) => UnitRate(r) * N(r.Id);
    public double Dps() => Data.Rites.Sum(RiteRate);

    // --- buying rites ---
    public double Cost(Rite r, int k)
    {
        double g = r.Growth;
        return r.BaseCost * Math.Pow(g, N(r.Id)) * (Math.Pow(g, k) - 1) / (g - 1);
    }
    public int MaxAffordable(Rite r)
    {
        double first = r.BaseCost * Math.Pow(r.Growth, N(r.Id));
        if (Dolor.Amount < first) return 0;
        int k = (int)Math.Floor(Math.Log(Dolor.Amount * (r.Growth - 1) / first + 1, r.Growth));
        while (k > 0 && Cost(r, k) > Dolor.Amount) k--;  // float guard at exact boundaries
        return k;
    }
    // Max mode with nothing affordable shows (and targets) a single unit.
    public int BuyCount(Rite r) => Settings.BuyMode switch { "x10" => 10, "max" => Math.Max(1, MaxAffordable(r)), _ => 1 };
    public bool CanBuy(Rite r) => Cost(r, BuyCount(r)) <= Dolor.Amount;
    public bool Buy(Rite r)
    {
        int k = BuyCount(r);
        double c = Cost(r, k);
        if (c > Dolor.Amount) return false;
        Dolor.Amount -= c;
        Owned[r.Id] = N(r.Id) + k;
        Unlocks();
        return true;
    }

    // --- sacraments ---
    public bool SacVisible(Sacrament s) => SacUnlocked.Contains(s.Id);
    public bool CanBuySac(Sacrament s) => SacVisible(s) && !Has(s.Id) && Dolor.Amount >= s.Cost;
    public bool BuySac(Sacrament s)
    {
        if (!CanBuySac(s)) return false;
        Dolor.Amount -= s.Cost;
        SacBought.Add(s.Id);
        if (Tolls > MaxTolls()) Tolls = MaxTolls();
        return true;
    }
    public bool AnySacAffordable() => Data.Sacraments.Any(CanBuySac);

    // --- toll ---
    public int MaxTolls() => Has("S5") ? 8 : 5;
    public double TollValue(bool onBeat) => Math.Max(5, 10 * Dps()) * MultFor("toll") * (onBeat ? 1.5 : 1);
    public double? Toll(bool onBeat)
    {
        if (Tolls <= 0) return null;
        if (Tolls == MaxTolls()) TollRegen = 0;  // regen timer starts when the first charge is spent
        Tolls--;
        double v = TollValue(onBeat);
        Gain(v);
        Stats.TollsTotal++;
        if (onBeat) Stats.TollsOnBeat++;
        Unlocks();
        return v;
    }

    // --- time ---
    public void Tick(double dt)
    {
        if (dt <= 0) return;
        double dps = Dps();
        Gain(dps * dt);
        if (Tolls < MaxTolls())
        {
            TollRegen += dt;
            int add = (int)(TollRegen / TollRegenSec);
            Tolls = Math.Min(MaxTolls(), Tolls + add);
            TollRegen -= add * TollRegenSec;
        }
        if (Tolls >= MaxTolls()) TollRegen = 0;  // timer pauses at max
        Stats.PlayTimeSec += dt;
        Stats.RunTimeSec += dt;
        Stats.BestDps = Math.Max(Stats.BestDps, dps);
        Unlocks();
    }

    void Gain(double v) { Dolor.Amount += v; Dolor.Run += v; Dolor.AllTime += v; }

    void Unlocks()
    {
        foreach (var r in Data.Rites) if (Dolor.Run >= 0.5 * r.BaseCost) Revealed.Add(r.Id);
        foreach (var s in Data.Sacraments) if (!SacUnlocked.Contains(s.Id) && s.Unlock(this)) SacUnlocked.Add(s.Id);
    }

    public double OfflineCap() => Has("S8") ? 24 * 3600 : 8 * 3600;

    // Grant time spent away (closed game, or PC asleep). Negative deltas (clock went back) count as 0.
    public (double away, double gained, bool capped) ApplyAway(double seconds)
    {
        double away = Math.Clamp(seconds, 0, OfflineCap());
        double before = Dolor.AllTime;
        Tick(away);
        if (seconds >= 60) { Tolls = MaxTolls(); TollRegen = 0; }  // real absences only: quick relaunches must not refill the rope
        return (away, Dolor.AllTime - before, seconds > OfflineCap());
    }

    // --- Immurement ---
    public static int TotalFor(double lifetime) => (int)Math.Floor(K * Math.Sqrt(lifetime / Gate));
    public static double LifetimeFor(int marrow) => Gate * Math.Pow(marrow / K, 2);
    public int Pending() => Math.Max(0, TotalFor(Dolor.AllTime) - MarrowEarned);
    public bool CanImmure() => Dolor.Run >= Gate && Pending() >= 1;
    public bool Ready() => CanImmure() && (MarrowEarned == 0 || Pending() >= MarrowEarned);
    public double NextMarrowAt() => LifetimeFor(TotalFor(Dolor.AllTime) + 1);

    public int Immure()
    {
        int gained = Pending();
        MarrowEarned += gained;
        Immurements++;
        if (Stats.FastestRunSec is not { } f || Stats.RunTimeSec < f) Stats.FastestRunSec = Stats.RunTimeSec;
        ResetRun();
        return gained;
    }

    void ResetRun()
    {
        bool vigil = Has("S8");
        Dolor.Amount = 0;
        Dolor.Run = 0;
        Owned.Clear();
        Revealed = ["kneeler"];
        SacBought.Clear();
        SacUnlocked.Clear();
        if (vigil) { SacBought.Add("S8"); SacUnlocked.Add("S8"); }
        Tolls = 5;
        TollRegen = 0;
        Stats.RunTimeSec = 0;
    }

    // Ichor pool fill 0..1: run progress to the first gate, then progress to the next Marrow.
    public double PoolLevel()
    {
        if (MarrowEarned == 0) return Math.Clamp(Math.Log10(Dolor.Run + 1) / 8, 0, 1);
        int m = TotalFor(Dolor.AllTime);
        double lo = LifetimeFor(m), hi = LifetimeFor(m + 1);
        return Math.Clamp((Dolor.AllTime - lo) / (hi - lo), 0, 1);
    }

    public double Bpm() => Math.Min(140, 40 + 12 * Math.Log10(Dps() + 1));
    public double Souls() => Math.Floor(Dolor.AllTime / 1000);
}
