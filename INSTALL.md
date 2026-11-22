# Installing Super Bladesman's FM27 Media Mod (beta)

The mod changes Football Manager 27's media and dressing-room world: press conferences, interviews, player and staff chats, team talks, news and fan reactions. It never touches the match engine.

## What you need

- **Football Manager 27 on Windows**, with the game in English. Tested on Steam; Epic should work but hasn't been tried. macOS is not supported in the beta.
- **BepInEx 6 (bleeding edge, Unity IL2CPP, x64)**, the loader that runs code mods in FM27. The mod is tested with build **be.785**; newer builds should work.
- About 5 minutes, plus a slow first launch while BepInEx prepares itself.

The mod is free. It ships only its own files: it never includes BepInEx, Sports Interactive's files or anyone else's mods.

---

## Step 1: Do you already have BepInEx?

Open your FM27 game folder. On Steam: right-click Football Manager 27 > **Manage** > **Browse local files**. It's the folder with `fm.exe` in it.

You **already have BepInEx** if you see **all** of these:

- a `BepInEx` folder;
- a `dotnet` folder;
- **either** `winhttp.dll` **or** `version.dll` (some mods, such as BassyBoy's stadium pack, install BepInEx with `version.dll`).

To check which build you have, open `BepInEx\LogOutput.log` in Notepad. The first line reads something like `BepInEx 6.0.0-be.785`.

- **You have it:** skip to **Step 3**. Do **not** install a second copy, and never have both `winhttp.dll` and `version.dll` in the game folder.
- **You don't:** do **Step 2** first.

## Step 2: Install BepInEx (only if you don't have it)

1. Close FM27.
2. Go to the BepInEx bleeding-edge builds page: <https://builds.bepinex.dev/projects/bepinex_be>
3. Under build **#785**, download **`BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.785+6abdba4.zip`**. Make sure it's **Unity.IL2CPP**, **win**, **x64**. Mono or x86 builds won't work.
4. Extract the zip **straight into the game folder**, so that `BepInEx`, `dotnet`, `winhttp.dll` and `doorstop_config.ini` sit beside `fm.exe`. No extra folder level.
5. Start FM27 and wait. **The first launch can take several minutes** while BepInEx builds its files, and FM may look frozen. Leave it until the main menu appears.
6. Quit FM27. You should now have `BepInEx\plugins`, `BepInEx\config` and `BepInEx\interop` folders.

## Step 3: Install the mod

1. Close FM27.
2. Download the latest `FM27-Media-Mod-<version>.zip` from the [Releases page](https://github.com/SuperBladesman/fm27-media-mod/releases).
3. Extract it **into the game folder**, letting the zip's `BepInEx` folder merge with yours. You should end up with:
   - in `BepInEx\plugins`: `FM27MediaMod PressRoom`, `FM27MediaMod Tabloid`, `FM27MediaMod MediaLab`, `FM27MediaMod TeamTalkFilter`;
   - in `BepInEx\patchers`: `FM27MediaMod Builder` (the builder and its `data` folder).
4. Start FM27. Before the main menu, the mod's **builder** runs once. It takes about 10 to 30 seconds, and FM may look frozen while it works. It:
   - writes the mod's language file, built from your own copy of FM27's English, into your languages folder;
   - updates the game's question, chat and commentary files (`media.fmf`, `simatch.fmf`), keeping copies of SI's originals in `BepInEx\FM27MediaMod\backups`;
   - moves FM's caches aside (into `_to_delete` beside them) so FM rebuilds them.

   What it did is written to `BepInEx\FM27MediaMod\builder.log`. After that it checks in under a second and does nothing unless something has changed.
5. Go to **Preferences > Language** and choose **English (FM27 Media Mod 1)**. FM compiles it the first time, which takes a moment.
6. Load your save, or start a new game. The **MEDIA** tab on the right-hand edge of the screen opens the Media Office.

If `builder.log` says FM's template (`example.ltf`) isn't there yet, FM hasn't created it so far: start FM once, quit, and start it again.

---

## After an FM27 patch

- SI's patches restore the game's media files, and BepInEx rebuilds its own files on the next launch, which is slow again, once.
- The builder notices the patch on that launch and redoes its work. No restart is needed.
- If SI has changed a file the mod patches so that a change no longer fits, the builder leaves SI's files as they are and says so in `builder.log`. The game then runs without the mod's extra questions, chats and commentary until a mod update arrives.
- If something stops working after a patch, check the Releases page for an update before reporting it.

## If the builder says "already holds mod content"

The game's `media.fmf` or `simatch.fmf` already has mod changes in it that the builder didn't make, for example from an older hand-made version. Steam: right-click FM27 > **Properties** > **Installed Files** > **Verify integrity of game files**, then start FM again.

## Your saves

The mod keeps its own memory (what you said, who you've upset, your press office) **per save**, in `BepInEx\FM27MediaMod`. Loading a save brings back that save's memory; a new game starts fresh.

## Settings

Most options are on the Media Office's **Settings** tab. The rest are in `BepInEx\config\uk.nik.fm27mediamod.<name>.cfg`, which you can open with Notepad while FM is closed.

## Uninstalling

1. Close FM27.
2. Delete the four `FM27MediaMod ...` folders from `BepInEx\plugins`, the `FM27MediaMod Builder` folder from `BepInEx\patchers`, and the `BepInEx\FM27MediaMod` folder.
3. Steam: right-click FM27 > **Properties** > **Installed Files** > **Verify integrity of game files**. This puts SI's original media files back.
4. In FM27, set **Preferences > Language** back to **English**. You can then delete `FM27 Media Mod 1.ltf` (and `.ltc`) from `Documents\Sports Interactive\Football Manager 27\languages`.
5. Only if no other mod needs BepInEx: delete `BepInEx`, `dotnet`, `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and `changelog.txt` from the game folder. Leave `version.dll` alone if another mod installed it.

## Works with

- BassyBoy's stadium pack, which installs BepInEx itself (see Step 1).
- Heffem Academy Manager.
- Other BepInEx plugins, as long as only one copy of BepInEx is installed.

## Known limits (beta)

- English only. Windows only.
- Built for men's and women's football; please report anything that reads wrong in a women's save.
- It changes words, memory and the media screens. It does not change the match engine.

## Reporting a problem

Open an [issue on GitHub](https://github.com/SuperBladesman/fm27-media-mod/issues). Please attach a screenshot, `BepInEx\LogOutput.log` and `BepInEx\FM27MediaMod\builder.log`, and say which screen you were on. The logs can contain your save's folder path, so check them before posting if that matters to you.

## Support the mod

The mod is and will stay free, with no paid features or early access. If you'd like to say thanks, there's a [Ko-fi page](https://ko-fi.com/superbladesman), also behind the **Sponsor** button at the top of the GitHub page.

*Super Bladesman's FM27 Media Mod is a fan-made mod with no official link to SEGA or Sports Interactive. Football Manager is a trademark of Sports Interactive Limited.*
