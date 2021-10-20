# A/B Test for Level Difficulty
An A/B Test is a useful mechanism for tweaking a single feature of game play or game design, and learning what variation of that feature is most engaging to your players.

In this sample, the feature we are testing is level difficulty, specifically how much XP is required to get to the next level.
When a player is signed in, the screen displays their level, as well as an XP meter that indicates their current XP and the total amount of XP required to level up (either 100, 80, 60, 50, or 30).
The amount of XP that a player needs to level up depends on which test group they were randomly placed into.

For diagnostic purposes, the player's test group is also displayed (A, B, C, D, or E).
While in a real game setting the test group would rarely be publicly displayed, it could be used internally to capture analytics details about how the different groups respond to the A/B test.

### Implementation Overview
When the scene first loads, Unity Services is initialized and the player is signed in to an anonymous player ID (either one that has already been created from a previous sign-in or a new one) using the Authentication service.
Once Authentication has completed sign-in, the client code gets the player's current data from Cloud Save.
If it is a new user (i.e. not a cached anonymous player), the player's initial level (1) and xp (0) will be saved in Cloud Save instead of loading existing data.
The client code will also download any existing currency data from the Economy service.
Finally, Remote Config will be queried to fetch the appropriate A/B test group name and xp needed for leveling up for this player.
All of this updated data will get displayed in the scene.
At this point, the "Gain XP" button is enabled for interaction.

When a player clicks the "Gain XP" button, a call is sent to the Cloud Code endpoint "GainXPAndLevelIfReady".
This server-authoritative call fetches the player's info from Cloud Code and Remote Config, increases the XP by the amount specified by Remote Config (10 points), and tests whether the new player XP total equals the amount of XP needed for leveling up.
If it does not, the player's new XP is saved in Cloud Code and returned to the client code, which updates the values and shows text indicating how much XP was added.
If it does, Economy is called to distribute the level-up rewards (100 COIN), and Cloud Code is called to save the increased player level and new player XP (XP gets reset to 0 upon level up) before returning the info to the client code.
The client code then opens a leveled up popup and updates the relevant data in the scene.

If a player instead clicks "Sign In As New Player", the current anonymous player ID is deleted, the cached values are reset to empty/default state, and then a new sign in is initiated with the Authentication service, following the same flow as when the scene first loads.
Each time this Sign In As New Player flow is followed, a new anonymous player ID is created, with the potential for being randomly placed into a different A/B test group.
Because the group determination is random, you may need to follow this flow a few times before you see yourself in a new group.

**Note**: This use case is created with the assumption that the game style won't trigger frequent XP increases.
While testing the use case, if "Gain XP" is clicked 15+ times in rapid succession, you will likely see either an "Unprocessable Entity" exception or a "CloudSaveException: Rate limit has been exceeded" exception.
Both of these indicate that the Cloud Save rate limit for number of calls has been exceeded (the rate limit includes both calls from Cloud Code and client code, read more about why that is [here](https://docs.unity.com//cloud-code/Content/types-of-scripts.htm?tocpath=Types%20of%20scripts%7C_____0#Available_libraries)).
To resolve either of these exceptions, wait a minute and try again, or sign out and back in with a new player ID (not recommended in a real game).
 
 
### Packages Required
- **Authentication:** Automatically signs in the user anonymously to keep track of their data on the server side.
- **Cloud Save:** Server authoritative way to save player data and game state. In this sample, it stores the player's level and XP.
- **Economy:** Keeps track of the player's currencies.
- **Remote Config:** Provides key-value pairs where the value that is mapped to a given key can be changed on the server-side, either manually or based on specific campaigns. In this sample, a single campaign with a built-in A/B test is used to return different values for the amount of XP required to level up.
- **Cloud Code:** Keeps important validation logic for increasing XP and leveling up the player on the server side.

See the [Authentication](https://docs.unity.com/authentication/Content/InstallAndConfigureSDK.htm),
[Cloud Save](https://docs.unity.com/cloud-save/Content/index.htm#Implementation)
[Economy](https://docs.unity.com/economy/Content/implementation.htm?tocpath=Implementation%7C_____0),
[Remote Config](https://docs.unity3d.com/Packages/com.unity.remote-config@2.0/manual/ConfiguringYourProject.html),
and [Cloud Code](https://docs.unity.com//cloud-code/Content/implementation.htm?tocpath=Implementation%7C_____0#SDK_installation) docs to learn how to install and configure these SDKs in your project.


### Dashboard Setup
To use Cloud Save, Economy, Remote Config, and Cloud Code services in your game, activate each service for your organization and project in the Unity Dashboard.
To duplicate this sample scene's setup on your own dashboard, you'll need a a currency in the Economy setup, some Config Values and a Campaign set up in Remote Config, and a script published in Cloud Code:

#### Economy Items
* Coin - `ID: "COIN"` - The currency distributed as a reward for the player leveling up.

#### Remote Config
##### Config Values
* A_B_TEST_GROUP - The identifier for which test user group the player is in.
  * Type: string
  * Default value: ""
* LEVEL_UP_XP_NEEDED - The amount of XP needed in order for the player to level up.
  * Type: int
  * Default value: 100
* XP_INCREASE
  * Type: int
  * Default value: 10
  * Campaign Override value examples: No campaign overrides for this value

##### Campaigns
* Level Difficulty A/B Test
  * Status: Active
  * Audience: true, 100%
  * Start Date: Immediately
  * End Date: Indefinitely
  * Overrides:
    * Variant 1 (control group):
      * LEVEL_UP_XP_NEEDED: 100
      * A_B_TEST_GROUP: A
    * Variant 2:
      * LEVEL_UP_XP_NEEDED: 80
      * A_B_TEST_GROUP: B
    * Variant 3:
      * LEVEL_UP_XP_NEEDED: 60
      * A_B_TEST_GROUP: C
    * Variant 2:
      * LEVEL_UP_XP_NEEDED: 50
      * A_B_TEST_GROUP: D
    * Variant 2:
      * LEVEL_UP_XP_NEEDED: 30
      * A_B_TEST_GROUP: E

#### Cloud Code Scripts
* GainXPAndLevelIfReady:
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/AB Test Level Difficulty/Cloud Code/GainXPAndLevelIfReady.js`

_**Note**:
The Cloud Code scripts included in the `Cloud Code` folder are just local copies, since you can't see the sample's dashboard. Changes to these scripts will not affect the behavior of this sample since they will not be automatically uploaded to Cloud Code service._
