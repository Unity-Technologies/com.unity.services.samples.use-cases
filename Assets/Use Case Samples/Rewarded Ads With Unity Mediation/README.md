# Rewarded Ads with Unity Mediation
Offering players the opportunity to receive rewards in exchange for viewing an ad can be a great way to increase your game's revenue.

In this sample we demonstrate offering players the opportunity to increase the rewards they can receive at the end of a level by watching a rewarded ad.
We've also added an additional feature to encourage interaction with the rewarded ad: a periodically-appearing booster challenge where players can get up to a 5x multiplier (instead of the standard 2x) by clicking at just the right time. 
In this way we gamify the rewarded ad opportunity making it more fun and tempting for players to interact with.

**Note:** Unity Mediation only supports iOS and Android, so if the project's build target is any platform other than these two you will see warnings in the console at playmode initialization and at Unity Services initialization indicating the platform is unsupported.
The use case will still function in the editor despite the warnings, however if you were to create a build for one of the unsupported platforms the ad would not show.
If you would like to see the use case without the warnings, you can change the project's build target to either the iOS or Android platforms.
This can be done by navigating to File -> Build Settings.

### Implementation Overview

#### Scene Initialization
When the scene first loads it will make calls to the Unity Services APIs in order to set up the scene:
- Initialize Unity Services
- Get currency balances from Economy
- Get turn count data from Cloud Save
- Load the first ad from Unity Mediation

#### Level Ended Popup
Players will see a `Complete Level` button on the screen (this is to shortcut the real-life experience of actually playing and completing a level).
When clicked, a popup will appear.
This popup could display one of three scenarios:
1. Gamified Rewarded Ad option
2. Standard Rewarded Ad option
3. Collect Level End Rewards only (no rewarded ad option)

For the first scenario, our gamified rewarded ad challenge is a metronome-type arrow bouncing back and forth and highlighting different multiplier options (2x, 3x, 5x) with a button indicating an ad watch to claim the rewards.
Below the rewarded ad challenge is a second button for players who opt to collect their level rewards without any multipliers and without watching an ad.
This scenario will appear the first time a player sees this popup, and every third time after that (i.e. first, fourth, seventh, etc).
The periodicity of seeing the gamified challenge is intended to make its appearance more exciting and increase the desire to interact with it due to its rareness.

The second, non-gamified, scenario includes a standard rewarded ad button indicating they can claim double the amount of their level rewards by watching an ad.
As with the first scenario, this button is followed by a second button for players who do not want to watch an ad and want only their base amount of level rewards.
This scenario occurs any time the gamified scenario doesn't appear (i.e. the second, third, fifth, sixth, etc).

The third and final scenario only has a basic level end reward collection button, and no watch rewarded ads options.
In our case this scenario only occurs when no ads have been successfully loaded.
This can occur for various reasons, such as lack of internet connectivity, or the sample having been built on an unsupported platform, etc.

#### Claiming Base Rewards
When any of the buttons are clicked (either the rewarded ad options or the basic collect rewards option), a Cloud Code script is executed to distribute the base rewards via the Economy service.
This ensures that if the player stops the rewarded ad partially through (thereby losing their boosted rewards) that they still receive the standard amount of level end rewards.

#### Watching a Rewarded Ad and Receiving Rewards
After the base reward distribution call to Cloud Code finishes, if they have clicked one of the rewarded ad buttons, Mediation is asked to show the ad that was previously loaded.
As soon as the ad has successfully started to be shown, we tell Mediation to load the next ad.
This gives the ad time to load and be ready for the next time we want to offer that interaction.

Once the ad has fully completed, we call the Cloud Code script again, this time indicating a multiplier.
The Cloud Code script verifies that this multiplier is a legitimate value based on the current level count (from Cloud Save) and the appropriate multiplier for that level (i.e. 2, 3, and 5 are valid on turn 1, but only 2 is valid on turn 2).
Once verified, the script distributes the appropriate amount of rewards via the Economy service.
At this point, the Cloud Code script has executed twice and the player has received the base reward * the multiplier amount of currency rewards.

Depending on the rewarded ad placement settings (as setup on the Mediation Dashboard), the player may be able to skip the ad before its finished.
This is how the fake ad in the editor works as well.
In that case, our sample simply does not call the Cloud Code script the second time, so that no boosted rewards are distributed.

### Packages Required
- **Mediation:** Shows the Rewarded Ad.
- **Cloud Code:** Validates and distributes the level end and rewarded ad watched rewards.
- **Economy:** Maintains the balance for the currency being rewarded for level end.
- **Cloud Save:** Tracks information needed for validating appropriate level end reward distribution, including how many times a level has been completed, and the time the last time the rewards were distributed.

See the [Mediation](https://docs.unity.com/mediation/MediationSetupChecklist.html),
[Cloud Code](https://docs.unity.com//cloud-code/Content/implementation.htm?tocpath=Implementation%7C_____0#SDK_installation),
[Economy](https://docs.unity.com/economy/Content/implementation.htm?tocpath=Implementation%7C_____0),
and [Cloud Save](https://docs.unity.com/cloud-save/Content/index.htm#Implementation) 
docs to learn how to install and configure these SDKs in your project.

### Dashboard Setup
To use Mediation, Cloud Code, Economy, and Cloud Save services in your game, activate each service for your organization and project in the Unity Dashboard.

#### Unity Mediation
##### Ad Units
* ID: `RewardedAds_Android`
  * Platform: Android
  * Ad Format: Rewarded
  * Waterfall: `Android_Rewarded` (this is a default waterfall provided by Mediation, but it needs to be attached to this Ad Unit)
  * Custom Settings:
    * Allow Skip: After 5 seconds
* ID: `RewardedAds_iOS`
  * Platform: iOS
  * Ad Format: Rewarded
  * Waterfall: `iOS_Rewarded` (this is a default waterfall provided by Mediation, but it needs to be attached to this Ad Unit)
  * Custom Settings:
      * Allow Skip: After 5 seconds

#### Economy Items
* Gem - `ID: "GEM"` - The currency being rewarded for completing the level and watching a rewarded ad.

#### Cloud Code Scripts
* RewardedAds_GrantLevelEndRewards:
  * Parameters: `multiplier` - Optional.
      An int representing the amount the base rewards should be multiplied with, if any.
      Not included if just distributing base rewards.
  * Script: `Assets/Use Case Samples/Rewarded Ads With Unity Mediation/Cloud Code/RewardedAds_GrantLevelEndRewards.js`

_**Note**:
The Cloud Code scripts included in the `Cloud Code` folder are just local copies, since you can't see the sample's dashboard. Changes to these scripts will not affect the behavior of this sample since they will not be automatically uploaded to Cloud Code service._
