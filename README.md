# BossMod Reborn Radar

An unofficial, radar-only companion build of [BossModReborn](https://github.com/FFXIV-CombatReborn/BossmodReborn) designed to run beside the [original BossMod](https://github.com/awgil/ffxiv_bossmod).

The goal is simple: keep the original BossMod as the one plugin Questionable talks to, while using BossModReborn's wider encounter-module coverage for visual radar and mechanic hints.

## Safety boundary

This is a code-level restriction, not just a suggested configuration:

- no `BossMod.*` IPC endpoints;
- no autorotation or AI managers;
- no automovement or movement override;
- no automatic action execution;
- no action tweaks, target changes, status cancellation, interaction, duty leave, or FATE sync;
- only a passive action-effect observer, world-state synchronization, encounter modules, and radar/hint rendering.

The plugin has its own internal identity (`BossModRebornRadar`), window namespace, config directory, and `/bmrr` command. Do not install the normal BossModReborn plugin at the same time as this build.

## Install

1. Open Dalamud Settings, select **Experimental**, and add this custom repository URL:

   `https://raw.githubusercontent.com/Surlako/BossModRebornRadar/main/pluginmaster.json`

2. Install **BossMod Reborn Radar** from the plugin installer.
3. Keep the original **BossMod** installed and enabled for Questionable.
4. Open the original BossMod with `/vbm` and disable its **Enable radar** setting if you do not want two radar windows.
5. Open this companion with `/bmrr` and configure its radar.

Questionable requires no changes. It will continue using the original BossMod's IPC and presets because this companion does not publish those IPC endpoints.

## Commands

| Command | Purpose |
| --- | --- |
| `/bmrr` | Open settings |
| `/bmrr radar` | Toggle radar |
| `/bmrr radar on` | Enable radar |
| `/bmrr radar off` | Disable radar |
| `/bmrr radar reset` | Recenter radar |
| `/bmrr resetcolors` | Reset radar colors |

## Coverage note

The often-quoted difference of roughly 200 is a count of registered encounter modules, not necessarily 200 complete duties. Coverage and module maturity change over time with upstream BossModReborn.

## Maintenance

The repository keeps BossModReborn as its upstream history. A scheduled workflow checks upstream weekly, merges new upstream commits into a branch, verifies the radar-only boundary, builds it, and opens a review PR when the merge is clean. Conflicts are intentionally left for manual review because changes to the plugin bootstrap or action handling must never be accepted blindly.

Releases use four-part numeric tags such as `7.5.5.70`. The release workflow builds `latest.zip`, publishes it, and updates `pluginmaster.json`.

See [MAINTENANCE.md](MAINTENANCE.md) for the short update checklist.

## Attribution and support

Encounter modules and most of the codebase are maintained by the BossModReborn and original BossMod contributors. Their licenses and third-party notices are preserved in this repository.

This companion is maintained independently by Surlako and is not supported or endorsed by the BossModReborn, original BossMod, Questionable, Dalamud, or Square Enix teams. Report companion-specific issues in this repository; reproduce module bugs in upstream BossModReborn before reporting them upstream.
