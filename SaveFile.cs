using System.Text.Json;

// JSON save in %APPDATA%\Mortis. Write temp -> swap in (keeping .bak), so a crash mid-write can't eat the save.
static class SaveFile
{
    // MORTIS_SAVE_DIR overrides the location (testing with throwaway saves).
    public static readonly string Dir = Environment.GetEnvironmentVariable("MORTIS_SAVE_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mortis");
    static readonly string Main = Path.Combine(Dir, "save.json"), Bak = Main + ".bak", Tmp = Main + ".tmp";
    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true, IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Serialize<T>(T state) => JsonSerializer.Serialize(state, Opts);
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Opts);

    // Tries save.json, then save.json.bak. A save.json that won't load is moved aside (never deleted) even when the .bak
    // rescues us: otherwise the next Save's File.Replace would push the broken file over the good backup.
    public static T? Load<T>() where T : class
    {
        if (TryRead<T>(Main) is { } main) return main;
        var bak = TryRead<T>(Bak);
        if (File.Exists(Main)) File.Move(Main, Path.Combine(Dir, $"save.corrupt-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.json"));
        return bak;
    }

    static T? TryRead<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try { return Deserialize<T>(File.ReadAllText(path)); }
        catch (Exception) { return null; }
    }

    // Copies save.json aside as save.<why>-<unixtime>.json before something replaces it on purpose (New Game).
    public static void KeepCopy(string why)
    {
        if (File.Exists(Main)) File.Copy(Main, Path.Combine(Dir, $"save.{why}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.json"));
    }

    public static void Save<T>(T state)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Tmp, Serialize(state));
        if (File.Exists(Main)) File.Replace(Tmp, Main, Bak);
        else File.Move(Tmp, Main);
    }
}
