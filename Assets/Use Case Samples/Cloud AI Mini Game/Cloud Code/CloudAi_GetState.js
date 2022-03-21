// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const playfieldSize = 3;
const currencyId = "COIN";
const initialQuantity = 0;
const rateLimitError = 429;
const validationError = 400;

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.0");
const { DataApi } = require("@unity-services/cloud-save-1.0");


// Entry point for the Cloud Code script 
module.exports = async ({ params, context, logger }) => {
  
  const { projectId, playerId, accessToken} = context;
  const cloudSaveApi = new DataApi({ accessToken });
  const economyCurrencyApi = new CurrenciesApi({ accessToken });
  
  logger.info("Authenticated within the following context: " + JSON.stringify(context));

  const services = { projectId, playerId, cloudSaveApi, economyCurrencyApi, logger };

  let gameState;

  try
  {
    gameState = await readState(services);
  
    if (gameState)
    {
      gameState.isNewMove = false;
      gameState.isNewGame = false;

      logger.info("read start state: " + JSON.stringify(gameState));
    }
    else
    {
      gameState = createInitialPlayerProgressState(services);

      startRandomGame(services, gameState);

      await setInitialCurrency(services, gameState);
      
      logger.info("created starting state: " + JSON.stringify(gameState));
    }

    await saveState(services, gameState);
  }
  catch (error)
  {
    TransformAndThrowCaughtError(error);
  }

  return gameState;
}

async function readState(services) {
  const response = await services.cloudSaveApi.getItems(services.projectId, services.playerId, [ "CLOUD_AI_GAME_STATE" ] );

  if (response.data.results &&
      response.data.results.length > 0 &&
      response.data.results[0] &&
      response.data.results[0].value)
  {
    return JSON.parse(response.data.results[0].value);
  }

  return null;
}

function createInitialPlayerProgressState(services) {
  return { winCount:0, lossCount:0, tieCount:0 };
}

function startRandomGame(services, gameState) {
  gameState.playerPieces = [];
  gameState.aiPieces = [];
  gameState.isNewGame = true; 
  gameState.isNewMove = true;
  gameState.isPlayerTurn = true;
  gameState.isGameOver = false;
  gameState.status = "playing";

  // Pick first player at random. If it's the AI then place the first move in random space.
  // Note: the fact that isNewGame is true and gameState.aiPieces contains an entry is used by client to detect that AI was player 1.
  if (_.random(1))
  {
    let x = _.random(playfieldSize - 1);
    let y = _.random(playfieldSize - 1);
    gameState.aiPieces = [{x,y}];
  }
}

async function setInitialCurrency(services, gameState) {
  await services.economyCurrencyApi.setPlayerCurrencyBalance(services.projectId, services.playerId, currencyId, 
    { currencyId, balance:initialQuantity });
}

async function saveState(services, gameState) {
  await services.cloudSaveApi.setItem(services.projectId, services.playerId, { key: "CLOUD_AI_GAME_STATE", value: JSON.stringify(gameState) } );
}

// Some form of this function appears in all Cloud Code scripts.
// Its purpose is to parse the errors thrown from the script into a standard exception object which can be stringified.
function TransformAndThrowCaughtError(error) {
  let result = {
    status: 0,
    title: "",
    message: "",
    retryAfter: null,
    additionalDetails: ""
  };

  if (error.response)
  {
    result.status = error.response.data.status ? error.response.data.status : 0;
    result.title = error.response.data.title ? error.response.data.title : "Unknown Error";
    result.message = error.response.data.detail ? error.response.data.detail : error.response.data;
    if (error.response.status === rateLimitError)
    {
      result.retryAfter = error.response.headers['retry-after'];
    }
    else if (error.response.status === validationError)
    {
      let arr = [];
      _.forEach(error.response.data.errors, error => {
        arr = _.concat(arr, error.messages);
      });
      result.additionalDetails = arr;
    }
  }
  else
  {
    result.title = error.name;
    result.message = error.message;
  }

  throw new Error(JSON.stringify(result));
}
