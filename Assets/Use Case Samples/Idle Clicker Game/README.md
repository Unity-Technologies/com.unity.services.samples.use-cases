# Idle Clicker game

In real-time idle clicker and social games, such as farming or city-building games, common considerations include:
- How to simulate real-time activities in a game that is not running all the time.
- How the simulation can occur on the cloud, to ensure that all players' games are updated properly, regardless of timezone or any modifications to the date and time on a player's device.
- How to merge pieces to form more powerful pieces by using Cloud Code logic and Economy Virtual Purchases to verify and consume appropriate quantities of resources (Water currency).
- How to implement an Unlock Manager to permit new game play options as the player achieves in-game goals.

This sample use case shows how to solve the above challenges while limiting calls to Unity Gaming Services, which can cause throttling issues or increased costs.

In this sample, the player uses a resource (Water) to purchase Wells, which initially produce 1 unit of Water per second. These 'Wood' Wells can be merged to form improved Wells which produce more Water. Once unlocked, the player can even merge identical 'improved' Wells together to create even better Wells that produce even more Water.

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
4. Retrieves or creates the game state by calling the `IdleClicker_GetUpdatedState.js` Cloud Code script. If this is a new game, this script will generate a random game playfield and reset the player's Water, otherwise it calculates the amount of Water generated since the last update and calls the Economy service to grant that amount to the player’s currency balance. It also retrieves the state of the Unlock Manager.
5. Enables button-click and drag functionality for the available tiles in the playfield.


### Functionality

#### Placing Wells

You can click any open tile in the playfield to place a Well in exchange for 100 Water. The client and Cloud Code both validate whether the purchase is valid (the space is empty and the player has enough Water), then a Well is placed. When you click a tile, the following occurs:
1. The client validates that the selected location is empty and that the player has enough Water to purchase the Well. If either test fails, a popup occurs and the client does not call Cloud Code.
2. Once the client validates the request, the Cloud Code script `IdleClicker_PlaceWell.js` is called to perform the following steps:
    - First it grants all Water generated since the last Cloud Code call using the Economy service. Note that, until this occurs, the Water total the player is seeing in the Unity Client is just a simulation of how much Water he or she should have. Once Cloud Code determines the actual amount and grants it, the Client will be updated with this new total and will then continue simulating Water from that point until Cloud Code is called again.
    - Cloud Save is updated with the latest timestamp so future calls will only grant Water generated since this latest update.
    - It then checks that the selected tile isn’t occupied (or throws an exception back to the client).
    - If the placement is valid, it attempts the Virtual Purchase to consume 100 Water. If this purchase fails (for example not enough Water), an exception will be thrown back to the client).
    - If above Virtual Purchase is successful, a new Well is added to the Cloud Save state data. The Well data includes its location (x,y) and creation timestamp.
    - All Cloud Save data is re-saved with the new Well included. Note that we save twice on each call to ensure that the timestamp matches the Water granted even if placement fails. The first save (above) ensures Water granted is accurate even if a throw occurs after Water is granted, but before Well has been successfully placed (such as player not having enough Water). This save adds the new Well to game state in Cloud Save.
    - The final updated game state is returned to the client which now includes the newly-placed Well.

#### Moving Wells

You can drag any Well to any other open tile in the playfield to change a Well's position as a free action. The client, then Cloud Code both validate that you are moving the Well to an empty tile. When you drag a Well, the following occurs:
1. The client first validates the move. If the destination is blocked, a popup appears and Cloud Code is not called.
2. If the move is valid, the Cloud Code script `IdleClicker_MoveWell.js` is called to perform the following actions:
    - Grant all Water that was generated since the last Cloud Code call.
    - Update Cloud Save with the latest timestamp.
    - Verify the move in Cloud Code. This ensures that the client's request is valid before permitting the move.
    - Update the Well's location in memory to prepare it to be stored in Cloud Save.
    - All Cloud Save data is re-saved with the Well in the new location. For explanation of saving twice, please see 'Placing Wells' above.
    - Return the updated game state to the client so it can update the UI.
3. The final updated game state is displayed on the client with the Well in the new location.

#### Merging Wells

You can drag any Well onto another Well of the same type to attempt to merge them into a single, improved Well that produces more Water. Both the client and Cloud Code will validate that the merge is valid. Any of the following issues would make a merge request invalid and result in a popup message:
1. The Wells are different types, such as a Wood Well and a Bronze Well.
2. The upgraded Well has not been unlocked. Bronze Wells begin unlocked. To unlock Silver Wells, the player must successfully merge Wood Wells 4 times (merging 8 Wood Wells to create 4 Bronze Wells). An indicator on the right side of the screen shows which Wells are unlocked, and overall progress toward unlocking each upgraded Well.
3. The player has insufficient Water. Each improvement requires 100 more Water than the last so making a Bronze Well requires 200 Water, making Silver Wells requires 300 Water, and Gold Wells require 400.
4. The best Well improvement has been made. Gold Wells are the best possible so they cannot be merged.

If the merge is valid, the client will call Cloud Code script `IdleClicker_MergeWells` to perform the following:
1. First it grants all Water generated since the last Cloud Code call using the Economy service.
2. Cloud Save is updated with the latest timestamp.
3. It then removes the Wells from both the starting and ending locations. This is done on the internal game state and, if anything prevents merging the Wells (including either of the Wells being missing), Cloud Code will throw an exception back to the client and this temporary state will be lost thus leaving the game state unchanged. Only if this and all future tests succeed will this game state become official, and be updated on Cloud Save and returned to the client.
4. The Wells are checked to ensure they are the same type. If not, an exception is thrown.
5. The Wells are checked to confirm they are eligible to upgrade (i.e. not already a Gold Well). If they are not upgradeable an exception is thrown.
6. The Unlock Manager is checked to confirm that the upgraded Well type has been unlocked. If less than 4 Wells of the previous type have been merged, an exception is thrown.
7. The Economy Virtual Purchase is attempted to consume the correct quantity of Water. If the purchase fails, an exception will be thrown back to the client.
8. With all checks complete and Water deducted (previous step), the newly upgraded Well is added to the internal state so it can be written to Cloud Save and returned to the client.
9. The unlock counters are updated to reflect that another Well has been successfully merged. This is the value that will be shown in the client and is used to permit merging better Wells as the player progresses in the game.
10. The updated state, including the existence of the new Well, removal of the old Wells, and update to the unlock counter, is saved to Cloud Save. Note that until this step, any failures would just discard the updated local state so that the saved state in Cloud Save would remain unchanged.
11. The updated state with old Wells removed and the newly-created, upgraded Well is returned to the client. This state also includes the latest unlock counts so it's possible that the player will now be able to merge a new type of Well.
12. Client updates state to show the new Well and Unlock Manager state.

#### Resetting Game State

Whenever you need to start over, the [Reset Game] button can be pressed to perform the following:
1. Call Cloud Code script `IdleClicker_Reset` which clears the Cloud Save data so the state resembles that of a new player.
2. Call Cloud Code script `IdleClicker_GetUpdatedState`. Since the Cloud Save data is missing, it treats the request as a new player and creates a random playfield, resets the player's Water currency to 1,000 and sets the starting values for the Unlock Manager.

#### Real-time resource updates

Between moves, while the player is viewing the game scene but not interacting, the client simulates Water production and updates the Currency HUD accordingly. Each Well produces one Water per second per level of the Well (Wood Wells produce 1/sec, Bronze Wells produce 2/sec, etc.). Because this sample is intended to be real-time, whenever the client calls Cloud Code, it checks the current time and calculates how much cumulative Water has been produced by each Well since its last production cycle. Every time the Use Case is opened or the player attempts to place/move/merge a Well, the following occurs on the backend:
1. The current timestamp is determined using Date.now().
2. Cloud Save is read to determine the last update timestamp.
3. Each Well is processed to determine how much Water should have been produced since the last time it was updated. This is determined by comparing how much Water should have been produced since it was created (using the current timestamp and the Well's Cloud Save data which records the Well's creation time) and deducting the amount of Water the Well has already produced (using the last update timestamp and the Well's creation time). By simply subtracting these numbers, we can determine how much Water the Well will produce now (i.e. how much Water each Well produced since the last time the game state was updated).
4. The Economy Service is called to grant the appropriate total quantity of Water.
5. Cloud Save is updated with the new last-update timestamp.

**Note**: Because the Water quantity displayed in the currency HUD is simulated by the client and not actually in sync with the server until the scene is reloaded or the player attempts to place/move/merge a Well, its server-side values will usually be different. However, the next time Cloud Code is called, the appropriate Water will be granted so they will reflect what the player is seeing in the HUD.

#### Virtual Purchases

This sample illustrates how Virtual Purchases occur through the Economy service. In this case, the Virtual Purchase for a Wood Well costs 100 Water. Upgraded Wells cost more Water. For example, Bronze Wells cost 200 Water and remove 2 Wood Wells from the game state. Note that the Virtual Purchases themselves do not effect the Well information stored in Cloud Save. However, once the Virtual Purchase has been successfully executed to deduct the correct amount of Water, Cloud Code updates the game state to remove any merged Wells and add the new Well.

Upon successful Virtual Purchase, all necessary data is added to the Cloud Save game state so it can be shown on the playfield and generate Water correctly. This data includes location (x,y) and timestamp when the item was created.

**Note**: Cloud Code only attempts the transaction after confirming that the move is valid.

#### Well Types

| **Well** | **Cost**                      | **Generates**         | **To Unlock**                                |
|----------|-------------------------------|-----------------------|----------------------------------------------|
| Wood     | 100 Water                     | 1 Water per Second    | N/A (already unlocked at start)              |
| Bronze   | 200 Water + 2 Wood Wells      | 2 Water per Second    | N/A (already unlocked at start)              |
| Silver   | 300 Water + 2 Bronze Wells    | 3 Water per Second    | Merge Wood Wells into Bronze Wells 4 times   |
| Gold     | 400 Water + 2 Silver Wells    | 4 Water per Second    | Merge Bronze Wells into Silver Wells 4 times |

## Setup

### Requirements

To replicate this use case, you need the following [Unity packages](https://docs.unity3d.com/Manual/Packages.html) in your project:

| **Package**                                                                           | **Role**                                                                                                                                                                                                                                         |
|---------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [Authentication](https://docs.unity.com/authentication/IntroUnityAuthentication.html) | Automatically signs in the user anonymously to keep track of their data on the server side.                                                                                                                                                      |
| [Cloud Code](https://docs.unity.com/cloud-code/implementation.html)                   | Sets up the game state for a new game by placing three random obstacles and setting the starting Water currency to 1000. It also validates moves, grants Water based on real-time production, and updates game state based on Virtual Purchases. |
| [Cloud Save](https://docs.unity.com/cloud-save/index.html#Implementation)             | Stores the game state, last update timestamp and unlock data. Cloud Code checks and updates these values directly.                                                                                                                               |
| [Economy](https://docs.unity.com/economy/implementation.html)                         | Retrieves the player's starting and updated Water balances at runtime and performs Virtual Purchases to place and/or merge Wells.                                                                                                                |

To use these services in your game, activate each service for your Organization and project in the [Unity Dashboard](https://dashboard.unity3d.com/).


### Dashboard Setup

To replicate this sample scene's setup on your own dashboard, you need to:
- Publish 5 scripts in Cloud Code.
- Create one Currency and 4 Virtual Transactions for the Economy service.


#### Cloud Code

[Publish the following scripts](https://docs.unity.com/cloud-code/implementation.html#Writing_your_first_script) in the **LiveOps** dashboard:

| **Script**                    | **Parameters**                                                                                                                                                                                                                                                          | **Description**                                                                                                                                                                                                                         | **Location**                                                                          |
|-------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------|
| `IdleClicker_GetUpdatedState` | None                                                                                                                                                                                                                                                                    | Creates a random game if necessary, updates the game state since the last call (including granting any necessary Water), and returns the state to the client.                                                                           | `Assets/Use Case Samples/Idle Clicker Game/Cloud Code/IdleClicker_GetUpdatedState.js` |
| `IdleClicker_PlaceWell`       | `coord`<br><br>`JSON`<br><br>The {x, y} coordinates for the new Well to add to the playfield.<br><br>Example: `{"x":0, "y":0}`                                                                                                                                          | Updates currency balances since the last server call, validates the player's moves, uses Economy service Virtual Purchases to “buy” new Wells, and updates the game state appropriately (for example, adding a Well to the game board). | `Assets/Use Case Samples/Idle Clicker Game/Cloud Code/IdleClicker_PlaceWell.js`       |
| `IdleClicker_MoveWell`        | `drag`<br><br>`JSON`<br><br>The {x, y} coordinates for the Well starting position on the playfield.<br><br>Example: `{"x":0, "y":0}`<hr>`drop`<br><br>`JSON`<br><br>The {x, y} coordinates for the Well ending position on the playfield.                               | Updates Water balances since the last server call, validates the player's move, and updates the game state appropriately.                                                                                                               | `Assets/Use Case Samples/Idle Clicker Game/Cloud Code/IdleClicker_MoveWell.js`        |
| `IdleClicker_MergeWells`      | `drag`<br><br>`JSON`<br><br>The {x, y} coordinates for the first Well's position on the playfield.<br><br>Example: `{"x":0, "y":0}`<hr>`drop`<br><br>`JSON`<br><br>The {x, y} coordinates for the second Well's position and upgraded Well's position on the playfield. | Updates currency balances since the last server call, validates the player's moves, uses Economy service Virtual Purchases to “buy” the new Well, and updates the game state appropriately.                                             | `Assets/Use Case Samples/Idle Clicker Game/Cloud Code/IdleClicker_MergeWells.js`      |
| `IdleClicker_Reset`           | None.                                                                                                                                                                                                                                                                   | Clears Cloud Save entries to simulate a new player                                                                                                                                                                                      | `Assets/Use Case Samples/Idle Clicker Game/Cloud Code/IdleClicker_Reset.js`           |

**Note**: The Cloud Code scripts included in the `Cloud Code` folder are local copies because you cannot see the sample's dashboard. Changes to these scripts do not affect the behavior of this sample because they are not automatically uploaded to the Cloud Code service.


#### Economy

[Configure the following resources](https://docs.unity.com/economy/) in the **LiveOps** dashboard:

| **Resource type** | **Resource name**                            | **ID**                                     | **Description**                                                                                                                                 |
| ----------------- | -------------------------------------------- |------------------------------------------- |-------------------------------------------------------------------------------------------------------------------------------------------------|
| Currency          | Water                                        | `WATER`                                    | Granted at the start of a new game, consumed in Virtual Purchases to place new Wells, and granted every second based on Wells on the playfield. |
| Virtual Purchase  | Idle Clicker Game Well Purchase Well Level 1 | `IDLE_CLICKER_GAME_PURCHASE_WELL_LEVEL_1`  | Virtual Purchase consumes 100 Water in order to place a Wood Well.                                                                              |
| Virtual Purchase  | Idle Clicker Game Well Purchase Well Level 2 | `IDLE_CLICKER_GAME_PURCHASE_WELL_LEVEL_2`  | Virtual Purchase consumes 200 Water to produce a Bronze Well.                                                                                   |
| Virtual Purchase  | Idle Clicker Game Well Purchase Well Level 3 | `IDLE_CLICKER_GAME_PURCHASE_WELL_LEVEL_3`  | Virtual Purchase consumes 300 Water to produce a Silver Well.                                                                                   |
| Virtual Purchase  | Idle Clicker Game Well Purchase Well Level 4 | `IDLE_CLICKER_GAME_PURCHASE_WELL_LEVEL_4`  | Virtual Purchase consumes 400 Water to produce a Gold Well.                                                                                     |
