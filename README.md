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
| `src/MediaLab` | Press-conference actions: storm out, no comment, wrap it up |
| `src/TeamTalkFilter` | Team-talk tidying and filters |

More is coming before the beta: the fans and headlines layer, and the **builder**, which makes the mod's language file and media changes from your own copy of FM27 on first launch. The mod never ships Sports Interactive's files.

## Building from source

The plugins are C# BepInEx 6 (Unity IL2CPP) plugins. Build against your own FM27 install, after BepInEx has run once:

- `<FM27>\dotnet` (the .NET runtime BepInEx uses);
- `<FM27>\BepInEx\core` (BepInEx, HarmonyX, Il2CppInterop);
- `<FM27>\BepInEx\interop` (the assemblies BepInEx generates from the game; never commit these).

Interop assemblies are generated on your machine from your copy of the game, which is why they are not in this repository.

## Support

The mod is free and will stay free: no paid features, no early access. A **Sponsor** button for GitHub Sponsors and Ko-fi will appear here once those are set up.

## Licence

The code is released under the [MIT licence](LICENSE).

*FM27 Media Mod is a fan-made mod with no official link to SEGA or Sports Interactive. Football Manager is a trademark of Sports Interactive Limited; the game, its data and its text belong to SEGA and Sports Interactive.*
