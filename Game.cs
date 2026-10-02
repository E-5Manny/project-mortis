// Game data and rules. No raylib in here: the self-test and the window both drive this.

record Rite(string Id, string Name, string Flavor, double BaseCost, double Growth, double BaseProd);

// Target: a rite id, "all", "toll", or null (effect checked by id elsewhere, e.g. S5/S8/S22).
record Sacrament(string Id, string Name, string Effect, double Cost, string? Target,
                 Func<Game, double> Mult, Func<Game, bool> Unlock, string Hint);

// Mods target a rite id, "all", "toll", "costs" (rite prices) or "tollRegen" (seconds per charge); Mult gets the rank.
record Wound(string Id, string Name, int Depth, (string Target, Func<int, double> Mult)[] Mods, Func<int, string> Good, string? Bad);

// The Lattice: bought with Marrow. Branch 0 Flesh, 1 Bell, 2 Bone (-1 = the root). Mods use the same targets as wounds.
record Node(string Id, string Name, int Branch, int Tier, int Cost, string? Requires, string Effect, (string Target, Func<Game, double> Mult)[] Mods);

// Deacons: automation granted by total Marrow earned. Each can be switched off.
record Vow(string Id, string Name, int Marrow, string Effect);

// The Deep: repeatable nodes under each finished branch, a sink for Marrow past the Lattice. Each rank multiplies Target
// by Mult (compounding) and costs Base × DeepGrowth^rank. Target "marrow" scales Marrow from Immurement.
record DeepNode(string Id, string Name, int Branch, string Target, double Mult, int Base, string Effect);

record Admission(string Id, string Name, string Line, Func<Game, bool> Earned);

// Visitors and their Biddings live in assets/biddings.json so new ones need no code.
// Objective types: toll, beat, buy (target = rite id), gather (amount = minutes of production), abstain, silence, mortify (amount = tier).
record Objective(string Type, string? Target, double Amount, double Seconds);
record Mod(string Target, double Mult, double Seconds = 0);
// By: null for the Prophet's Biddings, "wife" for Ysmay's chapters (told in file order, one per visit).
record Bidding(string Id, string Title, string[] Pages, string Ask, Objective Objective, Mod? Burden, Mod Boon, Mod? Curse,
               string Accept, string Refuse, string Success, string Fail, string? By = null);
// Spurned: the curse a visitor leaves after three refusals in a row (null: refusing stays free).
record Visitor(string Name, string Personal, string[] Greeting, string Leaves, Mod? Spurned = null, string? SpurnedLine = null);

// A boon or curse ticking down.
class Effect { public string Target = "", Source = ""; public double Mult = 1, Left; }

class ActiveBidding { public string Id = ""; public double Left, Base, Goal, Progress; }

static class Data
{
    public static readonly Rite[] Rites =
    [
        new("kneeler", "Salt-Kneeler", "They kneel on rock salt until their knees stop being theirs.", 15, 1.15, 0.3),
        new("choir", "Flagellant Choir", "The hymn keeps time. The cords keep count.", 120, 1.15, 1.8),
        new("tallow", "Tallow Saint", "Candles rendered from the canonized. Every wick was once a vow.", 1300, 1.14, 9),
        new("mason", "Charnel Mason", "Builds the new nave from the old congregation.", 14000, 1.14, 51),
        new("wheel", "Dawn Wheel", "Broken on it at dawn, winched back up by dusk. Every day.", 160000, 1.14, 288),
        new("engine", "Sepulchral Engine", "Brass lungs in the crypt, breathing for Him when He forgets.", 1500000, 1.14, 1800),
        new("gibbet", "Gibbet Orchard", "Cages hang in rows like fruit. The fruit sings.", 5e7, 1.15, 14000),
        new("bishop", "The Hollow Bishop", "He gave up his insides for the faith. They are still giving.", 2e9, 1.15, 110000),
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
        new("S5", "The Second Rope", "Toll charges +3", 6000, null, X(1), Run(3000), "Gather 3,000 Dolor this run."),
        new("S6", "Shared Scourge", "Kneelers +10% per Choir", 40000, "kneeler", g => 1 + 0.10 * g.N("choir"), Own("choir", 25), "Own 25 Flagellant Choirs."),
        new("S7", "Knuckle Mortar", "Charnel Masons ×2", 120000, "mason", X(2), Own("mason", 5), "Own 5 Charnel Masons."),
        new("S8", "Vigil Unbroken", "Away cap +16h. Kept through Immurement.", 50000, null, X(1), Run(25000), "Gather 25,000 Dolor this run."),
        new("S9", "Candlelit Vespers", "All +1% per Tallow Saint", 400000, "all", g => 1 + 0.01 * g.N("tallow"), Own("tallow", 15), "Own 15 Tallow Saints."),
        new("S10", "Greased Axle", "Dawn Wheels ×2", 1.5e6, "wheel", X(2), Own("wheel", 5), "Own 5 Dawn Wheels."),
        new("S11", "Sackcloth Edict", "All ×1.5", 4e6, "all", X(1.5), Run(2e6), "Gather 2M Dolor this run."),
        new("S12", "Brass Bellows", "Sepulchral Engines ×2", 1e7, "engine", X(2), Own("engine", 5), "Own 5 Sepulchral Engines."),
        new("S13", "Salt in Every Wound", "Salt-Kneelers ×3", 1e4, "kneeler", X(3), Own("kneeler", 25), "Own 25 Salt-Kneelers."),
        new("S14", "Knotted Cords", "Flagellant Choirs ×3", 8e4, "choir", X(3), Own("choir", 25), "Own 25 Flagellant Choirs."),
        new("S15", "Rendered Twice", "Tallow Saints ×3", 6e5, "tallow", X(3), Own("tallow", 25), "Own 25 Tallow Saints."),
        new("S16", "Lime and Marrow", "Charnel Masons ×3", 6e6, "mason", X(3), Own("mason", 25), "Own 25 Charnel Masons."),
        new("S17", "Dawn Without End", "Dawn Wheels ×3", 1.5e8, "wheel", X(3), Own("wheel", 25), "Own 25 Dawn Wheels."),
        new("S18", "Bellows of the Saints", "Sepulchral Engines ×3", 2e9, "engine", X(3), Own("engine", 25), "Own 25 Sepulchral Engines."),
        new("S19", "Ripened Cages", "Gibbet Orchards ×2", 5e8, "gibbet", X(2), Own("gibbet", 5), "Own 5 Gibbet Orchards."),
        new("S20", "The Bishop's Hunger", "Hollow Bishops ×2", 2e10, "bishop", X(2), Own("bishop", 5), "Own 5 Hollow Bishops."),
        new("S21", "Litany of Hours", "All +0.2% per Rite owned", 2e6, "all", g => 1 + 0.002 * g.TotalOwned(), g => g.TotalOwned() >= 100, "Own 100 Rites in all."),
        new("S22", "The Third Rope", "Tolls ×3, charges +2", 2e6, "toll", X(3), Run(5e5), "Gather 500K Dolor this run."),
        new("S23", "Choir of Masons", "Charnel Masons +4% per Choir", 5e7, "mason", g => 1 + 0.04 * g.N("choir"), Own("choir", 50), "Own 50 Flagellant Choirs."),
        new("S24", "The Second Edict", "All ×2", 4e9, "all", X(2), Run(2e9), "Gather 2B Dolor this run."),
    ];

    // Wounds: rewards of Mortification. Depth 0 Shallow, 1 Deep, 2 Grievous. Rank 1..5; deeper ones cost something.
    static (string, Func<int, double>) M(string target, Func<int, double> f) => (target, f);
    public static readonly Wound[] Wounds =
    [
        new("salted_knees", "Salted Knees", 0, [M("kneeler", r => 1.5 + 0.5 * r)], r => $"Salt-Kneelers ×{1.5 + 0.5 * r:0.0#}", null),
        new("ninth_stripe", "The Ninth Stripe", 0, [M("choir", r => 1.5 + 0.5 * r)], r => $"Flagellant Choirs ×{1.5 + 0.5 * r:0.0#}", null),
        new("tallow_fingers", "Tallow Fingers", 0, [M("tallow", r => 1.5 + 0.5 * r)], r => $"Tallow Saints ×{1.5 + 0.5 * r:0.0#}", null),
        new("rope_palms", "Rope-Burnt Palms", 0, [M("toll", r => 1.5 + 0.5 * r), M("tollRegen", _ => 1.33)], r => $"Tolls ×{1.5 + 0.5 * r:0.0#}", "the rope takes 40s to rest"),
        new("mortar_mouth", "Mortar in the Mouth", 1, [M("mason", r => 2 + 0.5 * r)], r => $"Charnel Masons ×{2 + 0.5 * r:0.0#}", null),
        new("weeping_shoulder", "The Weeping Shoulder", 1, [M("all", r => 1.15 + 0.10 * r)], r => $"All ×{1.15 + 0.10 * r:0.00}", null),
        new("wheel_kiss", "The Wheel's Kiss", 1, [M("wheel", r => 2.5 + 0.5 * r), M("kneeler", _ => 0.5)], r => $"Dawn Wheels ×{2.5 + 0.5 * r:0.0#}", "Salt-Kneelers ×0.5"),
        new("brass_lung", "A Brass Lung", 1, [M("engine", r => 2.5 + 0.5 * r), M("toll", _ => 0.5)], r => $"Sepulchral Engines ×{2.5 + 0.5 * r:0.0#}", "Tolls ×0.5"),
        new("open_side", "The Open Side", 2, [M("all", r => 1.6 + 0.2 * r), M("costs", _ => 1.3)], r => $"All ×{1.6 + 0.2 * r:0.0#}", "Rites cost ×1.3"),
        new("hollow_eye", "The Hollow Eye", 2, [M("costs", r => 0.9 - 0.04 * r), M("all", _ => 0.9)], r => $"Rites cost ×{0.9 - 0.04 * r:0.00}", "All ×0.9"),
        new("unhealing", "The Unhealing", 2, [M("all", r => 1.3 + 0.15 * r), M("toll", _ => 0.25)], r => $"All ×{1.3 + 0.15 * r:0.00}", "Tolls ×0.25"),
    ];
    public static Wound Wound(string id) => Wounds.First(w => w.Id == id);
    public static readonly string[] Depths = ["Shallow", "Deep", "Grievous"];

    public static readonly double[] MortifySeconds = [15 * 60, 60 * 60, 4 * 3600, 8 * 3600];
    public static readonly string[] MortifyLabels = ["15m", "1h", "4h", "8h"];
    public static readonly double[][] DepthOdds = [[0.8, 0.2, 0], [0.4, 0.55, 0.05], [0, 0.6, 0.4], [0, 0.25, 0.75]];

    // --- the Lattice ---
    static (string, Func<Game, double>) L(string target, double m) => (target, _ => m);
    public static readonly string[] Branches = ["Flesh", "Bell", "Bone"];
    public static readonly Node[] Lattice =
    [
        new("first_stone", "The First Stone", -1, 0, 1, null, "All ×1.25", [L("all", 1.25)]),
        new("kneeling_flesh", "Kneeling Flesh", 0, 1, 2, "first_stone", "Salt-Kneelers and Choirs ×3", [L("kneeler", 3), L("choir", 3)]),
        new("tallow_lime", "Tallow and Lime", 0, 2, 4, "kneeling_flesh", "Tallow Saints and Masons ×3", [L("tallow", 3), L("mason", 3)]),
        new("wheel_engine", "Wheel and Engine", 0, 3, 8, "tallow_lime", "Dawn Wheels and Engines ×3", [L("wheel", 3), L("engine", 3)]),
        new("hanging_garden", "The Hanging Garden", 0, 4, 16, "wheel_engine", "Gibbets and Bishops ×3", [L("gibbet", 3), L("bishop", 3)]),
        new("flesh_remembers", "The Flesh Remembers", 0, 5, 30, "hanging_garden", "Each run begins with 10 of the first three Rites", []),
        new("heavier_rope", "A Heavier Rope", 1, 1, 2, "first_stone", "Tolls ×3", [L("toll", 3)]),
        new("quick_hands", "Quick Hands", 1, 2, 4, "heavier_rope", "Toll charges rest in 20s, not 30s", [L("tollRegen", 2 / 3.0)]),
        new("bell_remembers", "The Bell Remembers", 1, 3, 8, "quick_hands", "All +1% per Toll this run, up to +50%",
            [("all", g => 1 + 0.01 * Math.Min(50, g.Stats.TollsThisRun))]),
        new("on_the_beat", "On the Beat", 1, 4, 16, "bell_remembers", "The beat is easier to hit, and pays ×2.5", []),
        new("third_rope", "A Rope for Each Hand", 1, 5, 30, "on_the_beat", "Toll charges +3", []),
        new("thin_mortar", "Thin Mortar", 2, 1, 2, "first_stone", "Rites cost ×0.9", [L("costs", 0.9)]),
        new("deeper_marrow", "Deeper Marrow", 2, 2, 4, "thin_mortar", "Marrow from Immurement ×1.25", []),
        new("long_vigil", "The Long Vigil", 2, 3, 8, "deeper_marrow", "Away cap +8h", []),
        new("grave_patience", "Grave Patience", 2, 4, 16, "long_vigil", "All +20% per hour of this run, up to 8h",
            [("all", g => 1 + 0.2 * Math.Min(8, g.Stats.RunTimeSec / 3600))]),
        new("fourth_wound", "A Fourth Wound", 2, 5, 30, "grave_patience", "One more Wound may stay open", []),
    ];
    public static Node Node(string id) => Lattice.First(n => n.Id == id);

    public static readonly DeepNode[] Deep =
    [
        new("deep_flesh", "Flesh Upon Flesh", 0, "all", 1.15, 300, "All ×1.15"),
        new("deep_bell", "The Bell Sinks Deeper", 1, "toll", 1.25, 300, "Tolls ×1.25"),
        new("deep_bone", "Bone Upon Bone", 2, "marrow", 1.05, 300, "Marrow from Immurement ×1.05"),
    ];
    public static DeepNode DeepNode(string id) => Deep.First(d => d.Id == id);

    // --- Deacons ---
    public static readonly Vow[] Vows =
    [
        new("deacon_low", "The First Deacon", 10, "Buys Salt-Kneelers and Choirs for you"),
        new("deacon_all", "A Deacon for Every Rite", 25, "Buys every Rite for you"),
        new("deacon_sac", "The Deacon of Edicts", 50, "Takes every affordable Sacrament"),
        new("deacon_rope", "The Bell-Boy", 100, "Toll charges +2; rings the rope when it is full"),
        new("deacon_tithe", "A Tithe Kept Back", 200, "Each run begins with 1M Dolor"),
        new("deacon_wall", "The Mason of Your Cell", 400, "Walls you in when enough Marrow is waiting"),
    ];
    public static Vow Vow(string id) => Vows.First(v => v.Id == id);

    // --- Admissions: each one is +1% to all production ---
    static Func<Game, bool> Has(string rite, int n) => g => g.N(rite) >= n;
    public static readonly Admission[] Admissions =
    [
        new("kneel1", "The First Knee", "I told the first of them it would not hurt for long. I did not say how long.", Has("kneeler", 1)),
        new("choir1", "A Hymn With Teeth", "I chose the hymn. I chose the cords.", Has("choir", 1)),
        new("tallow1", "First Wick", "She asked to be remembered. I made her into light.", Has("tallow", 1)),
        new("mason1", "The Old Congregation", "I knew the names in the walls. I laid them face-inward.", Has("mason", 1)),
        new("wheel1", "The Dawn Shift", "I set the hour of the breaking. I am always on time.", Has("wheel", 1)),
        new("engine1", "Brass Lungs", "When the Engine first breathed, the crypt coughed with it. I wrote it down as a blessing.", Has("engine", 1)),
        new("gibbet1", "The Orchard Planted", "I hung the first cage myself, so no one else would have to. Then no one else did.", Has("gibbet", 1)),
        new("bishop1", "His Grace, Emptied", "The Bishop asked me to hold the bowl.", Has("bishop", 1)),
        new("kneel50", "Fifty Knees", "The salt is pink now, all the way down.", Has("kneeler", 50)),
        new("choir50", "A Full Choir", "There is no silence left in Ashkirk to break.", Has("choir", 50)),
        new("tallow50", "A Hall of Candles", "I can read by them. I read the names.", Has("tallow", 50)),
        new("mason50", "The Nave Rises", "The new nave has fine acoustics. The screaming carries.", Has("mason", 50)),
        new("wheel50", "Fifty Dawns at Once", "The wheels turn in step now. I am proud of that.", Has("wheel", 50)),
        new("engine50", "The Crypt Breathes", "Fifty Engines. He no longer needs to remember to breathe.", Has("engine", 50)),
        new("gibbet50", "The Orchard Bears", "The cages sing in harmony. I tuned them.", Has("gibbet", 50)),
        new("bishop50", "A Synod of Hollows", "They are all bishops now. There is no one left to bless.", Has("bishop", 50)),
        new("dolor6", "A Small Grief", "A million moments given up. I counted none of them myself.", g => g.Dolor.AllTime >= 1e6),
        new("dolor9", "A Mountain of Sorrow", "The scales broke. We weigh it by the cartload now.", g => g.Dolor.AllTime >= 1e9),
        new("dolor12", "The Weight of a City", "Ashkirk has given more than it holds. I do not ask from where.", g => g.Dolor.AllTime >= 1e12),
        new("dolor15", "Grief Without Bottom", "I no longer remember a number small enough to be kind.", g => g.Dolor.AllTime >= 1e15),
        new("dolor18", "The Sea of Dolor", "If He drank it all at once, would He live, or drown?", g => g.Dolor.AllTime >= 1e18),
        new("toll100", "The Rope Learns Me", "My hands fit the rope. That is not a thing hands should do.", g => g.Stats.TollsTotal >= 100),
        new("toll1000", "A Thousand Tolls", "Every one of them called the knife again.", g => g.Stats.TollsTotal >= 1000),
        new("beat100", "In Time With Him", "I ring in time with His heart. I do not know which of us leads.", g => g.Stats.TollsOnBeat >= 100),
        new("immure1", "The First Wall", "I walled myself in. I was let out. I was not forgiven.", g => g.Immurements >= 1),
        new("immure5", "Mortar Under the Nails", "I know the chamber by touch now.", g => g.Immurements >= 5),
        new("immure10", "The Tenth Wall", "The bricks remember my shape.", g => g.Immurements >= 10),
        new("immure25", "A Room Built for One", "They have stopped clearing it between my deaths.", g => g.Immurements >= 25),
        new("haste", "Haste", "I went in eagerly. That frightened them more than anything.", g => g.Stats.FastestRunSec is < 1800),
        new("allsacs", "Every Edict Signed", "I signed every edict. My hand did not shake. I checked.", g => g.SacBought.Count == Sacraments.Length),
        new("mortify1", "The Scourge Taken Up", "The first stroke was for her. The rest were for me.", g => g.Mortifications >= 1),
        new("mortify10", "Ten Vigils", "The scourge has worn a groove into my palm.", g => g.Mortifications >= 10),
        new("longnight", "The Long Night", "I let Him starve a whole night, to feel something. I felt it.", g => g.Stats.LongVigils >= 1),
        new("grievous", "A Grievous Gift", "Some wounds should close. I keep this one open with salt.", g => g.Wounds.Keys.Any(w => Wound(w).Depth == 2)),
        new("rank5", "Down to the Bone", "It does not bleed any more. It weeps.", g => g.Wounds.Values.Any(r => r >= Game.MaxRank)),
        new("lattice1", "The First Lattice", "I spent my own bones. They were the only thing I owned.", g => g.Lattice.Count >= 1),
        new("branch", "A Branch Complete", "Something grows in me in the shape of a tree. It has no leaves.", g => g.Lattice.Any(id => Node(id).Tier == 5)),
        new("tree", "The Lattice Entire", "There is nothing of me left to spend.", g => g.Lattice.Count == Lattice.Length),
        new("omen1", "The Eye in the Wall", "It looked at me. I looked back. Only one of us blinked.", g => g.Stats.Omens >= 1),
        new("omen25", "Watched", "Twenty-five times it opened. It is always the same eye.", g => g.Stats.Omens >= 25),
        new("deacon1", "The First Deacon", "I taught a boy to do my work. Then I taught him not to think about it.", g => g.MarrowEarned >= 10),
        new("hidden50", "Hidden Grief", "Fifty times I hid Him from them. They never asked what I was hiding.", g => g.Stats.Hides >= 50),
        new("day", "A Whole Day Away", "I left for a day. He waited. He always waits.", g => g.Stats.LongestAwaySec >= 24 * 3600),
        new("bid1", "I Opened the Door", "He could not see me. He knew my name anyway.", g => g.Stats.BiddingsDone >= 1),
        new("bid10", "The Prophet's Errand-Boy", "Ten times he asked. Ten times I did it. I never asked why.", g => g.Stats.BiddingsDone >= 10),
        new("refuse5", "Bolted From Within", "Five times I left him knocking. He knocked the same way every time.", g => g.Stats.BiddingsRefused >= 5),
        new("deep1", "Deeper Than Bone", "I dug past the Lattice. There was more of me down there, and none of it was clean.", g => g.Deep.Count > 0),
        new("wife1", "She Knew Me", "The town forgets me every time. She never has. She has tried.", g => g.MetWife),
        new("wife_end", "She Stopped Knocking", "She said she would wait at the end of everything. I have never been good at endings.", g => g.WifeDone()),
    ];

    // --- Biddings, loaded once from assets/biddings.json ("_visitor" is the Prophet, "_wife" is Ysmay) ---
    public static Visitor? Prophet, Wife;
    public static Bidding[] Biddings = [];
    public static Bidding? Bid(string id) => Biddings.FirstOrDefault(b => b.Id == id);
    public static Bidding[] Chapters() => Biddings.Where(b => b.By == "wife").ToArray();
    public static Visitor? Who(Bidding b) => b.By == "wife" ? Wife : Prophet;

    public static void LoadBiddings(string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "assets", "biddings.json");
        if (!File.Exists(path)) return;  // no file: no visitors
        var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var all = doc.RootElement.EnumerateArray().ToList();
        Visitor? Read(string id) => all.Where(e => e.GetProperty("id").GetString() == id)
            .Select(e => System.Text.Json.JsonSerializer.Deserialize<Visitor>(e, opts)).FirstOrDefault();
        Prophet = Read("_visitor");
        Wife = Read("_wife");
        Biddings = all.Where(e => !e.GetProperty("id").GetString()!.StartsWith('_')).Select(e => System.Text.Json.JsonSerializer.Deserialize<Bidding>(e, opts)!).ToArray();
    }

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
    public double PlayTimeSec, RunTimeSec, BestDps, LongestAwaySec;
    public double? FastestRunSec;
    public long TollsTotal, TollsOnBeat, TollsThisRun;
    public int Omens, Hides, LongVigils;
    public int BiddingsDone, BiddingsFailed, BiddingsRefused;
    public long Purchases;
}

class Settings
{
    public string BuyMode = "x1";  // x1 | x10 | max
    public bool Quiet, IdleDim = true;
    public int FpsCap = 30;
    public bool SoundOn = true, Screams = true, SilentUnfocused;
    public float Volume = 0.5f;
    public bool Omens = true, Visitors = true;
    public int Heartbeat = 1;  // 0 off, 1 soft, 2 strong
    public bool Large, Fullscreen;  // 945×780 instead of 630×520; full screen keeps Large for when it's left
}

// The whole save. Public fields are the JSON; methods are the rules.
class Game
{
    public const double Gate = 4e10, K = 10;
    public const int MaxRank = 5;
    // Marrow about doubles each run, so ×5 a rank buys roughly one Deep rank per run across the three nodes.
    // Gentler curves let veteran runs shrink instead of lengthen (×2 from a base of 50 took them from minutes to seconds).
    public const double DeepGrowth = 5;
    const double OldGate = 1e8;  // save version 1

    public int Version = 2;
    public DateTime LastSeenUtc = DateTime.UtcNow;
    public Currency Dolor = new();
    public Dictionary<string, int> Owned = new();
    public HashSet<string> Revealed = ["kneeler"], SacUnlocked = [], SacBought = [];
    public int Tolls = 5;
    public double TollRegen;
    public int MarrowEarned, MarrowSpent, Immurements;
    public Stats Stats = new();
    public Settings Settings = new();

    // Mortification: the Sexton leaves the bell; no Dolor until it ends. Wounds persist through Immurement.
    public double MortifyLeft, MortifyTotal;
    public int MortifyTier = -1, Mortifications;
    public Dictionary<string, int> Wounds = new();  // id -> rank
    public HashSet<string> OpenWounds = [];

    public HashSet<string> Lattice = [];
    public Dictionary<string, int> Deep = new();  // Deep node id -> rank
    public HashSet<string> DeaconsOff = [];
    public double AutoImmureAt = 1;  // the Mason walls you in once pending Marrow reaches this × what you have
    public HashSet<string> Admitted = [];
    public double SurgeLeft;          // an Omen's ×3
    public ActiveBidding? Bidding;    // the Prophet's current Bidding, if one was accepted
    public List<Effect> Effects = new();
    public string? LastBidding;

    // Set by the rules, shown and cleared by the UI.
    [System.Text.Json.Serialization.JsonIgnore] public string? NewWound;
    [System.Text.Json.Serialization.JsonIgnore] public readonly Queue<string> NewAdmissions = new();
    [System.Text.Json.Serialization.JsonIgnore] public (string id, bool kept)? BiddingOutcome;
    double _deaconT, _admitT;

    // Version 1 saves used a gate of 1e8. Scale lifetime and run Dolor with the gate so Marrow keeps its place.
    public void Migrate()
    {
        if (Version >= 2) return;
        Dolor.AllTime *= Gate / OldGate;
        Dolor.Run *= Gate / OldGate;
        Version = 2;
    }

    public int N(string id) => Owned.GetValueOrDefault(id);
    public int TotalOwned() => Owned.Values.Sum();
    public bool Has(string sac) => SacBought.Contains(sac);
    public bool Knows(string node) => Lattice.Contains(node);

    // --- production ---
    public double MultFor(string target)
    {
        double m = 1;
        foreach (var s in Data.Sacraments)
            if (s.Target == target && Has(s.Id)) m *= s.Mult(this);
        foreach (var id in OpenWounds)
            foreach (var (t, f) in Data.Wound(id).Mods)
                if (t == target) m *= f(Wounds.GetValueOrDefault(id, 1));
        foreach (var id in Lattice)
            foreach (var (t, f) in Data.Node(id).Mods)
                if (t == target) m *= f(this);
        foreach (var (id, rank) in Deep)
            if (Data.DeepNode(id).Target == target) m *= Math.Pow(Data.DeepNode(id).Mult, rank);
        if (Bidding != null && Data.Bid(Bidding.Id)?.Burden is { } burden && burden.Target == target) m *= burden.Mult;
        foreach (var e in Effects) if (e.Target == target) m *= e.Mult;
        return m;
    }
    public double MarrowMult(int marrow) => 1 + 0.10 * marrow;
    public double AdmissionMult() => 1 + 0.01 * Admitted.Count;
    public double AllMult() => MultFor("all") * MarrowMult(MarrowEarned) * AdmissionMult() * (SurgeLeft > 0 ? 3 : 1);
    public double UnitRate(Rite r) => r.BaseProd * MultFor(r.Id) * AllMult();
    public double RiteRate(Rite r) => UnitRate(r) * N(r.Id);
    public double Dps() => Data.Rites.Sum(RiteRate);

    // --- buying rites ---
    public double Cost(Rite r, int k)
    {
        double g = r.Growth;
        return r.BaseCost * MultFor("costs") * Math.Pow(g, N(r.Id)) * (Math.Pow(g, k) - 1) / (g - 1);
    }
    public int MaxAffordable(Rite r)
    {
        double first = r.BaseCost * MultFor("costs") * Math.Pow(r.Growth, N(r.Id));
        if (Dolor.Amount < first) return 0;
        int k = (int)Math.Floor(Math.Log(Dolor.Amount * (r.Growth - 1) / first + 1, r.Growth));
        while (k > 0 && Cost(r, k) > Dolor.Amount) k--;  // float guard at exact boundaries
        return k;
    }
    // Max mode with nothing affordable shows (and targets) a single unit.
    public int BuyCount(Rite r) => Settings.BuyMode switch { "x10" => 10, "max" => Math.Max(1, MaxAffordable(r)), _ => 1 };
    public bool CanBuy(Rite r) => Cost(r, BuyCount(r)) <= Dolor.Amount;
    public bool Buy(Rite r, int? count = null)
    {
        int k = count ?? BuyCount(r);
        double c = Cost(r, k);
        if (c > Dolor.Amount) return false;
        Dolor.Amount -= c;
        Owned[r.Id] = N(r.Id) + k;
        Stats.Purchases++;
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
        Stats.Purchases++;
        if (Tolls > MaxTolls()) Tolls = MaxTolls();
        return true;
    }
    public bool AnySacAffordable() => Data.Sacraments.Any(CanBuySac);

    // --- toll ---
    public int MaxTolls() => 5 + (Has("S5") ? 3 : 0) + (Has("S22") ? 2 : 0) + (Knows("third_rope") ? 3 : 0) + (Deacon("deacon_rope") ? 2 : 0);
    public double BeatWindow() => Knows("on_the_beat") ? 0.20 : 0.10;
    public double TollValue(bool onBeat) => Math.Max(5, 10 * Dps()) * MultFor("toll") * (onBeat ? (Knows("on_the_beat") ? 2.5 : 1.5) : 1);
    public double TollRegenSec() => 30 * MultFor("tollRegen");
    public double? Toll(bool onBeat)
    {
        if (Tolls <= 0 || Mortifying()) return null;
        if (Tolls == MaxTolls()) TollRegen = 0;  // regen timer starts when the first charge is spent
        Tolls--;
        double v = TollValue(onBeat);
        Gain(v);
        Stats.TollsTotal++;
        Stats.TollsThisRun++;
        if (onBeat) Stats.TollsOnBeat++;
        Unlocks();
        return v;
    }

    // --- time ---
    public void Tick(double dt)
    {
        if (dt <= 0) return;
        double dps = Dps(), productive = dt;
        if (MortifyLeft > 0)
        {
            double m = Math.Min(dt, MortifyLeft);
            MortifyLeft -= m;
            productive = dt - m;
            if (MortifyLeft <= 0) CompleteMortify();
        }
        if (SurgeLeft > 0)  // the surge only covers its own seconds of this tick
        {
            double s = Math.Min(productive, SurgeLeft);
            Gain(dps * s);
            productive -= s;
            SurgeLeft = Math.Max(0, SurgeLeft - dt);
            dps = Dps();
        }
        Gain(dps * productive);
        if (Tolls < MaxTolls())
        {
            TollRegen += dt;
            int add = (int)(TollRegen / TollRegenSec());
            Tolls = Math.Min(MaxTolls(), Tolls + add);
            TollRegen -= add * TollRegenSec();
        }
        if (Tolls >= MaxTolls()) TollRegen = 0;  // timer pauses at max
        foreach (var e in Effects) e.Left -= dt;
        Effects.RemoveAll(e => e.Left <= 0);
        Stats.PlayTimeSec += dt;
        Stats.RunTimeSec += dt;
        Stats.BestDps = Math.Max(Stats.BestDps, dps);
        Unlocks();
        _deaconT += dt;
        if (_deaconT >= 1) { _deaconT = 0; Deacons(); }
        _admitT += dt;
        if (_admitT >= 1) { _admitT = 0; Admit(); }
    }

    void Gain(double v) { Dolor.Amount += v; Dolor.Run += v; Dolor.AllTime += v; }

    void Unlocks()
    {
        foreach (var r in Data.Rites) if (Dolor.Run >= 0.5 * r.BaseCost) Revealed.Add(r.Id);
        foreach (var s in Data.Sacraments) if (!SacUnlocked.Contains(s.Id) && s.Unlock(this)) SacUnlocked.Add(s.Id);
    }

    public double OfflineCap() => 8 * 3600 + (Has("S8") ? 16 * 3600 : 0) + (Knows("long_vigil") ? 8 * 3600 : 0);

    // Grant time spent away (closed game, or PC asleep). Negative deltas (clock went back) count as 0.
    public (double away, double gained, bool capped) ApplyAway(double seconds)
    {
        // Mortification time always passes in full; only productive time is capped.
        double mort = Math.Clamp(MortifyLeft, 0, Math.Max(0, seconds));
        double rest = Math.Max(0, seconds) - mort;
        double away = mort + Math.Min(rest, OfflineCap());
        double before = Dolor.AllTime;
        // Deacons keep buying while you're gone: step in minutes so their purchases compound.
        bool deacons = Vows().Any(v => Deacon(v.Id));
        for (double left = away, step = deacons ? 60 : away; left > 0; left -= step) Tick(Math.Min(step, left));
        if (seconds >= 60) { Tolls = MaxTolls(); TollRegen = 0; }  // real absences only: quick relaunches must not refill the rope
        Stats.LongestAwaySec = Math.Max(Stats.LongestAwaySec, seconds);
        return (away, Dolor.AllTime - before, rest > OfflineCap());
    }

    // --- Immurement ---
    public static int TotalFor(double lifetime, double gain = 1) => (int)Math.Floor(K * gain * Math.Sqrt(lifetime / Gate));
    public static double LifetimeFor(int marrow, double gain = 1) => Gate * Math.Pow(marrow / (K * gain), 2);
    public double MarrowGain() => (Knows("deeper_marrow") ? 1.25 : 1) * MultFor("marrow");
    public int Pending() => Math.Max(0, TotalFor(Dolor.AllTime, MarrowGain()) - MarrowEarned);
    public bool CanImmure() => Dolor.Run >= Gate && Pending() >= 1 && !Mortifying();
    public bool Ready() => CanImmure() && (MarrowEarned == 0 || Pending() >= MarrowEarned);
    public double NextMarrowAt() => LifetimeFor(TotalFor(Dolor.AllTime, MarrowGain()) + 1, MarrowGain());
    public int MarrowFree() => MarrowEarned - MarrowSpent;

    public int Immure()
    {
        int gained = Pending();
        MarrowEarned += gained;
        Immurements++;
        if (Stats.FastestRunSec is not { } f || Stats.RunTimeSec < f) Stats.FastestRunSec = Stats.RunTimeSec;
        ResetRun();
        Admit();
        return gained;
    }

    public void ResetRun()
    {
        bool vigil = Has("S8");
        Dolor.Amount = Deacon("deacon_tithe") ? 1e6 : 0;
        Dolor.Run = 0;
        Owned.Clear();
        Revealed = ["kneeler"];
        SacBought.Clear();
        SacUnlocked.Clear();
        if (vigil) { SacBought.Add("S8"); SacUnlocked.Add("S8"); }
        if (Knows("flesh_remembers"))
            foreach (var id in new[] { "kneeler", "choir", "tallow" }) { Owned[id] = 10; Revealed.Add(id); }
        Tolls = 5;
        TollRegen = 0;
        Stats.RunTimeSec = 0;
        Stats.TollsThisRun = 0;
        Bidding = null;  // a Bidding dies with the run; no curse
        Unlocks();
    }

    // Ichor pool fill 0..1: run progress to the first gate, then progress to the next Marrow.
    public double PoolLevel()
    {
        if (MarrowEarned == 0) return RunProgress();
        double g = MarrowGain();
        int m = TotalFor(Dolor.AllTime, g);
        double lo = LifetimeFor(m, g), hi = LifetimeFor(m + 1, g);
        return Math.Clamp((Dolor.AllTime - lo) / (hi - lo), 0, 1);
    }

    // 0..1 on a log scale toward the gate: drives the palette decay, the rot and the pool.
    public double RunProgress() => Math.Clamp(Math.Log10(Dolor.Run + 1) / Math.Log10(Gate), 0, 1);

    // --- the Lattice ---
    public bool CanLearn(Node n) => !Knows(n.Id) && (n.Requires == null || Knows(n.Requires)) && MarrowFree() >= n.Cost;
    public bool Learn(Node n)
    {
        if (!CanLearn(n)) return false;
        MarrowSpent += n.Cost;
        Lattice.Add(n.Id);
        Admit();
        return true;
    }

    // --- the Deep ---
    public int DeepRank(string id) => Deep.GetValueOrDefault(id);
    public int DeepCost(DeepNode d) => (int)Math.Min(int.MaxValue, d.Base * Math.Pow(DeepGrowth, DeepRank(d.Id)));
    public bool DeepOpen(DeepNode d) => Data.Lattice.Any(n => n.Branch == d.Branch && n.Tier == 5 && Knows(n.Id));
    public bool CanDeepen(DeepNode d) => DeepOpen(d) && MarrowFree() >= DeepCost(d);
    public bool Deepen(DeepNode d)
    {
        if (!CanDeepen(d)) return false;
        MarrowSpent += DeepCost(d);
        Deep[d.Id] = DeepRank(d.Id) + 1;
        Admit();
        return true;
    }

    // --- Deacons ---
    public IEnumerable<Vow> Vows() => Data.Vows;
    public bool Sworn(string vow) => MarrowEarned >= Data.Vow(vow).Marrow;
    public bool Deacon(string vow) => Sworn(vow) && !DeaconsOff.Contains(vow);

    void Deacons()
    {
        if (Mortifying()) return;  // the deacons kneel with him
        if (Deacon("deacon_sac"))
            foreach (var s in Data.Sacraments.Where(CanBuySac).OrderBy(s => s.Cost).ToList()) BuySac(s);
        string[] mine = Deacon("deacon_all") ? Data.Rites.Select(r => r.Id).ToArray() : Deacon("deacon_low") ? ["kneeler", "choir"] : [];
        // Buy the best-value Rite when it is one of theirs. Otherwise only buy their own when it costs next to nothing,
        // so they never starve a better purchase you are saving for.
        for (int i = 0; i < 500 && mine.Length > 0; i++)
        {
            var revealed = Data.Rites.Where(r => Revealed.Contains(r.Id) && UnitRate(r) > 0).ToList();
            var best = revealed.MinBy(r => Cost(r, 1) / UnitRate(r));
            var pick = best != null && mine.Contains(best.Id) ? best
                     : revealed.Where(r => mine.Contains(r.Id) && Cost(r, 1) <= 0.02 * Dolor.Amount).MinBy(r => Cost(r, 1));
            if (pick == null || !Buy(pick, 1)) break;
        }
        if (Deacon("deacon_rope") && Tolls >= MaxTolls()) Toll(false);
    }

    public bool AutoImmureDue() => Deacon("deacon_wall") && CanImmure() && Pending() >= AutoImmureAt * Math.Max(1, MarrowEarned);

    // --- Admissions ---
    void Admit()
    {
        foreach (var a in Data.Admissions)
            if (!Admitted.Contains(a.Id) && a.Earned(this)) { Admitted.Add(a.Id); NewAdmissions.Enqueue(a.Id); }
    }

    // --- Omens ---
    public int OmenKind() => Random.Shared.Next(3);  // 0 surge ×3 for 60s, 1 tithe of 15 minutes, 2 the rope refilled
    public double OmenValue(int kind) => kind == 1 ? Dps() * 900 : 0;
    public void ClaimOmen(int kind)
    {
        if (kind == 0) SurgeLeft = 60;
        else if (kind == 1) Gain(Dps() * 900);
        else { Tolls = MaxTolls(); TollRegen = 0; }
        Stats.Omens++;
        Unlocks();
        Admit();
    }

    // --- Biddings ---
    public bool MetProphet, MetWife;
    public List<bool> WifeKept = new();  // one entry per chapter of Ysmay's told so far: kept or not. Its count is her next chapter.
    public int RefusedInRow;   // the Prophet's refusals since you last accepted one
    public bool CanBeVisited() => Immurements > 0 && Bidding == null && !Mortifying() && Data.Biddings.Length > 0;
    public bool WifeDone() => Data.Chapters().Length > 0 && WifeKept.Count >= Data.Chapters().Length;

    // Once you have met the Prophet and been walled in twice, Ysmay takes every other knock until her story is told.
    public Bidding NextBidding()
    {
        var chapters = Data.Chapters();
        bool wifeLast = LastBidding != null && Data.Bid(LastBidding)?.By == "wife";
        if (Data.Wife != null && MetProphet && Immurements >= 2 && WifeKept.Count < chapters.Length && !wifeLast) return chapters[WifeKept.Count];
        var all = Data.Biddings.Where(b => b.By == null).ToArray();
        if (all.Length == 0) return chapters[Math.Min(WifeKept.Count, chapters.Length - 1)];
        var pool = all.Where(b => b.Id != LastBidding).ToArray();
        if (pool.Length == 0) pool = all;
        return pool[Random.Shared.Next(pool.Length)];
    }

    public void Accept(Bidding b)
    {
        var o = b.Objective;
        double baseline = o.Type switch
        {
            "toll" or "silence" => Stats.TollsTotal,
            "beat" => Stats.TollsOnBeat,
            "buy" => N(o.Target ?? ""),
            "gather" => Dolor.AllTime,
            "abstain" => Stats.Purchases,
            _ => 0,
        };
        double goal = o.Type == "gather" ? o.Amount * 60 * Math.Max(Dps(), 1) : o.Amount;
        Bidding = new ActiveBidding { Id = b.Id, Left = o.Seconds, Base = baseline, Goal = goal };
        LastBidding = b.Id;
        if (b.By == null) RefusedInRow = 0;
    }

    // True when this refusal spurns the Prophet: the third in a row leaves his curse (and the count starts again).
    public bool Refuse(Bidding b)
    {
        LastBidding = b.Id;
        if (b.By != null) return false;  // Ysmay just comes back with the same chapter
        Stats.BiddingsRefused++;
        Admit();
        if (++RefusedInRow < 3 || Data.Prophet?.Spurned is not { } m) return false;
        RefusedInRow = 0;
        Effects.Add(new Effect { Target = m.Target, Mult = m.Mult, Left = m.Seconds, Source = "spurned" });
        return true;
    }

    // 0..1 toward the goal (abstain and silence are measured in time endured).
    public double BiddingProgress()
    {
        if (Bidding == null || Data.Bid(Bidding.Id) is not { } b) return 0;
        var o = b.Objective;
        double now = o.Type switch
        {
            "toll" => Stats.TollsTotal - Bidding.Base,
            "beat" => Stats.TollsOnBeat - Bidding.Base,
            "buy" => N(o.Target ?? "") - Bidding.Base,
            "gather" => Dolor.AllTime - Bidding.Base,
            "mortify" => Bidding.Progress,
            _ => o.Seconds - Bidding.Left,
        };
        double goal = o.Type is "abstain" or "silence" ? o.Seconds : o.Type == "mortify" ? 1 : Bidding.Goal;
        return Math.Clamp(now / Math.Max(goal, 1e-9), 0, 1);
    }

    // Runs only while the window is visible: hiding the game never fails a Bidding.
    public void TickBidding(double dt)
    {
        if (Bidding == null || Data.Bid(Bidding.Id) is not { } b) { Bidding = null; return; }
        Bidding.Left -= dt;
        var o = b.Objective;
        bool broken = o.Type == "abstain" ? Stats.Purchases > Bidding.Base : o.Type == "silence" && Stats.TollsTotal > Bidding.Base;
        bool endured = o.Type is "abstain" or "silence";
        if (broken) EndBidding(b, false);
        else if (!endured && BiddingProgress() >= 1) EndBidding(b, true);
        else if (Bidding.Left <= 0) EndBidding(b, endured);
    }

    void EndBidding(Bidding b, bool kept)
    {
        var m = kept ? b.Boon : b.Curse;
        if (m != null) Effects.Add(new Effect { Target = m.Target, Mult = m.Mult, Left = m.Seconds, Source = b.Id });
        if (b.By == "wife") WifeKept.Add(kept);  // kept or not, her story moves on
        else if (kept) Stats.BiddingsDone++; else Stats.BiddingsFailed++;
        Bidding = null;
        BiddingOutcome = (b.Id, kept);
        Admit();
    }

    // --- Mortification ---
    public bool Mortifying() => MortifyLeft > 0;
    public bool CanMortify() => Immurements > 0 && !Mortifying();
    public double MortifyProgress() => MortifyTotal > 0 ? 1 - MortifyLeft / MortifyTotal : 0;
    public int OpenSlots() => 3 + (Knows("fourth_wound") ? 1 : 0);

    public void BeginMortify(int tier)
    {
        if (!CanMortify()) return;
        MortifyTier = tier;
        MortifyTotal = MortifyLeft = Data.MortifySeconds[tier];
    }

    public void AbandonMortify() { MortifyLeft = 0; MortifyTotal = 0; MortifyTier = -1; }

    // Roll a depth for the tier, then a wound of that depth that can still deepen (falling back to any that can).
    void CompleteMortify()
    {
        var odds = Data.DepthOdds[Math.Clamp(MortifyTier, 0, 3)];
        double roll = Random.Shared.NextDouble();
        int depth = roll < odds[0] ? 0 : roll < odds[0] + odds[1] ? 1 : 2;
        bool CanDeepen(Wound w) => Wounds.GetValueOrDefault(w.Id) < MaxRank;
        var pool = Data.Wounds.Where(w => w.Depth == depth && CanDeepen(w)).ToArray();
        if (pool.Length == 0) pool = Data.Wounds.Where(CanDeepen).ToArray();
        if (MortifyTier == 3) Stats.LongVigils++;
        if (Bidding != null && Data.Bid(Bidding.Id)?.Objective is { Type: "mortify" } o && MortifyTier >= o.Amount) Bidding.Progress = 1;
        MortifyTotal = 0;
        MortifyTier = -1;
        Mortifications++;
        if (pool.Length == 0) { NewWound = null; return; }
        var w = pool[Random.Shared.Next(pool.Length)];
        Wounds[w.Id] = Wounds.GetValueOrDefault(w.Id) + 1;
        if (OpenWounds.Count < OpenSlots()) OpenWounds.Add(w.Id);
        NewWound = w.Id;
    }

    public bool ToggleWound(string id)
    {
        if (OpenWounds.Remove(id)) return true;
        if (!Wounds.ContainsKey(id) || OpenWounds.Count >= OpenSlots()) return false;
        OpenWounds.Add(id);
        return true;
    }

    public double Bpm() => Mortifying() ? 30 : Math.Min(140, 40 + 12 * Math.Log10(Dps() + 1));  // it starves while he is away
    public double Souls() => Math.Floor(Dolor.AllTime / 1000);
}
