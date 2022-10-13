// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.3");
const { DataApi } = require("@unity-services/cloud-save-1.2");

const badRequestError = 400;
const tooManyRequestsError = 429;

const playfieldSize = 3;

// Entry point for the Cloud Code script 
module.exports = async ({ params, context, logger }) => {
  try {
    const { projectId, playerId, accessToken } = context;
    const cloudSaveApi = new DataApi({ accessToken });
    const economyCurrencyApi = new CurrenciesApi({ accessToken });

    logger.info("Authenticated within the following context: " + JSON.stringify(context));

    const services = { projectId, playerId, cloudSaveApi, economyCurrencyApi, logger };

    let gameState = await readState(services);

    // If save state is found (normal condition) then remember this isn't a new game/move to avoid duplicate popups.
    if (gameState) {
      logger.info("read start state: " + JSON.stringify(gameState));

      if (!gameState.isGameOver) {
        gameState.lossCount += 1;
        logger.info("player forfeited; updated state: " + JSON.stringify(gameState));
      }
    } else {
      // DASHBOARD TESTING CODE: If the starting state is not found (only occurs in dashboard) then setup dummy state. 
      gameState = { winCount:0, lossCount:0, tieCount:0};
      logger.info("created starting state: " + JSON.stringify(gameState));
    }

    startRandomGame(services, gameState);

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

function startRandomGame(services, gameState) {
  gameState.playerPieces = [];
  gameState.aiPieces = [];
  gameState.isNewGame = true;
  gameState.isNewMove = true;
  gameState.isPlayerTurn = true;
  gameState.isGameOver = false;
  gameState.status = "playing";

  if (_.random(1)) {
    const x = _.random(playfieldSize - 1);
    const y = _.random(playfieldSize - 1);
    gameState.aiPieces = [{x,y}];
  }
}

async function saveState(services, gameState) {
  await services.cloudSaveApi.setItem(services.projectId, services.playerId, { key: "CLOUD_AI_GAME_STATE", value: JSON.stringify(gameState) } );
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
    result.name = error.name;
    result.message = error.message;
  }

  throw new Error(JSON.stringify(result));
}
