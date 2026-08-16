# Changelog

## Version 1.1.0

- Fixed an stamina ussage issue that stopped the mod from working

## Version 1.0.0

### Added

- Charge system
  - Charge builds while holding the push key and is applied upon release.
  - Longer charge = stronger push (up to x1.5 at full charge).
  - Stamina cost dynamically scales with charge (up to x3 at full charge).
  - Visual charge indicator displayed as a progress bar at the side of the screen.

- Self-push
  - You cannot push yourself by accident anymore. Instead, a dedicated self-push key, `G`, is provided.
    - You can do some really funny item throws such as a BingBong orbital cannon by pushing yourself, try it out!

- Configuration system
  - The following options can be customized in the BepInEx configuration file:
    - `PushKey` — Key to perform a regular push (default: `F`)
    - `SelfPushKey` — Key to push yourself (default: `G`)
    - `CanCharge` — Enable or disable charge system (default: true)

### Fixed

- Pushing others uses a raycast filter that whitelists characters, bypassing held items like Bing Bong.
- Push animation and sound effect now plays on the sender, not just the target.
- Bing Bong item detection now uses item tags, avoiding compatibility issues when playing in other languages.

### Contributors

- AngelHeal
- goldenstein64
- Cyber-Corvid

## Version 0.6.0

- Recompiled checks to mitigate errors after the Mesa Update

## Version 0.5.0

- Added Bing Bong Force Multiplier
  - Holding Bing Bong while pushing will push with 10x Force
  - It also uses up the entirety of your stamina

## Version 0.4.1

- Removed log spam

## Version 0.4.0

- Fixed an issue where animations for other players would play when you were pushing then

## Version 0.3.0

- Added Animation for pushing
- Fixed the push cooldown to only be set when a player is actually interacted with
- Fixed an issue where players could push while climbing
- Fixed an issue where players could push while holding items

## Version 0.2.0

- Fixed an issue where players could push while dead or unconscious
- Fixed an issue where players could push while being carried

## Version 0.1.0

- Release
