// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.2");
const { DataApi } = require("@unity-services/cloud-save-1.1");

const badRequestError = 400;
const tooManyRequestsError = 429;

const millisecondsPerSecond = 1000;
const playfieldSize = 5;
const maxItemsInWorld = playfieldSize * playfieldSize;
const numStartingObstacles = 3;
const currencyId = "WATER";
const initialCurrencyBalance = 1000;
const gameStateCloudSaveKey = "IDLE_CLICKER_GAME_STATE";

// Entry point for the Cloud Code script 
module.exports = async ({ params, context, logger }) => {
  try {
    const { projectId, playerId, accessToken } = context;
    const cloudSaveApi = new DataApi({ accessToken });
    const currencyApi = new CurrenciesApi({ accessToken });

    let instance = { projectId, playerId, cloudSaveApi, currencyApi, logger };

    const timestamp = getCurrentTimestamp();
    instance.currentTimestamp = timestamp;

    // Read current state from Cloud Save.
    instance.state = await readState(instance);
    if (instance.state) {

      logger.info("Read start state: " + JSON.stringify(instance.state));

    // If state does not exist in Cloud Save, create random starting state.
    } else {

      createRandomState(instance);
      logger.info("Created random start state: " + JSON.stringify(instance.state));

      await setInitialCurrencyBalance(instance);
    }

    // Update the current state by granting Water and updating timestamp.
    await updateState(instance);
    logger.info("Updated state: " + JSON.stringify(instance.state));

    // Save updated state to Cloud Save.
    await saveGameState(instance);

    // After we've saved the state, insert the currency balance to return to caller.
    instance.state.currencyBalance = instance.currencyBalance;

    return instance.state;

  } catch (error) {
    transformAndThrowCaughtError(error);
  }
}

function getCurrentTimestamp() {
  return Date.now();
}

async function readState(instance) {
  let response = await instance.cloudSaveApi.getItems(instance.projectId, instance.playerId, [ gameStateCloudSaveKey ]);

  if (response.data.results &&
      response.data.results.length >= 1) {
    return response.data.results[0] ? response.data.results[0].value : null;
  }

  return null;
}

function createRandomState(instance) {
  instance.state = { 
    obstacles: [],
    wells_level1: [],
    wells_level2: [],
    wells_level3: [],
    wells_level4: [],

    timestamp: instance.currentTimestamp,

    // Unlock Manager starting state: Level-1 Wells can be placed and level-2 Wells can be created by merging level-1 Wells, however
    // level-3 and level-4 Wells must be unlocked by merging 4 times to create Wells of the previous level.
    unlockCounters: { 
      Well_Level1:4,
      Well_Level2:4,
      Well_Level3:0, 
      Well_Level4:0
    }
  };

  for (let i = 0; i < numStartingObstacles; i++) {
    addRandomObstacle(instance);
  }
}

async function setInitialCurrencyBalance(instance) {
    const projectId = instance.projectId;
    const playerId = instance.playerId;

    // Set starting water balance for a new game. This is also the default value in Economy, but, since we permit resetting the game state, 
    // we need to also reset the water balance so the board is in the 'default state' when starting a new game.
    const currencyBalanceRequest = { currencyId, balance: initialCurrencyBalance };
    const currenciesApiSetPlayerCurrencyBalanceRequest = { projectId, playerId, currencyBalanceRequest, currencyId, balance: initialCurrencyBalance };
    await instance.currencyApi.setPlayerCurrencyBalance(currenciesApiSetPlayerCurrencyBalanceRequest);
}

async function updateState(instance) {
  instance.lastTimestamp = instance.state.timestamp;
  let waterToProduce = updateAllWells(instance)

  instance.state.timestamp = instance.currentTimestamp;

  instance.currencyBalance = await grantWater(instance, waterToProduce);
}

async function saveGameState(instance) {
  await instance.cloudSaveApi.setItem(instance.projectId, instance.playerId, { key: gameStateCloudSaveKey, value: instance.state } );
}

function addRandomObstacle(instance) {
  // Find an empty space on the playfield.
  // The while statement makes it retry in the unlikely event that an obstacle is already in a given position until an empty space is found.
  while (true) {
    let x = _.random(playfieldSize - 1);
    let y = _.random(playfieldSize - 1);
    if (!instance.state.obstacles.some(item => item.x == x && item.y == y)) {
      instance.state.obstacles.push({x, y});
      return;
    }
  }
}

function updateAllWells(instance) {
  const waterToProduce = updateWells(instance, instance.state.wells_level1, 1) +
    updateWells(instance, instance.state.wells_level2, 2) +
    updateWells(instance, instance.state.wells_level3, 3) +
    updateWells(instance, instance.state.wells_level4, 4);
  
  return waterToProduce;
}

async function grantWater(instance, amount) {
  instance.logger.info("Granting " + amount + " water.");

  const projectId = instance.projectId;
  const playerId = instance.playerId;
  const currencyModifyBalanceRequest = { currencyId, amount };
  const result = await instance.currencyApi.incrementPlayerCurrencyBalance({ projectId, playerId, currencyId, currencyModifyBalanceRequest });

  return result.data.balance;
}

function updateWells(instance, wells, waterGrantedPerSecond) {
  let waterToProduce = 0;
  wells.forEach(well => waterToProduce += calculateWaterProducedSinceLastUpdate(instance, well, waterGrantedPerSecond));
  
  return waterToProduce;
}

function calculateWaterProducedSinceLastUpdate(instance, well, waterGrantedPerSecond) {
  const totalElapsed = instance.currentTimestamp - well.timestamp;
  const totalWaterProduced = Math.floor(totalElapsed * waterGrantedPerSecond / millisecondsPerSecond);
  const previousElapsed = instance.lastTimestamp - well.timestamp;
  const waterAlreadyProduced = Math.floor(previousElapsed * waterGrantedPerSecond / millisecondsPerSecond);

  return totalWaterProduced - waterAlreadyProduced;
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
