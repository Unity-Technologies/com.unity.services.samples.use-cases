// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const playfieldSize = 3;

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

  let gameState = await readState(services);
  
  // If save state is found (normal condition) then remember this isn't a new game/move to avoid duplicate popups.
  if (gameState)
  {
    logger.info("read start state: " + JSON.stringify(gameState));

    if (!gameState.isGameOver)
    {
      gameState.lossCount += 1;
      logger.info("player forfeited; updated state: " + JSON.stringify(gameState));
    }
  }
  else
  {
    // DASHBOARD TESTING CODE: If the starting state is not found (only occurs in dashboard) then setup dummy state. 
    gameState = { winCount:0, lossCount:0, tieCount:0};
    logger.info("created starting state: " + JSON.stringify(gameState));
  }

  startRandomGame(services, gameState);

  await saveState(services, gameState);

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

function startRandomGame(services, gameState) {
  gameState.playerPieces = [];
  gameState.aiPieces = [];
  gameState.isNewGame = true; 
  gameState.isNewMove = true;
  gameState.isPlayerTurn = true;
  gameState.isGameOver = false;
  gameState.status = "playing";

  if (_.random(1))
  {
    const x = _.random(playfieldSize - 1);
    const y = _.random(playfieldSize - 1);
    gameState.aiPieces = [{x,y}];
  }
}

async function saveState(services, gameState) {
  await services.cloudSaveApi.setItem(services.projectId, services.playerId, { key: "CLOUD_AI_GAME_STATE", value: JSON.stringify(gameState) } );
}
