# Idle Clicker Game
In real-time idle clicker and social games, such as farming games and city building games, common considerations are:

* How to simulate real-time activities in a game that isn't running all the time
* How the simulation can occur on the cloud to ensure all players' games are updated properly, regardless of timezone or any modifications to date/time on a player's device

This sample use case shows how to solve both challenges while limiting calls to UGS, which can cause throttling issues or increased costs.

In this sample, the player uses a Currency ("WATER") to purchase Wells which produce 1 Water Economy Currency item per second, for each Well that is created.

Each time a player loads this use case, or attempts to place a new Well by clicking on the playfield, the Unity script calls Cloud Code which, among other things, calculates how many Water Currency items the Wells should have produced since the last time the script was called. Then Cloud Code calls Economy directly to grant the required Currency.

Between moves, while the player is viewing the game scene but not interacting, the client simulates the Water Economy Currency item production and updates the Currency HUD accordingly. Then, whenever the player attempts to place a new Well, Cloud Code is again called and the simulated Water is actually granted before any Virutal Purchase is attempted.

### Implementation Overview
Since this sample game is intended to be real-time, whenever Cloud Code is called, it checks the current time and calculates how much cumulative Water Economy Currency item should have been produced by all Wells since their last production cycle at the rate of 1 Water per Well per second. To determine how much time has passed, a "last produce time" for each Well is stored on Cloud Save along with the full game state including obstacle locations, Well locations, last update time, etc. Once the appropriate quantity of Water Currency item are granted on the Economy Service, the last-produce-time is updated for all Wells and the fully-updated state is saved back to Cloud Save. This full update occurs at startup as well as whenever the player attempts to place a Well, regardless of whether the Well is successfully placed or not (Note: the player is only charged 500 quantity of Water Currency item for a successful Well placement).

The "WATER" Currency used in this Use Case is not displayed in any other Use Cases because the quantity displayed in the Currency HUD is often only simulated on device and not actually in sync with the server until the scene is reloaded or an attempt to place a Well is made. This means that if the Water Currency's server values were visible in other use cases, the quantity would often be inaccurate. To avoid confusion, we only show the Water quantity in this Use Case.

As with other samples, we are showing how Cloud Code can validate gameplay moves by sending even illegal requests to Cloud Code and allowing it to reject them as appropriate. This happens if the player attempts to place a Well in an occupied place (either obstacle or another Well), as well as when the player lacks sufficient funds to place a Well (i.e. less than 500 Water Currency item).

Another feature of this sample is how the Virtual Purchases are made. In this sample, the Virtual Purchase for a Well costs 500 Water currencies, but, unlike most other Virtual Purchases, the purchase itself does not actually grant anything. Instead, we use the Virtual Purchase to consume the transaction costs (i.e. 500 Water currency item) so, upon a successful transaction, we are permitting the game state to be updated with the new Well. Since this transaction is only attempted after all other failure tests have been passed (i.e. the space is determined to be empty), we know we can safely add the Well to the playfield and return the updated state upon a successful transaction.

### Packages Required
- **Authentication:** Automatically signs in the user anonymously to keep track of their data on the server side.
- **Cloud Code:** Sets up game state for a new game by placing 3 random obstacles and setting the starting Water Currency to 2000. It also validates moves, grants Water based on real-time production, and updates game state based on purchases.
- **Economy:** Stores current Water count and processes Virtual Purchases when Cloud Code deems a move to be valid at a cost of 500 Water currencies.
- **Cloud Save:** Stores updated game state for all obstacles, Wells (including last-production time), etc. Cloud Code checks and updates these values directly.

See the [Authentication](http://documentation.cloud.unity3d.com/en/articles/5385907-unity-authentication-anonymous-sign-in-guide),
[Cloud Code](https://docs.unity.com/cloud-code), [Economy](https://docs.unity.com/economy/Content/implementation.htm?tocpath=Implementation%7C_____0) and [Cloud Save](https://docs.unity.com/cloud-save)
docs to learn how to install and configure these SDKs in your project.

### Dashboard Setup
To use Economy, Cloud Code and Cloud Save services in your game, activate each service for your organization and project in the Unity Dashboard.
You'll need a few Currencies and inventory items in the Economy setup, as well as a couple of scripts in Cloud Code:

#### Economy Items
* Water - `ID: "WATER"` - Granted at the start of a new game, consumed in Virtual Purchase to place new Wells, granted every second for each Well on the playfield.
* Idle Clicker Game Well - `ID: "IDLE_CLICKER_GAME_WELL"` - Virtual Purchase used to charge 500 Water currencies. Note that the Well itself is not granted by the Virtual Purchase--it is added to the game state directly by Cloud Code upon successful completion of the Virtual Purchase.

#### Cloud Code Scripts
* IdleClicker_GetUpdatedState: Create random game if necessary, update game state since last call (including granting any necessary Water) and return state to caller.
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Idle Clicker Game/Cloud Code/IdleClicker_GetUpdatedState.js`
* IdleClicker_PlaceWell: Update state the since last call to grant any necessary Water, validate the player's move, use Economy Virtual Purchase to 'buy' the new Well and update the game state appropriately (i.e. add Well to the world, if successful).
  * Parameters: `coord` - x,y coordinate for new Well to add to the game.
  * Script: `Assets/Use Case Samples/Idle Clicker Game/Cloud Code/IdleClicker_PlaceWell.js`

_**Note**:
The Cloud Code scripts included in the `Cloud Code` folder are just local copies, since you can't see the sample's dashboard. Changes to these scripts will not affect the behavior of this sample since they will not be automatically uploaded to Cloud Code service._
