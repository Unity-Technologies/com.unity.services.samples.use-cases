// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const COOLDOWN_SECONDS = 60;
const EPOCH_TIME_TO_SECONDS = 1000;

const { DataApi } = require("@unity-services/cloud-save-1.0");

// Entry point for the Cloud Code script
module.exports = async ({ params, context, logger }) => {

  const { projectId, playerId, accessToken} = context;
  const cloudSaveApi = new DataApi({ accessToken });
  
  const getTimeResponse = await cloudSaveApi.getItems(projectId, playerId, [ "GRANT_TIMED_REWARD_TIME" ] );

  // Check for the last grant epoch time
  if (getTimeResponse.data.results &&
      getTimeResponse.data.results.length > 0 &&
      getTimeResponse.data.results[0] &&
      getTimeResponse.data.results[0].value)
  {
    // Cooldown value exists - check how long it has been in seconds
    var nowEpochTime = Math.floor(new Date().valueOf() / EPOCH_TIME_TO_SECONDS);
    var grantEpochTime = getTimeResponse.data.results[0].value;
    var cooldown = COOLDOWN_SECONDS - (nowEpochTime - grantEpochTime);
    
    // If cooldown has NOT expired
    if (cooldown > 0)
    {
      // Return canGrantFlag: false, and remaining cooldown time in seconds
      return { canGrantFlag: false, grantCooldown: cooldown, defaultCooldown: COOLDOWN_SECONDS };
    }
  }

  // Last grant time doesn't exist or cooldown has expired - okay to claim reward now
  return { canGrantFlag: true, grantCooldown: 0, defaultCooldown: COOLDOWN_SECONDS };
};
