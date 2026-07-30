# PRK Damage Meter

<img src="icon.png" width="64" align="right">

A tiny always-on-top damage meter overlay for **Project Rubi-Ka** (Anarchy Online 18.4) — WoW-style meter bars with class colors, pet rollup, healing tracking, nano cast stats, and clickable in-game chat dumps.

**No injection. No hooks. No automation.** It reads the chat log file the game itself writes, and you build the exe on your own machine from the source in this repo — there are no binaries to trust.

## Install (one time, ~2 minutes)

1. Download this repo (green **Code** button → Download ZIP) and extract it anywhere
2. Double-click **install.bat** — it builds `PRK-DamageMeter.exe` using the C# compiler included with Windows
3. Double-click **PRK-DamageMeter.exe**
4. First run offers to **auto-create the logged "Damage" chat window for all your characters** (say yes while logged out). Log in — done.

## Features

- **Four views** — Damage done / Healing done / Damage taken / My nano casts — click the tabs in the header
- **Fight / all toggle** — last fight (kept on screen until the next fight starts) or everything since your last reset. Boss tip: hit **R** at the pull, leave it on **all**
- **Class colors** — professions auto-detect from a database of 2,900+ profession-locked nanos extracted from the PRK client: your casts tag you, teammates' buffs landing in your NCU tag them. Manual tagging via right-click for everyone else
- **Pet rollup** — mark a pet once and its damage folds into its owner's bar
- **Mobs auto-hidden** — names with spaces (mobs) stay out of the rankings; player names never contain spaces
- **Nano cast stats** — every nano you cast with counts: casts / landed / resisted
- **Share to chat** — the meter keeps in-game scripts updated; make a macro once (`/macro dmg /prkdmg`) and click it to post a clickable dump anyone can open:
  - `/prkdmg` — damage rankings (follows your fight/all toggle)
  - `/prkheal` — healing rankings
  - `/prkcast` — your nano cast counts
- **Overlay-friendly** — always on top, drag to move, drag edges to resize, opacity control, pause, hover any bar for details (hits, crits, max hit, weapon/nano/shield split)
- Click **?** in the header for full help; all settings and tags are remembered between sessions

## How it works

The AO client can log any chat window to a text file. The meter tails that file and does math — that's the whole trick, same as classic log-based meters on live. The parser is built from the exact combat message templates in the PRK client's own text database, so parsing is exact, not guesswork.

## Credits

Built by **Everkill** — updates/requests: ping **.everkill** on Discord

*See also: [PRK Map](https://github.com/RAD-Talent/PRK-Map) &bull; [PRK WhatBuffs & Item Database](https://github.com/RAD-Talent/PRK-Items) &bull; [PRK 4K Support](https://github.com/RAD-Talent/PRK-4K-Support)*
