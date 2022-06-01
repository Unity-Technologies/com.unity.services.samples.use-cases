# Idle Clicker game

In real-time idle clicker and social games, such as farming or city-building games, common considerations include:
- How to simulate real-time activities in a game that is not running all the time.
- How the simulation can occur on the cloud, to ensure that all players' games are updated properly, regardless of timezone or any modifications to the date and time on a player's device.

This sample use case shows how to solve both challenges while limiting calls to Unity Gaming Services, which can cause throttling issues or increased costs.

In this sample, the player uses a resource (Water) to purchase Wells, which each produce 1 unit of Water per second.

![Idle Clicker scene](Documentation~/Idle_Clicker_scene.png)


## Overview

To see this use case in action:
1. In the Unity Editor **Project** window, select **Assets** > **Use Case Samples** > **Idle Clicker Game**, and then double-click `IdleClickerGameSample.unity` to open the sample scene.
2. Enter Play Mode to interact with the use case.


### Initialization

The `IdleClickerGameSceneManager.cs` script performs the following initialization tasks in its `Start` function:
1. Initializes Unity Gaming Services.
2. Signs in the player [anonymously](https://docs.unity.com/authentication/UsingAnonSignIn.html) using the Authentication service. If you’ve previously initialized any of the other sample scenes, Authentication will use your cached Player ID instead of creating a new one.
3. Retrieves and updates the player's currency balances from the Economy service.
4. Retrieves or creates the current game state.
<br>**Note:** If the game is already in progress, this step grants Water based on the quantity produced by all Wells since the last time the game state was updated.
5. Enables button-click functionality for the available tiles in the playfield.


### Functionality


#### Placing Wells

You can click any open tile in the playfield to place a Well in exchange for 500 Water. Cloud Code validates whether you’re attempting to place a Well in an empty tile. When you click a tile, the following occurs:
1. The `IdleClicker_GetUpdatedState.js` Cloud Code script calculates the amount of Water generated since the last update, then calls the Economy service to grant that amount to the player’s currency balance (see section on real-time resource updates, below).
2. The `IdleClicker_PlaceWell.js` Cloud Code script checks that you meet the criteria to purchase the Well (the selected tile isn’t occupied, and you have sufficient Water to make the purchase).
3. If you meet the criteria to place a Well, the same script calls the Economy service to conduct a virtual purchase, deducting 500 Water from the player’s currency balance (see section on virtual purchases, below).


#### Real-time resource updates

Between moves, while the player is viewing the game scene but not interacting, the client simulates Water production and updates the Currency HUD accordingly. Each Well produces one Water per second. Because this sample game is intended to be real-time, whenever the client calls Cloud Code, it checks the current time and calculates how much cumulative Water has been produced by each Well since its last production cycle. Every time the game loads, or the player attempts to place a Well, the following occurs on the backend:
1. To determine how much time has passed, Cloud Save stores timestamps for the last time each Well produced Water, along with the full game state that includes obstacle locations, Well locations, and the last update time.
2. After the Economy service grants the appropriate quantity of Water, each Well’s timestamp is updated, and the fully-updated state is saved back to Cloud Save.

**Note**: Because the Water quantity displayed in the currency HUD is simulated by the client and not actually in sync with the server until the scene is reloaded or the player attempts to place a Well, its server-side values will often appear inaccurate.


#### Virtual purchases


This sample illustrates how virtual purchases occur through the Economy service. In this case, the virtual purchase for a Well costs 500 Water, but, unlike most virtual purchases, the purchase itself does not actually grant anything. Instead, the virtual purchase consumes the transaction cost (500 Water) and updates the game state with a new Well in the corresponding tile. Cloud Save stores the full game state, including the locations of Wells and obstacles.

**Note**: The service only attempts the transaction after confirming that the space is empty.


## Setup


### Requirements

To replicate this use case, you need the following [Unity packages](https://docs.unity3d.com/Manual/Packages.html) in your project:

| **Package**                                                                                | **Role**                                                                                                                                                                                                                                         |
| ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| [Authentication](https://docs.unity.com/authentication/Content/InstallAndConfigureSDK.htm) | Automatically signs in the user anonymously to keep track of their data on the server side.                                                                                                                                                      |
| [Cloud Code](https://docs.unity.com//cloud-code/Content/implementation.htm)                | Sets up the game state for a new game by placing three random obstacles and setting the starting Water currency to 2000. It also validates moves, grants Water based on real-time production, and updates game state based on virtual purchases. |
| [Cloud Save](https://docs.unity.com/cloud-save/implementation.htm)                         | Stores the updated game state for obstacles and Wells (including last production times). Cloud Code checks and updates these values directly.                                                                                                    |
| [Economy](https://docs.unity.com/economy/Content/implementation.htm)                       | Retrieves the player's starting and updated Water balances at runtime.                                                                                                                                                                           |

To use these services in your game, activate each service for your Organization and project in the [Unity Dashboard](https://dashboard.unity3d.com/).


### Dashboard Setup

To replicate this sample scene's setup on your own dashboard, you need to:
- Publish two scripts in Cloud Code.
- Create one Currency and one Virtual Transaction for the Economy service.


#### Cloud Code

[Publish the following scripts](https://docs.unity.com/cloud-code/implementation.html#Writing_your_first_script) in the **LiveOps** dashboard:

| **Script**                    | **Parameters**                                                     | **Description**                                                                                                                                                                                                                         | **Location**                                                                          |
|-------------------------------| ------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |---------------------------------------------------------------------------------------|
| `IdleClicker_GetUpdatedState` | None                                                               | Creates a random game if necessary, updates the game state since the last call (including granting any necessary Water), and returns the state to Cloud Save.                                                                           | `Assets/Use Case Samples/Idle Clicker Game/Cloud Code/IdleClicker_GetUpdatedState.js` |
| `IdleClicker_PlaceWell`       | `coord`<br><br>`JSON`<br><br>The {x, y} coordinates for the new Well to add to the playfield.<br><br>Example: `{"x":0, "y":0}` | Updates currency balances since the last server call, validates the player's moves, uses Economy service Virtual Purchases to “buy” new Wells, and updates the game state appropriately (for example, adding a Well to the game board). | `Assets/Use Case Samples/Idle Clicker Game/Cloud Code/IdleClicker_PlaceWell.js`       |

**Note**: The Cloud Code scripts included in the `Cloud Code` folder are local copies because you cannot see the sample's dashboard. Changes to these scripts do not affect the behavior of this sample because they are not automatically uploaded to the Cloud Code service.


#### Economy

[Configure the following resources](https://docs.unity.com/economy/) in the **LiveOps** dashboard:

| **Resource type** | **Resource name**      | **ID**                    | **Description**                                                                                                                                                                                                 |
| ----------------- | ---------------------- |---------------------------| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Currency          | Water                  | `WATER`                   | Granted at the start of a new game, consumed in virtual purchases to place new Wells, and granted every second for each Well on the playfield.                                                                  |
| Virtual Purchase  | Idle Clicker Game Well | `IDLE_CLICKER_GAME_WELL`  | Virtual Purchases to consume 500 Water in order to place a Well. Note that the Well itself is not granted by the virtual purchase; Cloud Code adds it to the game state directly upon a successful transaction. |
