// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.0");
const { DataApi } = require("@unity-services/cloud-save-1.0");

const badRequestError = 400;
const tooManyRequestsError = 429;

const playfieldSize = 5;
const numStartingObstacles = 3;
const currencyId = "WATER";
const initialQuantity = 2000;
const factoryGrantFrequencySeconds = 1;
const factoryGrantFrequency = factoryGrantFrequencySeconds * 1000;
const factoryGrantPerCycle = 1;

// Entry point for the Cloud Code script 
module.exports = async ({ params, context, logger }) => {
  try {
    const { projectId, playerId, accessToken} = context;
    const cloudSaveApi = new DataApi({ accessToken });
    const economyCurrencyApi = new CurrenciesApi({ accessToken });

    logger.info("Authenticated within the following context: " + JSON.stringify(context));

    let instance = { projectId, playerId, cloudSaveApi, economyCurrencyApi, logger};

    instance.state = await readState(instance);
    
    instance.timestamp = getCurrentTimestamp();

    if (instance.state) {
      logger.info("read start state: " + JSON.stringify(instance.state));

      await updateState(instance);
      logger.info("updated state: " + JSON.stringify(instance.state));
    } else {
      createRandomState(instance);
      logger.info("created random start state: " + JSON.stringify(instance.state));
      
      await setInitialCurrency(instance);
    }

    await saveState(instance);

    return instance.state;
  } catch (error) {
    transformAndThrowCaughtError(error);
  }
}

async function readState(instance) {
  let response = await instance.cloudSaveApi.getItems(instance.projectId, instance.playerId, [ "IDLE_CLICKER_GAME_STATE" ] );

  if (response.data.results &&
      response.data.results.length > 0 &&
      response.data.results[0] &&
      response.data.results[0].value) {
    return JSON.parse(response.data.results[0].value);
  }

  return null;
}

async function updateState(instance) {
  let totalElapsedCycles = updateAllFactories(instance)

  instance.state.timestamp = instance.timestamp;
  
  await grantCurrencyForCycles(instance, totalElapsedCycles);
}

function createRandomState(instance) {
  instance.state = { timestamp:instance.timestamp, factories:[], obstacles:[] };

  while (instance.state.obstacles.length < numStartingObstacles) {
    addRandomObstacle(instance);
  }
}

async function setInitialCurrency(instance) {
  await instance.economyCurrencyApi.setPlayerCurrencyBalance(instance.projectId, instance.playerId, currencyId, 
    { currencyId, balance:initialQuantity });
}

async function saveState(instance) {
  await instance.cloudSaveApi.setItem(instance.projectId, instance.playerId, { key: "IDLE_CLICKER_GAME_STATE", value: JSON.stringify(instance.state) } );
}

function updateAllFactories(instance) {
  let factories = instance.state.factories;
  let totalElapsedCycles = 0;
  factories.forEach(factory => totalElapsedCycles += updateFactory(instance, factory));
  
  return totalElapsedCycles;
}

function updateFactory(instance, factory) {
  let elapsed = instance.timestamp - factory.timestamp;
  let elapsedCycles = Math.floor(elapsed / factoryGrantFrequency);

  instance.logger.info("factory " + JSON.stringify(factory) + "  elapsed time: " + elapsed + "  elapsed cycles: " + elapsedCycles);

  factory.timestamp += elapsedCycles * factoryGrantFrequency;

  return elapsedCycles;
}

async function grantCurrencyForCycles(instance, totalElapsedCycles) {
  if (totalElapsedCycles > 0) {
    let currencyProduced = totalElapsedCycles * factoryGrantPerCycle;

    instance.logger.info("granting currency for total cycles: " + totalElapsedCycles + "  currency produced: " + currencyProduced);
    
    await grantCurrency(instance, currencyProduced);
  }
}

function addRandomObstacle(instance) {
  let x = _.random(playfieldSize - 1);
  let y = _.random(playfieldSize - 1);
  if (instance.state.obstacles.some(item => item.x == x && item.y == y)) {
    return;
  }
  
  instance.state.obstacles.push({x, y});
}

async function grantCurrency(instance, amount) {
  await instance.economyCurrencyApi.incrementPlayerCurrencyBalance(instance.projectId, instance.playerId, currencyId, 
    { currencyId, amount });
}

function getCurrentTimestamp() {
  return Date.now();
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
