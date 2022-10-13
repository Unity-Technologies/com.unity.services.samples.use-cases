// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const { CurrenciesApi } = require("@unity-services/economy-2.3");

const badRequestError = 400;
const tooManyRequestsError = 429;

module.exports = async ({ params, context, logger }) => {
  try {
    const { projectId, playerId, accessToken } = context;
    const economyCurrencyAPI = new CurrenciesApi({ accessToken });

    let currencyIds = ["COIN", "GEM", "PEARL", "STAR"];

    const currencyId = pickRandomCurrencyId(currencyIds);

    const amount = pickRandomCurrencyQuantity(currencyId);

    await grantCurrency(economyCurrencyAPI, projectId, playerId, currencyId, amount);
    
    // return the granted currency and amount to calling (Unity) script
    return { currencyId: currencyId, amount: amount };
  } catch (error) {
    transformAndThrowCaughtError(error);
  }
};

async function grantCurrency(economyCurrencyAPI, projectId, playerId, currencyId, amount) {
  const currencyModifyBalanceRequest = { currencyId, amount };
  const requestParameters = { projectId, playerId, currencyId, currencyModifyBalanceRequest };
  await economyCurrencyAPI.incrementPlayerCurrencyBalance(requestParameters);
}

function pickRandomCurrencyId(currencyIds) {
  return currencyIds[Math.floor(Math.random() * (currencyIds.length))];
}

function pickRandomCurrencyQuantity(currencyId) {
  return Math.floor(Math.random() * 5) + 1;
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
    result.name = error.name;
    result.message = error.message;
  }

  throw new Error(JSON.stringify(result));
}
