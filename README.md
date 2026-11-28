# Super Bladesman's FM27 Media Mod

> **Public beta: v0.9.2-beta**, with the first betas of the sister mods, the **Treatment Room** and **Partnerships**. Download them from the [Releases page](https://github.com/SuperBladesman/fm27-media-mod/releases); install steps are in [INSTALL.md](INSTALL.md).

[![Support me on Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/H8C42B2XK0)

A mod for **Football Manager 27** that makes the media and dressing-room world feel like it's about *your* manager, and that it **remembers**.

- **Press conferences that remember.** Journalists bring back what you said weeks ago, notice when you dodge, walk out or send your assistant, and carry grudges or soften up depending on how you treat them.
- **A press pack with personalities.** Attack dogs, cheerleaders, old-school hacks and insiders, each with their own outlet and their own way of asking.
- **The Press Office.** Your head of media runs it: favour or freeze out outlets, plant a story or keep one quiet, brief back against an agent, close ranks after a dressing-room row. Its front page, *This week*, shows the pressure on you, the next press conference and who's after you, the stories running and how the dressing room took your last words. You have a few hours a week, so every choice costs something.
- **More to say, said better.** Thousands of rewritten and extra lines for press conferences, interviews, player and staff chats and team talks, all gender-neutral, for men's and women's football.
- **Consequences that carry forward.** What you say on the record, what leaks and who you upset follow you from conference to conference, and are kept separately for each save.

It never touches the match engine.

### The sister mods

Separate downloads from the same Releases page. Each works on its own or alongside the Media Mod, reads the game's own screens and changes nothing in the game.

- **Treatment Room.** Your medical department, readable: guidance from your own staff on who to watch and what they'd do, how much they can see depending on how good they are, the sports science evidence, each player's record, and how good your physios' estimates have been.
- **Partnerships.** Your player partnerships, readable: how each pair in your XI does together and apart, in which positions (is your back three better with one pair on the left and right, or in the middle?), who's next when one is missing, which pairs are coming through, and what the players say.

With the Media Mod as well, the press and the fans pick up their stories.

## Install

See **[INSTALL.md](INSTALL.md)**. It covers both cases: BepInEx already installed (for example by a stadium pack), or not.

## What's in this repository

| Folder | What it is |
|---|---|
| `src/PressRoom` | The main plugin: press lead-ins and callbacks, the press pack, the Press Office, memory per save (`voices.tsv` ships beside its DLL) |
| `src/Tabloid` | Headlines and The Terraces: your own fans reacting in their club's voice |
| `src/MediaLab` | Press-conference actions: storm out, no comment, wrap it up |
| `src/TeamTalkFilter` | Team-talk tidying and filters |
| `src/Core` | The column of buttons on the right-hand edge, and their notes, shared by every part |
| `src/Reader` | Reads a short fact sheet from the game (league position, opponents, medical staff) for the other parts; read-only |
| `src/TreatmentRoom` | The Treatment Room (sister mod), with its offline test in `test` |
| `src/Partnerships` | Partnerships (sister mod), with its offline test in `test` |
| `src/Builder` | The **builder**: a BepInEx preloader patcher that makes the mod's language file and media changes from your own copy of FM27 |
| `data` | The builder's data: the mod's own lines and patches (no Sports Interactive text) |
| `tools/builder` | How the data is made and checked |
| `third_party/ZstdSharp` | [ZstdSharp](https://github.com/oleg-st/ZstdSharp) (MIT), used to read and write FM's archives |

### How the builder works

The mod never ships Sports Interactive's files. Each time FM starts, the builder checks whether anything needs doing (first run, an SI patch, a mod update), and only then:

- **Language:** takes SI's own English from your game files and FM's own language template (the `example.zip` FM ships), applies the mod's lines, and writes the language **English (FM27 Media Mod 2)** (the number is the data version). Until it's chosen in Preferences > Language, the Press Room says so at the top of the screen.
- **Questions, chats and commentary:** backs up SI's `media.fmf` and `simatch.fmf`, applies the mod's patches, writes them back, and moves FM's caches aside so it rebuilds them.
- **Safety:** if an SI patch has changed a file the mod patches, so that a change no longer fits, the builder leaves SI's files alone and says so in `BepInEx\FM27MediaMod\builder.log`.

Most launches take under a second; a build takes about 10 to 30 seconds.

## Building from source

The plugins are C# BepInEx 6 (Unity IL2CPP) plugins. Build against your own FM27 install, after BepInEx has run once:

- `<FM27>\dotnet` (the .NET runtime BepInEx uses);
- `<FM27>\BepInEx\core` (BepInEx, HarmonyX, Il2CppInterop);
- `<FM27>\BepInEx\interop` (the assemblies BepInEx generates from the game; never commit these).

Interop assemblies are generated on your machine from your copy of the game, which is why they are not in this repository.

The builder (`src/Builder` plus `third_party/ZstdSharp`) needs only the .NET runtime and `BepInEx\core` (BepInEx.Core, BepInEx.Preloader.Core, Mono.Cecil). Compile with unsafe code allowed and `NET6_0_OR_GREATER;NET5_0_OR_GREATER;NETCOREAPP3_0_OR_GREATER;NETCOREAPP3_1_OR_GREATER` defined.

## Support

The mod is free and will stay free: no paid features, no early access, and nothing in the game asks for money.

If it adds something to your saves and you'd like to say thanks, you can buy me a coffee on [Ko-fi](https://ko-fi.com/superbladesman), with the button at the top of this page or the **Sponsor** button. Entirely optional, always appreciated.

Bug reports and ideas are just as welcome: open an issue here.

## Licence

The code is released under the [MIT licence](LICENSE).

*Super Bladesman's FM27 Media Mod is a fan-made mod with no official link to SEGA or Sports Interactive. Football Manager is a trademark of Sports Interactive Limited; the game, its data and its text belong to SEGA and Sports Interactive.*
