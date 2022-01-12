
const playfieldSize = 5;
const currencyId = "WATER";
const factoryGrantFrequencySeconds = 1;
const factoryGrantFrequency = factoryGrantFrequencySeconds * 1000;
const factoryGrantPerCycle = 1;

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
  
  let result = null;
  if (!isValidPlacement(instance, coord))
  {
    logger.error("space already occupied.");
    result = "spaceAlreadyOccupied";
  }
  else
  {
    if (!await isVirtualPurchaseSuccessful(instance))
    {
      logger.error("virtual purchase failed.");
      result = "virtualPurchaseFailure";
    }
    else
    {
      placeFactory(instance, coord);

      logger.info("placement successful.");
      result = "success";
    }
  }

  await saveState(instance);
  
  instance.state.placePieceResult = result;

  return instance.state;
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

function isValidPlacement(instance, coord) {
  if (coord.x < 0 || coord.x >= playfieldSize || coord.y < 0 || coord.y >= playfieldSize)
  {
    return false;
  }
  
  if (instance.state.factories.some(item => item.x == coord.x && item.y == coord.y) ||
    instance.state.obstacles.some(item => item.x == coord.x && item.y == coord.y))
  {
    return false;
  }
  
  return true;
}

async function isVirtualPurchaseSuccessful(instance) {
  try
  {
    await instance.purchasesApi.makeVirtualPurchase(instance.projectId, instance.playerId, { id: "IDLE_CLICKER_GAME_WELL" });
  }
  catch (error)
  {
    //TODO: check error for insufficient funds once feature is available
    instance.logger.info("virtual purchase threw exception: " + JSON.stringify(error));
    
    return false;
  }
  
  return true;
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
