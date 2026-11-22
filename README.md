# FM27 Media Mod

> Working name. **Status: in development. A public beta is on the way.**

A mod for **Football Manager 27** that makes the media and dressing-room world feel like it's about *your* manager, and that it **remembers**.

- **Press conferences that remember.** Journalists bring back what you said weeks ago, notice when you dodge, walk out or send your assistant, and carry grudges or soften up depending on how you treat them.
- **A press pack with personalities.** Attack dogs, cheerleaders, old-school hacks and insiders, each with their own outlet and their own way of asking.
- **The Media Office.** A new tab where your head of media runs the press office: favour or freeze out outlets, plant a story or keep one quiet, brief back against an agent, close ranks after a dressing-room row. You have a few hours a week, so every choice costs something.
- **More to say, said better.** Thousands of rewritten and extra lines for press conferences, interviews, player and staff chats and team talks, all gender-neutral, for men's and women's football.
- **Consequences that carry forward.** What you say on the record, what leaks and who you upset follow you from conference to conference, and are kept separately for each save.

It never touches the match engine.

## Install

See **[INSTALL.md](INSTALL.md)**. It covers both cases: BepInEx already installed (for example by a stadium pack), or not.

## What's in this repository

| Folder | What it is |
|---|---|
| `src/PressRoom` | The main plugin: press lead-ins and callbacks, the press pack, the Media Office, memory per save |
| `src/Tabloid` | Headlines and The Terraces: your own fans reacting in their club's voice |
| `src/MediaLab` | Press-conference actions: storm out, no comment, wrap it up |
| `src/TeamTalkFilter` | Team-talk tidying and filters |
| `src/Builder` | The **builder**: a BepInEx preloader patcher that makes the mod's language file and media changes from your own copy of FM27 |
| `data` | The builder's data: the mod's own lines and patches (no Sports Interactive text) |
| `tools/builder` | How the data is made and checked |
| `third_party/ZstdSharp` | [ZstdSharp](https://github.com/oleg-st/ZstdSharp) (MIT), used to read and write FM's archives |

### How the builder works

The mod never ships Sports Interactive's files. Each time FM starts, the builder checks whether anything needs doing (first run, an SI patch, a mod update), and only then:

- **Language:** takes SI's own English from your game files and FM's template from your languages folder, applies the mod's lines, and writes the language **English (FM27 Media Mod 1)**.
- **Questions, chats and commentary:** backs up SI's `media.fmf` and `simatch.fmf`, applies the mod's patches, writes them back, and moves FM's caches aside so it rebuilds them.
- **Safety:** if an SI patch has changed a file the mod patches, so that a change no longer fits, the builder leaves SI's files alone and says so in `BepInEx\FM27MediaMod\builder.log`.

Most launches take no time at all; a build takes about 15 to 30 seconds.

## Building from source

The plugins are C# BepInEx 6 (Unity IL2CPP) plugins. Build against your own FM27 install, after BepInEx has run once:

- `<FM27>\dotnet` (the .NET runtime BepInEx uses);
- `<FM27>\BepInEx\core` (BepInEx, HarmonyX, Il2CppInterop);
- `<FM27>\BepInEx\interop` (the assemblies BepInEx generates from the game; never commit these).

Interop assemblies are generated on your machine from your copy of the game, which is why they are not in this repository.

The builder (`src/Builder` plus `third_party/ZstdSharp`) needs only the .NET runtime and `BepInEx\core` (BepInEx.Core, BepInEx.Preloader.Core, Mono.Cecil). Compile with unsafe code allowed and `NET6_0_OR_GREATER;NET5_0_OR_GREATER;NETCOREAPP3_0_OR_GREATER;NETCOREAPP3_1_OR_GREATER` defined.

## Support

The mod is free and will stay free: no paid features, no early access, and nothing in the game asks for money.

If it adds something to your saves and you'd like to say thanks, you can buy me a coffee on [Ko-fi](https://ko-fi.com/superbladesman) (also behind the **Sponsor** button at the top of this page). Entirely optional, always appreciated.

Bug reports and ideas are just as welcome: open an issue here.

## Licence

The code is released under the [MIT licence](LICENSE).

*FM27 Media Mod is a fan-made mod with no official link to SEGA or Sports Interactive. Football Manager is a trademark of Sports Interactive Limited; the game, its data and its text belong to SEGA and Sports Interactive.*
