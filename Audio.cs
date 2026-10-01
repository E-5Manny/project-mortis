using System.Runtime.InteropServices;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Every sound is synthesized at startup (44.1 kHz mono): no audio files to ship or license.
// assets/sounds/<name>.wav|.ogg|.mp3 replaces the generated sound of that name (see --dump-sounds).
static class Audio
{
    const int Rate = 44100;
    static bool _ok, _loaded;
    static Task<(Dictionary<string, float[]> sounds, Dictionary<string, float[]> beds)>? _synth;
    static readonly Dictionary<string, Sound[]> Sounds = new();  // a few voices each so repeats can overlap
    static Music _drone, _candles;
    static readonly List<GCHandle> Pinned = new();  // raylib streams music straight from these buffers
    static readonly Random Pick = new();

    // name -> (generator, voices)
    static readonly (string name, Func<float[]> gen, int voices)[] Bank =
    [
        ("heart", Heart, 2), ("drip", Drip, 3), ("plop", Plop, 2),
        ("bell", Bell, 2), ("thunk", Thunk, 1), ("chime", Chime, 2), ("clack", Clack, 3),
        ("lash", Lash, 2), ("grunt_1", () => Grunt(1), 1), ("grunt_2", () => Grunt(2), 1), ("hiss", Hiss, 1),
        ("scream_far_1", () => Scream(1, true), 1), ("scream_far_2", () => Scream(2, true), 1),
        ("scream_far_3", () => Scream(3, true), 1), ("scream_crowd", Crowd, 1), ("scream_near", () => Scream(1, false), 1),
        ("brick", Brick, 3), ("wound", WoundSwell, 1),
    ];
    static readonly (string name, Func<float[]> gen)[] Beds = [("drone", Drone), ("candles", Candles)];

    public static IEnumerable<string> Names => Bank.Select(b => b.name).Concat(Beds.Select(b => b.name));

    public static void Init()
    {
        InitAudioDevice();
        _ok = IsAudioDeviceReady();
        if (!_ok) return;  // no output device: the game stays silent
        // ~1.5s of synthesis runs off the main thread; Update() hands the buffers to raylib when it's done.
        _synth = Task.Run(() => (
            Bank.Where(b => Override(b.name) == null).ToDictionary(b => b.name, b => b.gen()),
            Beds.Where(b => Override(b.name) == null).ToDictionary(b => b.name, b => b.gen())));
    }

    static void LoadAll()
    {
        var (sounds, beds) = _synth!.Result;
        foreach (var (name, _, voices) in Bank)
        {
            var main = Override(name) is { } path ? LoadSound(path) : FromSamples(sounds[name]);
            Sounds[name] = [main, .. Enumerable.Range(1, voices - 1).Select(_ => LoadSoundAlias(main))];
        }
        _drone = Loop("drone", beds);
        _candles = Loop("candles", beds);
        PlayMusicStream(_drone);
        PlayMusicStream(_candles);
        _loaded = true;
    }

    static string? Override(string name) =>
        new[] { ".wav", ".ogg", ".mp3" }.Select(e => Path.Combine(AppContext.BaseDirectory, "assets", "sounds", name + e)).FirstOrDefault(File.Exists);

    static Sound FromSamples(float[] s)
    {
        var wave = LoadWaveFromMemory(".wav", Wav(s));
        var snd = LoadSoundFromWave(wave);
        UnloadWave(wave);
        return snd;
    }

    static unsafe Music Loop(string name, Dictionary<string, float[]> beds)
    {
        if (Override(name) is { } path) return LoadMusicStream(path);
        var bytes = Wav(beds[name]);
        var h = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        Pinned.Add(h);
        var ext = stackalloc sbyte[] { (sbyte)'.', (sbyte)'w', (sbyte)'a', (sbyte)'v', 0 };
        var m = LoadMusicStreamFromMemory(ext, (byte*)h.AddrOfPinnedObject(), bytes.Length);
        m.Looping = true;
        return m;
    }

    // Once per frame. candles: 0..1 how many are lit.
    public static void Update(Settings s, bool focused, float candles)
    {
        if (!_ok) return;
        if (!_loaded) { if (_synth!.IsCompleted) LoadAll(); else return; }
        bool silent = !s.SoundOn || (s.SilentUnfocused && !focused);
        SetMasterVolume(silent ? 0 : s.Volume);
        UpdateMusicStream(_drone);
        UpdateMusicStream(_candles);
        SetMusicVolume(_drone, 0.55f);
        SetMusicVolume(_candles, candles <= 0 ? 0 : 0.25f + 0.75f * candles);
    }

    // Panic: silence at once, and stop feeding the streams while hidden.
    public static void Hide(bool hidden)
    {
        if (!_ok || !_loaded) return;
        if (hidden) { SetMasterVolume(0); PauseMusicStream(_drone); PauseMusicStream(_candles); }
        else { ResumeMusicStream(_drone); ResumeMusicStream(_candles); }
    }

    public static void Play(string name, float volume = 1, float pitch = 1, float pan = 0.5f)
    {
        if (!_ok || !_loaded || !Sounds.TryGetValue(name, out var voices)) return;
        var s = voices[0];
        foreach (var v in voices) if (!IsSoundPlaying(v)) { s = v; break; }
        SetSoundVolume(s, volume);
        SetSoundPitch(s, pitch);
        SetSoundPan(s, pan);
        PlaySound(s);
    }

    // Random pick among sounds starting with prefix, with a little pitch and pan drift.
    public static void PlayAny(string prefix, float volume = 1)
    {
        var names = Sounds.Keys.Where(k => k.StartsWith(prefix)).ToArray();
        if (names.Length == 0) return;
        Play(names[Pick.Next(names.Length)], volume, 0.92f + 0.16f * (float)Pick.NextDouble(), 0.3f + 0.4f * (float)Pick.NextDouble());
    }

    // Test button: beds play as a one-shot clip so they can be heard alone.
    public static void Audition(string name)
    {
        if (Sounds.ContainsKey(name)) { Play(name); return; }
        if (!_ok || !_loaded || Beds.FirstOrDefault(b => b.name == name).gen is not { } gen) return;
        if (!Sounds.ContainsKey("_" + name)) Sounds["_" + name] = [FromSamples(gen()[..(Rate * 6)])];
        Play("_" + name);
    }

    // --dump-sounds: every generated sound as a WAV.
    public static void Dump(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var (name, gen, _) in Bank) File.WriteAllBytes(Path.Combine(dir, name + ".wav"), Wav(gen()));
        foreach (var (name, gen) in Beds) File.WriteAllBytes(Path.Combine(dir, name + ".wav"), Wav(gen()));
    }

    public static IEnumerable<(string name, float[] samples, bool loop)> All() =>
        Bank.Select(b => (b.name, b.gen(), false)).Concat(Beds.Select(b => (b.name, b.gen(), true)));

    static byte[] Wav(float[] s)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + s.Length * 2); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(s.Length * 2);
        foreach (var v in s) w.Write((short)Math.Clamp(v * 32767, -32768, 32767));
        return ms.ToArray();
    }

    // ---------------------------------------------------------------- DSP

    static float[] Buf(double sec) => new float[(int)(sec * Rate)];
    static double T(int i) => (double)i / Rate;
    static double Noise(Random r) => r.NextDouble() * 2 - 1;

    static float[] Lowpass(float[] x, double hz)
    {
        double a = 1 - Math.Exp(-2 * Math.PI * hz / Rate), y = 0;
        var o = new float[x.Length];
        for (int i = 0; i < x.Length; i++) { y += a * (x[i] - y); o[i] = (float)y; }
        return o;
    }

    static float[] Highpass(float[] x, double hz)
    {
        var lo = Lowpass(x, hz);
        for (int i = 0; i < x.Length; i++) lo[i] = x[i] - lo[i];
        return lo;
    }

    // RBJ band-pass, 0 dB peak.
    static float[] Bandpass(float[] x, double f, double q)
    {
        double w0 = 2 * Math.PI * f / Rate, alpha = Math.Sin(w0) / (2 * q), a0 = 1 + alpha;
        double b0 = alpha / a0, b2 = -alpha / a0, a1 = -2 * Math.Cos(w0) / a0, a2 = (1 - alpha) / a0;
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        var o = new float[x.Length];
        for (int i = 0; i < x.Length; i++)
        {
            double y = b0 * x[i] + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x[i]; y2 = y1; y1 = y;
            o[i] = (float)y;
        }
        return o;
    }

    // Small Schroeder/Freeverb-style room: parallel damped combs into series all-passes. Extends the buffer by `tail`.
    static float[] Reverb(float[] dry, double wet, double room = 0.84, double tail = 2.0)
    {
        var x = new float[dry.Length + (int)(tail * Rate)];
        Array.Copy(dry, x, dry.Length);
        var wetBus = new float[x.Length];
        foreach (int d in new[] { 1557, 1617, 1491, 1422, 1277, 1356 })
        {
            var buf = new float[d];
            double filt = 0;
            for (int i = 0, k = 0; i < x.Length; i++, k = (k + 1) % d)
            {
                double y = buf[k];
                filt = y * 0.75 + filt * 0.25;
                buf[k] = (float)(x[i] + filt * room);
                wetBus[i] += (float)y;
            }
        }
        foreach (int d in new[] { 225, 556, 441, 341 })
        {
            var buf = new float[d];
            for (int i = 0, k = 0; i < x.Length; i++, k = (k + 1) % d)
            {
                double b = buf[k], y = -wetBus[i] + b;
                buf[k] = (float)(wetBus[i] + b * 0.5);
                wetBus[i] = (float)y;
            }
        }
        double scale = (1 - room) / 6 * 2.2;
        for (int i = 0; i < x.Length; i++) x[i] = (float)(x[i] * (1 - wet) + wetBus[i] * scale * wet * 3);
        return x;
    }

    static float[] Normalize(float[] x, double peak)
    {
        double m = 1e-9;
        foreach (var v in x) m = Math.Max(m, Math.Abs(v));
        for (int i = 0; i < x.Length; i++) x[i] = (float)(x[i] / m * peak);
        return x;
    }

    static float[] Add(float[] into, float[] src, double at = 0, double gain = 1)
    {
        int o = (int)(at * Rate);
        for (int i = 0; i < src.Length && o + i < into.Length; i++) into[o + i] += (float)(src[i] * gain);
        return into;
    }

    // Seamless loop: the extra `fade` seconds at the end are cross-faded (equal power) into the start.
    static float[] MakeLoop(float[] x, double fade)
    {
        int f = (int)(fade * Rate), n = x.Length - f;
        var o = x[..n];
        for (int i = 0; i < f; i++)
        {
            double a = (double)i / f;
            o[i] = (float)(x[i] * Math.Sqrt(a) + x[n + i] * Math.Sqrt(1 - a));
        }
        return o;
    }

    // Glottal source: band-limited-ish saw with jitter and breath, for screams.
    static float[] Voice(double sec, Func<double, double> f0, double breath, Random r)
    {
        var o = Buf(sec);
        double ph = 0, jit = 0;
        for (int i = 0; i < o.Length; i++)
        {
            jit = jit * 0.999 + Noise(r) * 0.02;
            ph += f0(T(i)) * (1 + jit * 0.02) / Rate;
            double saw = 2 * (ph - Math.Floor(ph)) - 1;
            o[i] = (float)(saw * 0.7 + Noise(r) * breath);
        }
        return Lowpass(o, 5000);
    }

    static float[] Formants(float[] src, (double f, double q, double gain)[] fs)
    {
        var o = new float[src.Length];
        foreach (var (f, q, g) in fs) Add(o, Bandpass(src, f, q), 0, g);
        return o;
    }

    static void Envelope(float[] x, Func<double, double> env) { for (int i = 0; i < x.Length; i++) x[i] *= (float)env(T(i)); }

    // ---------------------------------------------------------------- sounds

    // Wet "lub-dub": two falling low thumps with a squelch of filtered noise, softly clipped so small speakers carry it.
    static float[] Heart()
    {
        var r = new Random(1);
        var o = Buf(0.7);
        foreach (var (at, f0, sweep, decay, gain) in new[] { (0.0, 46.0, 36.0, 13.0, 1.0), (0.19, 56.0, 42.0, 18.0, 0.7) })
        {
            double ph = 0, nz = 0;
            for (int i = (int)(at * Rate); i < o.Length; i++)
            {
                double t = T(i) - at;
                ph += 2 * Math.PI * (f0 + sweep * Math.Exp(-t * 30)) / Rate;
                double env = (1 - Math.Exp(-t * 400)) * Math.Exp(-t * decay);
                nz += 0.08 * (Noise(r) - nz);
                o[i] += (float)(gain * env * (Math.Sin(ph) + 0.35 * Math.Sin(2 * ph) + nz * 3 * Math.Exp(-t * 50)));
            }
        }
        for (int i = 0; i < o.Length; i++) o[i] = (float)Math.Tanh(1.8 * o[i]);
        return Normalize(Reverb(Lowpass(o, 900), 0.12, 0.7, 0.5), 0.9);
    }

    // Water-drop resonance: a short sine whose pitch leaps upward as the bubble closes.
    static float[] Drip()
    {
        var o = Buf(0.25);
        double ph = 0;
        for (int i = 0; i < o.Length; i++)
        {
            double t = T(i);
            ph += 2 * Math.PI * Math.Min(2600, 700 * Math.Exp(t * 28)) / Rate;
            o[i] = (float)(Math.Sin(ph) * Math.Exp(-t * 42) * (1 - Math.Exp(-t * 3000)));
        }
        return Normalize(Reverb(o, 0.25, 0.8, 0.8), 0.7);
    }

    static float[] Plop()
    {
        var r = new Random(2);
        var o = Buf(0.35);
        double ph = 0;
        for (int i = 0; i < o.Length; i++)
        {
            double t = T(i);
            ph += 2 * Math.PI * Math.Min(900, 260 * Math.Exp(t * 12)) / Rate;
            o[i] = (float)(Math.Sin(ph) * Math.Exp(-t * 20) + Noise(r) * 0.3 * Math.Exp(-t * 40));
        }
        return Normalize(Reverb(Lowpass(o, 2500), 0.2, 0.8, 0.8), 0.7);
    }

    // A great bell: inharmonic partials (hum, prime, tierce, quint, nominal...), each pair slightly detuned so it beats.
    static float[] BellPartials(double f0, (double ratio, double amp, double decay)[] partials, double sec, Random r)
    {
        var o = Buf(sec);
        foreach (var (ratio, amp, decay) in partials)
            for (int d = 0; d < 2; d++)
            {
                double f = f0 * ratio + (d == 0 ? 0 : 0.35 + ratio * 0.1), ph = r.NextDouble() * 6.28;
                for (int i = 0; i < o.Length; i++)
                    o[i] += (float)(0.5 * amp * Math.Sin(ph + 2 * Math.PI * f * T(i)) * Math.Exp(-T(i) / decay));
            }
        for (int i = 0; i < 400; i++) o[i] += (float)(Noise(r) * 0.4 * Math.Exp(-i / 60.0));  // the clapper's strike
        return o;
    }

    static float[] Bell()
    {
        var r = new Random(3);
        var o = BellPartials(98, [(0.5, 0.5, 4.5), (1, 1, 3.6), (1.19, 0.6, 2.6), (1.5, 0.45, 2.2), (2, 0.55, 1.8),
                                  (2.51, 0.3, 1.2), (2.66, 0.25, 1.0), (3.0, 0.2, 0.8), (4.07, 0.15, 0.5), (5.4, 0.08, 0.3)], 5, r);
        return Normalize(Reverb(o, 0.35, 0.88, 2.5), 0.85);
    }

    // The rope pulled with no charge left: the clapper barely moves, a dead wooden knock.
    static float[] Thunk()
    {
        var r = new Random(4);
        var o = Buf(0.4);
        double ph = 0;
        for (int i = 0; i < o.Length; i++)
        {
            double t = T(i);
            ph += 2 * Math.PI * (50 + 25 * Math.Exp(-t * 40)) / Rate;
            o[i] = (float)(Math.Sin(ph) * Math.Exp(-t * 25) + Noise(r) * 0.5 * Math.Exp(-t * 60));
        }
        return Normalize(Reverb(Bandpass(o, 380, 0.8), 0.15, 0.7, 0.4), 0.7);
    }

    static float[] Chime()
    {
        var r = new Random(5);
        var o = BellPartials(880, [(1, 1, 1.0), (2.0, 0.5, 0.6), (2.76, 0.4, 0.4), (5.4, 0.2, 0.25)], 1.4, r);
        return Normalize(Reverb(o, 0.3, 0.85, 1.5), 0.5);
    }

    // Bone on bone: two short resonant clicks.
    static float[] Clack()
    {
        var r = new Random(6);
        var o = Buf(0.12);
        foreach (var (at, f) in new[] { (0.0, 1700.0), (0.025, 2400.0) })
        {
            var click = Buf(0.05);
            for (int i = 0; i < click.Length; i++) click[i] = (float)(Noise(r) * Math.Exp(-T(i) * 300));
            Add(o, Bandpass(click, f, 10), at, 3);
        }
        return Normalize(Reverb(o, 0.15, 0.75, 0.5), 0.6);
    }

    // The lash: a near-supersonic crack, then the wet slap of leather on a back.
    static float[] Lash()
    {
        var r = new Random(7);
        var o = Buf(0.35);
        for (int i = 0; i < 140; i++) o[i] = (float)(Noise(r) * (1 - i / 140.0));       // crack
        var hiss = Buf(0.1);
        for (int i = 0; i < hiss.Length; i++) hiss[i] = (float)(Noise(r) * Math.Exp(-T(i) * 90));
        Add(o, Highpass(hiss, 2000), 0, 0.7);
        var slap = Buf(0.2);
        for (int i = 0; i < slap.Length; i++) slap[i] = (float)(Noise(r) * Math.Exp(-T(i) * 35) + 0.5 * Math.Sin(2 * Math.PI * 120 * T(i)) * Math.Exp(-T(i) * 40));
        Add(o, Lowpass(slap, 800), 0.012, 0.9);
        var squish = Buf(0.15);
        for (int i = 0; i < squish.Length; i++) squish[i] = (float)(Noise(r) * Math.Exp(-Math.Pow((T(i) - 0.05) / 0.025, 2)));
        Add(o, Bandpass(squish, 1200, 2), 0.01, 0.6);
        return Normalize(Reverb(o, 0.2, 0.8, 0.7), 0.85);
    }

    // Pain through clenched teeth: an abrupt, strangled "nngh". Creaky, irregular pulses (vocal fry) and a closed,
    // nasal mouth; the breath is cut off rather than let out. Short on purpose: a long, smooth vowel reads as a moan.
    static float[] Grunt(int v)
    {
        var r = new Random(10 + v);
        var o = Buf(0.45);
        double t0 = 0, f0 = v == 1 ? 135 : 115;
        while (t0 < 0.32)
        {
            double f = f0 * (t0 < 0.05 ? 1 : 0.78) * (1 + Noise(r) * 0.12);   // pitch breaks down, period jitters
            double amp = 0.6 + 0.4 * r.NextDouble();                          // shimmer
            for (int k = 0; k < 400; k++)                                     // one sharp, pressed glottal pulse
            {
                int i = (int)(t0 * Rate) + k;
                if (i >= o.Length) break;
                o[i] += (float)(amp * Math.Exp(-k / 18.0) * (k < 3 ? 1 : 0.6));
            }
            t0 += 1 / f;
        }
        for (int i = 0; i < o.Length; i++) o[i] += (float)(Noise(r) * 0.08);  // strain in the throat
        var voiced = Formants(o, v == 1 ? [(280, 4, 1), (1450, 7, 0.35), (2300, 9, 0.15)] : [(250, 4, 1), (1250, 7, 0.3), (2500, 9, 0.12)]);
        Envelope(voiced, t => Math.Min(1, t / 0.012)                           // it hits, it doesn't swell
                              * (t > 0.11 && t < 0.15 ? 0.45 : 1)               // the breath catches
                              * (t > 0.2 ? Math.Exp(-(t - 0.2) * 14) : 1));
        var exhale = Buf(0.45);
        for (int i = 0; i < exhale.Length; i++) exhale[i] = (float)(Noise(r) * Math.Exp(-Math.Pow((T(i) - 0.3) / 0.06, 2)) * 0.25);
        Add(voiced, Lowpass(exhale, 900));
        return Normalize(Reverb(Lowpass(voiced, 1300), 0.22, 0.8, 0.7), 0.55);
    }

    // A sharp breath drawn in through the teeth after the lash lands.
    static float[] Hiss()
    {
        var r = new Random(15);
        var o = Buf(0.35);
        for (int i = 0; i < o.Length; i++) o[i] = (float)Noise(r);
        var s = Add(Bandpass(o, 5200, 2), Bandpass(o, 2900, 3), 0, 0.5);
        Envelope(s, t => Math.Min(1, t / 0.035) * Math.Exp(-Math.Max(0, t - 0.06) * 13) * (1 + 0.25 * Math.Sin(t * 90)));  // a shudder in it
        return Normalize(Reverb(s, 0.15, 0.75, 0.5), 0.35);
    }

    // A scream: a rising, breaking, vibrato-shaken pitch through an open vowel. Far = filtered by stone and distance.
    static float[] Scream(int v, bool far)
    {
        var r = new Random(20 + v);
        double sec = 2.6;
        (double start, double peak, double end) c = v switch { 1 => (330, 540, 380), 2 => (280, 470, 300), _ => (380, 610, 450) };
        Func<double, double> f0 = t =>
        {
            double shape = t < 0.4 ? c.start + (c.peak - c.start) * t / 0.4 : c.peak + (c.end - c.peak) * (t - 0.4) / (sec - 0.4);
            return shape * (1 + 0.03 * Math.Sin(2 * Math.PI * 5.5 * t) + (t > 1.2 && t < 1.35 ? 0.12 : 0));  // vibrato, and a crack in the voice
        };
        var src = Voice(sec, f0, 0.35, r);
        (double, double, double)[] vowel = v switch
        {
            1 => [(850, 6, 1), (1220, 8, 0.7), (2810, 10, 0.3)],
            2 => [(600, 6, 1), (1900, 9, 0.6), (2600, 10, 0.3)],
            _ => [(520, 6, 1), (900, 8, 0.7), (2500, 10, 0.25)],
        };
        var o = Formants(src, vowel);
        Envelope(o, t => Math.Min(1, t / 0.2) * (t > 1.9 ? Math.Exp(-(t - 1.9) * 5) : 1) * (0.85 + 0.15 * Math.Sin(t * 13)));
        o = far ? Reverb(Lowpass(o, 1400), 0.7, 0.9, 3.0) : Reverb(Lowpass(o, 3200), 0.35, 0.86, 2.0);
        return Normalize(o, far ? 0.55 : 0.8);
    }

    // "Each one is still screaming": many voices, very far away.
    static float[] Crowd()
    {
        var o = Buf(8);  // room for the last voice's tail
        for (int k = 0; k < 6; k++)
        {
            var s = Scream(1 + k % 3, true);
            Add(o, s, k * 0.37, 0.4 + 0.1 * (k % 2));
        }
        return Normalize(Lowpass(o, 900), 0.45);
    }

    // A brick set into place: a low knock and a grit of mortar.
    static float[] Brick()
    {
        var r = new Random(30);
        var o = Buf(0.4);
        double ph = 0;
        for (int i = 0; i < o.Length; i++)
        {
            double t = T(i);
            ph += 2 * Math.PI * (45 + 20 * Math.Exp(-t * 30)) / Rate;
            o[i] = (float)(Math.Sin(ph) * Math.Exp(-t * 14) + Noise(r) * 0.6 * Math.Exp(-t * 25));
        }
        var grit = Buf(0.12);
        for (int i = 0; i < grit.Length; i++) grit[i] = (float)(Noise(r) * Math.Exp(-T(i) * 30));
        Add(o, Bandpass(grit, 2000, 1.5), 0.01, 0.4);
        return Normalize(Reverb(Lowpass(o, 1200), 0.2, 0.8, 0.6), 0.75);
    }

    // A wound given: a detuned low swell that opens up, then one heavy beat.
    static float[] WoundSwell()
    {
        var r = new Random(40);
        var o = Buf(3.2);
        double[] freqs = [55, 55.4, 82.6, 110.3];
        foreach (var f in freqs)
            for (int i = 0; i < o.Length; i++)
                o[i] += (float)(0.25 * (2 * (f * T(i) % 1) - 1));
        var swell = Buf(3.2);
        for (int i = 0; i < swell.Length; i++) swell[i] = (float)(Noise(r) * 0.3);
        Add(o, Bandpass(swell, 600, 0.7));
        // sweep a low-pass open over two seconds by mixing three fixed ones
        var lo = Lowpass(o, 200); var mid = Lowpass(o, 500); var hi = Lowpass(o, 1100);
        for (int i = 0; i < o.Length; i++)
        {
            double t = T(i), k = Math.Min(1, t / 2.2);
            double mixed = k < 0.5 ? lo[i] + (mid[i] - lo[i]) * k * 2 : mid[i] + (hi[i] - mid[i]) * (k - 0.5) * 2;
            o[i] = (float)(mixed * Math.Min(1, t / 2.2) * (t > 2.25 ? Math.Exp(-(t - 2.25) * 6) : 1));
        }
        Add(o, Heart(), 2.2, 0.9);
        return Normalize(Reverb(o, 0.4, 0.86, 2.0), 0.8);
    }

    // ---------------------------------------------------------------- beds (looping)

    // The cathedral: wind moving through a hollow body, a low hum that beats against itself.
    static float[] Drone()
    {
        var r = new Random(50);
        double sec = 24, fade = 4;
        var o = Buf(sec + fade);
        var noise = Buf(sec + fade);
        for (int i = 0; i < noise.Length; i++) noise[i] = (float)Noise(r);
        // wind: band-pass whose centre wanders; recompute coefficients every 256 samples
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        double b0 = 0, b2 = 0, a1 = 0, a2 = 0;
        for (int i = 0; i < o.Length; i++)
        {
            double t = T(i);
            if (i % 256 == 0)
            {
                double fc = 300 + 170 * Math.Sin(2 * Math.PI * t / 11) + 80 * Math.Sin(2 * Math.PI * t / 4.3);
                double w0 = 2 * Math.PI * fc / Rate, alpha = Math.Sin(w0) / 3, a0 = 1 + alpha;
                b0 = alpha / a0; b2 = -alpha / a0; a1 = -2 * Math.Cos(w0) / a0; a2 = (1 - alpha) / a0;
            }
            double y = b0 * noise[i] + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = noise[i]; y2 = y1; y1 = y;
            double gust = 0.55 + 0.3 * Math.Sin(2 * Math.PI * t / 7) + 0.15 * Math.Sin(2 * Math.PI * t / 2.9);
            o[i] = (float)(y * gust * 0.9);
        }
        var rumble = Lowpass(Lowpass(noise, 90), 90);
        Add(o, rumble, 0, 6);
        for (int i = 0; i < o.Length; i++)
        {
            double t = T(i), trem = 0.7 + 0.3 * Math.Sin(2 * Math.PI * t / 9);
            o[i] += (float)(0.10 * trem * (Math.Sin(2 * Math.PI * 55 * t) + Math.Sin(2 * Math.PI * 55.18 * t) + 0.6 * Math.Sin(2 * Math.PI * 82.5 * t)));
        }
        o = Reverb(o, 0.3, 0.88, 0)[..o.Length];
        return Normalize(MakeLoop(o, fade), 0.5);
    }

    // Candle flames: a soft breathing hiss and the tick and pop of tallow.
    static float[] Candles()
    {
        var r = new Random(60);
        double sec = 12, fade = 1;
        var o = Buf(sec + fade);
        for (int i = 0; i < o.Length; i++)
        {
            double t = T(i), flicker = 0.6 + 0.2 * Math.Sin(t * 2 * 6.28) + 0.2 * Math.Sin(t * 6.7 * 6.28 + 1);
            o[i] = (float)(Noise(r) * 0.06 * flicker);
        }
        o = Highpass(Lowpass(o, 3000), 300);
        for (int i = 0; i < o.Length; i++)
        {
            if (r.NextDouble() < 5.0 / Rate)                      // crackle
            {
                double a = 0.2 + 0.8 * r.NextDouble();
                for (int k = 0; k < 120 && i + k < o.Length; k++) o[i + k] += (float)(Noise(r) * a * Math.Exp(-k / 25.0));
            }
            if (r.NextDouble() < 0.3 / Rate)                      // a fat pop
                for (int k = 0; k < 900 && i + k < o.Length; k++)
                    o[i + k] += (float)((Noise(r) * 0.6 + Math.Sin(k * 0.03)) * Math.Exp(-k / 150.0));
        }
        return Normalize(MakeLoop(Reverb(Highpass(o, 200), 0.1, 0.7, 0)[..o.Length], fade), 0.5);
    }
}
