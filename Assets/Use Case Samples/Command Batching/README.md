# Command Batching
Command batching is the concept where each game action is a Command which can be collected into a queue to be sent to the server in batches for processing.
Using Command batching can optimize the bandwidth your game uses to be as energy efficient as possible or to prevent your game from running slowly because of frequent server calls or server bottlenecks (rate limiting).
This optimization provides users with a more pleasant game experience with less wait times by reducing the number and/or frequency of server calls made by your game.

This sample demonstrates a game where the player has a specified number of turns, and Commands are created and saved for each move the player makes on a turn.
At the end of the game, all Commands that were created are then processed by the server as a group.

This strategy reduces how many server calls need to be made in a game.
Imagine a game that distributes rewards on the server after every Command, in particular rewarding a player XP by calling Cloud Save and Coins by calling Economy.
For example:
- The first command increases XP by 100 and distributes 10 Coins.
- The second command increases XP by 50 and distributes 5 Coins.
- The third command increases XP by 100 and distributes 30 Coins.

These three Commands would then result in a minimum of six calls to various Unity Services (in this case Cloud Save and Economy).
You can see how in a full game this could end up with a lot of server calls being made while the game is being played, potentially slowing the game.
However, if all three of these Commands are stored in a batch and processed at one time (described in greater detail in the [Implementation Overview](#implementation-overview) section below), it would result in just two Unity Services calls: one to Cloud Save to increase XP by 250, and one to Economy to add 45 Coins.

### Implementation Overview
This use case is structured such that a single "game" involves making a specified number of turns, where a single turn is completed by clicking on one of four buttons.
Each button generates a different Command, with different corresponding rewards.
The Command is processed by being added to a batch queue (for future server processing) as well as by distributing its rewards locally.
This local reward distribution allows players to immediately see the results of the turn, but is not considered truly authoritative; you'll see momentarily that any local distributions will get overwritten by the server after it validates and processes the Commands.
Once the player has completed all their turns, the game is over and the queue of Commands are sent to Cloud Code as a single json blob.

The first step of the Cloud Code script is to unpack the json blob into a list of Commands again.
The next step is to verify that the batch contains exactly the number of Commands expected (in this example the batch is always the same number of Commands, however that does not have to be a requirement), and that the Commands are in an order that is legal game play (for example the Achieve Bonus Goal Command cannot be triggered until the Open Chest Command has occurred).
If anything about the batch of Commands is deemed invalid, the script will return a failure and the Commands' rewards will not be distributed on the server.
However if the batch is valid, the Cloud Code script will proceed to the next step.
This step is to determine what rewards should be distributed as a result of the Commands (for example, Command = “enemy defeated”; reward = “add 10 XP”).
This information is received from Remote Config.
Storing the Command and Reward mapping on Remote Config allows for rewards to be tuned remotely in a single place, while still changing the distributions done locally by the client, as well as remotely by the server.
The rewards for all the Commands in the batch are grouped by type (for example, all Commands may increase XP, these XP increases would be added up so that a single server call updates the XP by the total amount).
Finally, the Cloud Code script will make all the necessary calls to the Unity Services APIs (in this case, Economy and Cloud Save) to distribute the rewards.
Once distribution is complete, the script returns, indicating the successful action to the client.

Whenever the Cloud Code script returns (either on success or failure) the client code calls the Unity Services to get their latest data.
If the batch was processed successfully by the server, this should result in no visible change to the player's wallet or other stats (because from their perspective the rewards were distributed immediately during game play).
However, if the batch was invalid and the Cloud Code script failed to process the batch, the client will update the player's wallet and other stats back to where they were at before the game was played, overwriting the local state.

This creates a bit of a mixed-authoritative game.
During certain periods of time the game relies on the local authority to know what the player's wallet and stats are.
However, the server ultimately has the final authority on the player's state.
While this mixed-authority is not appropriate for all game styles, it can be very useful for the game styles that can use it, such as turn-based, single-player, infinite runner, and puzzle games.
Additionally, Command batching can be a good starting point when developing a solution for offline support or bad connection tolerance in games that choose to provide such features.

### Packages Required
- **Cloud Code:** Processes Commands at the end of a game.
- **Remote Config:** Contains the mapping between Commands and their rewards used by both the client and Cloud Code when distributing rewards.
- **Economy:** Manages the currencies that are distributed as rewards.
- **Cloud Save:** Manages certain player stats (XP and Goals Achieved) that are increased as rewards.

See the
[Cloud Code](https://docs.unity.com//cloud-code/Content/implementation.htm?tocpath=Implementation%7C_____0#SDK_installation),
[Remote Config](https://docs.unity3d.com/Packages/com.unity.remote-config@2.0/manual/ConfiguringYourProject.html),
[Economy](https://docs.unity.com/economy/Content/implementation.htm?tocpath=Implementation%7C_____0),
and [Cloud Save](https://docs.unity.com/cloud-save/Content/index.htm#Implementation) 
docs to learn how to install and configure these SDKs in your project.

### Dashboard Setup
To use Cloud Code, Remote Config, Economy, and Cloud Save services in your game, activate each service for your organization and project in the Unity Dashboard.

#### Economy Items
* Coin - `ID: "COIN"` - A reward for certain Commands
* Gem - `ID: "GEM"` - A reward for certain Commands

#### Remote Config
##### Config Values
* COMMANDBATCH_ACHIEVE_BONUS_GOAL - Maps the Command Achieve Bonus Goal to its rewards.
  * Type: json
  * Default value:
  ```json
    {
        "rewards": [{
            "service": "cloudSave",
            "id": "COMMANDBATCH_XP",
            "amount": 150
        }, {
            "service": "cloudSave",
            "id": "COMMANDBATCH_GOALSACHIEVED",
            "amount": 1
        }]
    }
    ```
* COMMANDBATCH_DEFEAT_BLUE_ENEMY - Maps the Command Defeat Blue Enemy to its rewards.
  * Type: json
  * Default value:
  ```json
    {
        "rewards": [{
            "service": "currency",
            "id": "GEM",
            "amount": 5
        }, {
            "service": "cloudSave",
            "id": "COMMANDBATCH_XP",
            "amount": 50
        }]
    }
    ```
* COMMANDBATCH_DEFEAT_RED_ENEMY - Maps the Command Defeat Red Enemy to its rewards.
  * Type: json
  * Default value:
  ```json
    {
        "rewards": [{
            "service": "currency",
            "id": "COIN",
            "amount": 5
        }, {
            "service": "cloudSave",
            "id": "COMMANDBATCH_XP",
            "amount": 50
        }]
    }
    ```
* COMMANDBATCH_OPEN_CHEST - Maps the Command Open Chest to its rewards.
  * Type: json
  * Default value:
  ```json
    {
        "rewards": [{
            "service": "currency",
            "id": "COIN",
            "amount": 25
        }, {
            "service": "currency",
            "id": "GEM",
            "amount": 25
        }, {
            "service": "cloudSave",
            "id": "COMMANDBATCH_XP",
            "amount": 100
        }]
    }
    ```
* COMMANDBATCH_GAME_OVER - Maps the Command Game Over to its rewards.
  * Type: json
  * Default value:
  ```json
    {
        "rewards": [{
            "service": "cloudSave",
            "id": "COMMANDBATCH_XP",
            "amount": 100
        }]
    }
    ```

#### Cloud Code Scripts
* CommandBatch_ProcessBatch:
  * Parameters: `batch` - An array of Command keys
    * i.e. `{"commands": ["COMMANDBATCH_DEFEAT_RED_ENEMY", "COMMANDBATCH_OPEN_CHEST", "COMMANDBATCH_ACHIEVE_BONUS_GOAL", "COMMANDBATCH_DEFEAT_BLUE_ENEMY", "COMMANDBATCH_OPEN_CHEST", "COMMANDBATCH_ACHIEVE_BONUS_GOAL", "COMMANDBATCH_GAME_OVER"]}`
  * Script: `Assets/Use Case Samples/Command Batching/Cloud Code/CommandBatch_ProcessBatch.js`

_**Note**:
The Cloud Code scripts included in the `Cloud Code` folder are just local copies, since you can't see the sample's dashboard. Changes to these scripts will not affect the behavior of this sample since they will not be automatically uploaded to Cloud Code service._
