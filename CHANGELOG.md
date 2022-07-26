# Changelog

All notable changes to this project will be documented in this file.

​The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).​

## [1.5.1] - 2022-07-25

### Changed

* Updated the Mediation package to 0.5.0.
  * This upgrade fixes unexpected warnings that were appearing in non-Mediation-related scenes.
  * The new version deprecated two sync methods in favor of async methods, so we're now using the newer async methods.
  * This upgrade makes a cleaner distinction between supported and unsupported platforms for Mediation.
  * When the build target is Android or iOS, rewarded ads will work normally. In other build targets, ads will be disabled.
* Added a label to the Daily Rewards use case UI to indicate how many seconds are in a day.
  * Also decreased the day length from 30 seconds to 10 seconds.

## [1.5.0] - 2022-07-19

### Added

* Over-The-Air Content Delivery use case which demonstrates how you can use Cloud Content Delivery, Addressables, and Remote Config to download new content to a game while the game is running.

### Changed

* Updated Unity Gaming Services packages to the latest stable group of packages released as of 2022-07-18.
  * Changes to this project were not needed as a result of any of these updates.

### Fixed

* Fixed Daily Rewards use case so it doesn't fail if any currencies are missing custom data or addressable address.

## [1.4.2] - 2022-06-22

### Changed

* Updated Unity Gaming Services packages to the latest stable group of packages released as of 2022-06-07.
  * Economy and Remote Config API names changed which required trivial code updates.
  * EconomyService.Instance.Configuration.GetX methods now fetch and cache the latest Remote Config configuration as well.
    This change introduced a potential race condition if the Economy configuration call and the RemoteConfigService.Instance.FetchConfigsAsync call were in the same Task.WhenAll.
    They have now been separated out.

### Fixed

* There was a bug where if a new Economy Configuration was published, neither the client nor cloud code would get the latest Economy configuration until the cache expired on its own (after a few days).
  * Fixed by adding a call to `EconomyService.Instance.Configuration.GetCurrenciesAsync()` during the initialization flow of each use case; this method call forces the Economy configuration cache to be updated.
  * Note that this call must be finished before any other Economy or Remote Config async calls are started.
* Fixed a bug in the Virtual Shop use case. If an Inventory Item or Currency in Economy doesn't have any CustomData, that caused a null reference in the client at runtime.

## [1.4.1] - 2022-06-01

### Changed

* Various improvements to all readme documentation.
* Updated Unity Gaming Services packages to the latest stable group of packages released as of 2022-05-31.
  * No notable deprecations or breaking changes.
  * Authentication updated to release 2.0.0 ([Changelog](https://docs.unity.com/authentication/Changelog.html)).
* No longer validating the UGS Use Cases project versions (minimum, latest) if those keys don't exist in Remote Config. This will make it smoother for a user who recreates the backend for this project using their own account.

## [1.4.0] - 2022-05-17

### Changed

* Updated Unity Gaming Services packages to the latest stable group of packages released as of 2022-05-11.
  * Some UGS SDK classes were deprecated and moved. We've updated the sample code to use the new classes.
  * The Cloud Code 422 error message format has changed.
    We've updated our error handling for all sample code to account for the updated message format.
  * Cloud Code method signatures have changed from accepting a single parameter object to accepting a Dictionary of parameters instead.
    We've updated the sample code to use the new method signatures.
* HUD updates in the Rewarded Ad With Unity Mediation use case have been delayed until the rewarded ad is completely watched,
  this way the HUD shows an update to the new balance only once, after all possible reward distributions are complete.
* Added Editor popup dialog when, based on minimum client version number, the UGS samples project is too old to run.
* Fixed issue with Idle Clicker Use Case Sample where placing wells at invalid locations caused additional water to be dispensed for subsequent well placements.

### Removed

* Analytics has been removed from the Seasonal Events use case, see the A/B Test use case for an example of working with Unity Analytics.

### Added

* Virtual Shop use case which demonstrates a key feature in many games: allowing players to use in-game currency to purchase items and resources to facilitate a server-authoritative in-game economy with multiple store pages and server-managed badges.

## [1.3.1] - 2022-04-13

### Changed

* Fixed a bug in Seasonal Events which caused the incorrect season to appear when the client's clock was incorrect.
  This could cause the client to appear to claim the wrong season or be unable to claim subsequent seasons when server already claimed it.
* Fixed a bug which caused the Battle Pass to occasionally throw a null ref exception during initialization.
* Fixed a bug which caused the Battle Pass to sometimes fail to start the new season because of a clock mismatch between the client and the server.
The use case has been made more server-authoritative by always using the server timestamp when deciding which is the current season.
* Fixed a bug in AB Test where under certain circumstances Cloud Save keys weren't getting created as expected.
* Fixed Addressables build issue on mobile by adding call to Addressables BuildPlayerContent call when building.

## [1.3.0] - 2022-03-21

### Added

* Rewarded Ad use case which demonstrates how to offer rewarded ads via Unity Mediation to give bonus rewards at the end of a level.
* Daily Rewards use case sample demonstrates an escalating series of daily rewards incentivizing players to keep logging in to claim better and better prizes.
* Enabled the Unity Cloud Diagnostics service for this project.
    Crashes and exceptions are automatically sent to the backend for analysis, so that we can ensure the quality of the samples.
    You can disable Cloud Diagnostics in the Services window.
* Added the User Reporting feature, so you can send us feedback about the samples from directly within the sample scenes, with automatic screenshots included.
  This is a feature you can use in your projects for your customers to send feedback to your Unity Dashboard.

### Changed

* Updated Unity Services packages to the group of packages released as of 2022-03-21.
* Fixed minor UI bug where Cloud AI Use Case Sample cursor does not appear correctly at startup or when moved between plays.
* Improved throw handling to permit Cloud Code to throw standard and custom exceptions which are caught and handled correctly in Unity client scripts.
* Changed the name of the project from "Game Operations Samples" to "Unity Gaming Services Use Cases".
* Removed unused networking and multiplayer-related packages.
* Renamed existing "Daily Rewards" use case to "Loot Boxes With Cooldown" to better describe its behavior and avoid confusion with existing live games that use a monthly calendar to grant rewards on a daily basis.

## [1.2.0] - 2022-02-09

### Added

* "Cloud AI Mini Game" use case sample demonstrates server authoritative gameplay in a simple Tic-Tac-Toe
  game played against AI running on UGS with persistent state, Currency rewards, stats, and straightforward AI.
* "Command Batching" use case demonstrates how to group game Commands into a single batch for server processing.
* "Battle Pass" use case sample demonstrates a seasonal reward tier system with a free track and a premium track.

### Changed

* Updated Unity Services packages to the group of packages released as of 2022-02-07. If there are any Burst errors logged after updating, restart Unity, or delete the Library folder to resolve the errors.
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
