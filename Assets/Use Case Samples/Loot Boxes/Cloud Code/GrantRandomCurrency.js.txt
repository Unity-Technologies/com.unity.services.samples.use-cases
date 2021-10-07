// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const { CurrenciesApi } = require("@unity-services/economy-2.0");

module.exports = async ({ params, context, logger }) => {

  const { projectId, playerId, accessToken} = context;
  const economyCurrencyAPI = new CurrenciesApi({ accessToken });
  
  let currencyIds = ["COIN", "GEM", "PEARL", "STAR"];

  let currencyId = pickRandomCurrencyId(currencyIds);

  let amount = pickRandomCurrencyQuantity(currencyId);

  await grantCurrency(economyCurrencyAPI, projectId, playerId, currencyId, amount);
  
  // return the granted currency and amount to calling (Unity) script
  return { currencyId: currencyId, amount: amount };
};

async function grantCurrency(economyCurrencyAPI, projectId, playerId, currencyId, amount)
{
  await economyCurrencyAPI.incrementPlayerCurrencyBalance(projectId, playerId, currencyId, { currencyId, amount });
}

function pickRandomCurrencyId(currencyIds)
{
  return currencyIds[Math.floor(Math.random() * (currencyIds.length))];
}

function pickRandomCurrencyQuantity(currencyId)
{
  return Math.floor(Math.random() * 5) + 1;
}
