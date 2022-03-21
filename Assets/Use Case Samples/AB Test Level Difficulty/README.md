# A/B Test for Level Difficulty
An A/B Test is a useful mechanism for tweaking a single feature of game play or game design, and learning what variation of that feature is most engaging to your players.

In this sample, the feature we are testing is level difficulty, specifically how much XP is required to get to the next level.
When a player is signed in, the screen displays their level, as well as an XP meter that indicates their current XP and the total amount of XP required to level up (either 100, 80, 60, 50, or 30).
The amount of XP that a player needs to level up depends on which test group they were randomly placed into.

For diagnostic purposes, the player's test group is also displayed (A, B, C, D, or E).
While in a real game setting the test group would rarely be publicly displayed, it could be used internally to capture analytics details about how the different groups respond to the A/B test.

### Implementation Overview
When the scene first loads, Unity Services is initialized and the player is signed in to an anonymous player ID (either one that has already been created from a previous sign-in or a new one) using the Authentication service.
A `SceneOpened` Analytics event is also triggered at this time. 
Once Authentication has completed sign-in, the client code gets the player's current data from Cloud Save.
If it is a new user (i.e. not a cached anonymous player), the player's initial level (1) and xp (0) will be saved in Cloud Save instead of loading existing data.
The client code will also download any existing currency data from the Economy service.
Finally, Remote Config will be queried to fetch the appropriate A/B test group name and xp needed for leveling up for this player.
All of this updated data will get displayed in the scene.
At this point, the "Gain XP" button is enabled for interaction.

When a player clicks the "Gain XP" button, an `ActionButtonPressed` event is sent to Analytics, and a call is made to the Cloud Code endpoint "GainXPAndLevelIfReady".
This server-authoritative call fetches the player's info from Cloud Code and Remote Config, increases the XP by the amount specified by Remote Config (10 points), and tests whether the new player XP total equals the amount of XP needed for leveling up.
If it does not, the player's new XP is saved in Cloud Code and returned to the client code, which updates the values and shows text indicating how much XP was added.
If it does, Economy is called to distribute the level-up rewards (100 COIN), and Cloud Code is called to save the increased player level and new player XP (XP gets reset to 0 upon level up) before returning the info to the client code.

The client code then opens a dialog to indicate that the player has leveled up, what currency reward was granted, and updates the relevant data in the scene. Note: A cross reference dictionary located in the Remote Config and initialized at start-up is used to convert the rewarded currency ID (i.e. "COIN") to an Addressable address, which can be used to display the sprite (i.e. "Sprites/Currency/Coin"). This step allows players to experience different art based on their segmentation and provides a convenient way to retrieve additional data for a specific currency at runtime.

Please note that a simpler approach to display different art according to a player’s segmentation is to attach additional information to the Custom Data associated with Currencies in the Economy configuration data. However, for the purpose of this segmentation sample, the data was added to the Game Overrides themselves to demonstrate the flexibility permitted by the Remote Config service.

If a player instead clicks "Sign In As New Player", the current anonymous player ID is deleted, the cached values are reset to empty/default state, and then a new sign in is initiated with the Authentication service, following the same flow as when the scene first loads.
Once again, an `ActionButtonPressed` event is also triggered when the "Sign In As New Player" button is pressed. 
Each time this Sign In As New Player flow is followed, a new anonymous player ID is created, with the potential for being randomly placed into a different A/B test group.
Because the group determination is random, you may need to follow this flow a few times before you see yourself in a new group.

Finally, when a player presses the back button in the scene to return to the "Start Here" scene, a `SceneSessionLength` custom event is triggered, capturing the amount of time spent in this scene.

**Note**: This use case is created with the assumption that the game style won't trigger frequent XP increases.
While testing the use case, if "Gain XP" is clicked 15+ times in rapid succession, you will likely see either an "Unprocessable Entity" exception or a "CloudSaveException: Rate limit has been exceeded" exception.
Both of these indicate that the Cloud Save rate limit for number of calls has been exceeded (the rate limit includes both calls from Cloud Code and client code, read more about why that is [here](https://docs.unity.com//cloud-code/Content/types-of-scripts.htm?tocpath=Types%20of%20scripts%7C_____0#Available_libraries)).
To resolve either of these exceptions, wait a minute and try again, or sign out and back in with a new player ID (not recommended in a real game).
 
 
### Packages Required
- **Authentication:** Automatically signs in the user anonymously to keep track of their data on the server side.
- **Cloud Save:** Server authoritative way to save player data and game state. In this sample, it stores the player's level and XP.
- **Economy:** Keeps track of the player's currencies.
- **Remote Config:** Provides key-value pairs where the value that is mapped to a given key can be changed on the server-side, either manually or based on specific Game Overrides. In this sample, a single Game Override with a built-in A/B test is used to return different values for the amount of XP required to level up. Remote Config also stores data associated with Currency icon Addressable addresses.
- **Cloud Code:** Keeps important validation logic for increasing XP and leveling up the player on the server side.
- **Analytics:** Sends events that allows tracking of a player's in-game interactions, retention, and other information which can be used for analyzing and improving game experience.

See the [Authentication](https://docs.unity.com/authentication/Content/InstallAndConfigureSDK.htm),
[Cloud Save](https://docs.unity.com/cloud-save/Content/index.htm#Implementation),
[Economy](https://docs.unity.com/economy/Content/implementation.htm?tocpath=Implementation%7C_____0),
[Remote Config](https://docs.unity3d.com/Packages/com.unity.remote-config@2.0/manual/ConfiguringYourProject.html),
[Cloud Code](https://docs.unity.com//cloud-code/Content/implementation.htm?tocpath=Implementation%7C_____0#SDK_installation),
and [Analytics](https://docs.unity.com/analytics/SDKInstallation.htm) docs to learn how to install and configure these SDKs in your project.


### Dashboard Setup
To use Cloud Save, Economy, Remote Config, and Cloud Code services in your game, activate each service for your organization and project in the Unity Dashboard.
To duplicate this sample scene's setup on your own dashboard, you'll need a a currency in the Economy setup, some Config Values and a Game Override set up in Remote Config, and a script published in Cloud Code:

#### Economy Items
* Coin - `ID: "COIN"` - The currency distributed as a reward for the player leveling up.

#### Remote Config
##### Config Values
* A_B_TEST_GROUP - The identifier for which test user group the player is in.
  * Type: `string`
  * Value: `""`
* A_B_TEST_ID - The identifier for which AB Test is actively being run for this user.
  * Type: `string`
  * Value: `""`
* LEVEL_UP_XP_NEEDED - The amount of XP needed in order for the player to level up.
  * Type: `int`
  * Value: `100`
* XP_INCREASE - The amount the player's XP will increase by each time they gain XP.
  * Type: `int`
  * Value: `10`
* CURRENCIES - A cross reference from currencyId to spriteAddresses for all currency types
  * Type: `json`
  * Value:
  ```json
    {
        "currencyData": [{
            "currencyId": "COIN",
            "currencySpec": {
                "spriteAddress": "Sprites/Currency/Coin"
            }
        },{
            "currencyId": "GEM",
            "currencySpec": {
                "spriteAddress": "Sprites/Currency/Gem"
            }
        },{
            "currencyId": "PEARL",
            "currencySpec": {
                "spriteAddress": "Sprites/Currency/Pearl"
            }
        },{
            "currencyId": "STAR",
            "currencySpec": {
                "spriteAddress": "Sprites/Currency/Star"
            }
        }]
    }
    ```

##### Game Overrides
* Level Difficulty A/B Test
  * Status: Active
  * Audience: true, 100%
  * Start Date: Immediately
  * End Date: Indefinitely
  * Overrides:
    * Variant 1 (control group):
      * LEVEL_UP_XP_NEEDED: 100
      * A_B_TEST_GROUP: A
      * A_B_TEST_ID: LevelDifficultyTest1
    * Variant 2:
      * LEVEL_UP_XP_NEEDED: 80
      * A_B_TEST_GROUP: B
      * A_B_TEST_ID: LevelDifficultyTest1
    * Variant 3:
      * LEVEL_UP_XP_NEEDED: 60
      * A_B_TEST_GROUP: C
      * A_B_TEST_ID: LevelDifficultyTest1
    * Variant 4:
      * LEVEL_UP_XP_NEEDED: 50
      * A_B_TEST_GROUP: D
      * A_B_TEST_ID: LevelDifficultyTest1
    * Variant 5:
      * LEVEL_UP_XP_NEEDED: 30
      * A_B_TEST_GROUP: E
      * A_B_TEST_ID: LevelDifficultyTest1

#### Cloud Code Scripts
* ABTest_GainXPAndLevelIfReady:
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/AB Test Level Difficulty/Cloud Code/ABTest_GainXPAndLevelIfReady.js`

_**Note**:
The Cloud Code scripts included in the `Cloud Code` folder are just local copies, since you can't see the sample's dashboard. Changes to these scripts will not affect the behavior of this sample since they will not be automatically uploaded to Cloud Code service._

#### Analytics
In the configuration of the Analytics custom events and parameters, you can see a fairly long list of potential parameters that are sent with some of the events.
This extended list allows for a more flexible analysis of different parameter groupings in the Data Explorer on the Analytics tab of the Unity dashboard.
Alternatively, you could send just the ungrouped parameters (buttonName, sceneName, etc), and do any kind of grouped analysis desired using the Data Export feature within the Data Explorer on the dashboard.

_**Note**:
This sample demonstrates the code needed to trigger analytics events, however additional code may be necessary to meet legal requirements such as GDPR, CCPA, and PIPL.
See more info about managing data privacy [here](https://docs.unity.com/analytics/ManagingDataPrivacy.html)._

##### Custom Events
* `SceneOpened`
  * Description: Event sent each time the scene is loaded.
  * Enabled: true
  * Custom Parameters:
    * `sceneName`
* `ActionButtonPressed`
  * Description: Event sent for each button press in the scene.
  * Enabled: true
  * Custom Parameters:
    * `buttonName`
    * `sceneName`
    * `abGroup`
    * `buttonNameBySceneName`
    * `buttonNameByABGroup`
    * `buttonNameBySceneNameAndABGroup`
* `SceneSessionLength`
  * Description: Event sent to indicate the length of time between when `Start()` is triggered on the AnalyticsManager script and the back button in the scene is pressed (effectively the time spent in the scene).
  * Enabled: true
  * Custom Parameters:
    * `timeRange`
    * `sceneName`
    * `abGroup`
    * `timeRangeBySceneName`
    * `timeRangeByABGroup`
    * `timeRangeBySceneNameAndABGroup`

##### Custom Parameters
* `sceneName`
  * Description: The name of the scene where the event was triggered.
  * Type: `STRING`
* `buttonName`
  * Description: The name of the button that has been pressed.
  * Type: `STRING`
* `abGroup`
  * Description: The AB group and AB Test ID the user sending the analytics event has been grouped into. Formatted: "AB Group Name (AB Test ID)".
  * Type: `STRING`
* `timeRange`
  * Description: A range of time spent in the scene where the event was triggered.
  * Type: `STRING`
* `buttonNameBySceneName`
  * Description: Formatted string grouping button name with scene name. Formatted like "Button Name - Scene Name".
  * Type: `STRING`
* `buttonNameByABGroup`
  * Description: Formatted string grouping button name with the A/B Group the user is in as determined by Remote Config. Formatted like "Button Name - AB Group (AB Test ID)".
  * Type: `STRING`
* `buttonNameBySceneNameAndABGroup`
  * Description: Formatted string grouping button name with the scene name and A/B Group the user is in as determined by Remote Config. Formatted like "Button Name - Scene Name - AB Group (AB Test ID)".
  * Type: `STRING`
* `timeRangeBySceneName`
  * Description: Formatted string grouping time range with the name of the scene where the time was spent. Formatted like "Time Range - Scene Name".
  * Type: `STRING`
* `timeRangeByABGroup`
  * Description: Formatted string grouping time range with the the A/B Group the user is in as determined by Remote Config. Formatted like "Time Range - AB Group (AB Test ID)".
  * Type: `STRING`
* `timeRangeBySceneNameAndABGroup`
  * Description: Formatted string grouping time range with the the scene name and the A/B Group the user is in as determined by Remote Config. Formatted like "Time Range - Scene Name - AB Group (AB Test ID)".
  * Type: `STRING`
  