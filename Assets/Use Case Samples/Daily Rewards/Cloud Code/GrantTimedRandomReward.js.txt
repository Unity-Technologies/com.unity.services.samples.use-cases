// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const COOLDOWN_SECONDS = 60;
const EPOCH_TIME_TO_SECONDS = 1000;

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.0");
const { InventoryApi } = require("@unity-services/economy-2.0");
const { DataApi } = require("@unity-services/cloud-save-1.0");

// Entry point for the Cloud Code script
module.exports = async ({ params, context, logger }) => {

  const { projectId, playerId, accessToken} = context;
  const cloudSaveApi = new DataApi({ accessToken });
  
  var epochTime = Math.floor(new Date().valueOf() / EPOCH_TIME_TO_SECONDS);

  // Check if the cooldown has expired or was never set (the player hasn't yet ever claimed a Daily Reward)
  const getTimeResponse = await cloudSaveApi.getItems(projectId, playerId, [ "GRANT_TIMED_REWARD_TIME" ] );
  if (getTimeResponse.data.results &&
      getTimeResponse.data.results.length > 0 &&
      getTimeResponse.data.results[0] &&
      getTimeResponse.data.results[0].value)
  {
    var grantEpochTime = getTimeResponse.data.results[0].value;
    var cooldown = COOLDOWN_SECONDS - (epochTime - grantEpochTime);
    
    // If cooldown timer has not expired (using 1 for slight tolerance in case the Claim button is pressed early)
    if (cooldown > 1)
    {
      logger.error("The player tried to get a Daily Reward before the cooldown timer expired.");
      throw Error("The player tried to get a Daily Reward before the cooldown timer expired.");      
    }
  }  

  // Select a random reward to grant
  const currencyApi = new CurrenciesApi({ accessToken }); 
  const inventoryApi = new InventoryApi({ accessToken });
  let currencyIds = ["COIN", "GEM", "PEARL", "STAR"];
  let inventoryItemIds = ["SWORD", "SHIELD"];

  let currencyId1 = pickRandomCurrencyId(currencyIds, null);
  let currencyQuantity1 = pickRandomCurrencyQuantity(currencyId1);
  let currencyId2 = pickRandomCurrencyId(currencyIds, currencyId1);
  let currencyQuantity2 = pickRandomCurrencyQuantity(currencyId2);
  let inventoryItemId = pickRandomInventoryItemId(inventoryItemIds);
  let inventoryItemQuantity = pickRandomInventoryItemQuantity(inventoryItemId);

  // Grant all rewards and update the cooldown timer
  await Promise.all([
    cloudSaveApi.setItem(projectId, playerId, { key: "GRANT_TIMED_REWARD_TIME", value: epochTime } ),
    grantCurrency(currencyApi, projectId, playerId, currencyId1, currencyQuantity1),
    grantCurrency(currencyApi, projectId, playerId, currencyId2, currencyQuantity2),
    grantInventoryItem(inventoryApi, projectId, playerId, inventoryItemId, inventoryItemQuantity)
    ]);
  
  return { currencyId: [currencyId1, currencyId2], currencyQuantity: [currencyQuantity1, currencyQuantity2],
           inventoryItemId: [inventoryItemId], inventoryItemQuantity: [inventoryItemQuantity] };
};

// Pick a random currency reward from the list
function pickRandomCurrencyId(currencyIds, invalidId)
{
  let i = _.random(currencyIds.length - 1);
  
  if (currencyIds[i] === invalidId)
  {
    i++;
    if (i >= currencyIds.length)
    {
      i = 0;
    }
  }
  return currencyIds[i];
}

// Pick a random quantity for the specified currency (uses 1-5 for sample)
function pickRandomCurrencyQuantity(currencyId)
{
  return _.random(1, 5);
}

// Grant the specified currency reward using the Economy service
async function grantCurrency(currencyApi, projectId, playerId, currencyId, amount)
{
  await currencyApi.incrementPlayerCurrencyBalance(projectId, playerId, currencyId, { currencyId, amount });
}

// Pick a random inventory item from the list
function pickRandomInventoryItemId(inventoryItemIds)
{
  return inventoryItemIds[_.random(inventoryItemIds.length - 1)];
}

// Pick a quantity of inventory items to grant (75% chance to grant 1, but rarely to grant 2)
function pickRandomInventoryItemQuantity(inventoryItemId)
{
  if (_.random(1, 100) >= 75)
  {
    return 2;
  }
  return 1;
}

// Grant the specified inventory item the specified number of times
async function grantInventoryItem(inventoryApi, projectId, playerId, inventoryItemId, amount)
{
  for (let i = 0; i < amount; i++)
  {
    await inventoryApi.addInventoryItem(projectId, playerId, { inventoryItemId: inventoryItemId });
  }
}
