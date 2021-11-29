# Changelog

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
