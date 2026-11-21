# Making the builder's data

These scripts turn a full language build and a full media build (made on the maintainer's PC from SI's files plus
the mod's sources) into the shipped data in `data/`, which holds only the mod's own work.

| Script | What it does |
|---|---|
| `delta.py` | Compares the full language with a pure-SI baseline (same template, no mod content) and lists every changed line. |
| `make_data.py` | Writes `lang_lines.tsv`: our own lines as text; lines that are mostly SI's as a small edit checked against a hash of SI's line. |
| `rebuild2.py` | Reference builder: baseline + data -> the full language (must match the full build byte for byte). |
| `sicheck.py` | Reports how close each shipped line is to SI's wording. |
| `canon.py` | The canonical form of FM's XML used for patching (the C# builder follows the same rules). |
| `mkpatch.py` | Writes `media.patch` from SI's and our unpacked media/simatch files, with SI's wording masked. |
| `applyref.py` | Reference patch applier: SI's files + patches -> our files (must match file for file). |

Checks done for data version 1 (21 Nov 2026, against FM27 27.1.0, SI's patch of 17 Nov 2026):
- language rebuilt from SI's files + data: byte-identical to the full build (v0.64);
- media and commentary: all 57 changed files identical; no SI wording left in the patches (18,833 prose fields are ours;
  9,198 references to SI's wording are filled from the player's own files);
- the C# builder gives the same results (language byte-identical; 57/57 files; untouched archive files byte-identical).
