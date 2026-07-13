# Changelog

## 1.2.0

### Added
- **Fika support.** AutoIFF now works in coop raids, including headless-hosted ones. Background: on any Fika client (everyone who *joins* a raid — with a headless host that is every player), bots run on the host and have no local AI data, which previously meant no label ever appeared.
  - When you host (or play regular SPT), identification keeps using the exact bot hostility data as before.
  - When you join a raid, friend-or-foe is derived from the target's role and your faction. Human coop players are always shown as **Friendly** with their nickname.
  - New third label **Wary** (orange) for bots that are not hostile at spawn but escalate when approached or provoked (e.g. bosses, Raiders, and Rogues vs. player Scavs).
  - Scav traitor detection on clients: if you damage an innocent Scav, AutoIFF assumes traitor status — the warning fires and Scavs are labeled Hostile for the rest of the raid.
  - Safe to install on a headless host (the mod simply stays inactive there).
  - Fika is optional: without it nothing changes; Fika 2.3.x or newer is required for the coop features.

### Fixed
- **Identification memory is now truly per target.** Previously all SPT bots shared account id `0`, so identifying one bot silently skipped the identification delay for *every* bot for the memory duration (60s default). Each target now has to be identified once individually, as originally documented. Raids will feel slightly slower than 1.1.0 — this is intended.
- "Target too far" no longer prints the exact distance when `ShowDistance` is disabled.
- After an "Obscured by foliage" message, subsequent messages ("Target too far", "Losing target...") no longer stay stuck at the smaller font size.

## 1.1.0

### Added
- **Hotkey activation mode.** New `ActivationMode = Hotkey`: the mod attaches in every raid but starts inactive and is toggled on/off with a configurable keybind (`ActivationHotkey`, unassigned by default).
- **Friendly-only mode.** New `FriendlyOnly` toggle: only friendly targets are shown, instantly and without the identification delay; hostile targets show no label. Useful against friendly fire in any raid type.

## 1.0.0

Initial release — a complete rewrite of [LightsAutomaticIdentifier](https://hub.sp-tarkov.com/files/file/2669-lightsautomaticidentifier/) by **Light** (MIT License).

### Added
- BepInEx configuration for everything: activation mode, identification time/range/distance scaling, memory duration, skill scaling, display options.
- Activation modes: **Automatic** (active in Scav raids only — the default), **AlwaysOn**, **AlwaysOff**.
- **Bot role display** below the Friendly/Hostile label (PMC faction, Scav, Raider, Rogue, all bosses and followers, Cultists, Infected, …).
- **Skill scaling**: identification time and range scale with Attention, Perception, and Search levels, including Elite bonuses.
- **Identification memory**: identified targets are remembered for a configurable duration.
- **Scav traitor detection**: event-driven hook on `BotsGroup.AddEnemy` (no polling) with a bottom-right flash alert; the counter increments as more Scav groups learn about you.
- **Conflict detection**: if the original LightsAutomaticIdentifier is installed alongside, AutoIFF deactivates itself and shows an in-game warning.

### Fixed
- NullReferenceException at the end of Scav raids (HandsController teardown race).
