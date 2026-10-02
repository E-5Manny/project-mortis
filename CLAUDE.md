# Mortis

A grimdark desktop idle game (C# / .NET 10 + Raylib-cs 8.1.0, which wraps raylib 6.0).
- **Window:** a fixed 630×520 window that the user keeps open at work.
- **Tone:** original IP in the spirit of Blasphemous and Mortal Shell.
- **Design:** `DESIGN.md` is the source of truth. It covers the premise, glossary, pillars, roadmap and IP rules. Read it before adding content.

## Build, run, test

- **Build and run:** `run.cmd` builds and launches. `dotnet` may not be on PATH, so use `"C:\Program Files\dotnet\dotnet.exe"`.
- **If the user's game is running,** `bin\Debug\net10.0\win-x64\Mortis.exe` is locked and `dotnet build` fails. Never kill it, because it holds their real save. Build elsewhere instead: `dotnet build -o <scratch>\build`.
- **Command-line options.** It's a WinExe with no console of its own, so pipe the output (`| cat` or `| more`):
  - `Mortis.exe --selftest` runs rule asserts, a save round trip, sanity checks on the generated audio, and a greedy-player pacing sim. The exit code is the number of failures. **Keep it passing**, and add a check for any new rule.
  - Setting `MORTIS_SIM_TIMELINE=1` makes `--selftest` also print when each Rite and Sacrament first arrives. Use it for balancing.
  - `Mortis.exe --dump-art [dir]` and `--dump-sounds [dir]` export every generated texture (PNG) and sound (WAV).
- **`--selftest` runs at the very top of `Main`,** before `InitWindow`, the mutex or loading. `Game`, `Ui.Num` / `Ui.Duration` and the `Audio.All()` synthesis must therefore keep working without raylib being initialized. Because it runs before the single-instance check, a scratch build's self-test works while the user's game is open.
- **Installer:** `installer\build.cmd` publishes a self-contained single-file Release into `publish\` and runs Inno Setup 6 (`installer\Mortis.iss`) to produce `dist\MortisSetup-<version>.exe`. It installs per user with no admin prompt, and uninstalling leaves the save alone. The version comes from `<Version>` in the csproj. It never touches `bin\Debug`, so it's safe while the game is running.
- **Environment variables:**
  - `MORTIS_SAVE_DIR=<dir>` redirects the save. **Never test against the real save** in `%APPDATA%\Mortis\save.json`.
  - `MORTIS_OMEN_IN=<seconds>` and `MORTIS_VISIT_IN=<seconds>` bring the first Omen or visitor forward.
- **One copy per save:** the single-instance mutex is keyed by a hash of the save folder. A test copy with `MORTIS_SAVE_DIR` can therefore run alongside the user's game, and a second launch with the same folder reveals the hidden copy instead.
- **Verifying UI by screenshot:**
  1. Launch the exe with `MORTIS_SAVE_DIR` pointing at a fixture `save.json`. Set `lastSeenUtc` to a future date such as 2030, so no away time is applied.
  2. Bring the window forward. Tap Alt, then call `SetForegroundWindow`; Windows blocks the call otherwise.
  3. Drive it with `SetCursorPos`, `mouse_event` and `keybd_event`.
  4. Capture the client area with `System.Drawing` `CopyFromScreen`.

  Careful: Alt followed by Space opens the Windows system menu.

## Files

| File | Role |
|---|---|
| `Game.cs` | All data tables (`Data`: Rites, Sacraments, Wounds, Lattice nodes, Vows, Admissions, Stanzas) and all rules (`Game`). It has **no raylib**, so the self-test can drive it headless. |
| `Program.cs` | `App`: the main loop, every screen, tab and overlay, input, and save timing. |
| `Ui.cs` | Palette, fonts (`Face.Title` blackletter / `Face.Body` serif), immediate-mode widgets, number and time formatting. |
| `Fx.cs` | Atmosphere: ash, candles, the ichor drip and pool, blood seeps, flies, maggots, floating numbers. |
| `Art.cs` | Generated pixel-art textures (the wall, the heart, the heart with a face, Rite icons), with PNG overrides and the dump. |
| `Sexton.cs` | The 48×64 Sexton sprite. Frames are baked per pixel from poses, and welts and blood are drawn over them at runtime. |
| `Prophet.cs` | The Lampless Prophet's 64×80 dialog portrait (frames `prophet_0` with the mouth shut and `prophet_1` with it open), baked like the Sexton. |
| `assets/biddings.json` | Visitor and Bidding content. The first element (`"id": "_visitor"`) is the visitor; the rest are Biddings. It's loaded at the top of `Main`, before `--selftest`, which checks every Bidding uses known objectives and targets. |
| `Post.cs` | The full-screen post-process shader (grain, colour grade, vignette, fringing, Toll ripple), with a fallback if it fails to compile. |
| `Audio.cs` | Every sound, synthesized at startup on a background thread, with overrides. |
| `SaveFile.cs` | JSON save: writes `save.json.tmp`, then `File.Replace` into `save.json`, keeping the previous file as `save.json.bak`. Loading falls back to the `.bak`. A `save.json` that won't load is moved aside to `save.corrupt-<unixtime>.json` and never deleted, including when the `.bak` rescues it. |
| `Panic.cs` | Global hotkey Ctrl+Alt+Shift+Q, via `RegisterHotKey` on its own message-loop thread. |
| `SelfTest.cs` | `--selftest` and the greedy sim. |
| `assets/fonts` | UnifrakturMaguntia and IM Fell DW Pica, both under the SIL Open Font License, with their licence files. Copied to the output by the csproj. |

## Rules and invariants that are easy to break

- **The save is the public fields of `Game`.** It uses System.Text.Json with `IncludeFields` and camelCase.
  - A new field needs a sensible initializer, because old saves simply lack it.
  - Transient state needs `[JsonIgnore]`.
  - Use methods, not get-only properties, for derived values; otherwise they get serialized.
- **Data ids are save keys.** `Owned`, `SacBought`, `Wounds`, `OpenWounds`, `Lattice`, `DeaconsOff` and `Admitted` store ids. `Data.Rite`, `Sac`, `Wound`, `Node` and `Vow` use `First()`, so an id in a save that no longer exists throws right after load. Never rename or remove an id unless `Game.Migrate()` rewrites it, with a `Version` bump.
- **Immurement resets only what `Game.ResetRun()` lists:** Dolor, Owned, Revealed, Sacraments (S8 is kept), Tolls, regen and the run stats. Everything else carries over. When you add per-run state, clear it there. Don't touch persistent state (Wounds, Lattice, Admissions, Marrow).
- **Gate and Marrow:** `Game.Gate = 4e10`, `K = 10`, and `TotalFor(L) = floor(K·gain·√(L/Gate))`. Changing `Gate` silently strands existing players' Marrow. If you change it, bump `Version` and scale `Dolor.AllTime` and `Dolor.Run` by the ratio in `Game.Migrate()`, which shows how it was done for v1 to v2.
- **Spending Marrow never weakens it.** The passive bonus uses `MarrowEarned`; the Lattice spends from `MarrowEarned - MarrowSpent`.
- **Effects** go through `Game.MultFor(target)`, which combines bought Sacraments, open Wounds and Lattice nodes. The targets are a Rite id, `all`, `toll`, `costs` and `tollRegen`. One-off effects are checked by id (`S5`, `S8`, `S22`, `on_the_beat`, `fourth_wound`, the Deacon Vows and so on).
- **Time:** `Tick(dt)` takes a variable dt; production is linear, so a variable step is exact.
  - `ApplyAway` handles a closed game or a sleeping PC (any frame gap over 60s).
  - Mortification time always passes in full; only productive time is capped.
  - Tolls refill only after 60s or more away.
  - Deacons are simulated in one-minute steps while away.
- **Biddings** (`Game.Accept`, `TickBidding`, `EndBidding`): the burden applies through `MultFor` while one is active, and the boon or curse becomes an `Effect` with a timer.
  - `TickBidding` is called from `RunFrame` only, so a Bidding's timer never runs while the window is hidden or the game is closed.
  - `abstain` breaks on any purchase, deacons included (`Stats.Purchases`). `silence` breaks on any Toll. Both succeed when their time runs out.
  - Immurement drops an active Bidding without a curse.
  - The visit itself (knock, 10-minute wait, dialog) lives in `App` and opens only when the user clicks the whisper-bar notice. Never make it pop up by itself.
- **Deacons only buy their own Rites** (`Game.Deacons()`): the best-value Rite if it is one of theirs, otherwise their cheapest one, and then only if it costs at most 2% of current Dolor. Without this, the first Deacon drained Dolor and made run 2 slower than run 1.
- **Pacing targets,** checked by the self-test:
  - the greedy run 1 reaches the first Immurement in 70 to 110 min, about 1.5 to 2 h for a person (currently about 1h27m);
  - run 2 is at least 30% faster.

  After any change to numbers, rerun the self-test with the timeline and look for gaps over about 5 minutes without anything new.
- **Number formatting:** `Ui.Num(v, ceil: true)` for costs, plain (floor) for amounts, so "displayed cost ≤ displayed amount" always means affordable. All formatting is invariant culture: the machine is pt-BR, and `Main` forces `InvariantCulture`.

## Raylib pitfalls (each of these has bitten already)

- **`BeginTextureMode` can't nest.** Every frame renders into `Post.Target`, so anything else that renders into its own render texture (`Sexton.Render`) must run **before** the main `BeginTextureMode`. Inside the frame, only blit it.
- **Triangle winding:** raylib culls triangles by winding order, so use `Art.Tri(a, b, c, color)`, which draws both orders.
- **`GetKeyPressed()` drains a queue.** It's read once per frame into `AnyKey`. Modifier keys are excluded so that Alt-Tabbing in doesn't dismiss the away screen.
- **Scrolled lists:** use `BeginScroll` / `EndScroll`. That sets the scissor plus `Ui.Clip`, so widgets ignore clicks outside the visible area.
- **Overlays** set `Ui.Blocked`. Something that captures a click before the widgets draw (the Omen) calls `Ui.Consume()`.
- **Music** streams straight from the managed buffer: `LoadMusicStreamFromMemory` keeps the pointer. Generated loop buffers are pinned (`GCHandle`) for the life of the process.
- **Fonts lack some glyphs.** Neither font has `→` or `∞`, so write "to" instead of arrows. Allowed extra codepoints are listed in `Ui.Codepoints`. Body text is drawn 2px larger than its nominal size (`BodyBump`) because its x-height is small, and layout uses nominal sizes. The minimum text size is 13.

## Art and sound conventions

- **Art is generated at half resolution** and drawn at 2× (3× for the Sexton) with point filtering, so it reads as 16-bit pixel art: 4-tone ramps, ordered dither, an ink outline.
  - Any PNG in `assets/sprites/` with the generated texture's name replaces it: `wall`, `heart`, `heart_face`, `rite_<id>`, `sexton_whip_0..5`, `sexton_idle_0..1`, `icon` (16×16, the window icon).
  - The exe's icon is the checked-in `assets\icon.ico`. `--dump-art` writes a fresh `icon.ico` from `Art.AppIcon`; copy it over to change the exe's icon.
  - Keep that override hook when adding art. The user plans real pixel art later.
- **Sound is synthesized:** filters, a Freeverb-style reverb, bell partials, formant voices.
  - Any `assets/sounds/<name>.wav`, `.ogg` or `.mp3` overrides a sound; `--dump-sounds` lists the names.
  - Claude can't hear the result, so check sounds numerically and with waveform plots, then ask the user to listen through Settings → Test sounds.
  - User feedback: the heartbeat at full volume drowned everything else. It now defaults to Soft (0.3), gets quieter as the BPM rises, and has an Off/Soft/Strong setting. Keep alert sounds high and bright (like the visitor's handbell) so the low heartbeat can't mask them.
  - User feedback so far: the Sexton's original long, breathy groan "sounded sexual". Pain is now short, strangled, creaky grunts and teeth hisses, on only about half the lashes. Keep it that way.

## Office safety (user requirement)

The user plays at work, so the game has to stay safe to have open:
- the panic key Ctrl+Alt+Shift+Q hides the window from anywhere, including the taskbar and Alt-Tab, saves, and silences all audio;
- Esc hides the window while it has focus, and M mutes;
- volume defaults to 50%, and screams, Omens and silent-when-unfocused are all toggles.

Never add anything that pops up or grabs focus, and nothing makes noise while the window is hidden. Sound while it's merely unfocused is allowed (ambience, screams, the visitor's handbell), but it must go through `Audio.Play`, so the user's Silent-when-unfocused setting can mute it.

**Keep it light on a work PC.** The frame cap is `Settings.FpsCap` (30 or 20) when focused and 10 when unfocused. While hidden, the loop still ticks the game but sleeps 50ms and draws nothing, so don't add work to that path. Rendering is primitives and textures plus one shader pass. It costs about 10% of one core when focused.

## Content and IP

- **Original IP only.** Never use proper nouns or signature terms from Blasphemous or Mortal Shell. `DESIGN.md` has the ban list and the words deliberately avoided: Penance is called Mortification; Relics are Wounds; there's no Martyrdom, Ossuary or Confession.
- **Tone:** complicity and guilt rather than gore for its own sake. Flavor text never excuses the player.
- **Next on the roadmap:** v0.6 Ordeals, then v0.7 The Unnaming, a second prestige layer. v0.7 needs a mantissa/exponent number type before values pass 1e300; it's all `double` today.
- **Known open question:** veteran and late-game pacing. The sim only covers runs 1 and 2, but players with hundreds of Marrow plus the full Lattice and Deacons will be much faster. Tune with the timeline, starting from a save at that level.

## Work in progress (uncommitted, paused 2026-10-02 on top of 7d60b51)

The user picked two things: a **Marrow sink** for late-game tuning, and **expanded visitors** (the Sexton's wife plus refusal consequences). Ordeals are deferred.

**Ordeals, when they come:** the user chose "Stripped, but keep Deacons":
- inside an Ordeal there's no Marrow bonus, Lattice, Wounds or other Vows;
- the Deacons still buy, but the Tithe's 1M head start is off;
- goals are fixed, tuned to about 1h each with the greedy sim.

**Veteran measurement:**
- The user's real save has 1294 Marrow, 5 Immurements, the full Lattice and a fastest run of 72s.
- Greedy runs from it reach Ready in 14m, 9m, 13m and 21m, doubling Marrow each time, and each run is about 1.6× longer than the last. The sink should keep that self-limiting.
- `MORTIS_SIM_SAVE=<copy of save.json> Mortis.exe --selftest` prints this. A copy of the real save is in the session scratchpad. Never point it at the real save.

**Done (the build was not re-checked after the last `Game.cs` edit):**
- `Game.cs`:
  - **The Deep:** `DeepNode` records, `Data.Deep` and `Game.Deep`, which maps id to rank. `DeepCost` is Base × 2^rank, `DeepOpen` needs the branch's tier-5 node, and `Deepen` buys a rank. Ranks multiply through `MultFor`.
  - **Deep nodes:**
    - `deep_flesh`: All ×1.25 per rank.
    - `deep_bell`: Tolls ×1.5 per rank.
    - `deep_bone`: target `marrow`, ×1.1 per rank. `MarrowGain()` multiplies in `MultFor("marrow")`.
  - **Bidding and Visitor records:** `Bidding.By` is `"wife"` for Ysmay's chapters. `Visitor` has `Spurned` and `SpurnedLine`. `Data.Wife` loads from `"_wife"`, and `Data.Chapters()` and `Data.Who(b)` are new.
  - **New save fields:** `Deep`, `MetWife`, `WifeKept` (a `List<bool>` with one entry per chapter told; its count is her next chapter), and `RefusedInRow`.
  - **`NextBidding()`:** Ysmay takes every other knock once `MetProphet && Immurements >= 2`, until her chapters run out.
  - **`Refuse()`:** returns true on the Prophet's third refusal in a row, adds his `Spurned` curse, and resets the count. Refusing Ysmay costs nothing and keeps her chapter.
  - **`EndBidding`:** advances her chapter whether it was kept or failed. Her Biddings don't count in the Prophet's stats.
  - **Admissions:** `deep1`, `wife1` and `wife_end`.
  - **`ResetRun()`:** now public, because the veteran sim calls it.
- `assets/biddings.json`:
  - The Prophet now has `spurned` (All ×0.8 for 900s) and a `spurnedLine`.
  - `_wife` is "Ysmay", with a greeting and a leaves line.
  - Her five chapters: `wife_quiet` (silence 5m), `wife_wick` (abstain 8m), `wife_lash` (mortify within 1h), `wife_breaths` (9 on the beat in 10m) and `wife_street` (silence 15m, the last visit). She has boons but no curses.
- `Wife.cs`: her 64×80 portrait, with frames `wife_0` and `wife_1`. It has the same API as `Prophet`, and `Art.cs` initialises and dumps it.
- `Audio.cs`: the new sounds `breath` (her voice while dialog types out) and `cough`. The user still has to listen to both.
- `SelfTest.cs`: the `MORTIS_SIM_SAVE` veteran sim. `Greedy(..., ready: true)` stops at `Ready()`, and the time cap is now 20h.

**To do next:**
1. **`Program.cs`, visits.** Replace the `Data.Prophet` and `Prophet.*` uses with the visitor at the door (`Data.Who(AtDoor)`):
   - the card's face, name and "He/She will wait";
   - the leaves line;
   - the dialog portrait (give `Dialog` a `By`), and `breath` instead of `murmur` while she speaks;
   - play `cough` when her dialog opens;
   - show the greeting on the first visit, using `MetWife`;
   - the turned-away log line.
2. **`Program.cs`, refusal.**
   - When `G.RefusedInRow == 2`, the choice page warns that refusing the Prophet again leaves his curse.
   - When `Refuse` returns true, show the `SpurnedLine` plus `ModText`, and set `VisitIn = 300 + rand(300)`.
3. **`Program.cs`, long lines.** Success and fail lines run past one line in the whisper bar, where `Fit` truncates them; this affects the Prophet's too. Wrap `Override` onto two lines, then check it in a screenshot.
4. **`Program.cs`, Ledger.** Under the stanzas in the Account view, add "What Ysmay Said": each told chapter's title, then its success or fail line, according to `WifeKept`.
5. **`Program.cs`, the Deep.** Add a 4th Immure sub-tab, "The Deep", shown once any branch is complete. Lay out its rows like `VowsView`: name, effect, rank, current total, and a Deepen button with its cost.
6. **`SelfTest.cs`, rule checks:**
   - Deep cost, effects, and that a branch must be complete first;
   - `marrow` gain;
   - wife alternation and chapters advancing;
   - refusing her keeps the chapter;
   - the third refusal of the Prophet curses, and accepting resets the count;
   - the new save fields round-trip.
7. **`SelfTest.cs`, veteran guard.** Build a synthetic veteran (about 1300 Marrow and the full Lattice), let the sim buy Deep ranks between runs, and assert a band on run times. Tune the Deep base cost and growth: runs should still lengthen, only more gently.
8. **Docs.** Update `DESIGN.md` (glossary, roadmap) and this file (the file table gets `Wife.cs`; the save keys get `Deep` and `WifeKept`). Then remove this section.
9. **Review.** Run a review pass, take screenshots with a fixture save (`MORTIS_SAVE_DIR`, with `MORTIS_VISIT_IN` set low), and ask the user to listen to `breath` and `cough`.

## Working with this user

- **Language:** the user writes English, but their Windows is Portuguese (pt-BR), so tool output arrives in Portuguese.
- **Stack:** they prefer C#, Java or C++ for app code, not Python. Throwaway helper scripts are fine.
- **Pace:** they want a deep game built at a steady pace, one roadmap layer at a time. Each layer should be added without forcing a rewrite. Scope work to what they ask for. When a request is broad, offer the options first (they picked four at once last time).
- **Decisions:** they like being asked about design choices with concrete options and a recommendation, then having it built end to end with screenshots as proof.
- **Git:**
  - Commit only when asked; they usually ask after reviewing.
  - The remote is `origin` at https://github.com/E-5Manny/project-mortis on branch `master`. Push after committing when asked.
  - End commit messages with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Code style:** minimal and boring. Comments explain *why*, at the density of the surrounding code. `ponytail:` comments mark deliberate shortcuts and their limits.
