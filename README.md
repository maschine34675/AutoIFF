# AutoIFF — Automatic Identify Friend or Foe

Automatically identifies the target you are currently aiming at as **Friendly** or **Hostile**, and alerts you when you are marked as a **Scav traitor**.

Based on [LightsAutomaticIdentifier](https://hub.sp-tarkov.com/files/file/2669-lightsautomaticidentifier/) by **Light** (MIT License).

---

## Features

- **Target identification** — aim at any bot to identify it as Friendly or Hostile after a short delay
- **Bot role display** — shows the target's role: PMC (USEC/BEAR), Scav, Sniper Scav, Raider, Boss (Killa, Reshala, Gluhar, Sanitar, Tagilla, Knight, Zryachiy, Shturman, Kaban, Kolontay, Partisan), Boss Follower, Cultist, Infected, and more
- **Skill scaling** — identification time and range are affected by your Attention, Perception, and Search skill levels, including Elite bonuses
- **Identification memory** — previously identified targets are remembered for 60 seconds (configurable), so you don't have to re-identify them
- **Scav traitor detection** — detects the exact moment a Scav group marks you as hostile and shows a flash alert in the bottom-right corner. The counter increments each time an additional group finds out, giving you a sense of how far the information has spread across the map
- **Activation mode** — set to *Automatic* (Scav raids only), *AlwaysOn*, *AlwaysOff*, or *Hotkey* (toggle on/off via a configurable keybind)
- **Friendly-only mode** — instantly highlights friendly targets only, with no identification delay; hostile targets show no label — useful for preventing friendly fire in any raid type
- **Fika support** — works in coop raids, including headless-hosted ones (see below)
- **Conflict detection** — if the original LightsAutomaticIdentifier is also installed, AutoIFF disables itself and shows a warning in-game

---

## How It Works

While aiming down sights, a raycast is fired from the player camera. When it hits a bot within range, a short identification timer begins. Once complete, the target is labeled Friendly or Hostile based on whether it has registered the player as an enemy.

Scav traitor detection hooks directly into `BotsGroup.AddEnemy` — the single point through which all enemy-registration paths converge (direct hit response, group propagation, and zone-wide spread). This means the alert fires at the earliest possible moment, with no polling.

---

## Fika Support

AutoIFF works with [Fika](https://project-fika.gitbook.io/) coop raids (requires Fika 2.3.x or newer; AutoIFF only needs to be installed on your own client):

- **When you are the raid host** (or playing regular SPT), bots run locally and identification uses the exact hostility data, just like in singleplayer.
- **When you join a raid hosted by someone else — including a headless host** — bot AI only exists on the host, so exact hostility data is not available on your machine. AutoIFF then derives friend-or-foe from the bot's role and your faction:
  - Human coop players are always shown as **Friendly** with their nickname
  - Bots that are hostile by default (e.g. everything vs. PMCs; cultists, Killa, Shturman, the Goons vs. player Scavs) are shown as **Hostile**
  - Bots that are *not* hostile at spawn but escalate when approached or provoked (e.g. bosses, Raiders, and Rogues vs. player Scavs) are shown as **Wary** in orange
  - If you damage an innocent Scav as a player Scav, AutoIFF assumes traitor status: the traitor warning fires and Scavs are labeled Hostile for the rest of the raid

The `Wary` label and the traitor assumption only exist in these client-joined coop raids — everywhere else the mod shows the bot AI's real state.

---

## Configuration

All settings are available via BepInEx's configuration system (e.g. with [BepInEx Configuration Manager](https://hub.sp-tarkov.com/files/file/1304-bepinex-configuration-manager/)).

| Section | Setting | Default | Description |
|---|---|---|---|
| General | ActivationMode | Automatic | Automatic / AlwaysOn / AlwaysOff / Hotkey |
| General | ActivationHotkey | *(unbound)* | Keybind to toggle the mod when using Hotkey mode |
| General | FriendlyOnly | false | Only show friendly targets (no delay, hostile targets show nothing) |
| Identification | BaseIdentificationTime | 0.7s | Base time to identify a target |
| Identification | IdentificationRange | 100m | Maximum identification range |
| Identification | DistanceMultiplier | 0.1 | How much distance slows identification |
| Identification | MemoryDuration | 60s | How long identified targets are remembered |
| Skills | UseSkillScaling | true | Scale values based on Attention, Perception, Search |
| Display | ShowDistance | false | Show distance to target |
| Display | ShowBotRole | true | Show bot role below the Friendly/Hostile label |
| Display | ShowTraitorWarning | true | Show Scav traitor alert |
| Display | TraitorAlertDuration | 5s | How long each traitor alert stays on screen |

---

## Credits

Original concept and implementation by **Light** — [LightsAutomaticIdentifier](https://hub.sp-tarkov.com/files/file/2669-lightsautomaticidentifier/).
