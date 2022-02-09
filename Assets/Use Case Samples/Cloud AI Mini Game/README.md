# Cloud AI Mini Game
In some games, it's desirable to play a mini game as a reward or to advance game play. This sample demonstrates how to use Cloud Code and other UGS packages to validate game play in a mini game that implements a simple artificial opponent against the player. Additionally, this sample demonstrates how gameplay can be rewarded based on game outcome by awarding bonus Coins for wins and ties. This can make the mini games more enjoyable and even affect game economy going forward.

This sample demonstrates how a user can play a Tic-Tac-Toe game against the AI and receive a Coin Currency reward based on the outcome: 100 Coins for a win, 25 for a tie. Each game begins with a random player (50% human, 50% AI) and progresses until a player successfully places 3 pieces in a row (win) or the board is full (tie).

### Implementation Overview
This sample uses 3 Cloud Code scripts to manage the game: 
* CloudAi_GetState is called once at startup to return the existing game if possible or generate a random game if none exists. This script will also clear the player's Coin total to 0 at the start of the `first` game so win/tie count should reflect the current Coin quantity, unless other Use Case Samples are visited.
* CloudAi_PlayerMove is called for each player move. The script is called with x,y coordinates and Cloud Code validates the player's move, makes an AI move if the game isn't over, determines if the game should end, and returns the final updated state. This script also maintains a win/loss/tie count and calls Economy to grant Coins for Wins and Ties.
* CloudAi_StartNewGame is called when the player presses the [New Game] or [Forfeit] button (Forfeit replaces New Game whenever a game is in progress). This script checks if a game is in progress (`isGameOver` is false), and if so, the Loss Count is increased. Next, a new game is created, a random starting player is selected, and if AI is to go first, a piece is added at a random location on the board. Finally, the new game state is then returned to the client.

At the start of the first game or whenever [Reset Game] is pressed, Cloud Code will reset the quantity of the Coin Currency to zero. By removing any initial Coins, the quantity will be a multiple of 100 or 25 which simplifies reward visualization. This also serves as example of how Currencies can be set to specific quantities from Cloud Code.

To verify that the AI works as expected, the AI is programmed to follow 3 simple rules:
* If the AI can win, it always will (for example, if the AI has 2 pieces in a row with an empty space, it will make the winning play).
* If the player has 2 in a row, the AI will always block the player's winning move.
* Otherwise, it plays randomly.

Popups occur at various key moments in the game, such as at startup to explain who plays first, at game over to notify the player of the winner (or tie), whenever the player makes an invalid move (for example, a player placing a piece atop an existing piece or plays after the game is over), and so on.

Cloud Code directly accesses the Economy service to set a starting Coin quantity when no Cloud Save file is found (for example, the first time a player ever plays the game), as well as to grant Coins as a reward.

Cloud Save keeps a json record of the full game state using the key "CLOUD_AI_GAME_STATE". The associated value stores sequential moves made by each player, the overall state, flags for game over, player turn, if this is a new game/move (so UI can show a popup, if appropriate), as well as a permanent counter for wins, losses and ties. To demonstrate the full game cycle, we've added the [Debug Reset Game] button which will remove the "CLOUD_AI_GAME_STATE" key from Cloud Save and call "CloudAi_GetState" as it did at the start of the session. Since Cloud Code then believes this to be a new player, it will reset Coin quantity to 0, create a new save state with all counters set to 0 and generate a new starting game with a random starting player.

### Packages Required
- **Authentication:** Automatically signs in the user anonymously to keep track of their data on the server side.
- **Cloud Code:** 3 scripts used to generate random games, validate game logic, execute AI turns, and grant Economy rewards based on game outcomes.
- **Economy:** Stores current "COIN" count which is granted as a reward for winning/tying.
- **Cloud Save:** Stores game state.

See the [Authentication](http://documentation.cloud.unity3d.com/en/articles/5385907-unity-authentication-anonymous-sign-in-guide),
[Cloud Code](https://docs.unity.com/cloud-code), [Economy](https://docs.unity.com/economy/Content/implementation.htm?tocpath=Implementation%7C_____0) and [Cloud Save](https://docs.unity.com/cloud-save)
docs to learn how to install and configure these SDKs in your project.

### Dashboard Setup
To use Economy, Cloud Code and Cloud Save services in your game, activate each service for your organization and project in the Unity Dashboard.
You'll need a Currency item in the Economy, as well as a few scripts in Cloud Code:

#### Economy Item
* Coin - `ID: "COIN"` - Granted by the Cloud Code script `CloudAi_PlayerMove` for Wins and, in a lesser quantity, for Ties.

#### Cloud Code Scripts
* CloudAi_GetState: Create and save the random game if no game is in progress. If this is a player's first game or after [Reset Game] is pressed, the Coin quantity will be reset to 0. Finally, the current game state will be returned.
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Cloud AI Mini Game/Cloud Code/CloudAi_GetState.js`
* CloudAi_PlayerMove: Validate the player's requested move and add it to the game state based on coordinate passed, detect game over, if not game over then place 'AI' piece, detect game over again, and return final updated state to client. If player wins or draws, coins are awarded using the Economy service.
  * Parameters: `coord` - x,y coordinate for the player piece to add.
  * Script: `Assets/Use Case Samples/Cloud AI Mini Game/Cloud Code/CloudAi_PlayerMove.js`
* CloudAi_StartNewGame: Called when [New game] or [Forfeit] button is pressed to grant the player a loss (if [Forfeit] is pressed mid-game) and generate a new random game with a random starting player.
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Cloud AI Mini Game/Cloud Code/CloudAi_StartNewGame.js`

_**Note**:
The Cloud Code scripts included in the `Cloud Code` folder are just local copies, since you can't see the sample's dashboard. Changes to these scripts will not affect the behavior of this sample since they will not be automatically uploaded to Cloud Code service._

#### Sample Cloud Save "CLOUD_AI_GAME_STATE" entries:
Sample starting state with AI playing first (notice the `isNewGame` flag is true and aiPieces array already contains a move):
```json
{
	"winCount":1,
	"lossCount":1,
	"tieCount":0,
	"playerPieces":[],
	"aiPieces":[{"x":0,"y":1}],
	"isNewGame":true,
	"isNewMove":true,
	"isPlayerTurn":true,
	"isGameOver":false,
	"status":"playing"
}
```

Sample ending game state after the player wins the game (notice the `isGameOver` flag is true and the status is "playerWon"):
```json
{
	"winCount":2,
	"lossCount":1,
	"tieCount":0,
	"playerPieces":[{"x":0,"y":0},{"x":0,"y":2},{"x":2,"y":2},{"x":1,"y":1}],
	"aiPieces":[{"x":0,"y":1},{"x":2,"y":0},{"x":1,"y":2}],
	"isNewGame":false,
	"isNewMove":false,
	"isPlayerTurn":false,
	"isGameOver":true,
	"status":"playerWon"
}
```
