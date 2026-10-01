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

    // Tries save.json, then save.bak. If both exist but are unreadable, the broken main file is kept aside, never overwritten.
    public static T? Load<T>() where T : class
    {
        foreach (var path in new[] { Main, Bak })
        {
            if (!File.Exists(path)) continue;
            try { if (Deserialize<T>(File.ReadAllText(path)) is { } s) return s; }
            catch (Exception) { }
        }
        if (File.Exists(Main)) File.Move(Main, Path.Combine(Dir, $"save.corrupt-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.json"));
        return null;
    }

    public static void Save<T>(T state)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Tmp, Serialize(state));
        if (File.Exists(Main)) File.Replace(Tmp, Main, Bak);
        else File.Move(Tmp, Main);
    }
}
