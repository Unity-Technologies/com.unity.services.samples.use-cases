# Battle Pass

A seasonal points-based reward system is a popular and effective way to retain players in a game over time.
Unlike a time-based reward system, there is an element of skill required to progress through the reward track.
The Battle Pass adds another layer of exclusive appeal to this system, while also adding a monetization mechanic, by letting players purchase a second premium track with additional rewards.
This sample uses currencies as premium rewards, though most games are designed to award cosmetic items at the premium level, or other items that do not give players a gameplay advantage.

This sample expands on the [Seasonal Events sample](https://github.com/Unity-Technologies/com.unity.services.samples.game-operations/blob/main/Assets/Use%20Case%20Samples/Seasonal%20Events/README.md)
to add a seasonal points-based reward system and Battle Pass.
It's a good idea to check out that sample first, if you haven't already.
The README in that sample explains how the seasonal events are configured and how they work.
This document will explain how this Battle Pass sample builds on and complements the Seasonal Events sample, and specific Battle Pass features.

Each time a new season starts, the player is offered a set of rewards unique to the current season, which they can unlock by earning Season Experience Points (Season XP).
There are two tracks of rewards: the normal track, and the Battle Pass (premium) track.
All players are eligible to claim the normal rewards track, but only Battle Pass holders are eligible for the Battle Pass rewards.

The Season XP and Battle Pass are only relevant to the current season.
When a season ends, the player's Season XP progress and Battle Pass ownership are reset, but the claimed rewards are permanent.


### Implementation Overview

When this scene first loads, it will initialize Unity Services and sign the player in anonymously using Authentication.
This can be seen in the BattlePassSceneManager script.

Once Unity Services completes initialization, we use a Cloud Code function to retrieve the configuration and state of the Battle Pass.
The configuration that's returned to us is based on the server's timestamp and the Game Override that's currently active.
Reward tiers are stored as two JSON values in Remote Config: one array for the free tiers, and one array for the premium tiers.
From our Cloud Code call, we also get a number of seconds until the season is over.
With that we can control our countdown view, and eventually determine that it's time to query the server again for the next season's configs.

_**Note**: This sample determines which Game Override data should be returned based on the last digit of the number of minutes in the current server time.
This is a simplification to be able to frequently observe the season change.
In a real app, developers likely set up a Game Override to have specific start and end dates, then Remote Config determines when the Game Override is shown based on the server’s date/time.
In that case, the client and server implementations can be a bit different._

Everything about the reward system and Battle Pass is powered by Cloud Code scripts, from getting the progress to claiming tiers to purchasing a Battle Pass.
At first, it looks like some of these actions could be simpler calls directly to each service.
For example, to get the player's progress, you could download the current player state via the Cloud Save SDK, and to purchase a Battle Pass, you could make a call directly via the Economy SDK.
However, these actions have potential side effects that we want to be server-authoritative:

- By retrieving the player's progress, you also want it to reset the player's progress if the season has changed.

- By purchasing a Battle Pass, you might also be granted some premium rewards for tiers you already claimed.


### Packages Required

- **Authentication:**
Automatically signs in the user anonymously to keep track of their data on the server side.

- **Economy:**
Keeps track of the player's currencies and inventory items.

- **Cloud Code:**
Keeps important validation logic on the server side.
In this sample it is used for four main purposes:
  - Retrieve the player's season progress, or reset their progress if the season has changed.
  - Gain Season XP, potentially unlocking new reward tiers.
  - Claim a reward tier, granting currency or inventory items.
  - Purchase a Battle Pass, which unlocks more rewards and possibly grants rewards for tiers already claimed.

- **Remote Config:**
Provides key-value pairs where the value that is mapped to a given key can be changed on the server-side, either manually or based on specific Game Overrides.
In this sample, we use the Game Overrides feature to create the four seasonal events and return different values for certain keys based on the Game Override.

- **Addressables:**
Allows developers to ask for an asset via its address.
Wherever the asset resides (local or remote), the system will locate it and its dependencies, then return it.
Here, we use it to look up event specific images and prefabs based on the information we receive from Remote Config.

- **Cloud Save:**
Stores the player's Season XP progress and Battle Pass ownership token.
This sample doesn't actually use the Cloud Save methods in C#, as all of the Cloud Save work is done via Cloud Code.

See the [Authentication](https://docs.unity.com/authentication/Content/InstallAndConfigureSDK.htm),
[Economy](https://docs.unity.com/economy/Content/implementation.htm?tocpath=Implementation%7C_____0),
[Cloud Code](https://docs.unity.com//cloud-code/Content/implementation.htm?tocpath=Implementation%7C_____0#SDK_installation),
[Remote Config](https://docs.unity3d.com/Packages/com.unity.remote-config@2.0/manual/ConfiguringYourProject.html),
[Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest),
and [Cloud Save](https://docs.unity.com/cloud-save/implementation.htm) docs to learn how to install and configure these SDKs in your project.

### Dashboard Setup

The setup here has a lot in common with the setup in the Seasonal Event sample.
We won't be using the Challenge Reward or seasonal images in this sample, but we'll be using everything else, with some additions.

To use Economy, Remote Config, and Cloud Code services in your game, activate each service for your organization and project in the Unity Dashboard.
To duplicate this sample scene's setup on your own dashboard, you'll need a few currencies in the Economy setup, some Config Values and Game Overrides set up in Remote Config, and a number of scripts published in Cloud Code:

#### Economy Items

* Currency
  * Gem - `ID: "GEM"` - A premium currency used to purchase a Battle Pass, but also rewarded by claiming Battle Pass tiers.
  * Coin - `ID: "COIN"` - A soft currency granted by some reward tiers.
  * Pearl - `ID: "PEARL"` - A soft currency granted by some reward tiers.
  * Star - `ID:"STAR"` - A soft currency granted by some reward tiers.

* Inventory
  * Sword - `ID: "SWORD"` - A gameplay-related item granted by some normal reward tiers.
  * Shield - `ID: "SHIELD"` - A gameplay-related item granted by some normal reward tiers.

* Virtual Purchase
  * Name: Battle Pass
  * ID: BATTLE_PASS
  * Cost: 10 Gems
  * Rewards: None (Cloud Code will grant Battle Pass ownership in Cloud Save)


#### Remote Config

##### Config Values

* EVENT_NAME - The name of the event to display in the scene.
  * Type: `string`
  * Value: `""`

* EVENT_KEY - The key used to look up event-specific values, such as the addresses for the specific images.
  * Type: `string`
  * Value: `""`

* EVENT_END_TIME - The last digit of the last minute during which the Game Override is active. Used when determining how much time is left in the current event.
  * Type: `int`
  * Value: `0`

* EVENT_TOTAL_DURATION_MINUTES - The total number of minutes that a given season's Game Override is active for.
  * Type: `int`
  * Value: `0`

* BATTLE_PASS_TIER_COUNT - The total number of tiers each season. Not overridden by Game Overrides in this example.
  * Type: `int`
  * Value: `10`

* BATTLE_PASS_SEASON_XP_PER_TIER - The amount of Season XP needed to unlock each tier. Not overridden by Game Overrides in this example.
  * Type: `int`
  * Value: `100`

* BATTLE_PASS_REWARDS_FREE - The JSON that specifies what rewards are distributed when each tier is claimed. This design accounts for just one reward per tier. Overridden by seasonal Game Overrides.
  * Type: `json`
  * Value: `[]` (reward tiers aren't available outside of seasonal events)

* BATTLE_PASS_REWARDS_PREMIUM - Just like BATTLE_PASS_REWARDS_FREE, but only granted when the player owns that season's Battle Pass. Overridden by seasonal Game Overrides.
  * Type: `json`
  * Value: `[]` (reward tiers aren't available outside of seasonal events)

##### Game Overrides

* Fall Event
  * Status: Active
  * Audience: Stateless JEXL
    * `user.timestampMinutes % 10 == 0 || user.timestampMinutes % 10 == 1 || user.timestampMinutes % 10 == 2`
  * Start Date: Immediately
  * End Date: Indefinitely
  * Overrides:
    * EVENT_NAME: `Fall Event`
    * EVENT_KEY: `Fall`
    * EVENT_END_TIME: `2`
    * EVENT_TOTAL_DURATION_MINUTES: `3`
    * BATTLE_PASS_REWARDS_FREE:
      ```json
      [
          {
              "id": "SWORD",
              "quantity": 1,
              "spriteAddress": "Sprites/Inventory/Sword"
          },
          {
               etc...
          }
      ]
      ```
    * BATTLE_PASS_REWARDS_PREMIUM:
      ```json
      [
          {
              "id": "PEARL",
              "quantity": 50,
              "spriteAddress": "Sprites/Currency/Pearl"
          },
          {
               etc...
          }
      ]
      ```


* Winter Event
  * Audience: Stateless JEXL
    * `user.timestampMinutes % 10 == 3 || user.timestampMinutes % 10 == 4`
  * EVENT_NAME: `Winter Event`
  * EVENT_KEY: `Winter`
  * EVENT_END_TIME: `4`
  * EVENT_TOTAL_DURATION_MINUTES: `2`
  * _For everything else, just like the Fall Event, but with different rewards._


* Spring Event
  * Audience: Stateless JEXL
    * `user.timestampMinutes % 10 == 5 || user.timestampMinutes % 10 == 6 || user.timestampMinutes % 10 == 7`
  * EVENT_NAME: `Spring Event`
  * EVENT_KEY: `Spring`
  * EVENT_END_TIME: `7`
  * EVENT_TOTAL_DURATION_MINUTES: `3`
  * _For everything else, just like the Fall Event, but with different rewards._


* Summer Event
  * Audience: Stateless JEXL
    * `user.timestampMinutes % 10 == 8 || user.timestampMinutes % 10 == 9`
  * EVENT_NAME: `Summer Event`
  * EVENT_KEY: `Summer`
  * EVENT_END_TIME: `9`
  * EVENT_TOTAL_DURATION_MINUTES: `2`
  * _For everything else, just like the Fall Event, but with different rewards._


#### Cloud Code Scripts

* BattlePass_GetState:
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Battle Pass/Cloud Code/BattlePass_GetState.js`

* BattlePass_GainSeasonXP:
  * Parameters:
    * amount
      * Type: Numeric
      * The amount of season XP to gain.
  * Script: `Assets/Use Case Samples/Battle Pass/Cloud Code/BattlePass_GainSeasonXP.js`

* BattlePass_ClaimTier:
  * Parameters:
    * tierIndex
      * Type: Numeric
      * The 0-based index of the tier to claim.
  * Script: `Assets/Use Case Samples/Battle Pass/Cloud Code/BattlePass_ClaimTier.js`

* BattlePass_PurchaseBattlePass:
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Battle Pass/Cloud Code/BattlePass_PurchaseBattlePass.js`

_**Note**:
The Cloud Code scripts included in the `Cloud Code` folder are just local copies, since you can't see the sample's dashboard. Changes to these scripts will not affect the behavior of this sample since they will not be automatically uploaded to Cloud Code service._
