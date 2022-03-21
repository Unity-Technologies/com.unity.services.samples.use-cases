
const playfieldSize = 5;
const currencyId = "WATER";
const factoryGrantFrequencySeconds = 1;
const factoryGrantFrequency = factoryGrantFrequencySeconds * 1000;
const factoryGrantPerCycle = 1;
const rateLimitError = 429;
const validationError = 400;

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.0");
const { DataApi } = require("@unity-services/cloud-save-1.0");
const { PurchasesApi } = require("@unity-services/economy-2.0");


// Entry point for the Cloud Code script 
module.exports = async ({ params, context, logger }) => {
  
  logger.info("Script parameters: " + JSON.stringify(params));
  logger.info("Authenticated within the following context: " + JSON.stringify(context));
  
  const { projectId, playerId, accessToken} = context;
  const cloudSaveApi = new DataApi({ accessToken });
  const economyCurrencyApi = new CurrenciesApi({ accessToken });
  const purchasesApi = new PurchasesApi({ accessToken });
  
  let coord = params.coord;

  let instance = { projectId, playerId, cloudSaveApi, economyCurrencyApi, purchasesApi, logger };

  try
  {
    instance.state = await readState(instance);
    
    instance.timestamp = getCurrentTimestamp();
    
    // If save state is found (normal condition) then update it based on duration since last update.
    if (instance.state)
    {
      logger.info("read start state: " + JSON.stringify(instance.state));
      
      await updateState(instance);
      logger.info("updated state: " + JSON.stringify(instance.state));
    }
    else
    {
      // DASHBOARD TESTING CODE: If the starting state is not found (only occurs in dashboard) then setup dummy state 
      // for testing only. By not randomizing here, testing can request valid or invalid locations as needed.
      instance.state = { timestamp:instance.timestamp, factories:[], obstacles:[{x:1, y:2}, {x:2, y:2}, {x:3, y:2}] };
      await economyCurrencyApi.setPlayerCurrencyBalance(projectId, playerId, currencyId, 
        { currencyId, balance:2000 });
      logger.info("created debug start state: " + JSON.stringify(instance.state));
    }

    // Check the placement coord for Obstacles and Wells. Throws if placement is found to be invalid.    
    throwIfSpaceOccupied(instance, coord);

    // Attempt the Virtual Purchase for the Well. Throws if purchase is unsuccessful.
    await instance.purchasesApi.makeVirtualPurchase(instance.projectId, instance.playerId, { id: "IDLE_CLICKER_GAME_WELL" });

    // If we get to this point, the space is empty and Virtual Purchases has succeeded so we can add the factory to the game state.
    placeFactory(instance, coord);

    await saveState(instance);

    logger.info("placement successful.");

    return instance.state;
  }
  catch (error)
  {
    TransformAndThrowCaughtError(error);
  }
}

async function readState(instance) {
  let response = await instance.cloudSaveApi.getItems(instance.projectId, instance.playerId, [ "IDLE_CLICKER_GAME_STATE" ] );

  if (response.data.results &&
      response.data.results.length > 0 &&
      response.data.results[0] &&
      response.data.results[0].value)
  {
    return JSON.parse(response.data.results[0].value);
  }

  return null;
}

async function updateState(instance) {
  let totalElapsedCycles = updateAllFactories(instance)

  instance.state.timestamp = instance.timestamp;
  
  await grantCurrencyForCycles(instance, totalElapsedCycles);
}

function throwIfSpaceOccupied(instance, coord) {
  if (instance.state.factories.some(item => item.x == coord.x && item.y == coord.y) ||
    instance.state.obstacles.some(item => item.x == coord.x && item.y == coord.y))
  {
    throw new SpaceOccupiedError("Player attemped to place a piece in an occupied space.");
  }
}

function placeFactory(instance, coord) {
    instance.state.factories.push({x:coord.x, y:coord.y, timestamp:instance.timestamp});
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
  if (totalElapsedCycles > 0)
  {
    let currencyProduced = totalElapsedCycles * factoryGrantPerCycle;

    instance.logger.info("granting currency for total cycles: " + totalElapsedCycles + "  currency produced: " + currencyProduced);

    await grantCurrency(instance, currencyProduced);
  }
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
    if (error instanceof CloudCodeCustomError)
    {
      result.status = error.status;
    }
    result.title = error.name;
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
