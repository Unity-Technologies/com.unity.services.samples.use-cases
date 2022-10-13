// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.3");
const { DataApi } = require("@unity-services/cloud-save-1.2");

const badRequestError = 400;
const tooManyRequestsError = 429;

const playfieldSize = 3;
const currencyId = "COIN";
const winCurrencyQuantity = 100;
const tieCurrencyQuantity = 25;


// Entry point for the Cloud Code script 
module.exports = async ({ params, context, logger }) => {
  try {
    logger.info("Script parameters: " + JSON.stringify(params));
    logger.info("Authenticated within the following context: " + JSON.stringify(context));

    const { projectId, playerId, accessToken } = context;
    const cloudSaveApi = new DataApi({ accessToken });
    const economyCurrencyApi = new CurrenciesApi({ accessToken });

    const coord = params.coord;

    const services = { projectId, playerId, cloudSaveApi, economyCurrencyApi, logger };

    let gameState = await readState(services);

    // If save state is found (normal condition) then remember this isn't a new game/move to avoid duplicate popups.
    if (gameState) {
      gameState.isNewGame = false;
      gameState.isNewMove = false;
    } else {
      // DASHBOARD TESTING CODE: If the starting state is not found (only occurs in dashboard) then setup dummy state 
      // for testing only. By not randomizing here, testing can request valid or invalid locations as needed.
      gameState = { playerPieces:[], aiPieces:[{x:0, y:1}], isPlayerTurn:true, isNewGame:true, isNewMove:true, isGameOver:false,
        status:"playing", winCount: 0, lossCount: 0, tieCount: 0 };

      logger.info("created debug start state: " + JSON.stringify(gameState));
    }

    // If game is already over, return status to signal select-new-game popup.
    if (gameState.isGameOver) {
      logger.error("attempted to place when game over");
      throw new GameOverError("Game over when player attempted to place a piece.");
    }

    // If player placed a piece in occupied space, show popup on client.
    else if (isSpaceOccupied(services, gameState, coord)) {
      logger.error("space already occupied.");
      throw new SpaceOccupiedError("Player attemped to place a piece in an occupied space.");
    }

    // Handle valid move by placing the new piece.
    else {
      gameState.isNewMove = true;
      gameState.status = "playing";
      gameState.isPlayerTurn = false;

      placePlayerPiece(services, gameState, coord);
      logger.info("player placement successful");

      if (await detectAndHandleGameOver(services, gameState)) {
        logger.info("player triggered game over. updated status: " + gameState.status);
      } else {
        placeAiPiece(services, gameState);
        logger.info("ai placement successful");

        if (await detectAndHandleGameOver(services, gameState)) {
          logger.info("ai triggered game over. updated status: " + gameState.status);
        } else {
          gameState.isPlayerTurn = true;
        }
      }
    }

    await saveState(services, gameState);

    return gameState;
  } catch (error) {
    transformAndThrowCaughtError(error);
  }
}

async function readState(services) {
  const response = await services.cloudSaveApi.getItems(services.projectId, services.playerId, [ "CLOUD_AI_GAME_STATE" ]);

  if (response.data.results &&
      response.data.results.length > 0 &&
      response.data.results[0] &&
      response.data.results[0].value) {
    return JSON.parse(response.data.results[0].value);
  }

  return null;
}

function placePlayerPiece(services, gameState, coord) {
  gameState.playerPieces.push({x:coord.x, y:coord.y});
}

// Handle the ai logic: first try to win, next try to block, otherwise play randomly.
function placeAiPiece(services, gameState) {
  if (makeWinningAiMove(services, gameState)) {
    return;
  }

  if (makeBlockingAiMove(services, gameState)) {
    return;
  }

  makeRandomAiMove(services, gameState);
}

// To place a winning move, simply place the 3rd piece in any line aleady containing 2 ai pieces.
// Note: the player pieces are also needed to ensure the player hasn't already placed his/her blocking piece there.
function makeWinningAiMove(services, gameState) {
  return tryToPlace3rdPieceOnAnyLine(services, gameState, gameState.aiPieces, gameState.playerPieces);
}

// To block, place the 3rd piece in any line already containing 2 player pieces.
// Note: the ai pieces are needed to ensure the ai hasn't already blocked.
function makeBlockingAiMove(services, gameState) {
  return tryToPlace3rdPieceOnAnyLine(services, gameState, gameState.playerPieces, gameState.aiPieces);
}

function makeRandomAiMove(services, gameState) {
  let coord = {x:_.random(playfieldSize - 1), y:_.random(playfieldSize - 1)};

  while (isSpaceOccupied(services, gameState, coord)) {
    // Note: this allows all board positions to be tried only once quickly while avoiding 
    // horizontal/vertical line preference using a simple equation.
    coord.x = (coord.x + (coord.y == 1 ? 1 : 2)) % playfieldSize;
    coord.y = (coord.y + 1) % playfieldSize;
  }

  gameState.aiPieces.push({x:coord.x, y:coord.y});
}

function isSpaceOccupied(services, gameState, coord) {
  if (coord.x < 0 || coord.x >= playfieldSize || coord.y < 0 || coord.y >= playfieldSize) {
    return true;
  }

  return isPieceFound(services, gameState, gameState.playerPieces, coord.x, coord.y) ||
      isPieceFound(services, gameState, gameState.aiPieces, coord.x, coord.y);
}

// Try all lines (horizontal, vertical, diagonal) to see if any has 2 pieces already and 
// place the ai piece in the 3rd position, if possible.
function tryToPlace3rdPieceOnAnyLine(services, gameState, pieceListToCheck, opponentPieceList) {
  for (let i = 0; i < playfieldSize; i++) {
    // Try all 3 columns.
    if (tryToPlace3rdPieceOnLine(services, gameState, i, 0, 0, 1, pieceListToCheck, opponentPieceList)) {
      return true;
    }

    // Try all 3 rows.
    if (tryToPlace3rdPieceOnLine(services, gameState, 0, i, 1, 0, pieceListToCheck, opponentPieceList)) {
      return true;
    }
  }

  // Try diagonal starting at top-left
  if (tryToPlace3rdPieceOnLine(services, gameState, 0, 0, 1, 1, pieceListToCheck, opponentPieceList)) {
    return true;
  }

  // Try diagonal starting at bottom-left
  if (tryToPlace3rdPieceOnLine(services, gameState, 0, 2, 1, -1, pieceListToCheck, opponentPieceList)) {
    return true;
  }

  // All options exhausted--not possible to place the 3rd piece on any line
  return false;
}

// Check specified line (using starting x,y and delta dx,dy) to try to place last piece on the line to win or block.
// Note: dx,dy is the movement of x,y (the delta) as we move along the line we're checking. So, for example,
//       to start at bottom-left and move diagonally up, we'd start at x,y=(0,2) and move up and
//       right 1 each time so dx=1 (move 1 to the right each step) and dy=-1 (move up 1 each step).
function tryToPlace3rdPieceOnLine(services, gameState, x, y, dx, dy, pieceListToCheck, opponentPieceList) {
  if (countLinePieces(services, gameState, pieceListToCheck, x, y, dx, dy) === playfieldSize - 1) {
    for (let i = 0; i < playfieldSize; i++, x += dx, y += dy) {
      if (!isPieceFound(services, gameState, pieceListToCheck, x, y)) {
        if (!isPieceFound(services, gameState, opponentPieceList, x, y)) {
          gameState.aiPieces.push({x, y});

          return true;
        }
      }
    }
  }

  return false;
}

async function detectAndHandleGameOver(services, gameState) {
  if (gameState.isGameOver) {
    return true;
  }

  if (isWin(services, gameState, gameState.playerPieces)) {
    await grantCurrencyReward(services, gameState, winCurrencyQuantity);

    gameState.status = "playerWon";
    gameState.isGameOver = true;
    gameState.winCount += 1;

    return true;
  }

  if (isWin(services, gameState, gameState.aiPieces)) {
    gameState.status = "aiWon";
    gameState.isGameOver = true;
    gameState.lossCount += 1;

    return true;
  }

  if (isBoardFull(services, gameState)) {
    await grantCurrencyReward(services, gameState, tieCurrencyQuantity);

    gameState.status = "draw";
    gameState.isGameOver = true;
    gameState.tieCount += 1;

    return true;
  }

  return false;
}

function isWin(services, gameState, pieces) {
  for (let i = 0; i < playfieldSize; i++) {
    if (countLinePieces(services, gameState, pieces, i, 0, 0, 1) === playfieldSize) {
      return true;
    }

    if (countLinePieces(services, gameState, pieces, 0, i, 1, 0) === playfieldSize) {
      return true;
    }
  }

  if (countLinePieces(services, gameState, pieces, 0, 0, 1, 1) === playfieldSize) {
    return true;
  }

  if (countLinePieces(services, gameState, pieces, 0, playfieldSize - 1, 1, -1) === playfieldSize) {
    return true;
  }

  return false;
}

// Count number of game pieces (either player's or ai's) along specified line (starting at x,y and using delta dx,dy).
// Note: dx,dy is the movement of x,y (the delta) as we move along the line we're checking. So, for example,
//       to start at bottom-left and move diagonally up, we'd start at x,y=(0,2) and move up and
//       right 1 each time so dx=1 (move 1 to the right each step) and dy=-1 (move up 1 each step).
function countLinePieces(services, gameState, pieces, x, y, dx, dy) {
  let count = 0;
  for (let i = 0; i < playfieldSize; i++, x += dx, y += dy) {
    if (isPieceFound(services, gameState, pieces, x, y)) {
      count++;
    }
  }

  return count;
}

function isPieceFound(services, gameState, pieces, x, y) {
  return pieces.some(item => item.x == x && item.y == y);
}

function isBoardFull(services, gameState) {
  return gameState.playerPieces.length + gameState.aiPieces.length >= playfieldSize * playfieldSize;
}

async function saveState(services, gameState) {
  await services.cloudSaveApi.setItem(services.projectId, services.playerId, { key: "CLOUD_AI_GAME_STATE", value: JSON.stringify(gameState) } );
}

async function grantCurrencyReward(services, gameState, amount) {
  const currencyModifyBalanceRequest = { currencyId, amount };
  const requestParameters = { projectId: services.projectId, playerId: services.playerId, currencyId, currencyModifyBalanceRequest };
  await services.economyCurrencyApi.incrementPlayerCurrencyBalance(requestParameters);
}

// Some form of this function appears in all Cloud Code scripts.
// Its purpose is to parse the errors thrown from the script into a standard exception object which can be stringified.
function transformAndThrowCaughtError(error) {
  let result = {
    status: 0,
    name: "",
    message: "",
    retryAfter: null,
    details: ""
  };

  if (error.response) {
    result.status = error.response.data.status ? error.response.data.status : 0;
    result.name = error.response.data.title ? error.response.data.title : "Unknown Error";
    result.message = error.response.data.detail ? error.response.data.detail : error.response.data;

    if (error.response.status === tooManyRequestsError) {
      result.retryAfter = error.response.headers['retry-after'];
    } else if (error.response.status === badRequestError) {
      let arr = [];

      _.forEach(error.response.data.errors, error => {
        arr = _.concat(arr, error.messages);
      });

      result.details = arr;
    }
  } else {
    if (error instanceof CloudCodeCustomError) {
      result.status = error.status;
    }

    result.name = error.name;
    result.message = error.message;
  }

  throw new Error(JSON.stringify(result));
}

class CloudCodeCustomError extends Error {
  constructor(message) {
    super(message);
    this.name = "CloudCodeCustomError";
    this.status = 1;
  }
}

class SpaceOccupiedError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "SpaceOccupiedError";
    this.status = 2;
  }
}

class GameOverError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "GameOverError";
    this.status = 3;
  }
}
