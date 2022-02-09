# Changelog

## [1.2.0] - 2022-02-09

### Added

* "Cloud AI Mini Game" use case sample demonstrates server authoritative gameplay in a simple Tic-Tac-Toe
  game played against AI running on UGS with persistent state, Currency rewards, stats, and straightforward AI.
* "Command Batching" use case demonstrates how to group game Commands into a single batch for server processing.
* "Battle Pass" use case sample demonstrates a seasonal reward tier system with a free track and a premium track.

### Changed

* Updated Unity Services packages to the group of packages released as of 02/07/22. If there are any Burst errors logged after updating, restart Unity, or delete the Library folder to resolve the errors.
* In the AB Test scene, after calling Authentication.Instance.SignOut a call to Authentication.Instance.ClearSessionToken has been added to maintain current functionality of being able to immediately sign in again as a new anonymous user.
* Disabled "Reset Starter Pack" button during startup and when starter pack has not been purchased.
* Disabled playfield buttons during startup in Idle Clicker use case sample to permit initialization to complete before making UGS requests.
* Fixed singletons so they don't break when multiple components accidentally added to the same scene.

## [1.1.0] - 2022-01-12

### Added

* "Idle Clicker Mini-Game" use case sample that demonstrates server authoritative game state in real time,
  similar to idle clicker and social games.
  
### Changed

* Limited the frame rate during play mode to 30 FPS.
* Prevented Cloud Code double seasonal reward distribution in Seasonal Event use case.
* Updated RewardPopup prefab to allow for codeless customization.

## [1.0.1] - 2021-11-29

### Added

* All use case scenes now have a back button to return to the Start Here scene during Play Mode.
* Show the currency reward on the player's level-up popup for the AB Test Level Difficulty Sample Use Case.
* README _asset_ files have been added. They have the same content as the existing markdown README files, but these show up with proper formatting in the Inspector window when selected.
* Examples for how to use the Analytics service have been added to the AB Test Level Difficulty and Seasonal Events use cases.
* Additional Editor Analytics events to help Unity improve the samples.

### Changed

* General code cleanup in the samples.
* RemoteConfigManager in AB Test Level Difficulty Use Case now uses Remote Config's ConfigManager.FetchConfigsAsync() method, instead of the older (non-async) ConfigManager.FetchConfigs() method.

## [1.0.0] - 2021-10-20

This is the initial public release of *Game Operations Samples*.

### Features

* Use Case Samples:
  * AB Test Level Difficulty
  * Daily Rewards
  * Loot Boxes
  * Seasonal Events
  * Starter Pack
