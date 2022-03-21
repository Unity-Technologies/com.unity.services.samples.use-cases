# Daily Rewards
The Daily Reward (Monthly) Calendar is a prevalent feature in connected games. Whether it is presented in a weekly or monthly calendar, with or without completion streaks, Daily Rewards are present in games across all genre to boost retention.

This feature will present a calendar of Daily Rewards that generally increase in value over time to encourage players to return daily to claim them. Our implementation permits skipping days, but rewards are always claimed sequentially so, if a day is missed, the same reward will be available the next day. Only once a particular day is claimed will the subsequent day's reward be unlocked (on the next day).

### Implementation Overview
This sample demonstrates how to initialize Unity Services, retrieve and update current values from the Economy service, call Cloud Code to retrieve the updated status and claim each day's reward.

This Use Case Sample uses Cloud Save rather than Remote Config to store the Event start time (i.e. the first day of the month). The normal implementation for Daily Rewards is to set the start epoch time on Remote Config so all players experience the rewards starting on the first day of the month. However, to permit testing, we save this value to Cloud Save so each user can experience the Daily Rewards calendar as if the month starts when the Use Case sample is first opened.

**Note:** To permit faster testing, each "day" is compressed into 30 seconds. This permits an entire month's rewards to be claimed in 15 minutes.

#### Currency Icon Sprite Addressables Implementation
An added feature of this Use Case Sample is how it implements the Currency icon Sprites for all Currencies. A key feature of this implementation is that you, as a developer, can add the Addressables address to the Currency icon for each Currency directly in the Custom Data on the Economy Dashboard and retrieve it at runtime without need to change your code. This permits, for example, swapping in holiday-themed Sprites simply by changing the address used on the Economy Dashboard without needing to update your app.

To implement, `spriteAddress` values are added to each Economy Currency's Custom Data json, allowing this Use Case sample to determine each Currency's Addressables address and initialize all Currency icon Sprites at startup. Later, when Sprites are needed (for example, when showing the rewards granted for claiming a Daily Reward), the Currency's key (for example, "COIN") is used as a dictionary key to quickly find the associated Sprite, which is then used in the popup.

For details, please see the Daily Rewards' `EconomyManager.cs InitializeCurrencySprites()` method which is called at startup to initialize the dictionary with all Currency icon Sprites, and the GetSpriteForCurrencyId method which looks up Currency IDs (such as "COIN") to find the associated Sprite.

The Economy Dashboard Configuration contains the following data to facilitate these Currency icon Addressables:
* "COIN":
  * Custom data:
  ```json
    { 
      "spriteAddress": "Sprites/Currency/Coin" 
    }
  ```
* "GEM":
  * Custom data:
  ```json
    { 
      "spriteAddress": "Sprites/Currency/Gem" 
    }
  ```
* "PEARL":
  * Custom data:
  ```json
    { 
      "spriteAddress": "Sprites/Currency/Pearl" 
    }
  ```
* "STAR":
  * Custom data:
  ```json
    { 
      "spriteAddress": "Sprites/Currency/Star" 
    }
  ```

### Packages Required
- **Authentication:** Automatically signs in the user anonymously to keep track of their data on the server side.
- **Economy:** Retrieves the starting and updated currency balances at runtime.
- **Cloud Code:** Accesses the current event status, claims Daily Rewards and resets the feature at the end of the month for demonstration purposes. It also calls Remote Config to determine the parameters for the Daily Rewards (rewards to grant, number and duration of days, etc.)
- **Remote Config:** Defines parameters for the Daily Rewards event including rewards granted on each day, day duration, number of days, etc.
- **Addressables:** Allows asset retrieval by address.
- **Cloud Save:** Stores and retrieves the event status such as start epoch time, days claimed, etc.

See the [Authentication](https://docs.unity.com/authentication/Content/InstallAndConfigureSDK.htm),
[Economy](https://docs.unity.com/economy/Content/implementation.htm?tocpath=Implementation%7C_____0),
[Cloud Code](https://docs.unity.com//cloud-code/Content/implementation.htm?tocpath=Implementation%7C_____0#SDK_installation),
[Remote Config](https://docs.unity3d.com/Packages/com.unity.remote-config@2.0/manual/ConfiguringYourProject.html),
[Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest),
and [Cloud Save](https://docs.unity.com/cloud-save/implementation.htm) docs to learn how to install and configure these SDKs in your project.

### Dashboard Setup
To use Economy, Cloud Code, Remote Config and Cloud Save services in your game, activate each service for your organization and project in the Unity Dashboard. You’ll need a few currency items for rewards, as well as scripts in Cloud Code:

#### Economy Items
* Coin - `ID: "COIN"` - a currency reward
* Gem - `ID: "GEM"` - a currency reward
* Pearl - `ID: "PEARL"` - a currency reward
* Star - `ID:"STAR"` - a currency reward

#### Cloud Code Scripts
* DailyRewards_GetStatus: Called at startup to retrieve the current status of the event from Cloud Save, update it and return it to the client.
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Daily Rewards/Cloud Code/DailyRewards_GetStatus.js`
* DailyRewards_Claim: Called in response to a claim Daily Reward request, this script verifies eligibility, grants the appropriate day's rewards and updates the state on Cloud Save.
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Daily Rewards/Cloud Code/DailyRewards_Claim.js`
* DailyRewards_ResetEvent: For demonstration purposes, this script resets the Daily Rewards state on the client so a new month's rewards can be granted.
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Daily Rewards/Cloud Code/DailyRewards_ResetEvent.js`

_**Note**:
The Cloud Code scripts included in the `Cloud Code` folder are just local copies, since you can't see the sample's dashboard. Changes to these scripts will not affect the behavior of this sample since they will not be automatically uploaded to Cloud Code service._
