# Changelog

## [Unreleased]

## [2.1.0]

### Forge version notes
- Fixed the Scav traitor warning firing when you had not betrayed anyone — most
  noticeably counting up every few seconds while cultists were on the map, and
  whenever a boss, a raider or one of SPT's hostile player-Scav bots turned on you.
- New option "Show Target Name": shows the target's name on its own line below
  the label — for squad mates and coop players (default), for every identified
  target, or never. Coop players' names move there from the type line; set the
  option to Off to hide them.
- New option "Show Target Health" (off by default): shows the target's current
  and maximum health (all body parts combined) next to the Friendly/Hostile
  label.
- Target identification now keeps working after a mid-raid respawn, instead of
  going silent for the rest of the raid — and the previous body no longer keeps
  labeling every bot "Friendly" once you are back.
- Friend-or-foe now follows the bot group's own hostility, so bots that another
  mod (arena, squad, or AI mods such as ArenaMode or SAIN) turned hostile are no
  longer labeled "Friendly", and squad mates registered as allies stay "Friendly".
- The settings menu now shows readable names for every option, in a deliberate
  order instead of raw config keys.

### Added
- `ShowBotName` (Display): Off / Teammates / All. Teammates covers bots whose
  group lists the player as an ally (team mods such as PitFireTeam) and human
  coop players; All names every identified target. Names follow the death
  screen's rule: a player-scav shows the real player name
  (`MainProfileNickname`), everything else the game's `GetCorrectedNickname`
  (Cyrillic scav names transliterated on non-Russian clients). The name is
  resolved once per target and only when the option needs it. The label box now
  grows with its text instead of clipping a third line. Coop players previously
  carried their name inside the role line ("Player (name)"); it is now the
  separate name line governed by this option.
- `ShowTargetHealth` (Display, off by default): appends "HP current/max" to the
  label line, read through the same `EBodyPart.Common` query behind the overall
  figure on the game's health screen and on Fika's health bar. On a Fika client
  it therefore shows the health Fika syncs from the host for coop players and
  bots alike. A failing lookup is logged once and stays hidden for the session.

### Fixed
- The Scav-traitor hook on `BotsGroup.AddEnemy` raised false alerts for three
  independent reasons. It counted every call that returned `true`, but the method
  also returns `true` for a player the group already lists, and the cultist amulet
  check re-registers every human every 5 s. It took `BotsGroup.Side == Savage` for
  "a Scav group", which holds for cultists, bosses, raiders and rogues as well. And
  it counted registrations made when a group or a player enters the raid
  (`initial`/`AddNewMember`, `addPlayer`, `addPlayerToBoss`, `addCauseGroup`) — how
  hostile-Scav Fence standing and SPT's hostile "traitor" player-Scav bots take
  effect. The hook now counts only a first-time registration (a Harmony prefix
  records whether the player was listed before) by a group of an actual Scav role,
  for any cause other than those arrival causes. A debug-level line reports each
  new registration and why it was or was not counted.
- After a respawn, the identifier component on the previous body kept running.
  Respawn mods keep that body as a corpse, the component re-resolved the new
  player camera, and when the player had died while aiming it kept classifying
  along the live view against the dead player object — which no bot group knows,
  so every bot read as "Friendly" on top of the real readout. The watcher now
  destroys the outgoing component when the local player changes, and a component
  only acts while its player is the current, living local player.
- Hostility was read from the per-bot `EnemiesController.EnemyInfos`, a derived
  cache that is filled per member when the group adds an enemy or the member
  activates, dropped on the enemy's death, and pruned by SAIN whenever it stops
  tracking an enemy. Classification now asks the group the way the game does: a
  group whose `BotsGroup.Allies` holds the player (how PitFireTeam registers squad
  mates) is Friendly, a group whose `BotsGroup.IsEnemy` reports the player is
  Hostile, and the per-bot entry only counts as an additional positive signal.
  Ids are compared, mirroring `BotsGroup.IsEnemy`, so both checks share one
  identity rule.
- A debug-level log line reports those three signals whenever the aimed target
  changes, so a wrong label can be traced without per-frame noise.
- The respawn watcher re-activated the raid-start player once more on its first
  Update and logged it as a respawn; it is now told which player is already set up.
- Identification no longer stops permanently after a mid-raid respawn. Respawn
  mods (arena modes, CorpseRun-style redeploys) replace the local player object,
  which destroyed the identifier component; on top of that, unregistering the old
  player was treated as "raid over", which latched a kill switch and deleted the
  component outright. Three changes: the raid-over check now requires the game
  itself to be shutting down, a watcher on the plugin object re-activates the
  identifier on whichever player is currently local, and the component no longer
  latches that kill switch while being destroyed (Unity destroys at end of frame,
  so an outgoing instance could otherwise disable its own replacement).
- The aim raycast survives a camera rebind: the player camera is re-resolved when
  it disappears, instead of leaving the readout dark for the rest of the raid.

### Changed
- Every config entry carries a display name and an explicit order for the in-game
  settings menu (BepInEx ConfigurationManager), via the duck-typed
  `ConfigurationManagerAttributes` tag. The `.cfg` keys and section names are
  unchanged, so existing config files stay valid.
- Reworded four option descriptions that understated their effect: distance
  scaling is a factor rather than an added number of seconds, FriendlyOnly also
  bypasses the identification delay, the identification range is a maximum, and
  the target-type line shows Player for coop players.

## [2.0.1]

### Forge version notes
- Fixed an identification label or traitor warning that could stay on screen at
  the end of a Scav raid.
- Fixed Fika coop raids showing Scav bots as "Friendly" to a joining player whose
  Fence standing already makes every Scav hostile to them.
- A patch that fails to apply no longer takes the rest of the mod down with it.
- Fixed the Scav-traitor warning clipping its repeat counter on the right.

### Fixed
- The raid-end teardown never ran in Scav raids. It matched the leaving player
  against the session profile, which is always the PMC profile, while a Scav raid
  runs on the scav profile — so the check could not match. It now uses
  `IPlayer.IsYourPlayer`, the same criterion `GameWorld` uses to pick `MainPlayer`,
  which keeps registration and unregistration symmetric. Because the default
  `ActivationMode = Automatic` only attaches in Scav raids, this path was dead code
  in the default configuration. `OnGUI` now honours the same raid-over guard that
  `Update` already had, so nothing is drawn once the local player is gone.
- Fika client raids: friend-or-foe classification ignored the player's own Fence
  standing. Below the hostile-Scav threshold the game treats the player as an enemy
  of every bot group from spawn, but the mod still showed "Friendly" — inverted
  precisely for the FriendlyOnly mode, whose whole purpose is a safe-to-hold-fire
  signal. It now mirrors the game's own rule, which ORs `Loyalty.HostileScavs` into
  the hostility result in the Savage branch of `BotsGroup.IsPlayerEnemy`.
- The Scav-traitor warning drew into a fixed 240px rect, which clipped the repeat
  counter ("×2" and up) on the right. The rect is now sized to the rendered text
  and grows leftwards from the screen edge.

### Changed
- Patch activation is failure-tolerant. Each patch is applied individually and a
  `PatchException` is logged instead of aborting plugin startup, so one broken
  target costs a single feature rather than the whole mod. If the two Fika patches
  cannot both be applied, the first is rolled back, leaving nothing hooked behind
  the then-disabled Fika-support flag.
- Build only: the missing-Fika build error now points at a Fika 2.4.x install for
  SPT 4.1 instead of the frozen SPT 4.0 install.

## [2.0.0]

### Forge version notes
- AutoIFF now runs on SPT 4.1. Features are unchanged from 1.2.0. For coop
  raids, the matching Fika 2.4 line for SPT 4.1 is required.

### Changed
- Ported to SPT 4.1; the 1.x line stays available for SPT 4.0.
- Coop support is now built and tested against Fika 2.4.0 (the Fika line for
  SPT 4.1). Adapted the observed-shot patches to the renamed EFT damage type
  (`DamageInfoStruct` → `DamageInfo`).
- Build only: the Fika compile-time reference is now auto-detected across the
  known dev installs instead of pointing at the frozen 4.0 install.

## [1.2.0]

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

## [1.1.0]

### Added
- **Hotkey activation mode.** New `ActivationMode = Hotkey`: the mod attaches in every raid but starts inactive and is toggled on/off with a configurable keybind (`ActivationHotkey`, unassigned by default).
- **Friendly-only mode.** New `FriendlyOnly` toggle: only friendly targets are shown, instantly and without the identification delay; hostile targets show no label. Useful against friendly fire in any raid type.

## [1.0.0]

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
