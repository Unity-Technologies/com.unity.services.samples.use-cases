// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.0");
const { DataApi } = require("@unity-services/cloud-save-1.0");
const { PurchasesApi } = require("@unity-services/economy-2.0");

const badRequestError = 400;
const tooManyRequestsError = 429;

const playfieldSize = 5;
const currencyId = "WATER";
const factoryGrantFrequencySeconds = 1;
const factoryGrantFrequency = factoryGrantFrequencySeconds * 1000;
const factoryGrantPerCycle = 1;

// Entry point for the Cloud Code script 
module.exports = async ({ params, context, logger }) => {
  let instance;

  try {
    logger.info("Script parameters: " + JSON.stringify(params));
    logger.info("Authenticated within the following context: " + JSON.stringify(context));

    const { projectId, playerId, accessToken} = context;
    const cloudSaveApi = new DataApi({ accessToken });
    const economyCurrencyApi = new CurrenciesApi({ accessToken });
    const purchasesApi = new PurchasesApi({ accessToken });

    let coord = params.coord;

    instance = { projectId, playerId, cloudSaveApi, economyCurrencyApi, purchasesApi, logger };

    instance.state = await readState(instance);

    instance.timestamp = getCurrentTimestamp();

    // If save state is found (normal condition) then update it based on duration since last update.
    if (instance.state) {
      logger.info("read start state: " + JSON.stringify(instance.state));

      await updateState(instance);
      logger.info("updated state: " + JSON.stringify(instance.state));
    } else {
      // DASHBOARD TESTING CODE: If the starting state is not found (only occurs in dashboard) then setup dummy state 
      // for testing only. By not randomizing here, testing can request valid or invalid locations as needed.
      instance.state = { timestamp:instance.timestamp, factories:[], obstacles:[{x:1, y:2}, {x:2, y:2}, {x:3, y:2}] };
      await economyCurrencyApi.setPlayerCurrencyBalance(projectId, playerId, currencyId, { currencyId, balance:2000 });
      logger.info("created debug start state: " + JSON.stringify(instance.state));
    }

    // Check the placement coord for Obstacles and Wells. Throws if placement is found to be invalid.    
    throwIfSpaceOccupied(instance, coord);

    // Attempt the Virtual Purchase for the Well. Throws if purchase is unsuccessful.
    await purchaseWell(instance);

    // If we get to this point, the space is empty and Virtual Purchases has succeeded so we can add the factory to the game state.
    placeFactory(instance, coord);

    await saveState(instance);

    logger.info("placement successful.");

    return instance.state;
  } catch (error) {

    // This use case is unique in that it updates in real time. Since we've already distributed currency based on the passage of time
    // before an exception occurred, we must save the updated state so we do not redistribute more currency based on the original time.
    // This will save the correct time that currency was last distributed for each Well so correct currency will be granted for next call.
    try {
      if (instance.hasOwnProperty('state')) {
        logger.info("Saving state in exception handler catch statement to ensure water is not distributed multiple times in response to an exception.");
        await saveState(instance);
      }
    } catch (ignoreError) {

      // If a second exception occurs when trying to save the state, there's nothing we can do to prevent granting additional currency.
      // Note: The only known scenario that could cause this is due to rate limiting on Cloud Save. If it does occur, game play will
      //       need to be adjusted to prevent players 'spamming' the feature too quickly.
      logger.error("Exception thrown when saving state. Save FAILED so updated state lost.");
      logger.error(ignoreError);
    }

    // Throw the original exception so Unity client can display an appropriate popup. This will usually be insufficient funds or space occupied.
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
  let totalElapsedCycles = updateAllFactories(instance);

  instance.state.timestamp = instance.timestamp;

  await grantCurrencyForCycles(instance, totalElapsedCycles);
}

function throwIfSpaceOccupied(instance, coord) {
  if (instance.state.factories.some(item => item.x == coord.x && item.y == coord.y) ||
      instance.state.obstacles.some(item => item.x == coord.x && item.y == coord.y)) {
    throw new SpaceOccupiedError("Player attemped to place a piece in an occupied space.");
  }
}

async function purchaseWell(instance) {
  try {
    await instance.purchasesApi.makeVirtualPurchase(instance.projectId, instance.playerId, { id: "IDLE_CLICKER_GAME_WELL" });
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
  if (totalElapsedCycles > 0) {
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

class SpaceOccupiedError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "SpaceOccupiedError";
    this.status = 2;
  }
}

class EconomyError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "EconomyError";
    this.status = 3;
    this.retryAfter = null;
    this.details = "";
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
