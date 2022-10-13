// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.2");
const { PurchasesApi } = require("@unity-services/economy-2.2");
const { DataApi } = require("@unity-services/cloud-save-1.1");

const badRequestError = 400;
const tooManyRequestsError = 429;

const millisecondsPerSecond = 1000;
const playfieldSize = 5;
const currencyId = "WATER";
const wellCurrencyCostPerLevel = 100;
const maxWellLevel = 4;
const unlockCountRequired = 4;
const gameStateCloudSaveKey = "IDLE_CLICKER_GAME_STATE";
const wellPurchaseIdPrefix = "IDLE_CLICKER_GAME_PURCHASE_WELL_LEVEL_";
const wellUnlockCounterPrefix = "Well_Level";

// Entry point for the Cloud Code script 
module.exports = async ({ params, context, logger }) => {
  try {
    logger.info("Script parameters: " + JSON.stringify(params));

    const { projectId, playerId, accessToken } = context;
    const cloudSaveApi = new DataApi({ accessToken });
    const currencyApi = new CurrenciesApi({ accessToken });
    const purchasesApi = new PurchasesApi({ accessToken });

    let dragCoord = params.drag;
    let dropCoord = params.drop;

    let instance = { projectId, playerId, cloudSaveApi, currencyApi, purchasesApi, logger };

    const timestamp = getCurrentTimestamp();
    instance.currentTimestamp = timestamp;

    // Read current state from Cloud Save (throws if it doesn't exist).
    instance.state = await readState(instance);
    if (!instance.state) {
      throw new StateMissingError("MergeWells script executed without valid state setup. Be sure to call GetUpdatedState script before merging wells.");
    }
    logger.info("Read start state: " + JSON.stringify(instance.state));

    // Update the current state by granting Water and updating timestamp.
    await updateState(instance);
    logger.info("Updated state: " + JSON.stringify(instance.state));

    // Save state now with updated timestamp. 
    // It's important to save now and again after Well has been added so, if we throw between the calls, Cloud Save maintains
    // the correct timestamp for when we last granted water.
    await saveGameState(instance);

    // Validate locations and throw if either is invalid.
    // Note: This is done after updating status since we want to always update water even if the merge fails.
    throwIfLocationInvalid(instance, dragCoord);
    throwIfLocationInvalid(instance, dropCoord);

    // Make sure drag & drop not same well.
    if (_.isEqual(dragCoord, dropCoord)) {
      throw new InvalidDragError("Drag and drop locations are the same.");
    }

    // Find and remove the wells at both locations. Throws if either not found.
    // Note: we can remove the wells from the local state as we find them since, if anything goes wrong, we'll throw out 
    //       and the local data will just be discarded.
    const dragWell = removeWell(instance, dragCoord);
    const dropWell = removeWell(instance, dropCoord);

    if (dragWell.level !== dropWell.level)
    {
      throw new WellsDifferentLevelError("Drag and drop wells must be the same level to merge.");
    }

    const mergedLevel = dragWell.level + 1;
    if (mergedLevel > maxWellLevel)
    {
      throw new MaxWellLevelError("Drag and drop wells are max level and cannot be merged.");
    }

    if (!isWellLevelUnlocked(instance, mergedLevel))
    {
      throw new WellLevelLockedError("Next well level (" + mergedLevel + ") is locked.");
    }

    // Execute Virtual Purchase to consume the correct water. If successful, we can add the well to the game state.
    await purchaseWell(instance, mergedLevel);

    addWellToState(instance, dropCoord, mergedLevel);

    incrementUnlockCounter(instance, mergedLevel + 1);

    // Save state now with new well added to game state. 
    await saveGameState(instance);

    // After we've saved the state, insert the currency balance to return to caller.
    // We retrieved the currency balance at the start so we must now update it by deducting the cost of purchasing the Well.
    instance.state.currencyBalance = instance.currencyBalance - wellCurrencyCostPerLevel * mergedLevel;

    logger.info("Merge successful.");

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
    throw new InvalidLocationError("Player specified an invalid location.");
  }
}

function removeWell(instance, coord) {
  for (let i = 1; i <= maxWellLevel; i++) {
    const well = removeWellFromArray(instance, i, coord);
    if (well) {
      return well;
    }
  }

  throw new WellNotFoundError("Player attemped to merge wells, however no well was found at location: " + JSON.stringify(coord));
}

function isWellLevelUnlocked(instance, wellLevel) {
  const count = instance.state.unlockCounters[wellUnlockCounterPrefix + wellLevel];
  if (count >= unlockCountRequired) {
    return true;
  }
  return false;
}

function removeWellFromArray(instance, level, coord) {
  const array = instance.state["wells_level" + level];
  const index = array.findIndex(item => item.x === coord.x && item.y === coord.y);
  if (index >= 0) {
    let well = array[index];
    array.splice(index, 1);

    // Add Well level to the Well object so we can check it later. Only Wells of the same level may be merged.
    well.level = level;

    return well;
  }
  return null;
}

async function purchaseWell(instance, mergedLevel) {
  try {
    const projectId = instance.projectId;
    const playerId = instance.playerId;
    const playerPurchaseVirtualRequest = { id: wellPurchaseIdPrefix + mergedLevel };

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

function incrementUnlockCounter(instance, unlockLevel) {
  if (unlockLevel <= maxWellLevel) {
    const count = instance.state.unlockCounters[wellUnlockCounterPrefix + unlockLevel];
    instance.state.unlockCounters[wellUnlockCounterPrefix + unlockLevel] = count + 1;
  }
}

function addWellToState(instance, dropCoord, level) {
  const newWell = { x:dropCoord.x, y:dropCoord.y, timestamp:instance.currentTimestamp };
  instance.state["wells_level" + level].push(newWell);
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

class EconomyError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "EconomyError";
    this.status = 4;
    this.retryAfter = null;
    this.details = "";
  }
}

class WellNotFoundError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "WellNotFoundError";
    this.status = 5;
  }
}

class InvalidDragError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "InvalidDragError";
    this.status = 6;
  }
}

class WellsDifferentLevelError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "WellsDifferentLevelError";
    this.status = 7;
  }
}

class MaxWellLevelError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "MaxWellLevelError";
    this.status = 8;
  }
}

class InvalidLocationError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "InvalidLocationError";
    this.status = 9;
  }
}

class WellLevelLockedError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "WellLevelLockedError";
    this.status = 10;
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
