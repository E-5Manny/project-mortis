# MORTIS: The Long Dying

A desk-side idle game of grimdark devotion. C# / .NET 10 + Raylib-cs. Fixed 630×520 window. Art and sound are generated in code, with file overrides.

## Premise

Nine hundred years ago the god **Aumbrel** swallowed the world's death so that nothing would ever have to die again. Since then He has been dying of it.

The cathedral-town of **Ashkirk** is built inside His split ribcage. While His heart beats, nobody can die anywhere: not the plague-sick, not the burned, not the drowned. They only linger. The heart beats only when it is fed **Dolor**, the suffering the faithful give up of their own will.

You are **the Sexton of the Last Bell**. You rang the bell that called the knife into Him. He will not let you die. Out of guilt, you have made yourself the one who keeps Him alive. The game never says whether that is mercy or the cruelest thing anyone has done.

## Core fantasy

- **What you are:** the keeper of a god's deathbed who cannot die. You barely suffer yourself. You organise the suffering of others, and that complicity is the whole point.
- **What you do:** set up **Rites** (generators), approve harsher **Sacraments** (upgrades), and pull the bell-rope tied to His heart (**Toll**, the click action).
- **Why the number rises:** more Dolor means a stronger heartbeat, a longer delay before His death, and more people kept from dying. A small counter under the currency reads *"Souls held from death: N"*. It is cosmetic, and it is meant to be quietly horrifying.
- **Prestige, Immurement:** when the heart-chamber silts up, the Sexton bricks himself inside it. This is the only death Aumbrel allows. His bones turn to **Marrow**, faith hardened inside the god, and every future offering weighs more. He wakes at the foot of the bell tower and the town has forgotten him. Each Immurement reveals one stanza of *The Sexton's Account*.

## Pillars

1. **Being away is never punished.** Offline progress is 100%. Toll charges refill while you're gone. Nothing expires, and nothing ever spawns while the window is hidden.
2. **Read it at a glance.** The heart shows the state of the run. Its BPM tracks income. An irregular beat and a bone-white sigil mean prestige is ready. The ichor pool level shows progress, and the accent colour drifts toward crimson.
3. **Complicity is the fantasy.** Every number is suffering stretched out longer. The flavour text never excuses you.
4. **Simple engine, deep stack.** Data tables feed one modifier pipeline. Each milestone adds exactly one layer and never forces a rewrite.
5. **Light on a work PC.** 30 fps when focused, 10 fps when unfocused, no drawing when hidden. Primitives only.

## Glossary (original terms)

| Term | Meaning |
|---|---|
| Aumbrel | The dying god who swallowed death. Always "He/Him", never "Father". |
| Ashkirk | The cathedral-town built inside His ribcage. |
| Sexton of the Last Bell | The player. Undying. Rang the bell that called the knife. |
| Last Bell | The bell whose rope is tied to Aumbrel's heart. It is the click target. |
| Dolor | The primary currency: suffering that is offered and fed to the heart. |
| Rites | Generators, the institutions of suffering. |
| Sacraments | Upgrades. |
| Toll | The click action. Spends a charge of the bell-rope for a burst of Dolor. |
| Immurement | Prestige layer 1: being walled into the heart-chamber. |
| Marrow | Permanent prestige currency: faith turned to bone. |
| The Sexton's Account | A confession told one stanza per Immurement. |
| Deacons | Automation (v0.2). |
| Vows | Milestone rewards unlocked by total Marrow earned (v0.2). |
| The Lattice | The tree you spend Marrow on (v0.3). |
| The Deep | Repeatable nodes under each finished branch of the Lattice: a sink for Marrow that has no floor. |
| Admissions | Achievements. Each one is a line of guilt worth +1% (v0.4). |
| Mortification | The Sexton leaves the bell to scourge himself for 15m, 1h, 4h or 8h. No Dolor is made until it ends. Unlocks after the first Immurement. |
| Visitors / Biddings | Strangers knock at the bell-tower door, never interrupting, and offer Biddings in a visual-novel dialog. Accepting brings a burden while it lasts, a boon if you keep it, and sometimes a curse if you fail. The first visitor is the Lampless Prophet; the second is Ysmay, the Sexton's wife. |
| Wounds | What Mortification gives. They come in three depths (Shallow, Deep, Grievous) and five ranks, and deeper ones carry a trade-off. Three can be open at once, and they persist through Immurement. |
| Ordeals | Challenge runs (v0.6). |
| The Unnaming / Ashen Names | Prestige layer 2 and its currency (v0.7). |
| Ichor | Second resource: the god's own blood, spent on the Anatomy map (v0.8). |
| Keeper / Eschaton | The two endings of arc one (v1.0). |

## IP hygiene (hard rule)

- None of these may appear anywhere: Penitent One, Tears (of Atonement), Cvstodia, Miracle, Guilt Fragment, Mea Culpa, Shells, Glimpses, Tar, Fallgrim, Foundling.
- Words we deliberately avoid as system names:
  - Penance / Penitence (the currency is **Dolor**, the self-punishment mode is **Mortification**, challenges are **Ordeals**).
  - Relic / Reliquary (we use **Wounds**).
  - Martyrdom (we use **Immurement**).
  - Ossuary (we use **Charnel**).
  - Confession (we use **Admissions**).
  - Vow of Silence.
  - "Holy Wound of X" names.
  - Saint-body-part item naming.
- **Mortis** is only the game title, from the Latin *hora mortis*, "the hour of death". The god is **Aumbrel** and is never called "Father". This avoids the Star Wars Mortis echo.

## Tech

- **Stack:** C# on .NET 10 + Raylib-cs (NuGet). One project at the repo root. `run.cmd` builds and launches.
- **Files:** `Game.cs` holds the data tables and rules and has no raylib. `Program.cs` holds the loop and screens. `Ui.cs` has the palette, fonts, widgets and number formatting. `Fx.cs` draws the atmosphere. `Art.cs` generates textures. `Post.cs` is the post-process shader. `SaveFile.cs` handles saving. `Panic.cs` has the global hotkey. `SelfTest.cs` holds the self-test.
- **Look:**
  - Every frame renders into a texture and goes through one shader pass. The pass adds film grain, a dirty colour grade, a vignette that tightens on each beat with blood at the rim, colour fringing on beats and Tolls, and a ripple from the heart on each Toll.
  - Quiet mode turns off the ripple and the fringing.
  - If the shader can't compile, the game draws without it.
- **Art:**
  - Textures are generated at half resolution and drawn at 2x with point filtering, so they read as pixel art.
  - The textures are the wall, the heart, the heart with its face, and one 24x24 icon per Rite.
  - `Mortis.exe --dump-art [dir]` exports them as PNG templates.
  - A PNG with the same name in `assets/sprites/` replaces the generated texture, which is the path to real pixel art.
- **Sound:**
  - Everything is synthesized in `Audio.cs` at startup, on a background thread, at 44.1 kHz mono. The building blocks are filters, a Freeverb-style cathedral reverb, bell partials and formant voices.
  - There are two looping beds (the cathedral drone, and candles that scale with Tallow Saints) and event sounds for the heartbeat, drips, the bell, the lash, pain grunts and hisses, screams, bricks and wounds.
  - `--dump-sounds [dir]` exports the WAVs. A file with the same name in `assets/sounds/` (.wav, .ogg or .mp3) replaces any of them.
  - The panic key silences the game instantly and M mutes. Settings has volume, screams, silent-when-unfocused and a sound tester.
- **Fonts:** UnifrakturMaguntia (blackletter, used for titles and names) and IM Fell DW Pica (body text). Both are bundled in `assets/fonts` under the SIL Open Font License, and their licence files sit next to them.
- **Self-test:** `Mortis.exe --selftest | more` runs formula and save asserts plus a greedy-player sim, which should reach the first Immurement in 70 to 110 min (currently about 1h27m, about 1h40m for a person). The exit code is the number of failures.
- **Data:** Rites and Sacraments are plain rows. A Sacrament has a `Target` (a rite id, `all` or `toll`) and a `Mult` function, and `Game.MultFor(target)` multiplies all the bought ones together. S5 and S8 are checked by id.
- **Numbers:** `double` everywhere. It must be swapped for a mantissa/exponent type before values pass 1e300, which is expected around v0.7.
- **Reset:** an explicit `ResetRun()`. It gets generalised into `Reset(layer)` only when layer 2 arrives in v0.7.
- **Save:**
  - Stored in `%APPDATA%\Mortis\save.json`, or the folder in `MORTIS_SAVE_DIR` for testing.
  - Written to a temp file first, then swapped in with `File.Replace`, which keeps a `.bak`. Loading falls back to the `.bak`. A broken save is set aside, never overwritten.
  - Autosaves every 30 s, on hide, on Immurement, and on exit.
  - Offline time uses UTC, clamped to [0, cap]. A PC sleep longer than 60 s counts as time away.
- **Panic key:**
  - `Ctrl+Alt+Shift+Q`, registered system-wide with `RegisterHotKey` on its own thread, so it works while the game is unfocused. It hides and restores the window, and a hidden window is also gone from the taskbar and Alt-Tab.
  - `Esc` hides the window while it has focus.
  - If another app owns the key combination, hiding falls back to minimizing.
  - The simulation keeps running while hidden.

## Roadmap (one new layer per milestone)

*Deeper runs (2026-10-01):* there are now 8 Rites (adding the Gibbet Orchard and the Hollow Bishop), 24 Sacraments and a gate of 4e10. The first Immurement takes about 1h27m for the greedy sim, which is about 1h40m for a person. Version-1 saves are migrated by scaling lifetime and run Dolor with the gate. `MORTIS_SIM_TIMELINE=1 Mortis.exe --selftest` prints the unlock timeline.

1. **v0.1 The First Stone.** Dolor, 6 Rites, 12 Sacraments, Toll charges, Immurement → Marrow, 10 stanzas. First prestige takes about 45–100 min of casual play.
2. **v0.2 Deacons** (shipped 2026-10-01). Six Vows, granted by total Marrow earned (10, 25, 50, 100, 200, 400):
   - the first Deacon buys Kneelers and Choirs, then a Deacon buys every Rite;
   - Sacraments are auto-taken;
   - the Bell-Boy adds +2 charges and rings a full rope;
   - each run begins with 1M Dolor;
   - auto-Immurement at a chosen Marrow ratio.

   Each Vow can be switched off. Deacons buy the best-value Rite, but only buy their own Rites when cheap (2% of Dolor) if a better buy exists. They keep buying while you're away, simulated in one-minute steps.
3. **v0.3 The Lattice** (shipped 2026-10-01). A root plus three branches of five (Flesh, Bell, Bone), 181 Marrow in all. Spending Marrow never lowers the passive bonus, which counts Marrow earned.
4. **v0.4 Admissions and Omens** (shipped 2026-10-01).
   - **Admissions:** 49 lines of guilt, each +1% to all production.
   - **Omens:** an eye opens in the wall every 5 to 15 minutes while the window is visible. Clicking it grants ×3 for 60s, 15 minutes of production, or a full rope. Omens can be switched off and never appear during Mortification.
*Visitors (2026-10-01):* the Lampless Prophet knocks every 15 to 30 minutes after the first Immurement, never during Mortification or while a Bidding is active. He waits 10 minutes. His dialog is typed out, Accept or Refuse sits on the last page, and refusing is free for now. Content lives in `assets/biddings.json`, with objectives toll, beat, buy, gather, abstain, silence and mortify. Bidding timers run only while the window is visible. Ideas for later: more visitors, branching dialog.

*Ysmay and the Deep (2026-10-02):*
   - **Ysmay,** the Sexton's wife, has been dying of the same cough for nine hundred years. Once the Prophet has been met and you have been walled in twice, she takes every other knock and tells a five-chapter arc, one chapter per visit. Each chapter asks something small (silence, buying nothing, a Mortification, nine Tolls on the beat). Keeping it brings a boon; failing it brings only what she says. Either way the story moves on, and refusing her just brings the same chapter back. What she said is kept in the Ledger, under the Account. She has her own portrait and voice, and a cough when the door opens.
   - **Refusing the Prophet** three times in a row leaves a curse (All ×0.8 for 15m) and he returns sooner. The dialog warns before the third.
   - **The Deep** opens under each finished branch of the Lattice: Flesh Upon Flesh (All ×1.15 a rank), The Bell Sinks Deeper (Tolls ×1.25) and Bone Upon Bone (Marrow from Immurement ×1.05). A rank costs 300 Marrow, then 5× more each time. The self-test plays a veteran with and without it: runs get shorter, but each still takes longer than the one before.
5. **v0.5 Mortification** (shipped early, on 2026-10-01). Unlocked by the first Immurement, it trades production time for Wounds: 11 wounds across 3 depths, ranks I to V, 3 open slots. The Sexton view shows the animated 16-bit sprite.
6. **v0.6 Ordeals.** Six challenge runs that each impose a harsh rule: no Tolling, costs ×2, or only three Rites. Each one you complete grants a unique permanent modifier.
7. **v0.7 The Unnaming (layer 2).** Burn your name from the rolls. This resets Marrow, the Lattice and Vows in exchange for **Ashen Names**, which unlock new Rites beyond the Hollow Bishop (still to be designed) and per-Rite multipliers. It also introduces **Creeds**, a per-run choice between Keeping (idle-leaning) and Letting (active-leaning). The `Num` type swaps to mantissa/exponent here.
8. **v0.8 The Anatomy.** A map of His body drawn in text and rectangles: chambers, wounds, the ossified lung. Each node costs **Ichor** and opens one branch.
9. **v1.0 The Last Toll.** A meter for His final death appears. There are two endings:
   - **Keeper:** He lives forever, with infinite scaling.
   - **Eschaton:** you let Him die. Death returns to the world, the game resets into New Game+ modifiers, and the Sexton finally dies.
