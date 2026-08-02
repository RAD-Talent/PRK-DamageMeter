# PRK Damage Meter

<img src="icon.png" width="64" align="right">

A tiny always-on-top damage meter overlay for **Project Rubi-Ka** (Anarchy Online 18.4) — WoW-style meter bars with class colors, pet rollup, healing tracking, XP/hour tracking, nano cast stats, detailed per-player breakdowns, and clickable in-game chat dumps.

**No injection. No hooks. No automation.** It reads the chat log file the game itself writes, and you build the exe on your own machine from the source in this repo — there are no binaries to trust.

## Install (one time, ~2 minutes)

1. Download this repo (green **Code** button → Download ZIP) and extract it anywhere
2. Double-click **install.bat** — it builds `PRK-DamageMeter.exe` using the C# compiler included with Windows, then starts the meter for you
3. First run offers to **auto-create the logged "Damage" chat window for all your characters** (say yes while logged out). Log in — done.
4. Right-click the meter → **Set my character name** so the meter shows your name instead of "You"

**Updating to a new version?** Just replace `PRKDamageMeter.cs` with the newest one from this repo and run **install.bat** again — it closes the running meter, rebuilds, and relaunches it. Your settings and tags are kept.

## The window at a glance

- **Five tabs** — `DMG` damage done · `HEAL` healing done · `TAKE` damage taken · `CAST` your nano casts · `XP` xp per hour
- **Bottom-left corner** — the green dot means it's watching your log live, and the **fight / all** toggle switches between the last fight and everything since your last reset
- **Header buttons** — `?` help · `R` reset · `‖` pause · `X` quit
- Drag anywhere to move, drag the left/right edge to resize, mouse-wheel to scroll long lists (raids), right-click for the full menu

## Features

- **Detailed hover breakdown** — hover any bar for hits/min, average + max hit, crit % and glance %, weapon/nano/shield split with per-minute rates, **damage types** (melee / cold / poison... — handy for picking defensive gear) and **specials** (Burst, Fling Shot, Aimed Shot... with counts and totals)
- **DPM or DPS** — damage per minute by default (bold in every row); toggle to per-second in the right-click menu
- **XP tab** — live xp/hour session tracker: totals, per-hour rates, a rolling 5-minute pace (current speed vs session average), kill counts, avg + best tick, deaths and net xp. Shadowknowledge and Alien XP sections appear automatically once you earn some. Right-click → *Copy summary* copies the whole report as text
- **Class colors** — professions auto-detect from a database of 2,900+ profession-locked nanos extracted from the PRK client: your casts tag you, teammates' buffs landing in your NCU tag them. Manual tagging via right-click for everyone else
- **Pet rollup** — pets named like *"Sefira's robot"* auto-credit their owner; anything else can be marked once via right-click and folds into its owner's bar
- **Fight / all toggle** — last fight (kept on screen until the next fight starts) or everything since your last reset. Boss tip: hit **R** at the pull, leave it on **all**
- **Mobs auto-hidden** — names with spaces (mobs) stay out of the rankings; player names never contain spaces
- **Nano cast stats** — every nano you cast with counts: casts / landed / resisted / interrupted (aborts, counters and fumbles all count)
- **Skill-lock timers** — when the game says *"Cannot use the [skill] on this target for another X seconds"* (trimmers!) or *"skill is locked, able in hh:mm:ss"*, a live countdown bar appears above the footer on every tab, drains as the lock runs, and flashes a green **READY** when it's up. Click the locked hotbar button once to (re)start a timer
- **Share to chat** — the meter keeps in-game scripts updated; make a macro once (`/macro dmg /prkdmg`) and click it to post a clickable dump anyone can open:
  - `/prkdmg` — damage rankings (follows your fight/all toggle)
  - `/prkheal` — healing rankings
  - `/prkcast` — your nano cast counts
- **Smart log detection** — at startup the meter keeps searching until it finds the log that's actually producing combat events, then locks on (so it never jumps away and wipes your data mid-session). Switching characters? Right-click → *Auto-detect log* or *Choose log file*
- **Overlay-friendly** — always on top, opacity control, pause, minimal footprint
- Click **?** in the header for full help; all settings and tags are remembered between sessions

## About the "Unknown" row

Some log lines name nobody: actions by characters **outside your team** (an unteamed follower, a passer-by) and some heal-over-time ticks. The game just doesn't say who, so they all pool into one *Unknown* row. Right-click it to **hide it permanently**, or use **Mark as pet of** to credit a specific player (e.g. your box doc).

## How it works

The AO client can log any chat window to a text file. The meter tails that file and does math — that's the whole trick, same as classic log-based meters on live. The parser is built from the exact combat message templates in the PRK client's own text database, so parsing is exact, not guesswork.

## Credits

Built by **Everkill** — updates/requests: ping **.everkill** on Discord

*See also: [PRK Map](https://github.com/RAD-Talent/PRK-Map) &bull; [PRK WhatBuffs & Item Database](https://github.com/RAD-Talent/PRK-Items) &bull; [PRK 4K Support](https://github.com/RAD-Talent/PRK-4K-Support)*
