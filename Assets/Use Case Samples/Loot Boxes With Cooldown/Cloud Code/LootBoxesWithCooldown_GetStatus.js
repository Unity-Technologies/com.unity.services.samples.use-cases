// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const { DataApi } = require("@unity-services/cloud-save-1.2");

const badRequestError = 400;
const tooManyRequestsError = 429;

const cooldownSeconds = 60;
const epochTimeToSeconds = 1000;

const cloudSaveKeyGrantRewardTime = "LOOT_BOX_COOLDOWN_GRANT_REWARD_TIME";

// Entry point for the Cloud Code script
module.exports = async ({ params, context, logger }) => {
  try {
    const { projectId, playerId, accessToken} = context;
    const cloudSaveApi = new DataApi({ accessToken });

    const getTimeResponse = await cloudSaveApi.getItems(projectId, playerId, [ cloudSaveKeyGrantRewardTime ] );

    // Check for the last grant epoch time
    if (getTimeResponse.data.results &&
        getTimeResponse.data.results.length > 0 &&
        getTimeResponse.data.results[0] &&
        getTimeResponse.data.results[0].value) {
      // Cooldown value exists - check how long it has been in seconds
      var nowEpochTime = Math.floor(new Date().valueOf() / epochTimeToSeconds);
      var grantEpochTime = getTimeResponse.data.results[0].value;
      var cooldown = cooldownSeconds - (nowEpochTime - grantEpochTime);

      // If cooldown has NOT expired
      if (cooldown > 0) {
        // Return canGrantFlag: false, and remaining cooldown time in seconds
        return { canGrantFlag: false, grantCooldown: cooldown, defaultCooldown: cooldownSeconds };
      }
    }

    // Last grant time doesn't exist or cooldown has expired - okay to claim reward now
    return { canGrantFlag: true, grantCooldown: 0, defaultCooldown: cooldownSeconds };
  } catch (error) {
    transformAndThrowCaughtError(error);
  }
};

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
