# Daily Rewards
Similar to a loot box, daily rewards such as currencies and inventory items can be granted to players at timed intervals as a strategy to boost player retention and interest over time. Daily rewards are a great way for players to feel engaged and motivated to keep playing and get rewarded for it.

This sample shows how to set up a random daily reward in your game, or in other words, how to grant players timed rewards consisting of multiple, random currencies and inventory items. This sample covers the case where, after a player claims a reward, they must wait a pre-set amount of time before claiming another.

### Implementation Overview
This sample demonstrates how to initialize Unity Services, retrieve and update current values from the Economy service, call Cloud Code to pick random Currencies and Inventory Items from internal lists, then call the Economy service directly to grant the reward, returning the final results to the calling Unity C# script.

Cloud Code is used to access Cloud Save to implement a cooldown between rewards and returns:

* A flag if the claim button should be enabled
* The current cooldown in seconds
* The default cooldown needed to reset the timer locally when a reward is claimed

Note: This sample also includes enhanced error handling to catch and resolve issues arising from calling the Economy service too frequently (more than 5x per second) which causes the _EconomyException_ with reason _RateLimited_. This sample catches the exception, pauses .1 seconds with exponential back-off, then retries until success.

### Packages Required
- **Economy:** Retrieves the starting and updated currency balances at runtime.
- **Cloud Save:** Stores and retrieves the last grant time to allow cooldown values to persist between sessions.
- **Cloud Code:** Accesses the cooldown status, picks and grants random currency and inventory items through the Economy server, and returns the result of the reward.

See the [Economy](https://docs.unity.com/Economy), [Cloud Save](https://docs.unity.com/Cloud-Save) and [Cloud Code](https://docs.unity.com/Cloud-Code) docs to learn how to install and configure these SDKs in your project.

### Dashboard Setup
To use Economy, Cloud Save, and Cloud Code services in your game, activate each service for your organization and project in the Unity Dashboard. You’ll need a few currency and inventory items for your reward, as well as scripts in Cloud Code:

#### Economy Items
* Coin - `ID: "COIN"` - a currency reward
* Gem - `ID: "GEM"` - a currency reward
* Pearl - `ID: "PEARL"` - a currency reward
* Star - `ID:"STAR"` - a currency reward
* Sword - `ID:"SWORD"` - an inventory item reward
* Shield - `ID:"SHIELD"` - an inventory item reward

#### Cloud Code Scripts
* GrantTimedRandomReward:
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Daily Rewards/Cloud Code/GrantTimedRandomReward.js`
* GrantTimedRandomRewardCooldown:
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Daily Rewards/Cloud Code/GrantTimedRandomRewardCooldown.js`

_**Note**:
The Cloud Code scripts included in the `Cloud Code` folder are just local copies, since you can't see the sample's dashboard. Changes to these scripts will not affect the behavior of this sample since they will not be automatically uploaded to Cloud Code service._
