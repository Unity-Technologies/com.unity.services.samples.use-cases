const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.2");
const { PurchasesApi } = require("@unity-services/economy-2.2");
const { DataApi } = require("@unity-services/cloud-save-1.1");

const badRequestError = 400;
const tooManyRequestsError = 429;

const millisecondsPerSecond = 1000;
const playfieldSize = 5;
const currencyId = "WATER";
const wellCurrencyCost = 100;
const gameStateCloudSaveKey = "IDLE_CLICKER_GAME_STATE";
const wellPurchaseId_Level1 = "IDLE_CLICKER_GAME_PURCHASE_WELL_LEVEL_1";

// Entry point for the Cloud Code script 
module.exports = async ({ params, context, logger }) => {
  try {
    logger.info("Script parameters: " + JSON.stringify(params));

    const { projectId, playerId, accessToken } = context;
    const cloudSaveApi = new DataApi({ accessToken });
    const currencyApi = new CurrenciesApi({ accessToken });
    const purchasesApi = new PurchasesApi({ accessToken });

    let coord = params.coord;

    let instance = { projectId, playerId, cloudSaveApi, currencyApi, purchasesApi, logger };

    const timestamp = getCurrentTimestamp();
    instance.currentTimestamp = timestamp;

    instance.state = await readState(instance);
    if (!instance.state) {
      throw new StateMissingError("PlaceWell script executed without valid state setup. Be sure to call GetUpdatedState script before merging wells.");
    }
    logger.info("Start state: " + JSON.stringify(instance.state));

    await updateState(instance);
    logger.info("Updated state: " + JSON.stringify(instance.state));

    // Save state now with updated timestamp. 
    // It's important to save now and again after Well has been added so, if we throw between the calls, Cloud Save maintains
    // the correct timestamp for when we last granted water.
    await saveGameState(instance);

    // Validate the location and throw if invalid.
    // Note: This is done after updating status since we want to always update water even if the placement fails.
    throwIfLocationInvalid(instance, coord);

    throwIfSpaceOccupied(instance, coord);

    // Attempt the Virtual Purchase for the Well. Throws if purchase is unsuccessful.
    await purchaseWell(instance);

    // If we get to this point, the space is empty and Virtual Purchases has succeeded so we just need add it to the game state.
    addWellToState(instance, coord);

    // Save state now with new well added to game state. 
    await saveGameState(instance);

    // After we've saved the state, insert the currency balance to return to caller.
    // We retrieved the currency balance at the start so we must now update it by deducting the cost of purchasing the Well.
    instance.state.currencyBalance = instance.currencyBalance - wellCurrencyCost;

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

async function updateState(instance) {
  instance.lastTimestamp = instance.state.timestamp;
  let waterToProduce = updateAllWells(instance)

  instance.state.timestamp = instance.currentTimestamp;
  
  instance.currencyBalance = await grantWater(instance, waterToProduce);
}

async function saveGameState(instance) {
  await instance.cloudSaveApi.setItem(instance.projectId, instance.playerId, { key: gameStateCloudSaveKey, value: instance.state } );
}

function throwIfLocationInvalid(instance, coord) {
  if (coord.x < 0 || coord.x >= playfieldSize ||
      coord.y < 0 || coord.y >= playfieldSize) {
    throw new InvalidLocationError("Player attemped to place a piece at invalid location.");
  }
}

function throwIfSpaceOccupied(instance, coord) {
  if (instance.state.obstacles.some(item => item.x == coord.x && item.y == coord.y) ||
      instance.state.wells_level1.some(item => item.x == coord.x && item.y == coord.y) ||
      instance.state.wells_level2.some(item => item.x == coord.x && item.y == coord.y) ||
      instance.state.wells_level3.some(item => item.x == coord.x && item.y == coord.y) ||
      instance.state.wells_level4.some(item => item.x == coord.x && item.y == coord.y)) {
    throw new SpaceOccupiedError("Player attemped to place a piece in an occupied space.");
  }
}

async function purchaseWell(instance) {
  try {         
    const projectId = instance.projectId;
    const playerId = instance.playerId;
    const playerPurchaseVirtualRequest = { id: wellPurchaseId_Level1 };
    await instance.purchasesApi.makeVirtualPurchase({ projectId, playerId, playerPurchaseVirtualRequest });
  } catch (e) {
    const message = "Purchasing Well failed";

    if (e.response !== undefined && e.response !== null) {
      var exceptionData = e.response.data;
      var exceptionHeaders = e.response.headers;
      const statusCode = exceptionData.code ? exceptionData.code : exceptionData.status;

      if (e.response.status === tooManyRequestsError) {
        const retryAfter = exceptionHeaders['retry-after'] ? exceptionHeaders['retry-after'] : null;

        throw new EconomyRateLimitError(message, exceptionData.detail,
            exceptionData.title, statusCode, retryAfter);
      } else if (e.response.status === badRequestError) {
        let details = [];
        _.forEach(exceptionData.errors, error => {
          details = _.concat(details, error.messages);
        });

        throw new EconomyValidationError(message, exceptionData.detail,
            exceptionData.title, statusCode, details);
      } else {
        throw new EconomyProcessingError(message, exceptionData.detail,
            exceptionData.title, statusCode)
      }
    } else {
      throw new EconomyError(message);
    }
  }
}

function addWellToState(instance, coord) {
  const newWell = { x:coord.x, y:coord.y, timestamp:instance.currentTimestamp };
  instance.state.wells_level1.push(newWell);
  instance.logger.info("Placement successful. Updated state: " + JSON.stringify(instance.state));
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
    if (error instanceof EconomyError) {
      result.status = error.status;
      result.retryAfter = error.retryAfter;
      result.details = error.details;
    } else if (error instanceof CloudCodeCustomError) {
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

class StateMissingError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "StateMissingError";
    this.status = 2;
  }
}

class SpaceOccupiedError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "SpaceOccupiedError";
    this.status = 3;
  }
}

class EconomyError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "EconomyError";
    this.status = 4;
    this.retryAfter = null;
    this.details = "";
  }
}

class InvalidLocationError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "InvalidLocationError";
    this.status = 9;
  }
}

class EconomyProcessingError extends EconomyError {
  constructor(message, innerExceptionMessage, innerExceptionName, innerExceptionStatus) {
    super(message + ": " + innerExceptionMessage);
    this.name = "EconomyError: " + innerExceptionName;
    this.status = innerExceptionStatus;
  }
}

class EconomyRateLimitError extends EconomyProcessingError {
  constructor(message, innerExceptionMessage, innerExceptionName, innerExceptionStatus, retryAfter) {
    super(message, innerExceptionMessage, innerExceptionName, innerExceptionStatus);
    this.retryAfter = retryAfter
  }
}

class EconomyValidationError extends EconomyProcessingError {
  constructor(message, innerExceptionMessage, innerExceptionName, innerExceptionStatus, details) {
    super(message, innerExceptionMessage, innerExceptionName, innerExceptionStatus);
    this.details = details;
  }
}

module.exports.params = {
  "coord": {
    "type": "JSON",
    "required": true
  }
};
