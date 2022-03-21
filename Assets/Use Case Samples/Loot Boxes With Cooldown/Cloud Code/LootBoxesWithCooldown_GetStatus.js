// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const cooldownSeconds = 60;
const epochTimeToSeconds = 1000;
const rateLimitError = 429;
const validationError = 400;

const { DataApi } = require("@unity-services/cloud-save-1.0");

// Entry point for the Cloud Code script
module.exports = async ({ params, context, logger }) => {

  const { projectId, playerId, accessToken} = context;
  const cloudSaveApi = new DataApi({ accessToken });

  try
  {
    const getTimeResponse = await cloudSaveApi.getItems(projectId, playerId, [ "GRANT_TIMED_REWARD_TIME" ] );

    // Check for the last grant epoch time
    if (getTimeResponse.data.results &&
        getTimeResponse.data.results.length > 0 &&
        getTimeResponse.data.results[0] &&
        getTimeResponse.data.results[0].value)
    {
      // Cooldown value exists - check how long it has been in seconds
      var nowEpochTime = Math.floor(new Date().valueOf() / epochTimeToSeconds);
      var grantEpochTime = getTimeResponse.data.results[0].value;
      var cooldown = cooldownSeconds - (nowEpochTime - grantEpochTime);
      
      // If cooldown has NOT expired
      if (cooldown > 0)
      {
        // Return canGrantFlag: false, and remaining cooldown time in seconds
        return { canGrantFlag: false, grantCooldown: cooldown, defaultCooldown: cooldownSeconds };
      }
    }

    // Last grant time doesn't exist or cooldown has expired - okay to claim reward now
    return { canGrantFlag: true, grantCooldown: 0, defaultCooldown: cooldownSeconds };
  }
  catch (error)
  {
    transformAndThrowCaughtError(error);
  }
};

// Some form of this function appears in all Cloud Code scripts.
// Its purpose is to parse the errors thrown from the script into a standard exception object which can be stringified.
function transformAndThrowCaughtError(error) {
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
    result.title = error.name;
    result.message = error.message;
  }

  throw new Error(JSON.stringify(result));
}
