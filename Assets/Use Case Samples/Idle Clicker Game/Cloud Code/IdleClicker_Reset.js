// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { DataApi } = require("@unity-services/cloud-save-1.1");

const badRequestError = 400;
const tooManyRequestsError = 429;

const playfieldSize = 5;
const gameStateCloudSaveKey = "IDLE_CLICKER_GAME_STATE";

// Entry point for the Cloud Code script 
module.exports = async ({ params, context, logger }) => {
  try {
    const { projectId, playerId, accessToken } = context;
    const cloudSaveApi = new DataApi({ accessToken });

    let instance = { projectId, playerId, cloudSaveApi, logger };

    await clearCloudSaveData(instance);

    logger.info("Game state reset.");

  } catch (error) {
    transformAndThrowCaughtError(error);
  }
}

async function clearCloudSaveData(instance) {
  try {
    await instance.cloudSaveApi.deleteItem(gameStateCloudSaveKey, instance.projectId, instance.playerId);
  } catch (error) {
      // Cloud Save throws when the key does not exist. Check if it's the 'not found' error 404.
      if (error.response.status === 404)
      {
          instance.logger.info("Player record did not exist so it did not need to be deleted.");
          return;
      }

      // Rethrow this unexpected error so we return failure to client.
      throw error;
  }
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

class EconomyError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "EconomyError";
    this.status = 4;
    this.retryAfter = null;
    this.details = "";
  }
}
