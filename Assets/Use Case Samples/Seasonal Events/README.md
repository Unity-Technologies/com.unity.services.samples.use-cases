# Seasonal Events

Seasonal events can increase game sessions and overall interest in a game, as they give existing players new and fun content throughout the year to look forward to and can entice new players to begin playing.

This sample shows how you can set up seasonal events (fall, winter, spring, summer) for players in your game with a countdown indicating how much time is left in the current event, the currency rewards that can be won during the event, and a "Play Challenge" button that, when clicked, opens a popup where players can collect their rewards for "winning" the challenge.

Clicking the "Collect Rewards" button in the popup will add the rewards to a player’s wallet balance, as seen in the currency HUD at the top of the scene.
Once the countdown on the main screen hits 0, the scene will automatically change to the next event.

### Implementation Overview
When this scene first loads, it will initialize Unity Services and sign the player in anonymously using Authentication. This can be seen in the SeasonalEventsSceneManager script.
Once Unity Services completes initialization, Remote Config is queried to get the current values for the event-related keys. These values allow for displaying the active event name, the potential rewards for completing the event challenge, and the themed background image and play buttons.

Remote Config also tells us when the event ends, which is used in the CountdownManager for determining and displaying how much time is left in the current event.
When that time runs out, it triggers a new call to remote config, to get the updated values for the next event.

Note: This sample determines which Remote Config campaign data should be returned based on the user’s timestamp, to demonstrate how events can change and update local variables.
This is a simplification.
In a real app, developers likely set up a campaign to have specific start and end dates, then Remote Config determines when the campaign is shown based on the server’s date/time.

When a player clicks "Play Challenge", followed by "Collect Rewards" it initiates a call to the Cloud Code script "Grant Event Reward".
This script calls Remote Config to determine which rewards should be distributed (this has the potential to differ from what the player expects, if they're altering their device clock or if they clicked claim right at the very end of an event), and then calls Economy to add those rewards to their currency balances.
The challenge can only be played once per active season, so once the Cloud Code script distributes the rewards, it saves the current season name and timestamp to Cloud Save.
This info is used by the client to determine whether the current season's challenge has already been played, and if it has, to disable the "Play Challenge" button.

Additionally, Analytics custom events are sent each time the scene loads (`SceneOpened`), whenever a button is pressed (`ActionButtonPressed`), and when the back button in the scene is pressed, returning the player to the "Start Here" scene (`SceneSessionLength`).

### Packages Required
- **Authentication:** Automatically signs in the user anonymously to keep track of their data on the server side.
- **Economy:** Keeps track of the player's currencies.
- **Cloud Code:** Keeps important validation logic on the server side. In this sample it is used to distribute the rewards for the event challenge when the player clicks the "Collect Rewards" button. It independently verifies the timestamp at the time of reward distribution on the server-side to confirm which event's rewards should be distributed.
- **Remote Config:** Provides key-value pairs where the value that is mapped to a given key can be changed on the server-side, either manually or based on specific campaigns. In this sample, we use the campaigns feature to create the four seasonal events and return different values for certain keys based on the campaign. 
- **Addressables:** Allows developers to ask for an asset via its address. Wherever the asset resides (local or remote), the system will locate it and its dependencies, then return it. Here we use it to look up event specific images and prefabs based on the information we receive from Remote Config.
- **Analytics:** Sends events that allow the tracking of a player's in-game interactions, retention, and other information which can be used for analyzing and improving game experience.
- **Cloud Save:** Stores if the current season's challenge has already been played to prevent the user from playing the same season's challenge multiple times.

See the [Authentication](https://docs.unity.com/authentication/Content/InstallAndConfigureSDK.htm),
[Economy](https://docs.unity.com/economy/Content/implementation.htm?tocpath=Implementation%7C_____0),
[Cloud Code](https://docs.unity.com//cloud-code/Content/implementation.htm?tocpath=Implementation%7C_____0#SDK_installation),
[Remote Config](https://docs.unity3d.com/Packages/com.unity.remote-config@2.0/manual/ConfiguringYourProject.html),
[Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest),
[Analytics](https://docs.unity.com/analytics/SDKInstallation.htm),
and [Cloud Save](https://docs.unity.com/cloud-save/implementation.htm) docs to learn how to install and configure these SDKs in your project.

### Dashboard Setup
To use Economy, Remote Config, and Cloud Code services in your game, activate each service for your organization and project in the Unity Dashboard.
To duplicate this sample scene's setup on your own dashboard, you'll need a few currencies in the Economy setup, some Config Values and Campaigns set up in Remote Config, and a script published in Cloud Code:

#### Economy Items
* Coin - `ID: "COIN"` - A challenge reward during the fall, winter, and spring events
* Gem - `ID: "GEM"` - A challenge reward during the winter and summer events
* Pearl - `ID: "PEARL"` - A challenge reward during the fall and summer events
* Star - `ID:"STAR"` - A challenge reward during the spring and summer events

#### Remote Config
##### Config Values
* EVENT_NAME - The name of the event to display in the scene.
  * Type: `string`
  * Default value: `""`
* EVENT_KEY - The key used to look up event-specific values, such as the addresses for the specific images.
  * Type: `string`
  * Default value: `""`
* EVENT_END_TIME - The last digit that matches in the Audience JEXL statement, i.e. the last digit of the latest timestamp that would return this campaign.
  * Type: `int`
  * Default value: `0`
* CHALLENGE_REWARD - The json that specifies what rewards are distributed when a challenge has been "won".
  * Type: `json`
  * Default value:
  ```json
    {
        "rewards": [{
            "id": "COIN",
            "quantity": 100,
            "sprite_address": "Sprites/Currency/Coin"
        }]
    }
    ```

##### Campaigns
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
    * CHALLENGE_REWARD:
      ```json
        {
            "rewards": [{
                "id": "COIN",
                "quantity": 100,
                "spriteAddress": "Sprites/Currency/Coin"
            }, {
                "id": "PEARL",
                "quantity": 50,
                "spriteAddress": "Sprites/Currency/Pearl"
            }]
        }
      ```
* Winter Event
  * Status: Active
  * Audience: Stateless JEXL
    * `user.timestampMinutes % 10 == 3 || user.timestampMinutes % 10 == 4`
  * Start Date: Immediately
  * End Date: Indefinitely
    * EVENT_NAME: `Winter Event`
    * EVENT_KEY: `Winter`
    * EVENT_END_TIME: `4`
    * CHALLENGE_REWARD:
      ```json
        {
            "rewards": [{
                "id": "COIN",
                "quantity": 100,
                "spriteAddress": "Sprites/Currency/Coin"
            }, {
                "id": "GEM",
                "quantity": 50,
                "spriteAddress": "Sprites/Currency/Gem"
            }]
        }
      ```
* Spring Event
  * Status: Active
  * Audience: Stateless JEXL
    * `user.timestampMinutes % 10 == 5 || user.timestampMinutes % 10 == 6 || user.timestampMinutes % 10 == 7`
  * Start Date: Immediately
  * End Date: Indefinitely
    * EVENT_NAME: `Spring Event`
    * EVENT_KEY: `Spring`
    * EVENT_END_TIME: `7`
    * CHALLENGE_REWARD:
      ```json
        {
            "rewards": [{
                "id": "COIN",
                "quantity": 100,
                "spriteAddress": "Sprites/Currency/Coin"
            }, {
                "id": "STAR",
                "quantity": 50,
                "spriteAddress": "Sprites/Currency/Star"
            }]
        }
      ```
* Summer Event
  * Status: Active
  * Audience: Stateless JEXL
    * `user.timestampMinutes % 10 == 8 || user.timestampMinutes % 10 == 9`
  * Start Date: Immediately
  * End Date: Indefinitely
    * EVENT_NAME: `Summer Event`
    * EVENT_KEY: `Summer`
    * EVENT_END_TIME: `9`
    * CHALLENGE_REWARD:
      ```json
        {
            "rewards": [{
                "id": "STAR",
                "quantity": 50,
                "spriteAddress": "Sprites/Currency/Star"
            }, {
                "id": "PEARL",
                "quantity": 50,
                "spriteAddress": "Sprites/Currency/Pearl"
            }, {
                "id": "GEM",
                "quantity": 50,
                "spriteAddress": "Sprites/Currency/Gem"
            }]
        }
      ```

#### Cloud Code Scripts
* GrantEventReward:
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Seasonal Events/Cloud Code/GrantEventReward.js`

_**Note**:
The Cloud Code scripts included in the `Cloud Code` folder are just local copies, since you can't see the sample's dashboard. Changes to these scripts will not affect the behavior of this sample since they will not be automatically uploaded to Cloud Code service._

#### Analytics
In the configuration of the Analytics custom events and parameters, you can see a fairly long list of potential parameters that are sent with some of the events.
This extended list allows for a more flexible analysis of different parameter groupings in the Data Explorer on the Analytics tab of the Unity dashboard.
Alternatively, one could send just the ungrouped parameters (buttonName, sceneName, etc), and do any kind of grouped analysis desired using the Data Export feature within the Data Explorer on the dashboard.

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
    * `remoteConfigActiveEvent`
    * `buttonNameBySceneName`
    * `buttonNameByRemoteConfigEvent`
    * `buttonNameBySceneNameAndRemoteConfigEvent`
* `SceneSessionLength`
  * Description: Event sent to indicate the length of time between when `Start()` is triggered on the AnalyticsManager script and the back button in the scene is pressed (effectively the time spent in the scene).
  * Enabled: true
  * Custom Parameters:
    * `timeRange`
    * `sceneName`
    * `remoteConfigActiveEvent`
    * `timeRangeBySceneName`
    * `timeRangeByRemoteConfigEvent`
    * `timeRangeBySceneNameAndABGroup`

##### Custom Parameters
* `sceneName`
  * Description: The name of the scene where the event was triggered.
  * Type: `STRING`
* `buttonName`
  * Description: The name of the button that has been pressed.
  * Type: `STRING`
* `remoteConfigActiveEvent`
  * Description: The active event as defined in and determined by Remote Config.
  * Type: `STRING`
* `timeRange`
  * Description: A range of time spent in the scene where the event was triggered.
  * Type: `STRING`
* `buttonNameBySceneName`
  * Description: Formatted string grouping button name with scene name. Formatted like "Button Name - Scene Name".
  * Type: `STRING`
* `buttonNameByRemoteConfigEvent`
  * Description: Formatted string grouping button name with Remote Config active event. Formatted like "Button Name - Event Key".
  * Type: `STRING`
* `buttonNameBySceneNameAndRemoteConfigEvent`
  * Description: Formatted string grouping button name with scene name and Remote Config active event. Formatted like "Button Name - Scene Name - Event Key".
  * Type: `STRING`
* `timeRangeBySceneName`
  * Description: Formatted string grouping time range with the name of the scene where the time was spent. Formatted like "Time Range - Scene Name".
  * Type: `STRING`
* `timeRangeByRemoteConfigEvent`
  * Description: Formatted string grouping time range with the active Remote Config Event at the time the event was sent. Formatted like "Time Range - Event Key".
  * Type: `STRING`
* `timeRangeBySceneNameAndRemoteConfigEvent`
  * Description: Formatted string grouping time range with the scene name and the Remote Config event that was active at the time the analytics event was sent. Formatted like "Time Range - Scene Name - Event Key".
  * Type: `STRING`
  